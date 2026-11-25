using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Capstone.Game.SettingsSystem;

// Nhạc nền cho cả game. Chỉ có MỘT MusicManager, sống xuyên suốt khi chuyển scene.
// - Mỗi scene một bài (MainMenu, DemoScene, ...). Đổi scene -> nhạc chuyển mượt (fade).
// - Vùng trong scene (hang động, làng...) dùng MusicZone. Lúc đặc biệt (tiến hóa, đánh boss) dùng MusicOverride.
// Cách dùng: tạo Prefab tên "MusicManager" có script này, để trong thư mục Resources -> tự chạy ở mọi scene.
[DisallowMultipleComponent]
public class MusicManager : MonoBehaviour
{
    [Serializable]
    public class SceneTrack
    {
        [Tooltip("Tên scene, đúng như trong Build Profiles (vd. MainMenu, DemoScene)")]
        public string sceneName;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("Nhạc theo scene")]
    [SerializeField] List<SceneTrack> sceneTracks = new List<SceneTrack>();
    [Tooltip("Scene không có trong danh sách: bật = giữ bài đang phát, tắt = im lặng")]
    [SerializeField] bool keepMusicForUnknownScene = true;
    [Tooltip("Scene không có trong danh sách: dùng nhạc có sẵn của game (Resources/CapstoneAudio): MainMenu -> nhạc menu, DemoScene... -> nhạc gameplay")]
    [SerializeField] bool useGameDefaultTracks = true;

    [Header("Chuyển bài")]
    [SerializeField, Min(0f)] float fadeTime = 1.5f;
    [Tooltip("Tên Group nhạc trong GameAudioMixer")]
    [SerializeField] string musicGroup = "Music";

    public static MusicManager Instance { get; private set; }

    class Request { public object owner; public AudioClip clip; public float volume; public bool silent; }

    readonly List<Request> overrides = new List<Request>();
    AudioSource[] sources;
    int activeSource;
    AudioClip sceneClip;
    float sceneVolume = 1f;
    AudioClip playingClip;
    Coroutine fadeRoutine;

    // Tự tạo MusicManager từ Resources/MusicManager khi game bắt đầu (kể cả khi Play thẳng DemoScene).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null || FindFirstObjectByType<MusicManager>() != null) return;
        var prefab = Resources.Load<MusicManager>("MusicManager");
        if (prefab != null) Instantiate(prefab).name = "MusicManager";
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sources = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.ignoreListenerPause = true;
            sources[i] = source;
            GameAudioRouting.Route(source, GameAudioBus.Music);   // Thanh "Nhạc nền" trong Cài đặt chỉnh được.
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void Start()
    {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        StartCoroutine(ApplySettingsWhenReady());
    }

    // Play thẳng 1 scene: Cài đặt âm thanh có thể chưa được đẩy vào Audio Mixer
    // -> mixer giữ giá trị cũ (vd. Music -26 dB) làm nhạc rất nhỏ / như không có. Đẩy lại 1 lần.
    IEnumerator ApplySettingsWhenReady()
    {
        float until = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < until)
        {
            var data = GameSettingsService.Instance != null && GameSettingsService.Instance.Model != null
                ? GameSettingsService.Instance.Model.Data : null;
            if (data != null)
            {
                GameAudioRouting.Apply(data);
                yield break;
            }
            yield return null;
        }
    }

    float nextSuppressCheck;
    bool forceSilent;
    void Update()
    {
        // Nhạc có sẵn của game có thể được tạo sau MusicManager -> kiểm tra lại định kỳ.
        if ((playingClip == null && !forceSilent) || Time.unscaledTime < nextSuppressCheck) return;
        nextSuppressCheck = Time.unscaledTime + (forceSilent ? 0.2f : 1f);
        SuppressBuiltInMusic(true);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        overrides.RemoveAll(request => request.owner == null || (request.owner is UnityEngine.Object o && o == null));

        SceneTrack track = sceneTracks.Find(t => t != null && t.clip != null
            && string.Equals(t.sceneName, scene.name, StringComparison.OrdinalIgnoreCase));
        AudioClip builtIn = useGameDefaultTracks ? GameDefaultTrack(scene.name) : null;
        if (track != null) { sceneClip = track.clip; sceneVolume = track.volume; }
        else if (builtIn != null) { sceneClip = builtIn; sceneVolume = 0.42f; }   // Cùng độ to với nhạc có sẵn của game.
        else if (!keepMusicForUnknownScene) { sceneClip = null; }
        Refresh();
    }

    static AudioClip GameDefaultTrack(string scene)
    {
        var clips = GameAudioRouting.Assets;
        if (clips == null) return null;
        if (scene == "MainMenu") return clips.menuMusic;
        if (scene == "EvolutionScene") return clips.evolutionMusic;
        if (scene.StartsWith("DemoScene", StringComparison.Ordinal) || scene == "Dwarven Cave A1" || scene == "Map forest")
            return clips.gameplayMusic;
        return null;
    }

    // ---------- API cho script khác ----------

    // Phát bài riêng (đè lên nhạc scene) tới khi gọi Release với cùng owner.
    public static void Push(object owner, AudioClip clip, float volume = 1f)
    {
        if (owner == null || !EnsureInstance()) return;
        Instance.overrides.RemoveAll(request => request.owner == owner);
        Instance.overrides.Add(new Request { owner = owner, clip = clip, volume = volume, silent = clip == null });
        Instance.Refresh();
    }

    /// <summary>Tắt hết nhạc (cả nhạc scene có sẵn của game) tới khi gọi Release với cùng owner.</summary>
    public static void PushSilence(object owner) => Push(owner, null);

    // Chưa có prefab Resources/MusicManager -> tự tạo 1 cái trống để vẫn dùng được Push / Silence.
    static bool EnsureInstance()
    {
        if (Instance != null) return true;
        var existing = FindFirstObjectByType<MusicManager>();
        if (existing != null) return Instance != null;
        new GameObject("MusicManager").AddComponent<MusicManager>();
        return Instance != null;
    }

    public static void Release(object owner)
    {
        if (Instance == null || owner == null) return;
        if (Instance.overrides.RemoveAll(request => request.owner == owner) > 0) Instance.Refresh();
    }

    // Đổi nhạc của scene hiện tại bằng code (vd. sau khi đánh bại boss).
    public static void SetSceneMusic(AudioClip clip, float volume = 1f)
    {
        if (Instance == null) return;
        Instance.sceneClip = clip;
        Instance.sceneVolume = volume;
        Instance.Refresh();
    }

    // ---------- Chuyển bài ----------

    void Refresh()
    {
        AudioClip clip = sceneClip;
        float volume = sceneVolume;
        bool silent = false;
        if (overrides.Count > 0)
        {
            Request top = overrides[overrides.Count - 1];   // Cái mới nhất thắng.
            clip = top.clip;
            volume = top.volume;
            silent = top.silent;
        }
        forceSilent = silent;
        SuppressBuiltInMusic(clip != null || silent);

        if (clip == playingClip && clip != null)
        {
            // Cùng bài -> chỉ chỉnh âm lượng, không phát lại từ đầu.
            CrossFade(null, volume);
            return;
        }
        playingClip = clip;
        CrossFade(clip, volume);
    }

    void CrossFade(AudioClip clip, float volume)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CrossFadeRoutine(clip, volume));
    }

    IEnumerator CrossFadeRoutine(AudioClip clip, float volume)
    {
        AudioSource from = sources[activeSource];
        AudioSource to = from;
        if (clip != null || playingClip == null)
        {
            // Bài mới: phát ở nguồn còn lại rồi tăng dần, nguồn cũ giảm dần.
            if (clip != null)
            {
                activeSource = 1 - activeSource;
                to = sources[activeSource];
                to.clip = clip;
                to.volume = 0f;
                to.Play();
            }
            else to = null;   // Im lặng.
        }

        float fromStart = from.volume;
        float toStart = to != null ? to.volume : 0f;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeTime);
            if (to != from) from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (to != null) to.volume = Mathf.Lerp(toStart, volume, k);
            yield return null;
        }
        if (to != from) { from.volume = 0f; from.Stop(); }
        if (to != null) to.volume = volume;
        fadeRoutine = null;
    }

    // Game có sẵn nhạc theo scene (GameSettingsService + Resources/CapstoneAudio).
    // Khi MusicManager đang phát bài riêng thì tạm tắt tiếng nhạc có sẵn để không bị 2 bài chồng nhau.
    readonly Dictionary<AudioSource, float> suppressed = new Dictionary<AudioSource, float>();

    void SuppressBuiltInMusic(bool suppress)
    {
        var service = GameSettingsService.Instance;
        if (service != null && suppress)
        {
            foreach (AudioSource builtIn in service.GetComponents<AudioSource>())
            {
                if (builtIn == null || !builtIn.loop || suppressed.ContainsKey(builtIn)) continue;
                suppressed[builtIn] = builtIn.volume;
                builtIn.volume = 0f;
            }
            return;
        }
        foreach (var entry in suppressed) if (entry.Key != null) entry.Key.volume = entry.Value;
        suppressed.Clear();
    }

    AudioMixerGroup FindGroup()
    {
        AudioMixer mixer = GameOptions.LoadMixer();
        if (mixer == null) return null;
        AudioMixerGroup[] groups = mixer.FindMatchingGroups(musicGroup);
        return groups != null && groups.Length > 0 ? groups[0] : null;
    }
}
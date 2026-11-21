using System;
using System.Collections;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UI;

// Đoạn mở đầu khi bắt đầu game mới (sau màn tạo nhân vật):
//   1. Nền đen + từng dòng lời dẫn hiện lên rồi mờ đi.
//   2. Màn hình sáng dần, camera lia qua vài khung cảnh của map.
//   3. Logo game hiện lên từ từ ở shot cuối rồi mờ đi.
//   4. Tối lại -> vào trong nhà, camera lia vài góc quanh nhà.
//   5. Trong lúc lia, hộp thoại dưới màn hình: Player tự nói.
//      Mỗi shot nói xong phần của nó mới chuyển sang shot tiếp theo.
//   6. Tối dần -> sáng lại với camera theo Player, người chơi điều khiển như thường.
// Space / Enter / click: qua chữ nhanh.
// Esc (lúc lời dẫn / lia map / logo): bỏ qua, vào thẳng phần trong nhà. Phần trong nhà không bỏ qua được.
[DisallowMultipleComponent]
public class NewGameIntroSequence : MonoBehaviour
{
    [Serializable]
    public class CameraShot
    {
        public Transform start;
        [Tooltip("Để trống = camera đứng yên ở điểm đầu")]
        public Transform end;
        [Min(0.5f)] public float duration = 5f;
    }

    [Serializable]
    public class HouseShot
    {
        public Transform start;
        [Tooltip("Để trống = camera đứng yên ở điểm đầu")]
        public Transform end;
        [Tooltip("Thời gian camera trượt từ điểm đầu tới điểm cuối")]
        [Min(0.5f)] public float duration = 5f;
        [Tooltip("Những câu Player nói trong shot này. {name} = tên nhân vật")]
        [TextArea(2, 3)] public string[] lines = new string[0];
    }

    [Header("Khi nào chạy")]
    [Tooltip("Chạy luôn khi bấm Play thẳng ở DemoScene (để test, không cần qua màn tạo nhân vật)")]
    [SerializeField] bool playWhenTestingInEditor;

    [Header("1. Lời dẫn trên nền đen  ({name} = tên nhân vật)")]
    [TextArea(2, 4)]
    [SerializeField] string[] storyLines =
    {
        "Từ thuở xa xưa, vùng đất Fantasia là nơi con người và muôn loài thú cùng chung sống.",
        "Mỗi loài thú mang trong mình sức mạnh của tự nhiên:\nLửa, Nước, Cỏ, Điện, Bay, Bóng Tối, Rồng... và cả những sức mạnh chưa ai biết tới.",
        "Những người đủ dũng cảm để kết giao với chúng được gọi là Nhà Huấn Luyện.",
        "Nhưng gần đây, những vết nứt màu tím kỳ lạ bắt đầu xuất hiện khắp nơi...",
        "Thú hoang trở nên hung dữ, và người ta thì thầm về một cái tên đã ngủ yên hàng trăm năm:\nDetale.",
        "Tại một ngôi làng nhỏ yên bình, một hành trình mới sắp bắt đầu.",
        "Hành trình của {name}."
    };
    [SerializeField, Min(10)] int storyFontSize = 40;
    [SerializeField, Min(0.1f)] float lineFadeTime = 0.9f;
    [Tooltip("Thời gian tối thiểu mỗi dòng đứng yên trên màn hình")]
    [SerializeField, Min(0.5f)] float minLineHold = 2.5f;
    [Tooltip("Cộng thêm theo độ dài dòng chữ (giây / ký tự)")]
    [SerializeField, Min(0f)] float holdPerCharacter = 0.04f;

    [Header("2. Lia camera giới thiệu map")]
    [SerializeField] List<CameraShot> shots = new List<CameraShot>();
    [Tooltip("Thời gian màn hình sáng dần ở shot đầu tiên")]
    [SerializeField, Min(0.1f)] float revealFadeTime = 2.5f;
    [Tooltip("Thời gian tối / sáng khi chuyển giữa 2 shot")]
    [SerializeField, Min(0f)] float shotTransitionFade = 0.6f;

    [Header("3. Logo (hiện trong shot cuối)")]
    [SerializeField] Sprite logo;
    [SerializeField] string logoFallbackText = "BEAST OF FANTASY";
    [SerializeField] Vector2 logoSize = new Vector2(900f, 420f);
    [SerializeField, Min(0f)] float logoDelay = 0.8f;
    [SerializeField, Min(0.1f)] float logoFadeIn = 1.8f;
    [SerializeField, Min(0f)] float logoHold = 2.5f;
    [SerializeField, Min(0.1f)] float logoFadeOut = 1.8f;

    [Header("4. Trong nhà: lia camera quanh nhà")]
    [Tooltip("Chỗ Player đứng. Xoay mũi tên xanh dương (trục Z) về phía cửa sổ để Player đứng ngắm ra ngoài")]
    [SerializeField] Transform playerSpawnPoint;
    [Tooltip("Các shot lia quanh nhà. Shot CUỐI nên dừng ở góc nhìn thấy Player đang ngắm cửa sổ")]
    [SerializeField] List<HouseShot> houseShots = new List<HouseShot>();
    [Tooltip("Chỉ dùng khi House Shots trống: 1 góc camera đứng yên")]
    [SerializeField] Transform houseCameraPoint;
    [Tooltip("Thời gian sáng dần khi vào nhà")]
    [SerializeField, Min(0.1f)] float houseFadeTime = 1.5f;

    [Header("5. Player tự nói (hộp thoại dưới màn hình)")]
    [Tooltip("Tên hiện trên hộp thoại. {name} = tên nhân vật")]
    [SerializeField] string speakerName = "{name}";
    [Tooltip("Bật: shot có lời thoại sẽ chuyển ngay khi nói xong (camera chưa trượt hết cũng được). "
        + "Tắt: nói xong vẫn đợi camera trượt hết Duration")]
    [SerializeField] bool endShotWhenDialogueDone = true;
    [Tooltip("Shot có lời thoại tồn tại ít nhất bao lâu (giây)")]
    [SerializeField, Min(0f)] float minShotTimeWithDialogue = 2.5f;
    [Tooltip("CHỈ dùng khi mọi House Shot đều chưa có Lines: tự chia đều các câu này cho các shot")]
    [TextArea(2, 4)]
    [SerializeField] string[] playerMonologue =
    {
        "Trời hôm nay đẹp thật... nắng vàng, gió mát.",
        "Dạo này người trong làng cứ bàn tán mãi về những vết nứt màu tím ngoài kia.",
        "Không biết rồi chuyện gì sẽ xảy ra với Fantasia nữa...",
        "Nhưng mà... hôm nay là ngày quan trọng nhất của mình!",
        "Cuối cùng mình cũng được nhận thú đồng hành đầu tiên.",
        "Mình nhất định sẽ trở thành một Nhà Huấn Luyện thật giỏi!"
    };
    [Tooltip("Tốc độ chữ chạy (ký tự / giây)")]
    [SerializeField, Min(5f)] float typingSpeed = 40f;
    [Tooltip("Ảnh chân dung khi nhân vật là Nam (tùy chọn)")]
    [SerializeField] Sprite malePortrait;
    [Tooltip("Ảnh chân dung khi nhân vật là Nữ (tùy chọn)")]
    [SerializeField] Sprite femalePortrait;
    [Tooltip("Kiểu dáng hộp thoại. Để trống = tự thêm DialogueBoxUI với kiểu mặc định")]
    [SerializeField] DialogueBoxUI dialogueBox;

    [Header("6. Vào chơi")]
    [Tooltip("Tắt (mặc định): nói xong vào chơi ngay. Bật: tối màn hình, đặt camera sau lưng Player rồi mới sáng lại")]
    [SerializeField] bool fadeBeforeGameplay;
    [SerializeField, Min(0.1f)] float returnFadeOut = 1.5f;
    [SerializeField, Min(0.1f)] float returnFadeIn = 1.5f;
    [Tooltip("Chữ hiện ở dưới màn hình khi bắt đầu chơi. Để trống = không hiện")]
    [SerializeField] string locationTitle = "Nhà của {name}";

    [Header("Tư thế Player trong đoạn mở đầu")]
    [Tooltip("Player chỉ đứng yên ở state này suốt đoạn mở đầu. Vào chơi mới chạy lại đủ animation")]
    [SerializeField] string introIdleState = "IdleNeutral";

    [Header("Nhạc nền (tùy chọn)")]
    [SerializeField] AudioSource music;

    // UI
    Canvas canvas;
    Image black;
    Text story;
    Graphic logoGraphic;
    Text location;
    Text skipHint;

    // Trạng thái
    bool running;
    bool skipAll;
    Camera cam;
    GameMenuController menu;
    bool blockedWithMenu;
    readonly List<Behaviour> disabledCameraDrivers = new List<Behaviour>();
    float musicVolume = 1f;
    Coroutine blackFadeRoutine;
    Coroutine logoRoutine;
    Behaviour frozenMovement;
    bool inHouse;
    Animator idleAnimator;
    int idleHash;

    public bool IsPlaying => running;

    void Awake()
    {
        bool play = CharacterSelectionData.PlayNewGameIntro
            || (Application.isEditor && playWhenTestingInEditor);
        CharacterSelectionData.PlayNewGameIntro = false;
        if (!play) { enabled = false; return; }

        // Dựng màn đen ngay từ Awake để không lộ 1 frame gameplay.
        BuildOverlay();
        running = true;
    }

    void Start()
    {
        if (running) StartCoroutine(Run());
    }

    void OnDisable()
    {
        ReleasePlayerIdle();
    }

    void Update()
    {
        // Esc chỉ bỏ qua lời dẫn + lia map + logo. Phần trong nhà phải xem / nói hết.
        if (running && !inHouse && Input.GetKeyDown(KeyCode.Escape)) skipAll = true;
    }

    [ContextMenu("Xem thử (chỉ dùng khi đang Play)")]
    void PlayNow()
    {
        if (!Application.isPlaying || running) return;
        enabled = true;
        BuildOverlay();
        running = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        skipAll = false;
        Begin();

        yield return WaitRealtime(0.6f);
        yield return StoryPart();
        if (!skipAll) yield return FlyoverPart();
        yield return HousePart();
        yield return ReturnToGameplayPart();
    }

    // ---------- Các phần ----------

    void Begin()
    {
        black.raycastTarget = true;
        SetAlpha(black, 1f);
        SetAlpha(story, 0f);
        SetAlpha(logoGraphic, 0f);
        SetAlpha(location, 0f);
        inHouse = false;
        skipHint.text = "Space — Tiếp      Esc — Bỏ qua";
        SetAlpha(skipHint, 0.55f);
        dialogueBox.HideImmediate();
        canvas.gameObject.SetActive(true);

        BlockGameplay(true);
        Cursor.visible = false;

        cam = Camera.main;
        DisableCameraDrivers();
        TeleportPlayer();   // Đưa vào nhà ngay từ đầu để không lọt vào các shot ngoài trời.
        HoldPlayerIdle();

        if (music != null)
        {
            musicVolume = music.volume;
            if (!music.isPlaying) music.Play();
        }
    }

    IEnumerator StoryPart()
    {
        foreach (string rawLine in storyLines)
        {
            if (skipAll) yield break;
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            string line = FormatName(rawLine);
            story.text = line;
            SetAlpha(story, 0f);

            // Hiện dần. Bấm Space / Enter / click -> hiện ngay.
            float t = 0f;
            while (t < lineFadeTime && !skipAll)
            {
                if (AdvancePressed()) break;
                t += Time.unscaledDeltaTime;
                SetAlpha(story, Mathf.SmoothStep(0f, 1f, t / lineFadeTime));
                yield return null;
            }
            SetAlpha(story, 1f);
            yield return null;

            // Đứng yên cho người chơi đọc.
            float hold = Mathf.Max(minLineHold, line.Length * holdPerCharacter);
            t = 0f;
            while (t < hold && !skipAll)
            {
                if (AdvancePressed()) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fade(story, 0f, lineFadeTime * 0.7f);
            yield return WaitRealtime(0.35f);
        }
        SetAlpha(story, 0f);
    }

    IEnumerator FlyoverPart()
    {
        if (shots.Count == 0 || cam == null)
        {
            Debug.LogWarning($"{name}: chưa có Shot camera nào, bỏ qua phần lia camera.", this);
            yield break;
        }

        for (int i = 0; i < shots.Count; i++)
        {
            if (skipAll) yield break;
            CameraShot shot = shots[i];
            if (shot == null || shot.start == null) continue;
            bool last = i == shots.Count - 1;

            if (i > 0 && shotTransitionFade > 0f)
            {
                StopBlackFade();
                yield return Fade(black, 1f, shotTransitionFade);
            }
            if (skipAll) yield break;

            PlaceCamera(shot.start);
            StartBlackFade(0f, i == 0 ? revealFadeTime : shotTransitionFade, true);

            bool logoDone = true;
            if (last)
            {
                logoDone = false;
                logoRoutine = StartCoroutine(LogoRoutine(() => logoDone = true));
            }

            Transform end = shot.end != null ? shot.end : shot.start;
            float shotStart = Time.unscaledTime;
            Coroutine move = StartCoroutine(MoveCamera(shot.start.position, shot.start.rotation,
                end.position, end.rotation, shot.duration, true));

            // Đợi camera trượt hết (và logo xong ở shot cuối). Bấm Space -> sang cảnh tiếp ngay.
            bool advanced = false;
            yield return null;
            while (!skipAll)
            {
                if (AdvancePressed()) { advanced = true; break; }
                if (Time.unscaledTime - shotStart >= shot.duration && logoDone) break;
                yield return null;
            }
            StopCoroutine(move);

            if (advanced && last)
            {
                if (logoRoutine != null) { StopCoroutine(logoRoutine); logoRoutine = null; }
                yield return Fade(logoGraphic, 0f, 0.4f, false);
            }
        }
    }

    IEnumerator LogoRoutine(Action done)
    {
        yield return WaitRealtime(logoDelay);
        RectTransform rect = logoGraphic.rectTransform;

        // Hiện từ từ + phóng nhẹ 92% -> 100%.
        float t = 0f;
        while (t < logoFadeIn && !skipAll)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / logoFadeIn);
            SetAlpha(logoGraphic, k);
            rect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);
            yield return null;
        }

        t = 0f;
        while (t < logoHold && !skipAll) { t += Time.unscaledDeltaTime; yield return null; }

        yield return Fade(logoGraphic, 0f, logoFadeOut);
        done?.Invoke();
    }

    IEnumerator HousePart()
    {
        // Logo xong (hoặc bấm Esc) -> tối lại -> vào nhà.
        bool skipped = skipAll;
        inHouse = true;
        skipAll = false;   // Từ đây Esc không còn tác dụng.
        if (logoRoutine != null) { StopCoroutine(logoRoutine); logoRoutine = null; }
        StopBlackFade();
        SetAlpha(skipHint, 0f);
        yield return Fade(black, 1f, skipped ? 0.4f : 1.2f, false);
        SetAlpha(story, 0f);
        SetAlpha(logoGraphic, 0f);

        TeleportPlayer();
        HoldPlayerIdle();   // Đổi nhân vật Nam/Nữ xảy ra sau Begin -> ép lại tư thế.
        yield return WaitRealtime(0.3f);

        var list = new List<HouseShot>();
        foreach (HouseShot shot in houseShots)
            if (shot != null && shot.start != null) list.Add(shot);
        if (list.Count == 0 && houseCameraPoint != null)
            list.Add(new HouseShot { start = houseCameraPoint, duration = 3f });

        if (list.Count == 0 || cam == null)
        {
            Debug.LogWarning($"{name}: chưa có House Shots, bỏ qua phần lia camera trong nhà.", this);
            yield break;
        }

        string[][] shotLines = BuildShotLines(list);
        bool boxOpen = false;
        Coroutine cameraRoutine = null;

        for (int i = 0; i < list.Count; i++)
        {
            if (skipAll) break;
            HouseShot shot = list[i];
            bool hasLines = HasText(shotLines[i]);

            // Chuyển cảnh: tối nhẹ rồi sáng ở góc mới. Hộp thoại vẫn giữ nếu shot sau còn nói.
            if (i > 0 && shotTransitionFade > 0f)
            {
                StopBlackFade();
                yield return Fade(black, 1f, shotTransitionFade);
            }
            if (cameraRoutine != null) StopCoroutine(cameraRoutine);
            if (skipAll) break;

            PlaceCamera(shot.start);
            StartBlackFade(0f, i == 0 ? houseFadeTime : shotTransitionFade, true);
            Transform end = shot.end != null ? shot.end : shot.start;
            float shotStart = Time.unscaledTime;
            cameraRoutine = StartCoroutine(MoveCamera(shot.start.position, shot.start.rotation,
                end.position, end.rotation, shot.duration, true));

            if (hasLines)
            {
                yield return WaitRealtime(i == 0 ? 0.6f : 0.25f);
                if (!boxOpen) { yield return OpenDialogue(); boxOpen = true; }
                yield return PlayLines(shotLines[i]);

                // Nói xong phần của shot này mới được chuyển.
                float minTime = endShotWhenDialogueDone ? minShotTimeWithDialogue : shot.duration;
                while (!skipAll && Time.unscaledTime - shotStart < minTime) yield return null;
            }
            else
            {
                if (boxOpen) { yield return CloseDialogue(); boxOpen = false; }
                while (!skipAll && Time.unscaledTime - shotStart < shot.duration) yield return null;
            }

            bool nextHasLines = i + 1 < list.Count && HasText(shotLines[i + 1]);
            if (boxOpen && !nextHasLines) { yield return CloseDialogue(); boxOpen = false; }
        }

        if (cameraRoutine != null) StopCoroutine(cameraRoutine);
        if (boxOpen) yield return CloseDialogue();
    }

    // Lời thoại của từng shot. Nếu chưa shot nào có Lines thì chia đều Player Monologue.
    string[][] BuildShotLines(List<HouseShot> list)
    {
        var result = new string[list.Count][];
        bool anyShotHasLines = false;
        foreach (HouseShot shot in list) anyShotHasLines |= HasText(shot.lines);

        if (anyShotHasLines)
        {
            for (int i = 0; i < list.Count; i++) result[i] = list[i].lines ?? new string[0];
            return result;
        }

        var monologue = new List<string>();
        if (playerMonologue != null)
            foreach (string line in playerMonologue)
                if (!string.IsNullOrWhiteSpace(line)) monologue.Add(line);

        int index = 0;
        for (int i = 0; i < list.Count; i++)
        {
            int count = monologue.Count / list.Count + (i < monologue.Count % list.Count ? 1 : 0);
            result[i] = monologue.GetRange(index, count).ToArray();
            index += count;
        }
        return result;
    }

    static bool HasText(string[] lines)
    {
        if (lines == null) return false;
        foreach (string line in lines) if (!string.IsNullOrWhiteSpace(line)) return true;
        return false;
    }

    IEnumerator OpenDialogue()
    {
        SetAlpha(skipHint, 0f);
        yield return dialogueBox.Show(FormatName(speakerName), ResolvePortrait());
    }

    IEnumerator CloseDialogue()
    {
        yield return dialogueBox.Hide();
    }

    IEnumerator PlayLines(string[] lines)
    {
        foreach (string rawLine in lines)
        {
            if (skipAll) yield break;
            if (string.IsNullOrWhiteSpace(rawLine)) continue;
            yield return dialogueBox.TypeLine(FormatName(rawLine), typingSpeed);
        }
    }

    // Chọn ảnh chân dung theo nhân vật đang dùng (Boy / Girl).
    Sprite ResolvePortrait()
    {
        if (malePortrait == null) return femalePortrait;
        if (femalePortrait == null) return malePortrait;
        var switcher = FindFirstObjectByType<PlayerVisualSwitcher>();
        string visual = switcher != null && switcher.ActiveVisualRoot != null
            ? switcher.ActiveVisualRoot.name + " " + switcher.ActiveVisualName : string.Empty;
        bool female = visual.IndexOf("girl", StringComparison.OrdinalIgnoreCase) >= 0
            || visual.IndexOf("female", StringComparison.OrdinalIgnoreCase) >= 0
            || visual.IndexOf("nữ", StringComparison.OrdinalIgnoreCase) >= 0;
        return female ? femalePortrait : malePortrait;
    }

    IEnumerator ReturnToGameplayPart()
    {
        // Phần này luôn chạy hết.
        SetAlpha(skipHint, 0f);
        if (dialogueBox.IsVisible) yield return dialogueBox.Hide();

        // Mặc định: nói xong là vào chơi ngay, không tối màn hình,
        // không đặt camera ra sau lưng Player. Camera của game tự tiếp quản từ góc hiện tại.
        if (!fadeBeforeGameplay && black.color.a < 0.05f)
        {
            StartCoroutine(FadeMusic(0f, 1f));
            yield return FinishAndPlay();
            yield break;
        }

        float fadeOut = skipAll ? 0.5f : returnFadeOut;
        StartCoroutine(FadeMusic(0f, fadeOut + 0.5f));
        StopBlackFade();
        yield return Fade(black, 1f, fadeOut, false);
        SetAlpha(story, 0f);
        SetAlpha(logoGraphic, 0f);
        yield return WaitRealtime(0.4f);

        TeleportPlayer();

        // Cho camera follow chạy dưới màn đen tới khi đứng yên sau lưng Player,
        // rồi mới ghi lại góc đó. (Bản cũ chỉ đợi 0.5 giây nên camera chưa kịp về.)
        Vector3 followPosition = cam != null ? cam.transform.position : Vector3.zero;
        Quaternion followRotation = cam != null ? cam.transform.rotation : Quaternion.identity;
        if (cam != null)
        {
            Transform playerTransform = FindPlayerTransform();
            if (playerTransform != null)
            {
                // Đặt camera sẵn gần sau lưng Player để follow không phải bay từ trên trời về.
                Vector3 behind = playerTransform.position - playerTransform.forward * 4f + Vector3.up * 2.5f;
                cam.transform.SetPositionAndRotation(behind,
                    Quaternion.LookRotation(playerTransform.position + Vector3.up * 1.2f - behind));
            }

            RestoreCameraDrivers();
            var follow = cam.GetComponent<BasicCameraFollow>();
            if (follow != null)
            {
                follow.enabled = true;
                SetSoftInputLock(follow, false);   // Camera bị khóa theo Player lock -> mở riêng camera.
            }

            yield return WaitForCameraToSettle();
            followPosition = cam.transform.position;
            followRotation = cam.transform.rotation;

            if (follow != null) SetSoftInputLock(follow, true);
            DisableCameraDrivers();
        }

        // Sáng lại từ từ với camera theo Player.
        yield return Fade(black, 0f, returnFadeIn, false);

        yield return FinishAndPlay();
    }

    // Trả quyền điều khiển cho người chơi.
    IEnumerator FinishAndPlay()
    {
        SetAlpha(black, 0f);
        black.raycastTarget = false;
        RestoreCameraDrivers();
        ReleasePlayerIdle();   // Trả lại BasicPlayerMovement -> đủ animation như cũ.
        BlockGameplay(false);
        if (music != null) { music.Stop(); music.volume = musicVolume; }
        running = false;

        if (!string.IsNullOrWhiteSpace(locationTitle)) yield return LocationTitleRoutine();
        canvas.gameObject.SetActive(false);
    }

    IEnumerator LocationTitleRoutine()
    {
        location.text = FormatName(locationTitle);
        yield return WaitRealtime(0.3f);
        yield return Fade(location, 1f, 0.8f, false);
        yield return WaitRealtime(2.5f);
        yield return Fade(location, 0f, 0.8f, false);
    }

    // ---------- Camera / Player / Input ----------

    void PlaceCamera(Transform point)
    {
        if (cam != null && point != null) cam.transform.SetPositionAndRotation(point.position, point.rotation);
    }

    IEnumerator MoveCamera(Vector3 fromPosition, Quaternion fromRotation,
        Vector3 toPosition, Quaternion toRotation, float duration, bool breakOnSkip)
    {
        if (cam == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            if (breakOnSkip && skipAll) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            cam.transform.SetPositionAndRotation(
                Vector3.Lerp(fromPosition, toPosition, k),
                Quaternion.Slerp(fromRotation, toRotation, k));
            yield return null;
        }
        cam.transform.SetPositionAndRotation(toPosition, toRotation);
    }

    void DisableCameraDrivers()
    {
        if (cam == null) return;
        TryDisable(cam.GetComponent<BasicCameraFollow>());
        TryDisable(cam.GetComponent("CinemachineBrain") as Behaviour);
    }

    void TryDisable(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        behaviour.enabled = false;
        disabledCameraDrivers.Add(behaviour);
    }

    void RestoreCameraDrivers()
    {
        foreach (Behaviour behaviour in disabledCameraDrivers)
            if (behaviour != null) behaviour.enabled = true;
        disabledCameraDrivers.Clear();
    }

    IEnumerator WaitForCameraToSettle()
    {
        const float minTime = 0.4f, maxTime = 3f;
        float start = Time.unscaledTime;
        int stillFrames = 0;
        Vector3 last = cam.transform.position;
        Quaternion lastRotation = cam.transform.rotation;
        while (Time.unscaledTime - start < maxTime)
        {
            yield return null;
            bool still = (cam.transform.position - last).sqrMagnitude < 0.0001f
                && Quaternion.Angle(cam.transform.rotation, lastRotation) < 0.1f;
            stillFrames = still ? stillFrames + 1 : 0;
            last = cam.transform.position;
            lastRotation = cam.transform.rotation;
            if (stillFrames >= 8 && Time.unscaledTime - start >= minTime) yield break;
        }
    }

    static void SetSoftInputLock(Behaviour behaviour, bool locked)
    {
        var method = behaviour.GetType().GetMethod("SetGameplayInputLocked",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);
        if (method == null) return;
        var parameters = method.GetParameters();
        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
            method.Invoke(behaviour, new object[] { locked });
    }

    static Transform FindPlayerTransform()
    {
        var movement = FindFirstObjectByType<BasicPlayerMovement>();
        return movement != null ? movement.transform : null;
    }

    // Tắt BasicPlayerMovement (nó tự đổi idle buồn / vui / chiến đấu...) và ép Animator
    // đứng ở một state duy nhất cho tới khi vào chơi.
    void HoldPlayerIdle()
    {
        var movement = FindFirstObjectByType<BasicPlayerMovement>();
        if (movement == null) return;

        if (movement.enabled)
        {
            movement.enabled = false;
            frozenMovement = movement;
        }

        Animator animator = null;
        var switcher = movement.GetComponent<PlayerVisualSwitcher>();
        if (switcher != null) animator = switcher.ActiveVisualAnimator;
        if (animator == null) animator = movement.GetComponentInChildren<Animator>();
        if (animator == null || string.IsNullOrEmpty(introIdleState)) return;

        animator.enabled = true;
        int hash = Animator.StringToHash(introIdleState);
        if (animator.HasState(0, hash))
        {
            animator.CrossFadeInFixedTime(hash, 0.15f, 0);
            idleAnimator = animator;
            idleHash = hash;
        }
        else Debug.LogWarning($"{name}: Animator của Player không có state '{introIdleState}'.", this);
    }

    void ReleasePlayerIdle()
    {
        if (frozenMovement != null) frozenMovement.enabled = true;
        frozenMovement = null;
        idleAnimator = null;
    }

    // Nếu Animator tự chuyển sang state khác (transition có Exit Time...) thì kéo về lại.
    void LateUpdate()
    {
        if (frozenMovement == null || idleAnimator == null || !idleAnimator.isActiveAndEnabled) return;
        if (idleAnimator.IsInTransition(0)) return;
        if (idleAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash != idleHash)
            idleAnimator.CrossFadeInFixedTime(idleHash, 0.2f, 0);
    }

    void TeleportPlayer()
    {
        if (playerSpawnPoint == null) return;
        var movement = FindFirstObjectByType<BasicPlayerMovement>();
        if (movement == null) return;

        // CharacterController phải tắt thì mới dời vị trí được.
        var controller = movement.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        movement.transform.SetPositionAndRotation(playerSpawnPoint.position, playerSpawnPoint.rotation);
        if (controller != null) controller.enabled = controllerWasEnabled;
        Physics.SyncTransforms();
    }

    // Chế độ Dialogue của GameMenuController: khóa di chuyển, ẩn HUD + minimap,
    // Esc không mở Cài đặt.
    void BlockGameplay(bool block)
    {
        if (block)
        {
            menu = FindFirstObjectByType<GameMenuController>();
            blockedWithMenu = menu != null;
            if (blockedWithMenu) menu.SetDialogueOpen(this, true);
            else InventoryInputController.SetExternalGameplayInputBlocked(this, true);
            return;
        }

        if (blockedWithMenu && menu != null)
        {
            menu.MarkShortcutHandled(KeyCode.Escape);
            menu.SetDialogueOpen(this, false);
        }
        else InventoryInputController.SetExternalGameplayInputBlocked(this, false);
        blockedWithMenu = false;
    }

    static bool AdvancePressed() =>
        Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
        || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0);

    string FormatName(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{name}")) return text;
        string playerName = null;
        var provider = FindFirstObjectByType<PlayerProfileRuntimeProvider>(FindObjectsInactive.Include);
        if (provider != null) playerName = provider.GetSnapshot().DisplayName;
        if (string.IsNullOrWhiteSpace(playerName)) playerName = CharacterSelectionData.PlayerName;
        if (string.IsNullOrWhiteSpace(playerName)) playerName = "bạn";
        return text.Replace("{name}", playerName);
    }

    IEnumerator FadeMusic(float to, float time)
    {
        if (music == null) yield break;
        float from = music.volume;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            music.volume = Mathf.Lerp(from, to * musicVolume, t / time);
            yield return null;
        }
    }

    // ---------- UI ----------

    void StartBlackFade(float to, float time, bool breakOnSkip)
    {
        StopBlackFade();
        blackFadeRoutine = StartCoroutine(Fade(black, to, time, breakOnSkip));
    }

    void StopBlackFade()
    {
        if (blackFadeRoutine != null) StopCoroutine(blackFadeRoutine);
        blackFadeRoutine = null;
    }

    IEnumerator Fade(Graphic graphic, float to, float time, bool breakOnSkip = true)
    {
        if (graphic == null) yield break;
        float from = graphic.color.a;
        float t = 0f;
        while (t < time)
        {
            if (breakOnSkip && skipAll) yield break;
            t += Time.unscaledDeltaTime;
            SetAlpha(graphic, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time)));
            yield return null;
        }
        SetAlpha(graphic, to);
    }

    static IEnumerator FadeGroup(CanvasGroup group, float to, float time)
    {
        if (group == null) yield break;
        float from = group.alpha;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        group.alpha = to;
    }

    static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;
    }

    static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    void BuildOverlay()
    {
        if (canvas != null) return;

        var root = new GameObject("NewGameIntroCanvas", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // Trên mọi HUD / menu.
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = MenuTypography.Font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        black = CreateImage(root.transform, "Black", Color.black);
        Stretch(black.rectTransform);
        black.raycastTarget = true;   // Chặn click xuống UI bên dưới.

        story = CreateText(root.transform, "Story", font, storyFontSize, TextAnchor.MiddleCenter);
        story.rectTransform.sizeDelta = new Vector2(1400f, 400f);
        story.lineSpacing = 1.25f;

        if (logo != null)
        {
            var logoImage = CreateImage(root.transform, "Logo", Color.white);
            logoImage.sprite = logo;
            logoImage.preserveAspect = true;
            logoImage.rectTransform.sizeDelta = logoSize;
            logoGraphic = logoImage;
        }
        else
        {
            var logoText = CreateText(root.transform, "Logo", font, 96, TextAnchor.MiddleCenter);
            logoText.text = logoFallbackText;
            logoText.fontStyle = FontStyle.Bold;
            logoText.color = new Color(1f, 0.9f, 0.6f, 1f);
            logoText.rectTransform.sizeDelta = logoSize + new Vector2(400f, 0f);
            var outline = logoText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.1f, 0.02f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
            logoGraphic = logoText;
        }

        location = CreateText(root.transform, "Location", font, 36, TextAnchor.MiddleCenter);
        location.rectTransform.anchorMin = location.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        location.rectTransform.anchoredPosition = new Vector2(0f, 200f);
        location.rectTransform.sizeDelta = new Vector2(1200f, 80f);
        var shadow = location.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);

        skipHint = CreateText(root.transform, "SkipHint", font, 22, TextAnchor.LowerRight);
        skipHint.text = "Space — Tiếp      Esc — Bỏ qua";
        skipHint.rectTransform.anchorMin = skipHint.rectTransform.anchorMax = new Vector2(1f, 0f);
        skipHint.rectTransform.pivot = new Vector2(1f, 0f);
        skipHint.rectTransform.anchoredPosition = new Vector2(-40f, 30f);
        skipHint.rectTransform.sizeDelta = new Vector2(600f, 40f);

        if (dialogueBox == null) dialogueBox = GetComponent<DialogueBoxUI>();
        if (dialogueBox == null) dialogueBox = gameObject.AddComponent<DialogueBoxUI>();
        dialogueBox.Build(root.transform);
        root.SetActive(true);
    }

    static void SetInset(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static Image CreateImage(Transform parent, string objectName, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Text CreateText(Transform parent, string objectName, Font font, int size, TextAnchor anchor)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;
        text.raycastTarget = false;
        SetAlpha(text, 0f);
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // ---------- Công cụ trong Editor ----------

#if UNITY_EDITOR
    Transform CreatePointFromSceneView(string pointName)
    {
        var sceneView = UnityEditor.SceneView.lastActiveSceneView;
        if (sceneView == null) { Debug.LogWarning("Không tìm thấy cửa sổ Scene."); return null; }

        Transform container = transform.Find("IntroCameraPoints");
        if (container == null)
        {
            var containerObject = new GameObject("IntroCameraPoints");
            UnityEditor.Undo.RegisterCreatedObjectUndo(containerObject, "Create IntroCameraPoints");
            containerObject.transform.SetParent(transform, false);
            container = containerObject.transform;
        }

        var point = new GameObject(pointName);
        UnityEditor.Undo.RegisterCreatedObjectUndo(point, "Create " + pointName);
        point.transform.SetParent(container, false);
        Transform view = sceneView.camera.transform;
        point.transform.SetPositionAndRotation(view.position, view.rotation);
        return point.transform;
    }

    [ContextMenu("1. Shot mới: điểm ĐẦU = Scene View")]
    void AddShotStartFromSceneView()
    {
        UnityEditor.Undo.RecordObject(this, "Add Intro Shot");
        Transform point = CreatePointFromSceneView($"Shot{shots.Count + 1}_Start");
        if (point == null) return;
        shots.Add(new CameraShot { start = point });
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("2. Shot cuối: điểm CUỐI = Scene View")]
    void SetLastShotEndFromSceneView()
    {
        if (shots.Count == 0) { Debug.LogWarning("Hãy tạo Shot mới trước (menu số 1)."); return; }
        UnityEditor.Undo.RecordObject(this, "Set Intro Shot End");
        Transform point = CreatePointFromSceneView($"Shot{shots.Count}_End");
        if (point == null) return;
        shots[shots.Count - 1].end = point;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("3. Shot TRONG NHÀ mới: điểm ĐẦU = Scene View")]
    void AddHouseShotStartFromSceneView()
    {
        UnityEditor.Undo.RecordObject(this, "Add House Shot");
        Transform point = CreatePointFromSceneView($"House{houseShots.Count + 1}_Start");
        if (point == null) return;
        houseShots.Add(new HouseShot { start = point, duration = 6f });
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("4. Shot trong nhà cuối: điểm CUỐI = Scene View")]
    void SetLastHouseShotEndFromSceneView()
    {
        if (houseShots.Count == 0) { Debug.LogWarning("Hãy tạo Shot trong nhà trước (menu số 3)."); return; }
        UnityEditor.Undo.RecordObject(this, "Set House Shot End");
        Transform point = CreatePointFromSceneView($"House{houseShots.Count}_End");
        if (point == null) return;
        houseShots[houseShots.Count - 1].end = point;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("5. Tạo PlayerSpawnPoint ở tâm Scene View")]
    void CreateSpawnPoint()
    {
        var sceneView = UnityEditor.SceneView.lastActiveSceneView;
        if (sceneView == null) return;
        UnityEditor.Undo.RecordObject(this, "Create Spawn Point");
        if (playerSpawnPoint == null)
        {
            var point = new GameObject("PlayerSpawnPoint");
            UnityEditor.Undo.RegisterCreatedObjectUndo(point, "Create PlayerSpawnPoint");
            point.transform.SetParent(transform, false);
            playerSpawnPoint = point.transform;
        }
        playerSpawnPoint.position = sceneView.pivot;
        UnityEditor.Selection.activeTransform = playerSpawnPoint;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (CameraShot shot in shots)
        {
            if (shot == null || shot.start == null) continue;
            Gizmos.DrawSphere(shot.start.position, 0.25f);
            Gizmos.DrawRay(shot.start.position, shot.start.forward * 2f);
            if (shot.end == null) continue;
            Gizmos.DrawWireSphere(shot.end.position, 0.25f);
            Gizmos.DrawLine(shot.start.position, shot.end.position);
        }

        Gizmos.color = new Color(1f, 0.55f, 0.1f);
        foreach (HouseShot shot in houseShots)
        {
            if (shot == null || shot.start == null) continue;
            Gizmos.DrawSphere(shot.start.position, 0.12f);
            Gizmos.DrawRay(shot.start.position, shot.start.forward * 1.2f);
            if (shot.end == null) continue;
            Gizmos.DrawWireSphere(shot.end.position, 0.12f);
            Gizmos.DrawLine(shot.start.position, shot.end.position);
        }

        if (houseCameraPoint != null && houseShots.Count == 0)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(houseCameraPoint.position, 0.15f);
            Gizmos.DrawRay(houseCameraPoint.position, houseCameraPoint.forward * 1.5f);
        }

        if (playerSpawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerSpawnPoint.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawRay(playerSpawnPoint.position + Vector3.up * 0.9f, playerSpawnPoint.forward);
        }
    }
}
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tiếng rê chuột (hover) và tiếng bấm cho TẤT CẢ nút con. Gắn 1 lần lên object cha (vd. ButtonGroup hoặc Canvas).
// Rê chuột / dùng phím chọn tới nút -> Hover Clip. Bấm -> Click Clip (để trống nếu đã gắn tiếng bấm ở On Click).
[DisallowMultipleComponent]
public class UIButtonSounds : MonoBehaviour
{
    [Tooltip("Audio Source phát tiếng (vd. Ui Sound). Output nên là SFX. Để trống = tự tạo")]
    [SerializeField] AudioSource source;
    [SerializeField] AudioClip hoverClip;
    [SerializeField, Range(0f, 1f)] float hoverVolume = 0.6f;
    [Tooltip("Để trống nếu đã gắn tiếng bấm bằng On Click của từng nút (tránh kêu 2 lần)")]
    [SerializeField] AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] float clickVolume = 1f;
    [Tooltip("Bật = cả các nút được tạo sau này (vd. bảng Cài đặt dựng lúc chạy) cũng có tiếng")]
    [SerializeField] bool rescanEverySecond = true;

    float nextHoverAllowed;
    float nextScan;

    void Start()
    {
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var mixer = GameOptions.LoadMixer();
            var groups = mixer != null ? mixer.FindMatchingGroups("SFX") : null;
            if (groups != null && groups.Length > 0) source.outputAudioMixerGroup = groups[0];
        }
        Scan();
    }

    void Update()
    {
        if (!rescanEverySecond || Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1f;
        Scan();
    }

    // Gắn sự kiện hover / bấm cho các nút chưa được gắn.
    void Scan()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            var trigger = button.GetComponent<EventTrigger>();
            if (trigger != null && trigger.triggers.Exists(entry => entry.eventID == EventTriggerType.PointerEnter)) continue;
            if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

            Button captured = button;
            AddEntry(trigger, EventTriggerType.PointerEnter, () => PlayHover(captured));
            // Chọn bằng phím (W/S, mũi tên) cũng kêu; bấm chuột thì không (đã kêu lúc rê chuột vào rồi).
            AddEntry(trigger, EventTriggerType.Select, () => { if (!Input.GetMouseButton(0)) PlayHover(captured); });
            if (clickClip != null)
                captured.onClick.AddListener(() => Play(clickClip, clickVolume));
        }
    }

    static void AddEntry(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    void PlayHover(Button button)
    {
        if (button == null || !button.IsInteractable() || Time.unscaledTime < nextHoverAllowed) return;
        nextHoverAllowed = Time.unscaledTime + 0.06f;   // Tránh kêu dồn khi lướt chuột nhanh qua nhiều nút.
        Play(hoverClip, hoverVolume);
    }

    void Play(AudioClip clip, float volume)
    {
        if (clip != null && source != null) source.PlayOneShot(clip, volume);
    }
}
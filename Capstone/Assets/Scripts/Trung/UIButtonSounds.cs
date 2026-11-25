using Capstone.Game.SettingsSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tiếng rê chuột (hover) và tiếng bấm cho TẤT CẢ nút con. Gắn 1 lần lên object cha (vd. ButtonGroup hoặc Canvas).
// Rê chuột / dùng phím chọn tới nút -> Hover Clip. Bấm -> Click Clip (để trống nếu đã gắn tiếng bấm ở On Click).
[DisallowMultipleComponent]
public class UIButtonSounds : MonoBehaviour
{
    [Tooltip("Audio Source phát tiếng (vd. Ui Sound). Để trống = tự tạo. Luôn đi qua kênh UI của game")]
    [SerializeField] AudioSource source;
    [Tooltip("Để trống = dùng tiếng hover chung của game (Resources/CapstoneAudio)")]
    [SerializeField] AudioClip hoverClip;
    [SerializeField, Range(0f, 1f)] float hoverVolume = 0.6f;
    [Tooltip("Để trống = dùng tiếng bấm chung của game (Resources/CapstoneAudio)")]
    [SerializeField] AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] float clickVolume = 1f;
    [Tooltip("Bật = chỉ thêm tiếng bấm cho nút KHÔNG có gì trong On Click (các nút trong bảng Load Game, Cài đặt... do code tạo). " +
             "Nút bạn đã tự gắn tiếng bấm ở On Click sẽ không bị kêu 2 lần.\nTắt = thêm tiếng bấm cho mọi nút.")]
    [SerializeField] bool clickOnlyForCodeButtons = true;
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
        }
        // Kênh UI của game: thanh âm lượng UI / tắt tiếng trong Cài đặt đều áp dụng.
        GameAudioRouting.Route(source, GameAudioBus.UI);
        if (hoverClip == null && GameAudioRouting.Assets != null) hoverClip = GameAudioRouting.Assets.hover;
        if (clickClip == null && GameAudioRouting.Assets != null) clickClip = GameAudioRouting.Assets.click;
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
            // Nút kiểu GreenAngularGraphic của game đã tự kêu hover / bấm -> bỏ qua để không kêu 2 lần.
            if (button.GetComponent<Capstone.Game.UISystem.GreenAngularGraphic>() != null) continue;
            var trigger = button.GetComponent<EventTrigger>();
            if (trigger != null && trigger.triggers.Exists(entry => entry.eventID == EventTriggerType.PointerEnter)) continue;
            if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

            Button captured = button;
            AddEntry(trigger, EventTriggerType.PointerEnter, () => PlayHover(captured));
            // Chọn bằng phím (W/S, mũi tên) cũng kêu; bấm chuột thì không (đã kêu lúc rê chuột vào rồi).
            AddEntry(trigger, EventTriggerType.Select, () => { if (!MouseHeld()) PlayHover(captured); });
            // Nút do code tạo không có gì trong On Click (Inspector) -> thêm tiếng bấm.
            if (!clickOnlyForCodeButtons || captured.onClick.GetPersistentEventCount() == 0)
                captured.onClick.AddListener(() => Play(clickClip, clickVolume));
        }
    }

    // Dự án dùng Input System mới: Input.GetMouseButton cũ có thể báo lỗi.
    static bool MouseHeld()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        return mouse != null && mouse.leftButton.isPressed;
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

    AudioSource fallback;

    void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        AudioSource s = source;
        // Audio Source bị tắt (vd. nằm trong popup đang ẩn) thì không phát được -> dùng nguồn dự phòng.
        if (s == null || !s.isActiveAndEnabled)
        {
            if (fallback == null)
            {
                fallback = gameObject.AddComponent<AudioSource>();
                fallback.playOnAwake = false;
                fallback.spatialBlend = 0f;
                GameAudioRouting.Route(fallback, GameAudioBus.UI);
                if (source != null) Debug.LogWarning($"[UIButtonSounds] Audio Source '{source.name}' đang bị tắt / nằm trong object ẩn -> dùng nguồn dự phòng.");
            }
            s = fallback;
        }
        s.PlayOneShot(clip, volume);
    }
}
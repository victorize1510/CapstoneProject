using Capstone.Game.SettingsSystem;
using UnityEngine;

// Gắn lên object có AudioSource để thanh âm lượng trong Cài đặt chỉnh được nó.
// Dùng hệ thống âm thanh chung của game (GameAudioRouting -> CapstoneMixer).
// Nhạc nền -> Music. Bước chân, cửa, va chạm... -> Sfx. Nút bấm / hộp thoại -> Ui. Gió, nước, tiếng rừng -> Ambient.
[DisallowMultipleComponent]
public class AudioChannel : MonoBehaviour
{
    public enum Channel { Music, Sfx, Ui, Ambient }

    [SerializeField] Channel channel = Channel.Sfx;
    [Tooltip("Bật = áp dụng cho mọi AudioSource con (gắn 1 lần lên object cha chứa nhiều âm thanh)")]
    [SerializeField] bool includeChildren = true;

    void Awake() => Apply();

    [ContextMenu("Gán lại Output")]
    public void Apply()
    {
        GameAudioBus bus = channel switch
        {
            Channel.Music => GameAudioBus.Music,
            Channel.Ui => GameAudioBus.UI,
            Channel.Ambient => GameAudioBus.Ambient,
            _ => GameAudioBus.SFX
        };
        AudioSource[] sources = includeChildren ? GetComponentsInChildren<AudioSource>(true) : GetComponents<AudioSource>();
        foreach (AudioSource source in sources) GameAudioRouting.Route(source, bus);
    }
}
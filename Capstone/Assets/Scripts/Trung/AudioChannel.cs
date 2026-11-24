using UnityEngine;
using UnityEngine.Audio;

// Gắn lên object có AudioSource để thanh "Nhạc nền" / "Hiệu ứng âm thanh" trong Cài đặt chỉnh được nó.
// Nhạc nền (BGM, nhạc menu) -> Music.  Tiếng bước chân, nút bấm, chiêu thức, cửa mở... -> Sfx.
[DisallowMultipleComponent]
public class AudioChannel : MonoBehaviour
{
    public enum Channel { Music, Sfx }

    [SerializeField] Channel channel = Channel.Sfx;
    [Tooltip("Tên Group trong AudioMixer")]
    [SerializeField] string musicGroup = "Music";
    [SerializeField] string sfxGroup = "SFX";
    [Tooltip("Bật = gán cho mọi AudioSource con (vd. gắn 1 lần lên object cha chứa nhiều âm thanh)")]
    [SerializeField] bool includeChildren = true;

    void Awake() => Apply();

    [ContextMenu("Gán lại Output")]
    public void Apply()
    {
        AudioMixer mixer = GameOptions.LoadMixer();
        if (mixer == null)
        {
            Debug.LogWarning($"{name}: không tìm thấy AudioMixer 'Resources/{GameOptions.MixerResourceName}'.", this);
            return;
        }

        string groupName = channel == Channel.Music ? musicGroup : sfxGroup;
        AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
        if (groups == null || groups.Length == 0)
        {
            Debug.LogWarning($"{name}: AudioMixer không có Group '{groupName}'.", this);
            return;
        }

        AudioSource[] sources = includeChildren ? GetComponentsInChildren<AudioSource>(true) : GetComponents<AudioSource>();
        foreach (AudioSource source in sources) source.outputAudioMixerGroup = groups[0];
    }
}
using UnityEngine;

// Object này BẬT -> phát bài riêng, TẮT -> trả lại nhạc cũ.
// Gắn lên object chỉ bật lúc đặc biệt: màn tiến hóa, trận đấu boss, cutscene...
public class MusicOverride : MonoBehaviour
{
    [SerializeField] AudioClip clip;
    [SerializeField, Range(0f, 1f)] float volume = 1f;

    void OnEnable() => MusicManager.Push(this, clip, volume);
    void OnDisable() => MusicManager.Release(this);
}
using Capstone.Game.QuestSystem;
using UnityEngine;

// Đổi nhạc khi Player vào khu hang động trong DemoScene, ra khỏi hang thì về lại nhạc DemoScene.
// Gắn lên 1 object bất kỳ trong DemoScene. KHÔNG cần đặt Collider:
// tự dùng vùng hang có sẵn của ChapterWorldLayout (cùng vùng mà minimap / nhiệm vụ dùng để biết "đang ở trong hang").
public class CaveMusicSwitcher : MonoBehaviour
{
    [Tooltip("Nhạc của map hang (cavescene)")]
    [SerializeField] AudioClip caveMusic;
    [SerializeField, Range(0f, 1f)] float volume = 0.6f;
    [Tooltip("Mấy giây kiểm tra vị trí Player 1 lần")]
    [SerializeField, Min(0.1f)] float checkInterval = 0.5f;

    [Header("Dự phòng (scene không có ChapterWorldLayout)")]
    [Tooltip("Collider bao quanh khu hang. Chỉ dùng khi không tìm thấy ChapterWorldLayout")]
    [SerializeField] Collider caveArea;

    [SerializeField] bool logChanges = true;

    ChapterWorldLayout world;
    Transform player;
    bool inCave;
    float nextCheck;

    void Start()
    {
        world = FindFirstObjectByType<ChapterWorldLayout>(FindObjectsInactive.Include);
        if (world == null && caveArea == null)
            Debug.LogWarning($"{name}: không có ChapterWorldLayout và chưa gắn Cave Area -> không biết đâu là hang.", this);
        if (caveMusic == null)
            Debug.LogWarning($"{name}: chưa gắn Cave Music.", this);
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkInterval;

        if (player == null)
        {
            var movement = FindFirstObjectByType<BasicPlayerMovement>();
            if (movement == null) return;
            player = movement.transform;
        }

        bool nowInCave = IsInCave(player.position);
        if (nowInCave == inCave) return;
        inCave = nowInCave;

        if (inCave && caveMusic != null) MusicManager.Push(this, caveMusic, volume);
        else MusicManager.Release(this);
        if (logChanges) Debug.Log(inCave ? "[Music] Vào hang -> nhạc hang." : "[Music] Ra khỏi hang -> nhạc DemoScene.", this);
    }

    bool IsInCave(Vector3 position)
    {
        if (world != null && world.isActiveAndEnabled) return world.InCave(position);
        if (caveArea != null)
        {
            Bounds b = caveArea.bounds;
            return position.x >= b.min.x && position.x <= b.max.x && position.z >= b.min.z && position.z <= b.max.z;
        }
        return false;
    }

    void OnDisable()
    {
        inCave = false;
        MusicManager.Release(this);
    }
}
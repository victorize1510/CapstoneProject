using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Bộ điều khiển hiệu ứng dưới nước. Đặt lên Main Camera.
/// Mỗi khung hình kiểm tra camera có nằm trong khối nước nào (WaterTrigger) không:
/// có -> bật Full Screen Pass (hiệu ứng nước), không -> tắt.
/// Camera KHÔNG cần Collider hay Rigidbody.
/// </summary>
public class UnderwaterController : MonoBehaviour
{
    public static UnderwaterController Instance { get; private set; }

    [Tooltip("Kéo feature 'Full Screen Pass Renderer Feature' (sub-asset trong PC_Renderer) vào đây")]
    [SerializeField] private ScriptableRendererFeature underwaterFeature;
    [Tooltip("Để trống = camera gắn script này (hoặc Main Camera)")]
    [SerializeField] private Transform viewPoint;
    [Tooltip("Bật = tính theo ĐẦU NHÂN VẬT (lội xuống ngập đầu là bật). Tắt = tính theo camera chìm dưới mặt nước")]
    [SerializeField] private bool usePlayerHead = true;
    [Tooltip("Chiều cao đầu nhân vật tính từ chân (m)")]
    [SerializeField, Min(0f)] private float headHeight = 1.5f;
    [SerializeField] private bool logChanges = true;

    Transform player;

    bool underwater;
    int manualCount;   // Giữ cho code cũ gọi EnterWater/ExitWater.

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnEnable()
    {
        underwater = false;
        manualCount = 0;
        SetFeature(false);
    }

    void OnDisable() => SetFeature(false);
    void OnApplicationQuit() => SetFeature(false);   // Không để asset renderer bị "kẹt" bật sau khi dừng Play.

    void Start()
    {
        // Hiệu ứng nằm trong PC_Renderer -> chỉ chạy khi game đang dùng PC_RPAsset.
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Debug.Log($"[Underwater] Pipeline đang dùng: {(pipeline != null ? pipeline.name : "không có")}. Khối nước tìm thấy: {WaterTrigger.All.Count}." +
                  (underwaterFeature == null ? " CHƯA gắn Underwater Feature!" : ""), this);
    }

    void LateUpdate()
    {
        Vector3 point;
        if (viewPoint != null) point = viewPoint.position;
        else if (usePlayerHead)
        {
            if (player == null)
            {
                var move = FindFirstObjectByType<BasicPlayerMovement>();
                if (move == null) return;
                player = move.transform;
            }
            point = player.position + Vector3.up * headHeight;
        }
        else
        {
            Transform cam = GetComponent<Camera>() != null ? transform : Camera.main != null ? Camera.main.transform : null;
            if (cam == null) return;
            point = cam.position;
        }

        bool inside = manualCount > 0;
        if (!inside)
            foreach (var water in WaterTrigger.All)
                if (water != null && water.Contains(point)) { inside = true; break; }

        if (inside == underwater) return;
        underwater = inside;
        SetFeature(inside);
        if (logChanges) Debug.Log(inside ? "[Underwater] Camera xuống nước -> bật hiệu ứng." : "[Underwater] Camera lên khỏi nước -> tắt hiệu ứng.", this);
    }

    void SetFeature(bool on)
    {
        if (underwaterFeature != null) underwaterFeature.SetActive(on);
    }

    // Giữ tương thích với code cũ.
    public void EnterWater() => manualCount++;
    public void ExitWater() => manualCount = Mathf.Max(0, manualCount - 1);
}
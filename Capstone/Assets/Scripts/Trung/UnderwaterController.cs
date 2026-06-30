using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Bộ điều khiển trung tâm cho hiệu ứng underwater.
/// Đặt script này lên Main Camera (hoặc một GameObject quản lý luôn tồn tại).
///
/// Nó KHÔNG so sánh độ cao nữa. Thay vào đó, mỗi hồ (WaterTrigger) sẽ gọi
/// EnterWater()/ExitWater() khi player bơi vào/ra. Dùng bộ đếm để lỡ player
/// đứng giữa 2 vùng nước chồng nhau cũng không bị tắt nhầm.
/// </summary>
public class UnderwaterController : MonoBehaviour
{
    public static UnderwaterController Instance { get; private set; }

    [Tooltip("Kéo feature 'Full Screen Pass Renderer Feature' (sub-asset trong PC_Renderer) vào đây")]
    [SerializeField] private ScriptableRendererFeature underwaterFeature;

    private int submergedCount = 0;

    void Awake()
    {
        // Chỉ giữ 1 instance (phòng khi Main Camera nằm trong DontDestroyOnLoad và scene load lại)
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        // Mặc định tắt khi bắt đầu (player đang trên cạn)
        if (underwaterFeature != null) underwaterFeature.SetActive(false);
        submergedCount = 0;
    }

    /// <summary>Gọi khi player bắt đầu chạm vào một khối nước.</summary>
    public void EnterWater()
    {
        submergedCount++;
        if (submergedCount == 1 && underwaterFeature != null)
            underwaterFeature.SetActive(true);
    }

    /// <summary>Gọi khi player rời khỏi một khối nước.</summary>
    public void ExitWater()
    {
        submergedCount = Mathf.Max(0, submergedCount - 1);
        if (submergedCount == 0 && underwaterFeature != null)
            underwaterFeature.SetActive(false);
    }
}

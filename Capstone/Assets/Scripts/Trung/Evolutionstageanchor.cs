using UnityEngine;

/// <summary>
/// Đặt script này lên 1 GameObject trống ("EvolutionAnchor") trong EvolutionScene,
/// tại đúng vị trí bạn muốn con thú xuất hiện để chạy animation tiến hóa
/// (thường là chính giữa khung hình, trước mặt camera của Evolution Scene).
///
/// EvolutionController sẽ tìm tới Instance này ngay sau khi Evolution Scene load xong,
/// để biết cần đặt con thú vào đâu.
/// </summary>
public class EvolutionStageAnchor : MonoBehaviour
{
    public static EvolutionStageAnchor Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

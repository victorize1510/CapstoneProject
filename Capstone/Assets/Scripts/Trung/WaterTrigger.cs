using UnityEngine;

/// <summary>
/// Gắn lên object nước (Water Surface) cùng một Box Collider đã tick Is Trigger.
/// Khối Box Collider phải có MẶT TRÊN ngang đúng mặt nước, kéo xuống tới đáy hồ.
///
/// Khi CAMERA chìm xuống dưới mặt nước (đi vào trong khối box) -> bật underwater.
/// Khi camera nổi lên trên mặt nước (ra khỏi box) -> tắt.
/// Player không cần lặn sâu; chỉ cần lội vào tới mức camera thụt xuống dưới nước.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaterTrigger : MonoBehaviour
{
    [Tooltip("Tag của camera cần theo dõi. Main Camera mặc định có tag 'MainCamera'.")]
    [SerializeField] private string cameraTag = "MainCamera";

    // Tự bật isTrigger khi mới add component cho đỡ quên
    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(cameraTag)) return;
        if (UnderwaterController.Instance != null)
            UnderwaterController.Instance.EnterWater();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(cameraTag)) return;
        if (UnderwaterController.Instance != null)
            UnderwaterController.Instance.ExitWater();
    }
}
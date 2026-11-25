using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gắn lên object nước (Water Surface) cùng một Box Collider (Is Trigger).
/// Khối Box Collider phải có MẶT TRÊN ngang đúng mặt nước, kéo xuống tới đáy hồ.
///
/// Không cần gắn Collider / Rigidbody cho camera nữa: UnderwaterController tự kiểm tra
/// vị trí camera có nằm trong khối nước này không, mỗi khung hình.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaterTrigger : MonoBehaviour
{
    static readonly List<WaterTrigger> all = new List<WaterTrigger>();
    public static IReadOnlyList<WaterTrigger> All => all;

    Collider area;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Awake() => area = GetComponent<Collider>();
    void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    void OnDisable() => all.Remove(this);

    /// <summary>Điểm này có nằm trong khối nước không.</summary>
    public bool Contains(Vector3 point)
    {
        if (area == null) area = GetComponent<Collider>();
        if (area == null || !area.enabled) return false;
        if (!area.bounds.Contains(point)) return false;
        // ClosestPoint trả về chính điểm đó khi điểm nằm bên trong collider.
        return (area.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
    }
}
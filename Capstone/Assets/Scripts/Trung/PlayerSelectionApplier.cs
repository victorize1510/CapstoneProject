using System;
using System.Collections;
using Capstone.Game.ProfileSystem;
using UnityEngine;

// Gắn vào Player trong DemoScene.
// Đọc lựa chọn từ CharacterCreationScene rồi áp dụng nhân vật + tên.
[DisallowMultipleComponent]
public class PlayerSelectionApplier : MonoBehaviour
{
    [Header("Nhân vật (tìm theo tên object trong danh sách Visuals)")]
    [SerializeField] PlayerVisualSwitcher visualSwitcher;
    [SerializeField] string maleVisualKeyword = "Boy";
    [SerializeField] string femaleVisualKeyword = "Girl";
    [Tooltip("Khóa không cho đổi nhân vật bằng F8 sau khi đã chọn")]
    [SerializeField] bool lockVisualAfterApply = true;

    [Header("Debug")]
    [SerializeField] bool logSteps = true;

    const string Tag = "[PlayerSelection]";

    IEnumerator Start()
    {
        Log($"Start. Gender='{CharacterSelectionData.Gender}', Name='{CharacterSelectionData.PlayerName}'");

        // Vào DemoScene trực tiếp (bấm Play ngay ở DemoScene) thì không có dữ liệu.
        if (!CharacterSelectionData.HasPendingSelection)
        {
            Log("Không có lựa chọn nào -> giữ mặc định. (Phải bấm Play từ CharacterCreationScene)");
            yield break;
        }

        string gender = CharacterSelectionData.Gender;
        string playerName = CharacterSelectionData.PlayerName;
        CharacterSelectionData.Clear();

        // Đợi các Awake/Start khác chạy xong.
        yield return null;

        ApplyVisual(gender);
        ApplyName(playerName);

        // Kiểm tra lại sau 1 giây: nếu hệ thống save nạp đè thì áp lại.
        yield return new WaitForSecondsRealtime(1f);
        if (visualSwitcher != null)
        {
            int expected = FindVisualIndex(gender);
            if (expected >= 0 && visualSwitcher.ActiveVisualIndex != expected)
            {
                Debug.LogWarning($"{Tag} Nhân vật bị đổi lại sau khi áp dụng -> áp lại.");
                ApplyVisual(gender);
            }
        }

        var provider = FindProvider();
        if (provider != null && provider.GetSnapshot().DisplayName != playerName)
        {
            Debug.LogWarning($"{Tag} Tên bị ghi đè thành '{provider.GetSnapshot().DisplayName}' (có thể do save nạp vào) -> áp lại.");
            ApplyName(playerName);
        }
    }

    void ApplyVisual(string gender)
    {
        if (visualSwitcher == null) visualSwitcher = GetComponent<PlayerVisualSwitcher>();
        if (visualSwitcher == null) visualSwitcher = FindFirstObjectByType<PlayerVisualSwitcher>();
        if (visualSwitcher == null)
        {
            Debug.LogWarning($"{Tag} Không tìm thấy PlayerVisualSwitcher.");
            return;
        }

        int index = FindVisualIndex(gender);
        if (index < 0)
        {
            Debug.LogWarning($"{Tag} Không có visual nào tên chứa '{KeywordFor(gender)}' trong danh sách Visuals.");
            return;
        }

        // SwitchTo bị bỏ qua nếu đang khóa, nên mở khóa trước.
        visualSwitcher.SetRuntimeSwitchLocked(false);
        visualSwitcher.SwitchTo(index, true);
        if (lockVisualAfterApply) visualSwitcher.SetRuntimeSwitchLocked(true);

        Log($"Gender '{gender}' -> visual {index} ({visualSwitcher.ActiveVisualName}). Đang active: {visualSwitcher.ActiveVisualIndex}");
    }

    int FindVisualIndex(string gender)
    {
        if (visualSwitcher == null) return -1;
        string keyword = KeywordFor(gender);
        for (int i = 0; i < 64; i++)
        {
            if (!visualSwitcher.TryGetVisualProfile(i, out var profile)) continue;
            if (Contains(profile.root != null ? profile.root.name : null, keyword)
                || Contains(profile.displayName, keyword))
                return i;
        }
        return -1;
    }

    string KeywordFor(string gender) =>
        gender == CharacterSelectionData.Female ? femaleVisualKeyword : maleVisualKeyword;

    static bool Contains(string value, string keyword) =>
        !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(keyword)
        && value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

    void ApplyName(string playerName)
    {
        var provider = FindProvider();
        if (provider == null)
        {
            Debug.LogWarning($"{Tag} Không tìm thấy PlayerProfileRuntimeProvider.");
            return;
        }

        // TrySetDisplayName tự bắn ProfileChanged -> Trainer Menu / Profile cập nhật tên.
        if (provider.TrySetDisplayName(playerName, out string error))
            Log($"Đã đặt tên '{playerName}'. Snapshot hiện tại: '{provider.GetSnapshot().DisplayName}'");
        else
            Debug.LogWarning($"{Tag} Không đặt được tên '{playerName}': {error}");
    }

    static PlayerProfileRuntimeProvider FindProvider() =>
        FindFirstObjectByType<PlayerProfileRuntimeProvider>(FindObjectsInactive.Include);

    void Log(string message)
    {
        if (logSteps) Debug.Log($"{Tag} {message}");
    }
}
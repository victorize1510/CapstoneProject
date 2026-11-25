using System;
using System.Reflection;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.SaveSystem;
using UnityEngine;

// Quản lý nhiều ô save. Ô 1 = "main" (save cũ của bạn vẫn nằm ở ô 1).
// Main Menu chọn ô -> SaveSlotSession.Current. Vào scene gameplay, PlayerContinueState
// gán ô đó cho PlayerSaveController + QuestSaveController trước khi chúng nạp save.
public static class SaveSlotSession
{
    public static readonly string[] Slots = { "main", "slot2", "slot3" };
    const string LastSlotKey = "saveslot.last";

    static string current;
    public static string Current
    {
        get
        {
            if (string.IsNullOrEmpty(current)) current = PlayerPrefs.GetString(LastSlotKey, Slots[0]);
            return current;
        }
        set
        {
            current = string.IsNullOrWhiteSpace(value) ? Slots[0] : value.Trim();
            PlayerPrefs.SetString(LastSlotKey, current);
            PlayerPrefs.Save();
        }
    }

    public static int IndexOf(string slot) => Array.IndexOf(Slots, slot);
    public static string DisplayName(string slot) => $"Ô lưu {IndexOf(slot) + 1}";

    public static bool Exists(string slot) => new PlayerJsonFileSaveStore().Exists(slot);

    public static string FirstEmptySlot()
    {
        foreach (var s in Slots) if (!Exists(s)) return s;
        return null;
    }

    // ----- Thông tin hiển thị trong danh sách -----
    public struct Info
    {
        public bool exists, readable;
        public string playerName, scene;
        public int level, gold, partyCount;
        public double playSeconds;
        public DateTime savedAtLocal;
    }

    public static Info ReadInfo(string slot)
    {
        var info = new Info();
        var store = new PlayerJsonFileSaveStore();
        info.exists = store.Exists(slot);
        if (!info.exists) return info;

        if (store.TryLoad(slot, out PlayerSaveData data, out _) && data != null)
        {
            info.readable = true;
            info.playerName = data.profile != null ? data.profile.displayName : "";
            info.level = data.profile != null ? data.profile.level : 0;
            info.playSeconds = data.profile != null ? data.profile.playTimeSeconds : 0;
            info.gold = data.currency != null ? data.currency.gold : 0;
            info.partyCount = data.pets != null && data.pets.partyPetIds != null ? data.pets.partyPetIds.Count : 0;
            if (DateTime.TryParse(data.savedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc))
                info.savedAtLocal = utc.ToLocalTime();
        }
        if (info.savedAtLocal == default)
        {
            try { info.savedAtLocal = System.IO.File.GetLastWriteTime(store.GetPath(slot)); } catch { }
        }
        PlayerContinueState.HasSavedScene(slot, out info.scene);
        return info;
    }

    // ----- Chơi mới / xóa -----
    // Đánh dấu ô sẽ bị xóa khi vào scene gameplay (PlayerSaveController tự xóa).
    public static void RequestReset(string slot)
    {
        try
        {
            string marker = new PlayerJsonFileSaveStore().GetPath(slot) + ".reset-pending";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(marker));
            System.IO.File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
        }
        catch (Exception e) { Debug.LogWarning("Không tạo được đánh dấu New Game: " + e.Message); }
        PlayerContinueState.Clear(slot);
    }

    public static void CancelReset(string slot)
    {
        try
        {
            string marker = new PlayerJsonFileSaveStore().GetPath(slot) + ".reset-pending";
            if (System.IO.File.Exists(marker)) System.IO.File.Delete(marker);
        }
        catch (Exception e) { Debug.LogWarning("Không xóa được dấu New Game: " + e.Message); }
    }

    public static bool Delete(string slot, out string error)
    {
        var store = new PlayerJsonFileSaveStore();
        if (!store.TryDelete(slot, out error)) return false;
        new QuestJsonFileSaveStore(store.RootDirectory).TryDelete(slot, out _);
        CancelReset(slot);
        PlayerContinueState.Clear(slot);
        return true;
    }

    // ----- Gán ô cho các hệ thống save trong scene -----
    // Code save của bạn mình để slotId là private nên phải gán bằng Reflection.
    // (Nếu bạn ấy thêm hàm public SetSlot thì đổi sang gọi hàm đó.)
    public static void ApplyToScene(string slot)
    {
        foreach (var c in UnityEngine.Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetField(c, slot);
        foreach (var c in UnityEngine.Object.FindObjectsByType<Capstone.Game.QuestSystem.Save.QuestSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetField(c, slot);
    }

    static void SetField(UnityEngine.Object target, string slot)
    {
        var field = target.GetType().GetField("slotId", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null) { Debug.LogWarning($"[SaveSlot] {target.GetType().Name} không có biến slotId."); return; }
        field.SetValue(target, slot);
    }

    // ----- Lưu trong game vào ô bất kỳ (ghi đè được) -----
    // Đổi ô của PlayerSaveController sang ô mới rồi lưu. Từ đó game tự lưu tiếp vào ô mới.
    public static bool SaveInto(string slot, out string message)
    {
        var save = UnityEngine.Object.FindFirstObjectByType<PlayerSaveController>();
        if (save == null) { message = "Hiện chưa có phiên chơi để lưu."; return false; }
        if (save.IsSaveBlocked) { message = "Chưa lưu được: " + save.SaveBlockedReason; return false; }
        if (save.HasSave && !save.IsCurrentSlotLoaded)
        {
            message = "Hãy tải bản lưu hiện có trước khi lưu.";
            return false;
        }

        string oldSlot = save.SlotId;
        object oldLoadedPath = GetPrivate(save, "loadedSlotPath");
        var continueState = UnityEngine.Object.FindFirstObjectByType<PlayerContinueState>();

        if (slot != oldSlot)
        {
            CancelReset(slot);                     // Không để ô đích bị xóa lần sau.
            SetPrivate(save, "slotId", slot);
            // Cho phép ghi đè ô đích: coi như ô đó là ô đang chơi.
            SetPrivate(save, "loadedSlotPath", new PlayerJsonFileSaveStore().GetPath(slot));
            continueState?.SetSlot(slot);
        }

        if (save.SaveNow())
        {
            Current = slot;
            ApplyToScene(slot);
            message = $"Đã lưu vào {DisplayName(slot)}.";
            return true;
        }

        // Lưu lỗi -> trả về ô cũ.
        string reason = save.SaveBlockedReason;
        if (slot != oldSlot)
        {
            SetPrivate(save, "slotId", oldSlot);
            SetPrivate(save, "loadedSlotPath", oldLoadedPath);
            continueState?.SetSlot(oldSlot);
        }
        message = string.IsNullOrEmpty(reason) ? "Chưa lưu được tiến trình. Hãy thử lại." : "Chưa lưu được: " + reason;
        return false;
    }

    static object GetPrivate(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

    static void SetPrivate(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) Debug.LogWarning($"[SaveSlot] {target.GetType().Name} không có biến {name}.");
        else field.SetValue(target, value);
    }

    public static string FormatPlayTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours} giờ {t.Minutes:00} phút" : $"{t.Minutes} phút";
    }
}
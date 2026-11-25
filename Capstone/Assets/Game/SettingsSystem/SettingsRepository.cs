using System;
using UnityEngine;

namespace Capstone.Game.SettingsSystem {
    public interface ISettingsStorage { string Read(); void Write(string json); }
    public sealed class PlayerPrefsSettingsStorage : ISettingsStorage {
        public const string Key = "Capstone.UserSettings.v1";
        public string Read() => PlayerPrefs.GetString(Key, "");
        public void Write(string json) { PlayerPrefs.SetString(Key, json); PlayerPrefs.Save(); }
    }

    public sealed class SettingsRepository {
        readonly ISettingsStorage storage;
        public SettingsRepository(ISettingsStorage storage) { this.storage = storage ?? throw new ArgumentNullException(nameof(storage)); }
        public GameSettingsData Load(GameSettingsData defaults, out string warning) {
            warning = ""; var result = defaults.Copy(); string json = storage.Read();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try {
                JsonUtility.FromJsonOverwrite(json, result);
                if (result.version != 1) { warning = "Phiên bản cài đặt không tương thích; dùng giá trị mặc định."; return defaults.Copy(); }
                result.Normalize(); return result;
            } catch (ArgumentException exception) {
                warning = "Không đọc được cài đặt; dùng giá trị mặc định. " + exception.Message;
                return defaults.Copy();
            }
        }
        public void Save(GameSettingsData data) { data.Normalize(); storage.Write(JsonUtility.ToJson(data)); }
    }
}

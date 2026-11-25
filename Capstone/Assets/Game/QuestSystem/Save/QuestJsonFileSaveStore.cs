using System;
using System.IO;
using System.Text;
using Capstone.Game.SaveSystem;
using Newtonsoft.Json;
using UnityEngine;

namespace Capstone.Game.QuestSystem.Save {
    public sealed class QuestJsonFileSaveStore : IQuestSaveStore {
        static readonly object FileLock = new object();
        readonly string rootDirectory;
        public QuestJsonFileSaveStore(string rootDirectory = null) {
            this.rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "Saves") : rootDirectory;
        }
        public bool Exists(string slotId) => File.Exists(GetPath(slotId)) || File.Exists(GetPath(slotId) + ".bak");

        public bool TrySave(string slotId, QuestSaveData data, out string error) {
            lock (FileLock) {
                error = string.Empty;
                try {
                    if (data == null || data.version != 1) throw new InvalidDataException("Unsupported quest save.");
                    string json = JsonUtility.ToJson(data, true);
                    Read(json);
                    string path = GetPath(slotId);
                    bool primaryValid = TryRead(path, out _, out _, out bool future);
                    bool backupValid = TryRead(path + ".bak", out _, out _, out bool backupFuture);
                    if (future || backupFuture) throw new InvalidDataException("Newer quest save/backup is protected.");
                    if (Exists(slotId) && !primaryValid && !backupValid) throw new InvalidDataException("No valid quest save/backup; recovery is required.");
                    AtomicSaveFile.Write(path, json, primaryValid);
                    return true;
                } catch (Exception exception) { error = exception.Message; return false; }
            }
        }

        public bool TryLoad(string slotId, out QuestSaveData data, out string error) {
            lock (FileLock) {
                string path = GetPath(slotId);
                if (TryRead(path, out data, out error, out bool future)) return true;
                if (future) return false;
                string primaryError = error;
                if (TryRead(path + ".bak", out data, out error, out _)) return true;
                if (string.IsNullOrEmpty(error)) error = primaryError;
                return false;
            }
        }

        public bool TryDelete(string slotId, out string error) {
            lock (FileLock) {
                error = string.Empty;
                try {
                    foreach (string suffix in new[] { "", ".bak", ".tmp" }) {
                        string path = GetPath(slotId) + suffix;
                        if (File.Exists(path)) File.Delete(path);
                    }
                    return true;
                } catch (Exception exception) { error = exception.Message; return false; }
            }
        }

        public string GetPath(string slotId) => Path.Combine(rootDirectory, "quests_" + AtomicSaveFile.Slot(slotId) + ".json");

        static QuestSaveData Read(string json) {
            var root = PlayerSaveMigration.Parse(json);
            int version = PlayerSaveMigration.ReadVersion(root, 1);
            if (version > 1) throw new NotSupportedException("Newer quest save version: " + version);
            if (version < 1 || (root["questStates"] == null && root["activeQuests"] == null && root["completedQuestIds"] == null))
                throw new InvalidDataException("No recognized quest data in save.");
            var data = new QuestSaveData();
            JsonUtility.FromJsonOverwrite(root.ToString(Formatting.None), data);
            return data;
        }

        static bool TryRead(string path, out QuestSaveData data, out string error, out bool future) {
            data = null; error = string.Empty; future = false;
            try {
                if (!File.Exists(path)) return false;
                data = Read(File.ReadAllText(path, Encoding.UTF8));
                return true;
            } catch (NotSupportedException exception) { future = true; error = exception.Message; return false; }
            catch (Exception exception) { error = exception.Message; return false; }
        }
    }
}

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Capstone.Game.SaveSystem {
    public sealed class PlayerJsonFileSaveStore {
        static readonly object FileLock = new object();
        readonly string rootDirectory;
        public string RootDirectory => rootDirectory;
        public bool RecoveredFromBackup { get; private set; }
        public int LoadedVersion { get; private set; }

        public PlayerJsonFileSaveStore(string rootDirectory = null) {
            this.rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "Saves") : rootDirectory;
        }

        public bool Exists(string slotId) => File.Exists(GetPath(slotId)) || File.Exists(GetPath(slotId) + ".bak");

        public bool TrySave(string slotId, PlayerSaveData data, out string error) {
            lock (FileLock) {
                error = string.Empty;
                try {
                    PlayerSaveMigration.Validate(data);
                    string json = PlayerSaveMigration.Write(data);
                    PlayerSaveMigration.Read(json, out _);
                    string path = GetPath(slotId);
                    bool primaryValid = TryRead(path, out _, out _, out bool future, out int sourceVersion);
                    if (future) throw new InvalidDataException("Refusing to overwrite a newer player save.");
                    bool backupValid = TryRead(path + ".bak", out _, out _, out bool backupFuture, out int backupVersion);
                    if (backupFuture) throw new InvalidDataException("Refusing to overwrite a slot with a newer backup.");
                    if (Exists(slotId) && !primaryValid && !backupValid) throw new InvalidDataException("No valid save/backup; explicit reset or recovery is required before saving.");
                    string source = primaryValid ? path : backupValid ? path + ".bak" : null;
                    int version = primaryValid ? sourceVersion : backupVersion;
                    if (source != null && version < PlayerSaveData.CurrentVersion) {
                        string archive = path + ".pre-v" + PlayerSaveData.CurrentVersion + ".bak";
                        if (!File.Exists(archive)) File.Copy(source, archive, false);
                    }
                    AtomicSaveFile.Write(path, json, primaryValid);
                    return true;
                } catch (Exception exception) {
                    error = exception.Message;
                    return false;
                }
            }
        }

        public bool TryLoad(string slotId, out PlayerSaveData data, out string error) {
            lock (FileLock) {
                RecoveredFromBackup = false;
                LoadedVersion = 0;
                string path = GetPath(slotId);
                if (TryRead(path, out data, out error, out bool future, out int version)) {
                    LoadedVersion = version;
                    return true;
                }
                if (future) return false;
                string primaryError = error;
                if (TryRead(path + ".bak", out data, out error, out _, out version)) {
                    LoadedVersion = version;
                    RecoveredFromBackup = true;
                    return true;
                }
                error = string.IsNullOrEmpty(error) ? primaryError : error;
                return false;
            }
        }

        public bool TryDelete(string slotId, out string error) {
            lock (FileLock) {
                error = string.Empty;
                try {
                    string path = GetPath(slotId);
                    foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
                    return true;
                } catch (Exception exception) { error = exception.Message; return false; }
            }
        }

        public string GetPath(string slotId) => Path.Combine(rootDirectory, "player_" + AtomicSaveFile.Slot(slotId) + ".json");

        static bool TryRead(string path, out PlayerSaveData data, out string error, out bool future, out int version) {
            data = null;
            error = string.Empty;
            future = false;
            version = 0;
            try {
                if (!File.Exists(path)) return false;
                data = PlayerSaveMigration.Read(File.ReadAllText(path, Encoding.UTF8), out version);
                return true;
            } catch (NotSupportedException exception) { future = true; error = exception.Message; return false; }
            catch (Exception exception) { error = exception.Message; return false; }
        }
    }
}

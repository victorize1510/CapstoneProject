using System;
using UnityEngine;

namespace Capstone.Game.SettingsSystem {
    [Flags]
    public enum SettingsArea { Audio = 1, Camera = 2, Interface = 4, Display = 8, All = 15 }

    [Serializable]
    public sealed class GameSettingsData {
        public int version = 1;
        public float masterVolume = 1;
        public bool muteAll;
        public float mouseSensitivity = .5f;
        public bool invertY;
        public bool cameraSmoothing = true;
        public bool showInteractionPrompts = true;
        public bool showMinimap = true;
        // TestChar intentionally hides its standalone tracker by default.
        public bool showQuestTracker;
        public bool showSaveIndicator = true;
        public string languageCode = "vi";
        public bool fullscreen;
        public int resolutionWidth;
        public int resolutionHeight;

        public void Normalize() {
            masterVolume = float.IsFinite(masterVolume) ? Mathf.Clamp01(masterVolume) : 1;
            mouseSensitivity = float.IsFinite(mouseSensitivity) ? Mathf.Clamp01(mouseSensitivity) : .5f;
            languageCode = "vi";
            if (resolutionWidth < 1 || resolutionHeight < 1) resolutionWidth = resolutionHeight = 0;
        }
        public GameSettingsData Copy() => JsonUtility.FromJson<GameSettingsData>(JsonUtility.ToJson(this));
    }

    public sealed class SettingsState {
        readonly GameSettingsData defaults;
        public GameSettingsData Data { get; private set; }
        public event Action<SettingsArea> Changed;
        public SettingsState(GameSettingsData values, GameSettingsData defaults = null) {
            this.defaults = (defaults ?? new GameSettingsData()).Copy();
            Data = (values ?? this.defaults).Copy(); Data.Normalize();
        }
        public void Change(SettingsArea area, Action<GameSettingsData> change) {
            change(Data); Data.Normalize(); Changed?.Invoke(area);
        }
        public void Reset() { Data = defaults.Copy(); Data.Normalize(); Changed?.Invoke(SettingsArea.All); }
    }
}

using System;
using UnityEngine;
using Capstone.Game.PetSummon;

namespace Capstone.Game.SettingsSystem {
    [Flags]
    public enum SettingsArea { Audio = 1, Camera = 2, Interface = 4, Display = 8, VisualEffects = 16, All = 31 }

    [Serializable]
    public sealed class GameSettingsData {
        public int version = 1;
        public float masterVolume = 1;
        public float musicVolume = 1;
        public float sfxVolume = 1;
        public float skillSfxVolume = 1;
        public float petCryVolume = 1;
        public float uiVolume = 1;
        public float ambientVolume = 1;
        public bool muteAll;
        public PetVFXQuality vfxQuality = PetVFXQuality.Medium;
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
            masterVolume = Volume(masterVolume);
            musicVolume = Volume(musicVolume);
            sfxVolume = Volume(sfxVolume);
            skillSfxVolume = Volume(skillSfxVolume);
            petCryVolume = Volume(petCryVolume);
            uiVolume = Volume(uiVolume);
            ambientVolume = Volume(ambientVolume);
            if (vfxQuality < PetVFXQuality.Low || vfxQuality > PetVFXQuality.High)
                vfxQuality = PetVFXQuality.Medium;
            mouseSensitivity = float.IsFinite(mouseSensitivity) ? Mathf.Clamp01(mouseSensitivity) : .5f;
            languageCode = "vi";
            if (resolutionWidth < 1 || resolutionHeight < 1) resolutionWidth = resolutionHeight = 0;
        }
        static float Volume(float value) => float.IsFinite(value) ? Mathf.Clamp01(value) : 1;
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

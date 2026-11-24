using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Capstone.Game.SettingsSystem
{
    public enum GameAudioBus { Music, SFX, Skill, PetCry, UI, Ambient }

    /// <summary>Routes the existing producers into the settings mixer. The listener owns global gain and mute.</summary>
    public static class GameAudioRouting
    {
        static readonly string[] paths = { "Master/Music", "Master/SFX", "Master/SFX/Skill",
            "Master/SFX/PetCry", "Master/UI", "Master/Ambient" };
        static readonly string[] parameters = { "MusicVolume", "SfxVolume", "SkillVolume",
            "PetCryVolume", "UiVolume", "AmbientVolume" };
        static readonly AudioMixerGroup[] groups = new AudioMixerGroup[paths.Length];
        static readonly Dictionary<AudioSource, (GameAudioBus bus, bool originalMute)> sources = new();
        static readonly List<AudioSource> expired = new();
        static GameAudioAssets assets;
        static bool loaded;

        internal static GameAudioAssets Assets {
            get {
                if (!loaded && Application.isPlaying) {
                    loaded = true;
                    assets = Resources.Load<GameAudioAssets>("CapstoneAudio");
                    if (assets != null && assets.mixer != null)
                        for (int i = 0; i < groups.Length; i++) {
                            var matches = assets.mixer.FindMatchingGroups(paths[i]);
                            if (matches.Length > 0) groups[i] = matches[0];
                        }
                }
                return assets;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() {
            loaded = false;
            assets = null;
            sources.Clear();
            expired.Clear();
            System.Array.Clear(groups, 0, groups.Length);
        }

        public static void Route(AudioSource source, GameAudioBus bus, AudioMixerGroup fallback = null)
        {
            if (source == null || !Application.isPlaying) return;
            if (Assets != null && groups[(int)bus] != null) source.outputAudioMixerGroup = groups[(int)bus];
            else if (fallback != null) source.outputAudioMixerGroup = fallback;
            bool original = sources.TryGetValue(source, out var current) ? current.originalMute : source.mute;
            sources[source] = (bus, original);
            var data = GameSettingsService.Instance?.Model?.Data;
            if (data != null) source.mute = original || IsSilent(data, bus);
        }

        internal static void Apply(GameSettingsData data)
        {
            AudioListener.volume = data.muteAll ? 0 : data.masterVolume;
            var mixer = Assets != null ? Assets.mixer : null;
            if (mixer != null)
                for (int i = 0; i < parameters.Length; i++)
                    mixer.SetFloat(parameters[i], Decibels(Volume(data, (GameAudioBus)i)));
            foreach (var entry in sources)
                if (entry.Key != null) entry.Key.mute = entry.Value.originalMute || IsSilent(data, entry.Value.bus);
            Prune();
        }

        internal static void Prune()
        {
            foreach (var entry in sources)
                if (entry.Key == null) expired.Add(entry.Key);
            foreach (var source in expired) sources.Remove(source);
            expired.Clear();
        }

        internal static void PlayWorldSound(AudioClip clip, Vector3 point)
        {
            var source = new GameObject("World SFX").AddComponent<AudioSource>();
            source.transform.position = point;
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.clip = clip;
            Route(source, GameAudioBus.SFX);
            source.Play();
            Object.Destroy(source.gameObject, clip.length + .1f);
        }

        internal static bool HasAmbient {
            get {
                foreach (var entry in sources)
                    if (entry.Key != null && entry.Value.bus == GameAudioBus.Ambient) return true;
                return false;
            }
        }

        public static float Decibels(float linear) => linear <= .0001f ? -80 : 20 * Mathf.Log10(Mathf.Clamp01(linear));

        static bool IsSilent(GameSettingsData data, GameAudioBus bus) => Volume(data, bus) <= 0
            || (bus == GameAudioBus.Skill || bus == GameAudioBus.PetCry) && data.sfxVolume <= 0;

        static float Volume(GameSettingsData data, GameAudioBus bus) => bus switch {
            GameAudioBus.Music => data.musicVolume,
            GameAudioBus.SFX => data.sfxVolume,
            GameAudioBus.Skill => data.skillSfxVolume,
            GameAudioBus.PetCry => data.petCryVolume,
            GameAudioBus.UI => data.uiVolume,
            _ => data.ambientVolume
        };
    }
}

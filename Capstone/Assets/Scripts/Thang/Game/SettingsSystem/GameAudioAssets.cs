using UnityEngine;
using UnityEngine.Audio;

namespace Capstone.Game.SettingsSystem
{
    public sealed class GameAudioAssets : ScriptableObject
    {
        public AudioMixer mixer;
        public AudioClip menuMusic;
        public AudioClip gameplayMusic;
        public AudioClip evolutionMusic;
        public AudioClip click;
        public AudioClip hover;
    }
}

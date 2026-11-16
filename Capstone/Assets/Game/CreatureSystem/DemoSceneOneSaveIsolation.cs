using System.IO;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>The development scene owns a separate playable profile, never the original main save.</summary>
    [DefaultExecutionOrder(-10000), DisallowMultipleComponent]
    public sealed class DemoSceneOneSaveIsolation : MonoBehaviour {
        public string developmentProfile = "E:/UnityWork/CapstoneProjectData/PlayProfiles/DemoScene1";
        public string ActiveProfile { get; private set; }
        void Awake() {
            if (gameObject.scene.name != "DemoScene 1") return;
            ActiveProfile = Application.isEditor || Debug.isDebugBuild ? developmentProfile : Path.Combine(Application.persistentDataPath, "DemoScene1");
            foreach (var root in gameObject.scene.GetRootGameObjects()) {
                foreach (var save in root.GetComponentsInChildren<PlayerSaveController>(true)) save.SetSaveStore(new PlayerJsonFileSaveStore(ActiveProfile));
                foreach (var save in root.GetComponentsInChildren<QuestSaveController>(true)) save.SetSaveStore(new QuestJsonFileSaveStore(ActiveProfile));
            }
        }
    }
}

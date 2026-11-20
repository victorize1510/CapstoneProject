using System.IO;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>The development scene owns a separate playable profile, never the original main save.</summary>
    [DefaultExecutionOrder(-10000), DisallowMultipleComponent]
    public sealed class DemoSceneOneSaveIsolation : MonoBehaviour {
        public const string CreatureQaSceneName = "DemoScene 1 - Creature QA";
        public const string CreatureQaSlot = "creature-qa-items-test";
        public string developmentProfile = "E:/UnityWork/CapstoneProjectData/PlayProfiles/DemoScene1";
        public string ActiveProfile { get; private set; }
        void Awake() {
            string sceneName = gameObject.scene.name;
            bool isCreatureQa = sceneName == CreatureQaSceneName;
            if (sceneName != "DemoScene 1" && !isCreatureQa) return;
            ActiveProfile = Application.isEditor || Debug.isDebugBuild ? developmentProfile : Path.Combine(Application.persistentDataPath, "DemoScene1");
            if (isCreatureQa) {
                if (!Application.isEditor && !Debug.isDebugBuild) ActiveProfile = Path.Combine(Application.persistentDataPath, "CreatureQAItemsTest");
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var save in root.GetComponentsInChildren<PlayerSaveController>(true))
                        if (save.SlotId == CreatureQaSlot) save.SetSaveStore(new PlayerJsonFileSaveStore(ActiveProfile));
                return;
            }
            foreach (var root in gameObject.scene.GetRootGameObjects()) {
                foreach (var save in root.GetComponentsInChildren<PlayerSaveController>(true)) save.SetSaveStore(new PlayerJsonFileSaveStore(ActiveProfile));
                foreach (var save in root.GetComponentsInChildren<QuestSaveController>(true)) save.SetSaveStore(new QuestJsonFileSaveStore(ActiveProfile));
            }
        }
    }
}

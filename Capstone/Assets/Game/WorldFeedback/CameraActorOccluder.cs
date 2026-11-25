using System.Collections.Generic;
using Capstone.Game.CreatureSystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Capstone.Game.WorldFeedback {
    // Dang ky theo vong doi; collider gameplay khong thay doi.
    [DisallowMultipleComponent]
    public sealed class CameraActorOccluder:MonoBehaviour {
        internal static readonly HashSet<CameraActorOccluder> Active=new HashSet<CameraActorOccluder>();
        public Transform visualRoot;
        internal Transform Root=>visualRoot!=null?visualRoot:GetComponent<Capstone.Game.HudSystem.PetEvolutionVisualController>()?.ActiveVisualTransform??transform;
        void OnEnable(){Active.Add(this);}void OnDisable(){Active.Remove(this);}
        public static void Ensure(GameObject actor){if(Application.isPlaying&&actor.GetComponent<CameraActorOccluder>()==null)actor.AddComponent<CameraActorOccluder>();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Active.Clear();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;Scan();}
        static void Loaded(Scene scene,LoadSceneMode mode){Scan();}
        static void Scan(){
            foreach(var actor in Object.FindObjectsByType<CreatureCombatant>(FindObjectsSortMode.None))Ensure(actor.gameObject);
            foreach(var actor in Object.FindObjectsByType<NpcPanelInteractable>(FindObjectsSortMode.None))Ensure(actor.gameObject);
            foreach(var actor in Object.FindObjectsByType<QuestGiverInteractable>(FindObjectsSortMode.None))Ensure(actor.gameObject);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.PetSummon.Editor
{
    // Integration fixture uses the authored player and clips, without joining the live player's party.
    public sealed class PetSummonDirectorValidation
    {
        static PetSummonDirectorValidation active;
        readonly List<int> completedSlots = new List<int>();
        readonly List<PetSummonDirector.SequenceState> observedStates = new List<PetSummonDirector.SequenceState>();
        GameObject root, player;
        PetSummonDirector director;
        BasicPlayerMovement movement;
        PetCommandInput input;
        PetController[] pets;
        string sequenceFailure;

        static PetSummonDirectorValidation()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingPlayMode) Stop();
            };
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        [MenuItem("Game Tools/Pets/Validate Director in Play Mode")]
        static void Begin()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().name != "TestChar") {
                Debug.LogWarning("Enter Play Mode in TestChar before validating the director."); return;
            }
            if (active != null) { Debug.LogWarning("Director validation is already running."); return; }
            var fixture = new PetSummonDirectorValidation();
            active = fixture;
            try {
                fixture.CreateFixture();
                fixture.director.StartCoroutine(fixture.Run());
            }
            catch { fixture.CleanUp(); throw; }
        }

        static void Stop()
        {
            if (active == null) return;
            var fixture = active;
            if (fixture.director != null) fixture.director.StopAllCoroutines();
            fixture.CleanUp();
        }

        void CreateFixture()
        {
            root = new GameObject("TemporarySummonDirectorValidation");
            root.SetActive(false);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ValidationFloor";
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(1000,499.5f,1000);
            floor.transform.localScale = new Vector3(100,1,100);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Thắng/Player.prefab");
            Require(prefab != null,"authored player prefab missing");
            player = Object.Instantiate(prefab,root.transform);
            player.name = "ValidationPlayer";
            player.transform.SetPositionAndRotation(new Vector3(1000,500.2f,1000),Quaternion.identity);
            player.tag = "Untagged";
            // Remove unrelated scripts before Awake can run (the parent remains inactive).
            foreach (var script in player.GetComponentsInChildren<MonoBehaviour>(true)) {
                bool playerSystem = script.gameObject == player && (script is BasicPlayerMovement
                    || script is PlayerVisualSwitcher || script is PetSummonDirector
                    || script is PetSummonHandSocket || script is PetSummonReleaseEffect);
                if (!playerSystem && !(script is AnimatorRootMotionRelay)) Object.DestroyImmediate(script);
            }
            input = player.AddComponent<PetCommandInput>();
            input.enabled = false; // Requests are made through the public director API, without keyboard side effects.
            movement = player.GetComponent<BasicPlayerMovement>();
            director = player.GetComponent<PetSummonDirector>();
            Require(movement != null && director != null,"authored player components missing");
            var aim = new GameObject("ValidationAim");
            aim.transform.SetParent(root.transform);
            var camera = aim.AddComponent<Camera>(); camera.enabled = false;
            var serialized = new SerializedObject(director);
            serialized.FindProperty("aimCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            pets = new PetController[3];
            for (int form=1;form<=3;form++) {
                var petPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/HudSystem/Prefabs/Dragon0{form}Gameplay.prefab");
                pets[form-1] = Object.Instantiate(petPrefab,root.transform).GetComponent<PetController>();
                pets[form-1].name = "ValidationDragon0"+form;
                pets[form-1].gameObject.hideFlags = HideFlags.DontSave;
                pets[form-1].transform.position = player.transform.position;
            }
            input.petSlots = pets;
            input.activePet = pets[0];
            root.SetActive(true);
            // Seed a real collision contact after teleporting; do not fake the movement grounded flag.
            Physics.SyncTransforms();
            player.GetComponent<CharacterController>().Move(Vector3.down*.5f);
            for (int form=1;form<=3;form++) {
                var pet = pets[form-1];
                pet.GetComponent<Capstone.Game.HudSystem.PetEvolutionVisualController>().RestoreForm(form-1);
                pet.AssignOwner(player.transform);
                Require(Array.IndexOf(Object.FindObjectsByType<PetController>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None),pet)<0,"temporary pet entered save discovery");
            }
            director.SequenceFinished += OnSequenceFinished;
        }

        IEnumerator Run()
        {
            try {
                float deadline = Time.unscaledTime+5;
                while (!movement.CanStartPetThrow && Time.unscaledTime<deadline) yield return null;
                var controller=player.GetComponent<CharacterController>();
                Require(movement.CanStartPetThrow,"authored player did not become grounded on flat floor; enabled="
                    +movement.enabled+"; controller="+controller.enabled+"; grounded="+controller.isGrounded
                    +"; position="+player.transform.position);
                Require(!player.GetComponent<PetSummonHandSocket>().HasHeldBall,"idle player is holding a ball");
                var groundLayers = movement.groundLayers;
                try {
                    movement.groundLayers = 0;
                    Require(!director.RequestSummonSlot(0),"summon without a landing surface was accepted");
                    Require(!player.GetComponent<PetSummonHandSocket>().HasHeldBall,"rejected summon left a hand ball");
                }
                finally { movement.groundLayers = groundLayers; }
                yield return new WaitForSecondsRealtime(.2f);
                Require(director.RequestSummonSlot(0),director.LastFailureReason);
                Require(player.GetComponent<PetSummonHandSocket>().HasHeldBall,"accepted summon did not prepare the hand ball");
                yield return WaitForState(PetSummonDirector.SequenceState.Release);
                Require(director.RequestSummonSlot(1),"switch during spawn was rejected");
                yield return WaitForCompletions(3); // Dusk spawn -> Dusk recall -> Nightfall spawn.
                Require(completedSlots[0]==0 && completedSlots[1]==0 && completedSlots[2]==1,"queued switch order incorrect");
                Require(pets[1].IsSummoned && !pets[0].IsSummoned,"queued switch left the wrong pet out");

                Require(director.RequestRecall(),"Nightfall recall rejected");
                yield return WaitForState(PetSummonDirector.SequenceState.Recall);
                Require(director.RequestSummonSlot(0),"request during recall rejected");
                Require(director.RequestSummonSlot(2),"latest request during recall rejected");
                yield return WaitForCompletions(5); // Nightfall recall -> Darkness spawn; queued Dusk is superseded.
                Require(completedSlots[3]==1 && completedSlots[4]==2,"latest queued request did not win");
                Require(input.activePet==pets[2] && pets[2].CanReceiveCommands,"Darkness did not finish its Cast Spell");

                Require(director.RequestRecall(),"Darkness recall rejected");
                yield return WaitForCompletions(6);
                Require(observedStates.Contains(PetSummonDirector.SequenceState.Throw)
                    && observedStates.Contains(PetSummonDirector.SequenceState.Flight)
                    && observedStates.Contains(PetSummonDirector.SequenceState.Bounce)
                    && observedStates.Contains(PetSummonDirector.SequenceState.Release)
                    && observedStates.Contains(PetSummonDirector.SequenceState.Recall),"director skipped a required phase");
                foreach (var pet in pets) Require(!pet.IsSummoned,"more than one pet remained out after final recall");
                Require(director.ReleaseEffect.ActiveVfx==null,"director left an active effect");
                Require(!player.GetComponent<PetSummonHandSocket>().HasHeldBall,"recall left the ball in hand");
                yield return new WaitForSecondsRealtime(.2f);
                Require(director.RequestSummonSlot(0),"cancellation test summon rejected: " + director.LastFailureReason);
                yield return WaitForState(PetSummonDirector.SequenceState.Throw);
                director.enabled = false;
                yield return null;
                Require(!player.GetComponent<PetSummonHandSocket>().HasHeldBall,"cancelled sequence left a hand ball");
                Require(director.ReleaseEffect.ActiveVfx == null,"cancelled sequence left an effect");
                Debug.Log("Director integration passed: idle/accepted/cancelled ball visibility, ball disappears after recall, authored Throw/Pickup, flight/bounce, three Cast Spell clips, queued switching and latest-request wins.");
            }
            finally { CleanUp(); }
        }

        void Observe()
        {
            Require(string.IsNullOrEmpty(sequenceFailure),sequenceFailure);
            if (!observedStates.Contains(director.State)) observedStates.Add(director.State);
            int summoned=0;
            foreach (var pet in pets) if (pet.IsSummoned) summoned++;
            Require(summoned<=1,"multiple pets summoned simultaneously");
        }

        IEnumerator WaitForState(PetSummonDirector.SequenceState state)
        {
            float deadline=Time.unscaledTime+20;
            while (director.State!=state && Time.unscaledTime<deadline) { Observe(); yield return null; }
            Observe(); Require(director.State==state,"state timeout: "+state+"; "+director.LastFailureReason);
        }

        IEnumerator WaitForCompletions(int count)
        {
            float deadline=Time.unscaledTime+35;
            while (completedSlots.Count<count && Time.unscaledTime<deadline) { Observe(); yield return null; }
            Observe(); Require(completedSlots.Count==count,"sequence completion timeout; "+director.LastFailureReason);
            yield return null;
        }

        void OnSequenceFinished(int slot,bool success)
        {
            if (!success) sequenceFailure=director.LastFailureReason;
            completedSlots.Add(slot);
        }

        void CleanUp()
        {
            if (ReferenceEquals(active,this)) active=null;
            if (director != null) { director.SequenceFinished-=OnSequenceFinished; director.enabled=false; }
            if (root != null) { root.SetActive(false); Object.DestroyImmediate(root); root=null; }
        }

        static void Require(bool value,string message)
        {
            if (!value) throw new InvalidOperationException("Director integration: "+message);
        }
    }
}

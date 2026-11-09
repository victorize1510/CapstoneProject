#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem {
    public sealed class CreatureRuntimePilotVerification : MonoBehaviour {
        const string Root = QaArtifactPaths.Root + "creature-runtime-pilot-2026-11-05/";
        const string Session = "Capstone.CreatureRuntimePilot";
        const string Pilot = "Assets/Game/CreatureSystem/Pilot/";
        readonly List<string> checks = new List<string>();
        readonly List<GameObject> spawned = new List<GameObject>();
        static void Write(string file, string text) { Directory.CreateDirectory(Root); File.AppendAllText(Root + "owned-paths.txt", Root + file + "\n"); File.WriteAllText(Root + file, text); }
        void Check(bool ok, string label) { checks.Add((ok ? "PASS " : "FAIL ") + label); Write("runtime-checks.txt", string.Join("\n", checks)); if (!ok) throw new Exception(label); }
        [MenuItem("Tools/Capstone/Creatures/5. Verify isolated dragon runtime pilot")]
        public static void Run() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Write("status.txt", "NOT RUN: stop existing Play Mode first."); return; }
            Install();
            var missing = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>(Pilot + "DragonPilotCatalog.asset") == null) missing.Add("DragonPilotCatalog.asset");
            foreach (string role in new[] { "OwnedPet", "Wild", "Enemy" }) foreach (string form in new[] { "01", "02", "03" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Pilot + role + "/" + role + "_Dragon" + form + ".prefab") == null) missing.Add(role + "/" + form);
            if (missing.Count > 0) { Write("status.txt", "NOT RUN: complete menu 4 first. Missing: " + string.Join(", ", missing)); return; }
            SessionState.SetBool(Session, true); SessionState.SetBool(Session + "enabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Session + "options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetString(Session + "saves", Root + "test-saves/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            Write("status.txt", "ENTERING PLAY MODE: test saves on E; no real input or production asset edits.");
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Install() {
            EditorApplication.playModeStateChanged -= RestoreSettings;
            EditorApplication.playModeStateChanged += RestoreSettings;
        }
        static void RestoreSettings(PlayModeStateChange state) {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Session + "restore", false)) return;
            SessionState.SetBool(Session + "restore", false);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Session + "enabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Session + "options", 0);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() {
            if (!SessionState.GetBool(Session, false)) return;
            string saves = SessionState.GetString(Session + "saves", ""); Directory.CreateDirectory(saves);
            string live = Path.Combine(Application.persistentDataPath, "Saves");
            if (Directory.Exists(live)) foreach (string path in Directory.GetFiles(live, "*.json*")) File.Copy(path, Path.Combine(saves, Path.GetFileName(path)), false);
            foreach (var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) save.SetSaveStore(new PlayerJsonFileSaveStore(saves));
            foreach (var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) save.SetSaveStore(new QuestJsonFileSaveStore(saves));
            new GameObject("Creature runtime pilot QA", typeof(CreatureRuntimePilotVerification)) { hideFlags = HideFlags.DontSave };
        }
        static Task Gap(int ms = 160) => Task.Delay(ms);
        GameObject Spawn(string role, string form, Vector3 position) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pilot + role + "/" + role + "_Dragon" + form + ".prefab");
            Check(prefab != null, role + "/" + form + " prefab exists");
            var go = Instantiate(prefab, position, Quaternion.identity); go.hideFlags = HideFlags.DontSave; spawned.Add(go); return go;
        }
        async void Start() {
            Write("status.txt", "RUNNING Editor runtime pilot " + DateTime.Now);
            try {
                await Gap(900);
                var save = Object.FindFirstObjectByType<PlayerSaveController>();
                Check(save != null && save.SavePath.StartsWith(Root, StringComparison.OrdinalIgnoreCase), "Player save redirected to E before Start");
                var family = AssetDatabase.LoadAssetAtPath<CreatureFamilyDefinition>("Assets/Game/CreatureSystem/Data/Families/Family_Dragon_Dusk.asset");
                Check(family != null && family.forms.Count == 3, "Three-form family available");
                Vector3 origin = new Vector3(10000, 0, 10000);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Pilot test floor"; floor.hideFlags = HideFlags.DontSave;
                floor.transform.position = origin + Vector3.down * .6f; floor.transform.localScale = new Vector3(60, 1, 60); spawned.Add(floor);
                var owner = new GameObject("Pilot command owner") { hideFlags = HideFlags.DontSave }; owner.transform.position = origin; spawned.Add(owner);
                Physics.SyncTransforms();
                var go = Spawn("OwnedPet", "01", origin + Vector3.right * 2);
                var pet = go.GetComponent<PetController>(); pet.AssignOwner(owner.transform);
                var stats = go.GetComponent<PetHudRuntimeStats>(); var metadata = go.GetComponent<PetCollectionMetadata>();
                metadata.AssignPersistentId("qa-pilot-" + Guid.NewGuid().ToString("N"));
                await Gap(250);
                Check(!pet.IsSummoned && !go.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled), "Owned root begins dormant");
                pet.Summon();
                for (int i = 0; i < 40 && !pet.CanReceiveCommands; i++) await Gap(100);
                Check(pet.IsSummoned && pet.CanReceiveCommands, "Real summon completes on isolated floor");
                var driver = go.GetComponent<CreatureAnimationDriver>();
                Check(driver.Profile != null && driver.Profile.formId == "01", "Owned driver binds form 01 profile");
                foreach (var action in driver.Profile.actions) Check(driver.TryGetStates(action.action, out _), "Action resolves in real Animator: " + action.action);
                Check(!driver.TryGetStates(CreatureAction.Walk, out _) && !string.IsNullOrEmpty(driver.LastError), "Missing Walk is reported, not guessed from Fly");
                int accepted = 0; stats.SkillAnimationAccepted += _ => accepted++;
                stats.RequestSkill(0); await Gap(180);
                Check(accepted == 1 && stats.GetSkills()[0].cooldownRemainingSeconds > 0, "Bite accepted once with cooldown");
                string[] skillOrder = stats.GetSkills().Select(s => s.skillId).ToArray();
                var actorVisual = go.GetComponent<PetEvolutionVisualController>();
                var bad = Instantiate(driver.Profile); bad.hideFlags = HideFlags.DontSave;
                bad.actions.RemoveAll(a => a.action == CreatureAction.WingStrike);
                var visualBinding = actorVisual.ActiveAnimator.GetComponent<CreatureVisualBinding>(); var originalProfile = visualBinding.animationProfile;
                visualBinding.animationProfile = bad; driver.Bind(actorVisual.ActiveAnimator);
                stats.RequestSkill(3);
                Check(accepted == 1 && stats.GetSkills()[3].cooldownRemainingSeconds == 0, "Missing WingStrike consumes no cooldown and emits no accepted event");
                visualBinding.animationProfile = originalProfile; driver.Bind(actorVisual.ActiveAnimator); Destroy(bad);
                stats.SetIdentity(stats.DisplayName, 40, stats.Icon);
                string genetics = JsonUtility.ToJson(metadata.IndividualValues) + JsonUtility.ToJson(metadata.EffortValues) + metadata.Nature;
                float cooldown = stats.GetSkills()[0].cooldownRemainingSeconds;
                var serviceHost = new GameObject("Pilot evolution service") { hideFlags = HideFlags.DontSave }; spawned.Add(serviceHost);
                var evolution = serviceHost.AddComponent<PetEvolutionService>();
                Check(evolution.TryEvolve(pet, out string message), "Real evolution transaction 01 to 02: " + message);
                Check(metadata.CurrentFormId == "02" && driver.Profile.formId == "02" && stats.SpeciesSkillSet == family.forms[1].skillSet, "Form/model/Animator/skill set switch together");
                Check(stats.Icon == family.forms[1].avatar, "Evolution updates runtime avatar");
                Check(stats.GetSkills().Select(s => s.skillId).SequenceEqual(skillOrder), "Evolution preserves equipped skill order");
                Check(cooldown > 0 && stats.GetSkills()[0].cooldownRemainingSeconds > 0, "Visual rebind preserves active cooldown");
                Check(go.GetComponent<PetSkillPresentationPlayer>().sockets.First(s => s.key == "Mouth").anchor.IsChildOf(actorVisual.ActiveVisualTransform), "Mouth socket rebound to current skeleton");
                Check(genetics == JsonUtility.ToJson(metadata.IndividualValues) + JsonUtility.ToJson(metadata.EffortValues) + metadata.Nature, "Evolution preserves genetics and nature");
                await Gap(180);
                Check(evolution.TryEvolve(pet, out message), "Real evolution transaction 02 to 03: " + message);
                Check(metadata.CurrentFormId == "03" && driver.Profile.formId == "03" && stats.Icon == family.forms[2].avatar, "Final form identity/animation/avatar agree");
                Check(!evolution.CreatePreview(pet).CanEvolve, "Final form cannot evolve again");
                var metadataSave = metadata.CreateSaveData(); var statsSave = stats.CreateSaveData();
                var catalog = AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>(Pilot + "DragonPilotCatalog.asset");
                Check(catalog.TryInstantiate("Dragon Dusk", "03", null, out var restored), "Existing catalog API instantiates pilot form 03");
                spawned.Add(restored.gameObject); restored.gameObject.hideFlags = HideFlags.DontSave; restored.transform.position = origin + Vector3.right * 10;
                var restoredMeta = restored.GetComponent<PetCollectionMetadata>(); var restoredStats = restored.GetComponent<PetHudRuntimeStats>();
                restoredMeta.RestoreFromSaveData(metadataSave); restoredStats.RestoreFromSaveData(statsSave); restoredMeta.RestoreEvolutionPresentation(restoredStats);
                Check(restored.GetComponentsInChildren<Animator>(true).Length == 1, "Load of matching form reuses its visual without duplicate Animator");
                Check(restoredStats.Icon == stats.Icon && restoredStats.GetSkills().Select(s => s.skillId).SequenceEqual(skillOrder), "Load preserves avatar and skill IDs");
                pet.Withdraw(); Check(!pet.IsSummoned, "Recall returns pilot pet to dormant");
                var wilds = new List<GameObject>();
                for (int i = 0; i < 3; i++) {
                    var wild = Spawn("Wild", (i + 1).ToString("00"), origin + new Vector3((i - 1) * 4, 0, 10)); wilds.Add(wild);
                    var wp = wild.GetComponent<PetController>(); Check(wp.IsWildEncounter && wild.GetComponent<WildPetCaptureTarget>().IsWild, "Wild role owns capture adapter: " + i);
                    Check(wild.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled), "Wild visual is visible: " + i);
                    Check(wild.GetComponent<PetHudRuntimeStats>().Icon == family.forms[i].avatar, "Wild form avatar correct: " + i);
                    var wildStats = wild.GetComponent<PetHudRuntimeStats>();
                    Check(wildStats.Health > 0 && Mathf.Approximately(wildStats.Health, wildStats.MaxHealth)
                        && Mathf.Approximately(wild.GetComponent<DummyEnemy>().CurrentHealth, wildStats.Health), "Wild spawn starts with full calculated health: " + i);
                }
                Check(wilds.Select(w => w.GetComponent<PetCollectionMetadata>().PersistentId).Distinct().Count() == 3, "Wild individuals have distinct IDs");
                var enemyRoot = Spawn("Enemy", "02", origin + new Vector3(18, 0, 10)); await Gap(180);
                Check(enemyRoot.GetComponent<PetController>() == null && enemyRoot.GetComponent<WildPetCaptureTarget>() == null, "Enemy has no pet brain or implicit capture adapter");
                var enemy = enemyRoot.GetComponent<DummyEnemy>();
                Check(enemy.CurrentHealth == enemyRoot.GetComponent<PetHudRuntimeStats>().MaxHealth, "Enemy target health initialized from existing profile");
                enemy.TakeDamage(enemy.maxHealth + 1); await Gap(180);
                Check(!enemy.IsAlive && enemyRoot.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Die"), "Real defeat callback plays Death action");
                foreach (var form in family.forms) {
                    Check(form.visualPrefab.GetComponentsInChildren<MonoBehaviour>(true).All(b => b is CreatureVisualBinding), "Visual prefab has no gameplay scripts: " + form.formId);
                    Check(form.petPrefab.GetComponent<CreatureRolePresentation>().role == CreatureRole.OwnedPet && form.enemyPrefab.GetComponent<CreatureRolePresentation>().role == CreatureRole.Enemy, "Separate role roots: " + form.formId);
                }
                await Capture(wilds, origin);
                Write("status.txt", "COMPLETE: " + checks.Count + " actual Editor runtime checks. No hardware input, production scene/catalog edits or Enemy AI claim.");
            } catch (Exception e) { Write("status.txt", "FAILED: " + e); Debug.LogException(e); }
            finally {
                foreach (var go in spawned) if (go != null) Destroy(go);
                SessionState.SetBool(Session, false); SessionState.SetBool(Session + "restore", true);
                EditorApplication.ExitPlaymode();
            }
        }
        async Task Capture(List<GameObject> wilds, Vector3 origin) {
            int layer = LayerMask.NameToLayer("Evolution"); Check(layer >= 0, "Existing isolated render layer available");
            foreach (var go in wilds) foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            var cameraGo = new GameObject("Pilot proof camera") { hideFlags = HideFlags.DontSave }; spawned.Add(cameraGo);
            var camera = cameraGo.AddComponent<Camera>(); camera.cullingMask = 1 << layer; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.17f, .2f, .27f);
            camera.transform.position = origin + new Vector3(0, 4, -4); camera.transform.LookAt(origin + new Vector3(0, 1, 10)); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1366,768) }) {
                var texture = new RenderTexture(size.x,size.y,24); var image = new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                try {
                    camera.targetTexture = texture; camera.aspect = (float)size.x/size.y; await Gap(400);
                    var previous = RenderTexture.active; RenderTexture.active = texture;
                    try { image.ReadPixels(new Rect(0,0,size.x,size.y),0,0); image.Apply(); } finally { RenderTexture.active = previous; }
                    string file = Root + "three-shared-visuals-" + size.x + ".png"; File.AppendAllText(Root + "owned-paths.txt", file + "\n"); File.WriteAllBytes(file,image.EncodeToPNG());
                    Check(texture.IsCreated(), "Actual runtime camera render at " + size);
                } finally { camera.targetTexture = null; texture.Release(); Destroy(texture); Destroy(image); }
            }
        }
    }
}
#endif

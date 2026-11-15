using System;
using System.IO;
using System.Linq;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Skills;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor {
    public static class CreatureRuntimePilotSetup {
        public const string Root = "Assets/Game/CreatureSystem/Pilot/";
        const string FamilyPath = "Assets/Game/CreatureSystem/Data/Families/Family_Dragon_Dusk.asset";
        const string Template = "Assets/Game/HudSystem/Prefabs/Dragon01Gameplay.prefab";
        public const string CatalogPath = Root + "DragonPilotCatalog.asset";
        [MenuItem("Tools/Capstone/Creatures/4. Build three-form dragon runtime pilot")]
        public static void Build() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dừng Play Mode trước khi tạo prefab thử.");
            var family = AssetDatabase.LoadAssetAtPath<CreatureFamilyDefinition>(FamilyPath);
            if (family == null || family.forms.Count != 3) throw new InvalidOperationException("Thiếu dòng rồng 3 dạng.");
            foreach (var form in family.forms) {
                var profile = LoadOrCreate<CreatureAnimationProfile>(Root + "Animation/Dragon" + form.formId + ".asset");
                // A profile edited by the author is preserved on subsequent builds.
                if (profile.actions.Count == 0) {
                    profile.definitionId = family.definitionId; profile.formId = form.formId;
                    Bind(profile, CreatureAction.Idle, "Idle");
                    Bind(profile, CreatureAction.Move, form.formId == "01" ? "Move" : "Fly Forward In Place");
                    Bind(profile, CreatureAction.Fly, "Fly Forward In Place");
                    Bind(profile, CreatureAction.Spawn, "Spawn");
                    Bind(profile, CreatureAction.Attack, "Bite Attack");
                    Bind(profile, CreatureAction.Bite, "Bite Attack");
                    Bind(profile, CreatureAction.Cast, "Cast Spell");
                    Bind(profile, CreatureAction.Blast, "Blast Attack");
                    Bind(profile, CreatureAction.Projectile, "Projectile Attack");
                    Bind(profile, CreatureAction.WingStrike, "Wing Attack");
                    Bind(profile, CreatureAction.Hit, "Take Damage"); Bind(profile, CreatureAction.Death, "Die");
                    profile.skills.Add(new CreatureSkillAction { skillId = "bite", action = CreatureAction.Bite });
                    profile.skills.Add(new CreatureSkillAction { skillId = "blast", action = CreatureAction.Blast });
                    profile.skills.Add(new CreatureSkillAction { skillId = "projectile", action = CreatureAction.Projectile });
                    profile.skills.Add(new CreatureSkillAction { skillId = "wing strike", action = CreatureAction.WingStrike });
                    EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
                }
                form.animationProfile = profile;
                form.visualPrefab = BuildVisual(form);
            }
            foreach (var form in family.forms) {
                form.petPrefab = BuildActor(family, form, CreatureRole.OwnedPet);
                form.wildPrefab = BuildActor(family, form, CreatureRole.Wild);
                form.enemyPrefab = BuildActor(family, form, CreatureRole.Enemy);
            }
            EditorUtility.SetDirty(family); AssetDatabase.SaveAssetIfDirty(family);
            var catalog = LoadOrCreate<PetPrefabCatalog>(CatalogPath);
            var data = new SerializedObject(catalog); var entries = data.FindProperty("entries"); entries.arraySize = 3;
            for (int i = 0; i < 3; i++) {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("definitionId").stringValue = family.definitionId;
                entry.FindPropertyRelative("formId").stringValue = family.forms[i].formId;
                entry.FindPropertyRelative("prefab").objectReferenceValue = family.forms[i].petPrefab.GetComponent<PetController>();
            }
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log("Dragon pilot built: 3 shared visuals, 3 profiles, 9 role roots. Original prefabs, scenes and production catalog unchanged.");
        }
        static void Bind(CreatureAnimationProfile p, CreatureAction action, string state) => p.actions.Add(new CreatureActionBinding { action = action, state = state });
        static GameObject BuildVisual(CreatureFormDefinition form) {
            string path = AssetDatabase.GetAssetPath(form.sourcePrefab);
            var go = PrefabUtility.LoadPrefabContents(path);
            try {
                go.SetActive(false);
                // Work only on an isolated loaded copy. Never writes the source path.
                foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true).OrderBy(s => s is PetHudRuntimeStats ? 1 : 0).ToArray()) Object.DestroyImmediate(script);
                foreach (var c in go.GetComponentsInChildren<Component>(true).ToArray()) {
                    if (c == null || c is Transform || c is Animator || c is SkinnedMeshRenderer || c is MeshRenderer || c is MeshFilter || c is LODGroup) continue;
                    Object.DestroyImmediate(c);
                }
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
                var animator = go.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException(form.displayName + ": thiếu Animator.");
                animator.enabled = true; animator.applyRootMotion = false;
                var binding = animator.gameObject.AddComponent<CreatureVisualBinding>();
                binding.animationProfile = form.animationProfile; binding.skillSet = form.skillSet;
                var bones = go.GetComponentsInChildren<Transform>(true);
                var jaw = bones.FirstOrDefault(t => t.name == "RigJaw");
                if (jaw == null) throw new InvalidOperationException(form.displayName + ": chưa xác định xương hàm; không đoán socket miệng.");
                binding.sockets.Add(new PetSkillSocket { key = "Mouth", anchor = jaw });
                binding.sockets.Add(new PetSkillSocket { key = "Body", anchor = bones.FirstOrDefault(t => t.name == "RigSpine") ?? go.transform });
                binding.sockets.Add(new PetSkillSocket { key = "Ground", anchor = go.transform });
                go.name = "Visual_Dragon" + form.formId; go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); go.SetActive(true);
                return Save(go, Root + "Visual/" + go.name + ".prefab");
            } finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        static GameObject BuildActor(CreatureFamilyDefinition family, CreatureFormDefinition form, CreatureRole role) {
            var go = PrefabUtility.LoadPrefabContents(Template);
            try {
                go.SetActive(false);
                foreach (Transform child in go.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                foreach (var renderer in go.GetComponents<Renderer>()) Object.DestroyImmediate(renderer);
                foreach (var filter in go.GetComponents<MeshFilter>()) Object.DestroyImmediate(filter);
                foreach (var animator in go.GetComponents<Animator>()) Object.DestroyImmediate(animator);
                var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent != null) agent.enabled = false;
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(form.visualPrefab, go.scene);
                visual.transform.SetParent(go.transform, false);
                var selectedAnimator = visual.GetComponentInChildren<Animator>(true);
                var driver = go.GetComponent<CreatureAnimationDriver>();
                if (driver == null) driver = go.AddComponent<CreatureAnimationDriver>();
                var presentation = go.GetComponent<CreatureRolePresentation>();
                if (presentation == null) presentation = go.AddComponent<CreatureRolePresentation>();
                presentation.role = role;
                var metadata = go.GetComponent<PetCollectionMetadata>();
                var data = new SerializedObject(metadata);
                string[] names = { "Dragon Dusk", "Dragon Nightfall", "Dragon Darkness" };
                int stage = int.Parse(form.formId) - 1;
                foreach (string field in new[] { "petId", "nickname", "captureDate", "captureLocation", "captureBallId", "trainerName" }) data.FindProperty(field).stringValue = "";
                data.FindProperty("favorite").boolValue = false; data.FindProperty("released").boolValue = false;
                data.FindProperty("geneticsInitialized").boolValue = false; data.FindProperty("captureGeneticsRolled").boolValue = false;
                data.FindProperty("definitionId").stringValue = family.definitionId; data.FindProperty("species").stringValue = names[stage];
                data.FindProperty("currentFormId").stringValue = form.formId; data.FindProperty("evolutionStage").intValue = stage;
                var rules = data.FindProperty("evolutionRules");
                if (rules.arraySize != 2) throw new InvalidOperationException("Legacy dragon template must retain both original rules.");
                for (int i = 0; i < 2; i++) {
                    var rule = rules.GetArrayElementAtIndex(i);
                    rule.FindPropertyRelative("targetVisualPrefab").objectReferenceValue = family.forms[i + 1].visualPrefab;
                    rule.FindPropertyRelative("targetIcon").objectReferenceValue = family.forms[i + 1].avatar;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                var stats = go.GetComponent<PetHudRuntimeStats>(); data = new SerializedObject(stats);
                data.FindProperty("displayName").stringValue = names[stage]; data.FindProperty("icon").objectReferenceValue = form.avatar;
                data.FindProperty("speciesSkillSet").objectReferenceValue = form.skillSet; data.FindProperty("animator").objectReferenceValue = selectedAnimator;
                data.FindProperty("usePrototypeSkillsWhenEmpty").boolValue = false;
                // A new actor starts at a full ratio. Metadata replaces 1/1 with the
                // existing profile's calculated HP in Awake; 0/0 means zero HP there.
                data.FindProperty("health").floatValue = 1; data.FindProperty("maxHealth").floatValue = 1;
                data.ApplyModifiedPropertiesWithoutUndo();
                var pet = go.GetComponent<PetController>(); pet.owner = null; pet.animator = selectedAnimator; pet.useSelfInput = false;
                data = new SerializedObject(pet); data.FindProperty("wildEncounter").boolValue = role == CreatureRole.Wild; data.ApplyModifiedPropertiesWithoutUndo();
                var evolution = go.GetComponent<PetEvolutionVisualController>(); data = new SerializedObject(evolution);
                data.FindProperty("visualRoot").objectReferenceValue = go.transform;
                data.FindProperty("baseVisual").objectReferenceValue = visual;
                data.FindProperty("baseVisualPrefab").objectReferenceValue = form.visualPrefab; data.ApplyModifiedPropertiesWithoutUndo();
                if (role == CreatureRole.Enemy) { Object.DestroyImmediate(evolution); Object.DestroyImmediate(pet); }
                if (role != CreatureRole.OwnedPet) {
                    var enemy = go.GetComponent<DummyEnemy>();
                    if (enemy == null) enemy = go.AddComponent<DummyEnemy>();
                    enemy.autoRevive = false; enemy.writeHealthToName = false; enemy.isBoss = false;
                    data = new SerializedObject(enemy); data.FindProperty("experienceReward").intValue = 0;
                    foreach (string axis in new[] { "hp", "attack", "defense", "magicAttack", "magicDefense", "speed" }) {
                        var stat = data.FindProperty("effortYield").FindPropertyRelative(axis); if (stat != null) stat.intValue = 0;
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    // Unity's missing native components can be fake-null managed wrappers in Edit Mode.
                    // Use Unity equality; ?? would keep that wrapper instead of adding the collider.
                    var collider = go.GetComponent<CapsuleCollider>();
                    if (collider == null) collider = go.AddComponent<CapsuleCollider>();
                    if (collider == null) throw new InvalidOperationException("Không tạo được collider cho " + role + "/" + form.formId);
                    var renderers = visual.GetComponentsInChildren<Renderer>(true); var bounds = renderers[0].bounds;
                    foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                    collider.center = go.transform.InverseTransformPoint(bounds.center);
                    collider.radius = Mathf.Max(.1f, Mathf.Min(bounds.size.x, bounds.size.z) * .22f);
                    collider.height = Mathf.Max(collider.radius * 2, bounds.size.y);
                    if (role == CreatureRole.Wild && go.GetComponent<WildPetCaptureTarget>() == null) go.AddComponent<WildPetCaptureTarget>();
                }
                go.name = role + "_Dragon" + form.formId;
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.SetActive(true);
                return Save(go, Root + role + "/" + go.name + ".prefab");
            } finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        static GameObject Save(GameObject go, string path) { EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/')); return PrefabUtility.SaveAsPrefabAsset(go, path); }
        static T LoadOrCreate<T>(string path) where T : ScriptableObject {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/')); asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        static void EnsureFolder(string folder) { string current = "Assets"; foreach (string part in folder.Split('/').Skip(1)) { if (!AssetDatabase.IsValidFolder(current + "/" + part)) AssetDatabase.CreateFolder(current, part); current += "/" + part; } }
    }
}

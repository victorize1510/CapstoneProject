using System;
using System.IO;
using System.Linq;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor {
    public static class PracticeCombatSetup {
        public const string PrefabPath = "Assets/Game/CreatureSystem/Runtime/Training/TrainingDummy_Bat.prefab";
        public const string Qa = Capstone.Game.UISystem.QaArtifactPaths.Root + "practice-damage-freecast-2026-11-07/";
        const string EffectPath = "Assets/Game/Skills/Data/Tactics/BiteFangs.prefab";
        [MenuItem("Tools/Capstone/Creatures/13. Add combat dummy and bite feedback")]
        public static void Apply() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dừng Play Mode trước.");
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "DemoScene" || scene.isDirty) throw new InvalidOperationException("Mở DemoScene đã lưu trước. Không tự lưu thay đổi của người dùng.");
            var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PetCommandInput>(true)).FirstOrDefault();
            if (player == null) throw new InvalidOperationException("Thiếu player PetCommandInput trong DemoScene.");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Skills/Data/Tactics/WindBuff.mat");
            if (material == null) throw new InvalidOperationException("Thiếu material URP của skill đã tạo.");
            var effect = BiteEffect(material);
            var bite = AssetDatabase.LoadAssetAtPath<PetSkillDefinition>("Assets/Game/Skills/Data/Definitions/Skill_bite.asset");
            bite.displayName = "Cắn"; bite.description = "Cắn một mục tiêu trước mặt. Ưu tiên mục tiêu khóa, tự tìm địch gần; không có địch vẫn ra đòn và hồi chiêu.";
            bite.uiMetadata.damageKind = SkillDamageLabel.Physical;
            bite.presentation.release.effectPrefab = effect; bite.presentation.release.followSocket = true;
            bite.presentation.release.socket = "Mouth"; bite.presentation.release.localEulerAngles = new Vector3(90, 0, 0);
            bite.presentation.release.actorSpace = true; bite.presentation.release.localOffset = new Vector3(0, 0, .4f);
            bite.presentation.release.scale = .7f; bite.presentation.release.lifetimeSeconds = .3f;
            bite.presentation.impact.effectPrefab = effect; bite.presentation.impact.scale = .9f; bite.presentation.impact.lifetimeSeconds = .32f;
            Save(bite); Save(bite.presentation);
            foreach (string guid in AssetDatabase.FindAssets("t:PetSkillCombatProfile", new[] { "Assets/Game" })) {
                var profile = AssetDatabase.LoadAssetAtPath<PetSkillCombatProfile>(AssetDatabase.GUIDToAssetPath(guid));
                profile.autoAcquireTarget = true; Save(profile);
            }
            Folder("Assets/Game/CreatureSystem/Runtime/Training");
            var root = PrefabUtility.LoadPrefabContents("Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab");
            GameObject prefab;
            try {
                var ai = root.GetComponent<BasicCreatureEnemy>(); if (ai != null) Object.DestroyImmediate(ai);
                var capture = root.GetComponent<WildPetCaptureTarget>(); if (capture != null) Object.DestroyImmediate(capture);
                var agent = root.GetComponent<NavMeshAgent>(); if (agent != null) agent.enabled = false;
                var enemy = root.GetComponent<DummyEnemy>(); enemy.trainingDummy = true; enemy.maxHealth = 5000;
                enemy.autoRevive = true; enemy.reviveDelay = 2.5f; enemy.writeHealthToName = false; enemy.tintFeedback = false;
                var stats = root.GetComponent<PetHudRuntimeStats>(); var data = new SerializedObject(stats);
                data.FindProperty("displayName").stringValue = "Hình nhân tập đánh"; data.FindProperty("level").intValue = 10;
                data.ApplyModifiedPropertiesWithoutUndo(); root.name = "TrainingDummy_Bat";
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
            var existing = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DummyEnemy>(true)).FirstOrDefault(d => d.IsTrainingDummy);
            if (existing == null) {
                Physics.SyncTransforms(); Vector3 point = FindGround(player.transform);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); instance.name = "TrainingDummy_Bat_Demo";
                instance.transform.position = point; Vector3 facing = player.transform.position - point; facing.y = 0;
                if (facing.sqrMagnitude > .01f) instance.transform.rotation = Quaternion.LookRotation(facing);
                Directory.CreateDirectory(Qa); File.Copy(scene.path, Qa + "scene-before-practice.unity", true);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Không lưu được dummy trong DemoScene.");
                File.WriteAllText(Qa + "dummy-position.txt", "Scene dummy at " + point + "; player at " + player.transform.position + "; source " + PrefabPath);
            }
            Debug.Log("Combat practice configured: stationary no-reward dummy, free casts and bite feedback.");
        }
        static Vector3 FindGround(Transform player) {
            var forward = player.forward; forward.y = 0; forward.Normalize(); var right = Vector3.Cross(Vector3.up, forward);
            foreach (var offset in new[] { forward * 5 + right * 3, right * 5, -right * 5, forward * 8, -forward * 5 }) {
                Vector3 candidate = player.position + offset;
                var hits = Physics.RaycastAll(candidate + Vector3.up * 3, Vector3.down, 12, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
                foreach (var hit in hits) {
                    if (hit.normal.y < .65f || hit.collider.transform.IsChildOf(player) || hit.collider.GetComponentInParent<DummyEnemy>() != null || hit.collider.GetComponentInParent<PetController>() != null) continue;
                    Vector3 p = hit.point;
                    if (Physics.CheckCapsule(p + Vector3.up * .8f, p + Vector3.up * 1.5f, .65f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return p;
                }
            }
            throw new InvalidOperationException("Không tìm được chỗ nền trống gần player. Chưa thêm hoặc lưu scene.");
        }
        static GameObject BiteEffect(Material material) {
            var root = new GameObject("BiteFangs");
            try {
                for (int jaw = 0; jaw < 2; jaw++) {
                    var line = new GameObject("Jaw" + jaw).AddComponent<LineRenderer>(); line.transform.SetParent(root.transform, false);
                    line.useWorldSpace = false; line.sharedMaterial = material; line.positionCount = 17; line.widthMultiplier = .085f;
                    line.startColor = line.endColor = new Color(1.5f, .65f, .4f, 1);
                    for (int i = 0; i < 17; i++) { float a = Mathf.PI * i / 16; line.SetPosition(i, new Vector3(Mathf.Cos(a) * .45f, 0, Mathf.Sin(a) * .3f * (jaw == 0 ? 1 : -1))); }
                }
                for (int i = 0; i < 2; i++) {
                    var line = new GameObject("Fang" + i).AddComponent<LineRenderer>(); line.transform.SetParent(root.transform, false);
                    line.useWorldSpace = false; line.sharedMaterial = material; line.positionCount = 3; line.widthMultiplier = .1f;
                    line.startColor = line.endColor = new Color(1.5f, 1.4f, 1.15f, 1);
                    float x = i == 0 ? -.22f : .22f;
                    line.SetPosition(0, new Vector3(x - .07f, 0, .22f)); line.SetPosition(1, new Vector3(x, 0, -.14f)); line.SetPosition(2, new Vector3(x + .07f, 0, .22f));
                }
                var ps = root.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = false; main.duration = .25f; main.startLifetime = .2f; main.startSpeed = 1; main.startSize = .08f; main.maxParticles = 12;
                main.startColor = new Color(1, .7f, .4f, .8f); var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 8) });
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(root, EffectPath);
            } finally { Object.DestroyImmediate(root); }
        }
        static void Save(Object value) { EditorUtility.SetDirty(value); AssetDatabase.SaveAssetIfDirty(value); }
        static void Folder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    }
}

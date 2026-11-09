using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Capstone.Game.HudSystem;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Capstone.Game.CreatureSystem.Editor {
    public static class CreatureLibraryAuthoring {
        public const string LibraryPath = "Assets/Game/CreatureSystem/Data/CreatureLibrary.asset";
        public const string AvatarPath = "Assets/Game/CreatureSystem/Resources/Creatures/CreatureAvatarCatalog.asset";
        const string Prefabs = "Assets/Prefabs/Trung/Pets/";
        static string ReportRoot => QaArtifactPaths.Root + "creature-library-foundation-2026-11-05/";
        // Deliberate source mapping, checked against the supplied artwork. Never generates new creature art.
        static readonly string[][] Seeds = {
            new[] { "chick", "Dòng chim", "Chim Con", "Gà Lôi", "Gà Gió Lốc", "edd4e1b6436dd2942a7fa8ae8536b0de", "20d480f9f0b88c148853bfab3b57feb3", "13aac3624e546e74f92b7c49a3d4502b" },
            new[] { "bat", "Dòng dơi", "Dơi Nhỏ", "Dơi Ma Cà Rồng", "Dơi Chúa Tể", "5a6ab5b08820ca241a3446c80a97a57b", "7305f2b68cd957c4a85fdf3ba7ab50ba", "656f11ffedf653740b96ad4aac0bbef9" },
            new[] { "sun-blossom", "Dòng hướng dương", "Hướng Dương Nảy Mầm", "Hướng Dương Tiên", "Hướng Dương Nữ Hoàng", "eb4f3636ae4c4af419919587424ebdc4", "2fcdcf804183e8e4c95ec4664f4ca2c4", "049cb7170f927004a820f487c8b5e2b2" },
            new[] { "dragon-fire", "Dòng rồng lửa", "Khủng Long Lửa", "Rồng Lửa Bay", "Rồng Lửa Địa Ngục", "dc2be1dc358c14e48a083f2eca623df7", "848f48cbb61ce6d4caae2e202b0fa64c", "ad6b6080d1afd99429089b40b18db182" },
            new[] { "ghost", "Dòng ma", "Ma Nhỏ", "Ma Mặt Nạ", "Ma Nguyền Rủa", "9d63cba7d49a8c84c9827a646e643490", "c8f971810f3803b42b00e0ef4aafb68e", "8bc7cd3691be4be4dac8baeb4494ded5" },
            new[] { "cat-electric", "Dòng mèo điện", "Mèo Điện", "Mèo Tia Chớp", "Mèo Sấm Sét", "d105e8f46ae9fcc4e82e2f986b9df2d0", "d836a8f72a4a411448ed7b0811ebd57d", "0bd712b3415f30a4187992c96165494c" },
            new[] { "shell", "Dòng ốc", "Ốc Nhà", "Ốc Gai", "Ốc Mượn Hồn Vua", "86b4ece01f74cd542b48267e3f58c311", "6d187bff9e5a98f42af111987c04cc64", "54a5505aab0ffa84a9f520acf4a85939" },
            new[] { "snake-fire", "Dòng rắn", "Rắn Lửa", "Rắn Bùng Cháy", "Rắn 3 Đầu", "64aa7d8720d140841bf56c34fa093f7f", "83f53a524bf0e8d4db1c34f0ef1772d3", "5d91ab8e5f65a024c868ba1d4e9186ce" },
            new[] { "Dragon Dusk", "Dòng rồng bóng tối", "Rồng Chạng Vạng", "Rồng Đêm Tối", "Dragon Bóng Tối", "7f5438365e3289243ac5dbf19f2d7b66", "76b9541a488b2cd44b579d65568cb7a3", "52c2bf9b2e45e0744b0d3c3eb1bd932c" },
            new[] { "wolf", "Dòng sói", "Sói Tí Nị", "Sói Bóng Tối", "Người Sói Quỷ", "fca5f40bf7e17f441acae646345e619f", "361040a0e64957b45a1f1904f9197c53", "9b30e715e5a37a341bbfcf682fa8852f" },
            new[] { "egg-shadow", "Dòng quái trứng", "Trứng Ma Quái", "Quái Trứng Quỷ", "Quái Trứng Lực SĨ", "5b9f4cae6a1d80d4c81db1f496e56a36", "933104ba23a2a7447b2ea9edfa4daa82", "abdcfcaf8ea6bcc408fcd559fb1bc239" }
        };

        [MenuItem("Tools/Capstone/Creatures/1. Create missing family assets")]
        public static void CreateMissing() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dừng Play Mode trước khi tạo danh mục.");
            var library = LoadOrCreate<CreatureLibrary>(LibraryPath);
            foreach (var seed in Seeds) {
                string path = "Assets/Game/CreatureSystem/Data/Families/Family_" + seed[0].Replace(' ', '_') + ".asset";
                var family = AssetDatabase.LoadAssetAtPath<CreatureFamilyDefinition>(path);
                if (family == null) {
                    family = LoadOrCreate<CreatureFamilyDefinition>(path);
                    family.definitionId = seed[0]; family.displayName = seed[1];
                    if (seed[0] == "Dragon Dusk") family.legacyDefinitionIds = new[] { "Dragon Nightfall", "Dragon Darkness", "Dragon Dusk Winged", "Dragon Dusk Elder" };
                    for (int i = 0; i < 3; i++) {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + seed[i + 2] + ".prefab");
                        if (prefab == null) throw new FileNotFoundException("Thiếu prefab nguồn: " + seed[i + 2]);
                        var avatar = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(seed[i + 5]));
                        if (avatar == null) throw new InvalidOperationException("Thiếu sprite avatar: " + seed[i + 2]);
                        var form = new CreatureFormDefinition { formId = (i + 1).ToString("00"), displayName = seed[i + 2], sourcePrefab = prefab, avatar = avatar };
                        if (seed[0] == "Dragon Dusk") {
                            string[] stats = { "DragonDusk", "DragonDuskWinged", "DragonDuskElder" };
                            form.statProfile = AssetDatabase.LoadAssetAtPath<PetStatProfile>("Assets/Resources/PetStats/" + stats[i] + ".asset");
                            form.skillSet = AssetDatabase.LoadAssetAtPath<PetSpeciesSkillSet>("Assets/Game/Skills/Data/Species/Dragon0" + (i + 1) + ".asset");
                            family.evolutionRulesReviewed = true;
                        }
                        family.forms.Add(form);
                    }
                    EditorUtility.SetDirty(family);
                }
                if (!library.families.Contains(family)) library.families.Add(family);
            }
            EditorUtility.SetDirty(library);
            foreach (var family in library.families) if (family != null) AssetDatabase.SaveAssetIfDirty(family);
            AssetDatabase.SaveAssetIfDirty(library); BakeAvatars();
        }

        [MenuItem("Tools/Capstone/Creatures/2. Bake lightweight avatar catalog")]
        public static void BakeAvatars() {
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>(LibraryPath);
            if (library == null) throw new InvalidOperationException("Chưa tạo CreatureLibrary.");
            var avatarCatalog = LoadOrCreate<CreatureAvatarCatalog>(AvatarPath);
            var next = new List<CreatureAvatarCatalog.Entry>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var family in library.families) {
                if (family == null || string.IsNullOrWhiteSpace(family.definitionId)) throw new InvalidOperationException("Dòng thú thiếu ID.");
                foreach (var form in family.forms) {
                    if (form == null || string.IsNullOrWhiteSpace(form.formId)) throw new InvalidOperationException(family.name + ": dạng thiếu ID.");
                    foreach (string id in new[] { family.definitionId }.Concat(family.legacyDefinitionIds ?? Array.Empty<string>())) {
                        if (string.IsNullOrWhiteSpace(id) || !keys.Add(id.Trim() + "\n" + form.formId.Trim())) throw new InvalidOperationException("ID/alias bị trùng: " + id + "/" + form.formId);
                    }
                    next.Add(new CreatureAvatarCatalog.Entry { definitionId = family.definitionId, aliases = family.legacyDefinitionIds, formId = form.formId, avatar = form.avatar });
                }
            }
            avatarCatalog.entries = next; avatarCatalog.Invalidate(); EditorUtility.SetDirty(avatarCatalog); AssetDatabase.SaveAssetIfDirty(avatarCatalog);
        }

        [Serializable] public sealed class FormAudit {
            public string familyId, formId, name, sourcePrefab, animatorController;
            public bool avatar, statProfile, skillSet, sourceContainsPetController;
            public List<string> states = new List<string>();
        }
        [Serializable] public sealed class AuditReport {
            public string utc, verification = "Unity Editor asset/object checks; no Play Mode, standalone or performance claim.";
            public int families, forms, passed;
            public List<string> errors = new List<string>(), warnings = new List<string>(), checks = new List<string>();
            public List<FormAudit> assets = new List<FormAudit>();
        }

        [MenuItem("Tools/Capstone/Creatures/3. Validate library and write report on E")]
        public static void ValidateAndReport() {
            var report = Inspect();
            Directory.CreateDirectory(ReportRoot);
            File.WriteAllText(ReportRoot + "validation.json", JsonUtility.ToJson(report, true));
            if (report.errors.Count > 0) throw new InvalidOperationException(string.Join("\n", report.errors));
            Debug.Log($"Creature library: {report.families} dòng, {report.forms} dạng, {report.passed} kiểm tra đạt. Còn {report.warnings.Count} mục authoring chưa hoàn thiện. Xem {ReportRoot}validation.json");
        }
        static AuditReport Inspect() {
            var report = new AuditReport { utc = DateTime.UtcNow.ToString("O") };
            void Check(bool ok, string text) { if (ok) { report.passed++; report.checks.Add(text); } else report.errors.Add(text); }
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>(LibraryPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CreatureAvatarCatalog>(AvatarPath);
            Check(library != null && catalog != null, "Library and lightweight catalog exist");
            if (library == null || catalog == null) return report;
            report.families = library.families.Count;
            Check(report.families == 11, "11 imported families");
            foreach (var family in library.families) {
                Check(family != null && family.forms.Count == 3, "Three forms: " + family?.name);
                if (family == null) continue;
                if (!family.evolutionRulesReviewed) report.warnings.Add(family.displayName + ": điều kiện/ID tiến hóa chưa duyệt; thư viện không bật hay sửa rule gameplay.");
                foreach (var form in family.forms) {
                    if (form == null) { Check(false, "Null form in " + family.name); continue; }
                    report.forms++;
                    string key = family.definitionId + "/" + form.formId;
                    Check(form.sourcePrefab != null && form.avatar != null, "Source and avatar: " + key);
                    Check(catalog.TryResolve(family.definitionId, form.formId, out var icon) && icon == form.avatar, "Avatar projection: " + key);
                    if (!form.statProfile) report.warnings.Add(key + ": chưa có profile chỉ số; không tự đặt số.");
                    if (!form.skillSet) report.warnings.Add(key + ": chưa có bộ skill theo dạng; không sao chép bốn skill mẫu.");
                    var animator = form.sourcePrefab != null ? form.sourcePrefab.GetComponentInChildren<Animator>(true) : null;
                    Check(animator != null && animator.runtimeAnimatorController != null, "Animator assigned: " + key);
                    var row = new FormAudit { familyId = family.definitionId, formId = form.formId, name = form.displayName,
                        sourcePrefab = AssetDatabase.GetAssetPath(form.sourcePrefab), avatar = form.avatar, statProfile = form.statProfile, skillSet = form.skillSet,
                        sourceContainsPetController = form.sourcePrefab != null && form.sourcePrefab.GetComponentInChildren<PetController>(true) != null,
                        animatorController = animator != null ? AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) : string.Empty };
                    var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
                    if (controller != null) foreach (var layer in controller.layers) CollectStates(layer.stateMachine, row.states);
                    report.assets.Add(row);
                }
            }
            Check(report.forms == 33, "33 imported forms");
            var dependencies = AssetDatabase.GetDependencies(AvatarPath, true);
            Check(!dependencies.Any(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)), "Avatar catalog has no model/prefab dependencies");
            Check(!catalog.TryResolve("not-a-species", "01", out _), "Unknown species is not guessed");
            Check(!catalog.TryResolve("Dragon Dusk", "99", out _), "Unknown form is not replaced with form 01");
            var dragon = library.families.Find(f => f.definitionId == "Dragon Dusk");
            if (dragon != null) {
                Check(dragon.forms.Select(f => f.avatar).Distinct().Count() == 3, "Three distinct dragon avatars");
                Check(catalog.TryResolve("Dragon Dusk", "dragon-dusk-winged", out var legacy) && legacy == dragon.forms[1].avatar, "Legacy form alias preserved");
                var authoredMetadata = dragon.forms[0].sourcePrefab.GetComponent<PetCollectionMetadata>();
                Check(authoredMetadata != null && authoredMetadata.NextEvolutionIcon == dragon.forms[1].avatar,
                    "Existing source metadata preview uses form 02 avatar without editing its prefab");
                var go = new GameObject("Creature library verification") { hideFlags = HideFlags.HideAndDontSave };
                try {
                    var metadata = go.AddComponent<PetCollectionMetadata>();
                    var stats = go.AddComponent<PetHudRuntimeStats>();
                    foreach (var form in dragon.forms) {
                        JsonUtility.FromJsonOverwrite("{\"definitionId\":\"Dragon Dusk\",\"currentFormId\":\"" + form.formId + "\"}", metadata);
                        Check(stats.Icon == form.avatar, "Actual stats avatar follows form: " + form.formId);
                    }
                } finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            return report;
        }
        static void CollectStates(AnimatorStateMachine machine, List<string> names) {
            foreach (var state in machine.states) names.Add(state.state.name);
            foreach (var child in machine.stateMachines) CollectStates(child.stateMachine, names);
        }
        static T LoadOrCreate<T>(string path) where T : ScriptableObject {
            var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value != null) return value;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'), current = "Assets";
            foreach (string part in parent.Split('/').Skip(1)) { if (!AssetDatabase.IsValidFolder(current + "/" + part)) AssetDatabase.CreateFolder(current, part); current += "/" + part; }
            value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
        }
        public static void SetupBatch() {
            try { CreateMissing(); ValidateAndReport(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }

    [CustomEditor(typeof(CreatureFamilyDefinition))]
    sealed class CreatureFamilyInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            EditorGUILayout.HelpBox("Mỗi dạng gắn Avatar riêng tại đây. Hero Portrait là ảnh lớn độc lập. Source Prefab có thể còn script gameplay; chưa dùng nó làm model-only hoặc prefab spawn. Các trường này không cấp thú, không sửa save hay điều kiện tiến hóa.", MessageType.Info);
            DrawDefaultInspector();
            if (GUILayout.Button("Cập nhật avatar runtime từ thư viện")) CreatureLibraryAuthoring.BakeAvatars();
            if (GUILayout.Button("Kiểm thư viện — báo cáo trên E")) CreatureLibraryAuthoring.ValidateAndReport();
        }
    }
}

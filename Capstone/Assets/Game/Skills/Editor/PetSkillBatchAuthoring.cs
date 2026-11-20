using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.Skills.Editor {
    [Serializable] public sealed class SkillBatchDocument {
        public int version = 1;
        public string catalog;
        public SkillBatchSkill[] skills = Array.Empty<SkillBatchSkill>();
        public SkillBatchSet[] sets = Array.Empty<SkillBatchSet>();
    }
    [Serializable] public sealed class SkillBatchSkill {
        public bool approved;
        public string id, asset, displayName, description, element, icon, combat, presentation;
        public float cooldown;
        public string[] legacyIds = Array.Empty<string>();
        public string[] animatorStates = Array.Empty<string>();
        // These are review references only. The tool never assigns raw library VFX or guesses sockets.
        public string candidateVfx, notes;
    }
    [Serializable] public sealed class SkillBatchLearn {
        public string id;
        public int level = 1;
    }
    [Serializable] public sealed class SkillBatchForm {
        public string family, formId, displayName;
        public int proposedCodexNumber;
    }
    [Serializable] public sealed class SkillBatchSet {
        public bool approved;
        public bool preserveExisting;
        public string asset, speciesId, notes;
        public int newPetLevel = 1;
        public SkillBatchLearn[] learnset = Array.Empty<SkillBatchLearn>();
        public string[] defaultLoadout = Array.Empty<string>();
        public SkillBatchForm[] forms = Array.Empty<SkillBatchForm>();
    }

    /// <summary>Add-only authoring. No startup hooks, scene changes, save access or library reset.</summary>
    public static class PetSkillBatchAuthoring {
        public const string Input = "Assets/Game/Skills/Data/Authoring/PetSkillBatch.json";
        public const string ReportRoot = QaArtifactPaths.Root + "pet-skill-batch-2026-11-17/";
        const string Menu = "Tools/Capstone/Pet Skills/Batch/";

        sealed class Plan : IDisposable {
            public readonly List<string> errors = new List<string>(), notes = new List<string>();
            public readonly Dictionary<string, ScriptableObject> desired = new Dictionary<string, ScriptableObject>();
            public readonly Dictionary<string, ScriptableObject> originals = new Dictionary<string, ScriptableObject>();
            public readonly Dictionary<string, PetSpeciesSkillSet> prefabs = new Dictionary<string, PetSpeciesSkillSet>();
            public readonly List<ScriptableObject> temporary = new List<ScriptableObject>();
            public IEnumerable<string> Changes => desired.Where(p => !originals.TryGetValue(p.Key, out var old)
                || EditorJsonUtility.ToJson(old) != EditorJsonUtility.ToJson(p.Value)).Select(p => p.Key);
            public void Dispose() { foreach (var item in temporary) if (item != null && !EditorUtility.IsPersistent(item)) Object.DestroyImmediate(item); }
        }

        [MenuItem(Menu + "1. Open authoring table")]
        public static void OpenTable() => AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(Input));

        [MenuItem(Menu + "2. Validate and preview (no asset writes)")]
        public static void Preview() {
            Guard();
            using (var plan = Build()) WriteReport(plan, "preview");
        }

        [MenuItem(Menu + "3. Apply approved additions")]
        public static void Apply() {
            Guard();
            using (var plan = Build()) {
                WriteReport(plan, "before-apply");
                if (plan.errors.Count != 0) throw new InvalidOperationException("Batch bị chặn; đọc " + ReportRoot + "before-apply.txt");
                Commit(plan);
            }
            using (var after = Build()) {
                WriteReport(after, "after-apply");
                if (after.errors.Count != 0 || after.Changes.Any() || after.prefabs.Count != 0)
                    throw new InvalidOperationException("Batch chưa ổn định sau apply. Đọc after-apply.txt; không tự chạy lại.");
            }
        }

        [MenuItem(Menu + "4. Verify merge safety (memory only)")]
        public static void Verify() {
            Guard();
            var skill = ScriptableObject.CreateInstance<PetSkillDefinition>();
            var set = ScriptableObject.CreateInstance<PetSpeciesSkillSet>();
            var profile = ScriptableObject.CreateInstance<PetSkillPresentationProfile>();
            try {
                skill.skillId = "qa.bite"; skill.element = PetElement.Normal;
                set.speciesId = "qa";
                set.learnset.Add(new PetSpeciesSkillEntry { skill = skill, learnAtLevel = 7, overrideAnimation = true,
                    animatorStates = new[] { "ArtistState" }, presentationOverride = profile });
                set.defaultLoadout.Add(skill);
                var map = new Dictionary<string, PetSkillDefinition>(StringComparer.OrdinalIgnoreCase) { [skill.Id] = skill };
                var row = new SkillBatchSet { learnset = new[] { new SkillBatchLearn { id = skill.Id, level = 1 } }, defaultLoadout = new[] { skill.Id } };
                string before = EditorJsonUtility.ToJson(set);
                Merge(set, row, map, false);
                if (before != EditorJsonUtility.ToJson(set)) throw new InvalidOperationException("Merge overwrote an authored entry/default.");
                set.learnset.Clear(); set.defaultLoadout.Clear();
                Merge(set, row, map, true);
                string once = EditorJsonUtility.ToJson(set);
                Merge(set, row, map, false);
                if (once != EditorJsonUtility.ToJson(set) || set.learnset.Count != 1 || set.defaultLoadout.Count != 1)
                    throw new InvalidOperationException("Merge is not idempotent.");
                using (var plan = Build()) WriteReport(plan, "verification", new[] {
                    "PASS: existing learn level, animation override, presentation override and defaults preserved.",
                    "PASS: first merge adds one entry/default; second merge changes nothing.",
                    "NOT RUN: actual asset apply twice, Play/HUD/cooldown/save integration."
                });
            } finally { Object.DestroyImmediate(set); Object.DestroyImmediate(skill); Object.DestroyImmediate(profile); }
        }

        static void Guard() {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Dừng Play và chờ compile xong trước khi dùng batch authoring.");
        }
        static bool SafeAsset(string path) => !string.IsNullOrWhiteSpace(path) && path.StartsWith("Assets/Game/Skills/Data/", StringComparison.Ordinal)
            && path.EndsWith(".asset", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains('\\');
        static T Load<T>(string path) where T : Object => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        static T Draft<T>(Plan plan, string path) where T : ScriptableObject {
            if (plan.desired.TryGetValue(path, out var known)) return (T)known;
            var original = Load<T>(path);
            var draft = original != null ? Object.Instantiate(original) : ScriptableObject.CreateInstance<T>();
            draft.name = original != null ? original.name : Path.GetFileNameWithoutExtension(path);
            plan.temporary.Add(draft); plan.desired.Add(path, draft);
            if (original != null) plan.originals.Add(path, original);
            return draft;
        }

        static Plan Build() {
            var plan = new Plan();
            try { Build(plan); }
            catch (Exception e) { plan.errors.Add(e.Message); }
            return plan;
        }
        static void Build(Plan plan) {
            var input = Load<TextAsset>(Input);
            if (input == null) throw new InvalidOperationException("Thiếu " + Input);
            var data = JsonUtility.FromJson<SkillBatchDocument>(input.text);
            if (data == null || data.version != 1 || data.skills == null || data.sets == null) throw new InvalidDataException("Sai schema/version batch.");
            ValidateDraftInput(plan, data);
            // An explicit existing catalog is required; this command never creates a parallel library.
            var catalog = Load<PetSkillCatalog>(data.catalog);
            if (catalog == null || data.catalog != PetSkillLibrarySetup.CatalogPath) throw new InvalidDataException("Phải dùng catalog runtime hiện có.");
            var definitions = AssetDatabase.FindAssets("t:PetSkillDefinition").Select(g => Load<PetSkillDefinition>(AssetDatabase.GUIDToAssetPath(g))).Where(s => s != null).ToList();
            var map = new Dictionary<string, PetSkillDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var skill in definitions) AddKeys(plan, map, skill);
            var skillRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var skillPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in data.skills) {
                if (row == null || string.IsNullOrWhiteSpace(row.id) || !skillRows.Add(row.id.Trim()) || !skillPaths.Add(row.asset ?? "")) { plan.errors.Add("ID/path dòng skill trống/trùng."); continue; }
                if (!row.approved) { plan.notes.Add("CHỜ DUYỆT skill: " + row.id + " — " + row.notes); continue; }
                var old = Load<PetSkillDefinition>(row.asset);
                if (old != null) {
                    if (old.Id != row.id) plan.errors.Add("Không đổi ID/GUID skill hiện có: " + row.asset);
                    continue; // No scalar/icon/profile/alias edits to an existing definition.
                }
                if (!SafeAsset(row.asset) || AssetDatabase.LoadMainAssetAtPath(row.asset) != null || File.Exists(row.asset)) {
                    plan.errors.Add("Đường dẫn skill không an toàn hoặc đã bị chiếm: " + row.asset); continue;
                }
                if (map.ContainsKey(row.id)) { plan.errors.Add("Skill mới đụng ID/alias: " + row.id); continue; }
                if (!Enum.TryParse(row.element, out PetElement element) || !Elements.ElementCatalog.IsSupported(element)
                    || string.IsNullOrWhiteSpace(row.displayName) || float.IsNaN(row.cooldown) || float.IsInfinity(row.cooldown) || row.cooldown < 0) {
                    plan.errors.Add("Thiếu tên/hệ/cooldown hợp lệ: " + row.id); continue;
                }
                var combat = Load<PetSkillCombatProfile>(row.combat);
                if (combat == null) { plan.errors.Add("Chưa có Combat Profile thật cho skill mới: " + row.id); continue; }
                if (!string.IsNullOrEmpty(row.icon) && Load<Sprite>(row.icon) == null) plan.errors.Add("Icon chưa import dạng Sprite: " + row.icon);
                if (!string.IsNullOrEmpty(row.presentation) && Load<PetSkillPresentationProfile>(row.presentation) == null) plan.errors.Add("Sai Presentation: " + row.id);
                var next = Draft<PetSkillDefinition>(plan, row.asset);
                next.skillId = row.id.Trim(); next.displayName = row.displayName; next.description = row.description;
                next.element = element; next.cooldownSeconds = row.cooldown; next.icon = Load<Sprite>(row.icon);
                next.combat = combat; next.presentation = Load<PetSkillPresentationProfile>(row.presentation);
                next.legacyIds = row.legacyIds ?? Array.Empty<string>(); next.animatorStates = row.animatorStates ?? Array.Empty<string>();
                definitions.Add(next); AddKeys(plan, map, next);
            }
            var catalogDraft = Draft<PetSkillCatalog>(plan, data.catalog);
            foreach (var skill in definitions.Where(s => data.skills.Any(r => r != null && r.approved && r.id == s.Id)))
                if (!catalogDraft.skills.Contains(skill)) catalogDraft.skills.Add(skill);
            var setIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var formOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in data.sets) {
                if (row == null || string.IsNullOrWhiteSpace(row.speciesId) || !setIds.Add(row.speciesId.Trim()) || !paths.Add(row.asset ?? "")) {
                    plan.errors.Add("Dòng set trống hoặc trùng species/path."); continue;
                }
                foreach (var form in row.forms ?? Array.Empty<SkillBatchForm>())
                    if (!formOwners.Add(form.family + "::" + form.formId)) plan.errors.Add("Một form bị gán hai set: " + form.family + "::" + form.formId);
                if (!row.approved) { plan.notes.Add("CHỜ DUYỆT learnset: " + row.speciesId); continue; }
                if (!SafeAsset(row.asset) && Load<PetSpeciesSkillSet>(row.asset) == null) { plan.errors.Add("Sai đường dẫn set: " + row.asset); continue; }
                var original = Load<PetSpeciesSkillSet>(row.asset);
                if (original != null && original.speciesId != row.speciesId) { plan.errors.Add("Không đổi Species ID: " + row.asset); continue; }
                if (original == null && (AssetDatabase.LoadMainAssetAtPath(row.asset) != null || File.Exists(row.asset))) { plan.errors.Add("Path set bị chiếm: " + row.asset); continue; }
                var set = Draft<PetSpeciesSkillSet>(plan, row.asset);
                if (original == null) { set.speciesId = row.speciesId; set.catalog = catalog; }
                if (set.catalog != catalog) plan.errors.Add("Set dùng catalog khác: " + row.asset);
                if (original == null || !row.preserveExisting) {
                    ValidateRow(plan, row, map);
                    if (plan.errors.Count == 0) Merge(set, row, map, original == null);
                }
                // The validation graph may refer to drafts, but persisted references always point at originals or new assets.
                var target = original != null ? original : set;
                if (!catalogDraft.speciesSets.Contains(target)) catalogDraft.speciesSets.Add(target);
                foreach (var link in row.forms ?? Array.Empty<SkillBatchForm>()) BindForm(plan, link, target);
            }
            var validation = Object.Instantiate(catalogDraft); plan.temporary.Add(validation);
            validation.speciesSets = validation.speciesSets.Select(s => {
                string p = AssetDatabase.GetAssetPath(s);
                return plan.desired.TryGetValue(p, out var d) ? (PetSpeciesSkillSet)d : s;
            }).ToList();
            // Existing catalog validation uses identity, so use a temporary validation graph with matching back-references.
            validation.speciesSets = validation.speciesSets.Select(set => {
                if (set == null) return null;
                var check = Object.Instantiate(set); plan.temporary.Add(check); check.name = set.name; check.catalog = validation;
                return check;
            }).ToList();
            foreach (var check in validation.speciesSets.Where(s => s != null)) {
                foreach (var error in check.ValidateSet()) plan.errors.Add(check.name + ": " + error);
                if (check.defaultLoadout.Count > 4) plan.errors.Add(check.name + ": quá bốn ô mặc định.");
            }
            foreach (var error in catalog.ValidateCatalog()) plan.errors.Add(error);
            if (catalogDraft.skills.Count != catalogDraft.skills.Distinct().Count()) plan.errors.Add("Catalog trùng tham chiếu skill.");
            if (catalogDraft.speciesSets.Count != catalogDraft.speciesSets.Distinct().Count()) plan.errors.Add("Catalog trùng tham chiếu set.");
            foreach (var guid in AssetDatabase.FindAssets("t:CreatureFamilyDefinition")) {
                var family = Load<CreatureFamilyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var form in family.forms) {
                    if (form.skillSet == null) plan.notes.Add("CHƯA NỐI Family learnset: " + family.definitionId + "::" + form.formId);
                    if (form.petPrefab == null) plan.notes.Add("CHƯA CÓ prefab gameplay: " + family.definitionId + "::" + form.formId);
                }
            }
        }

        static void ValidateDraftInput(Plan plan, SkillBatchDocument data) {
            var ids = new HashSet<string>(data.skills.Where(s => s != null).Select(s => s.id ?? ""), StringComparer.OrdinalIgnoreCase);
            foreach (var row in data.sets.Where(s => s != null)) {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in row.learnset ?? Array.Empty<SkillBatchLearn>())
                    if (item == null || !ids.Contains(item.id ?? "") || !seen.Add(item.id ?? "") || item.level < 1 || item.level > 100)
                        plan.errors.Add("Dự thảo learnset sai ID/level/trùng: " + row.speciesId);
                seen.Clear();
                if (row.defaultLoadout == null || row.defaultLoadout.Length == 0 || row.defaultLoadout.Length > 4 || row.newPetLevel < 1 || row.newPetLevel > 100)
                    plan.errors.Add("Dự thảo default/cấp khởi đầu không hợp lệ: " + row.speciesId);
                foreach (string id in row.defaultLoadout ?? Array.Empty<string>())
                    if (!seen.Add(id ?? "") || !(row.learnset ?? Array.Empty<SkillBatchLearn>()).Any(l => l != null && l.id == id && l.level <= row.newPetLevel))
                        plan.errors.Add("Dự thảo default chưa học/trùng: " + row.speciesId + "/" + id);
            }
            var links = data.sets.Where(r => r != null).SelectMany(r => (r.forms ?? Array.Empty<SkillBatchForm>()).Where(f => f != null).Select(f => new { row = r, form = f }));
            foreach (var group in links.GroupBy(l => l.form.family)) {
                var ordered = group.OrderBy(l => l.form.formId, StringComparer.Ordinal).ToList();
                for (int i = 1; i < ordered.Count; i++) {
                    var previous = ordered[i - 1]; var next = ordered[i];
                    foreach (var learned in previous.row.learnset ?? Array.Empty<SkillBatchLearn>()) {
                        if (learned == null) continue;
                        var inherited = (next.row.learnset ?? Array.Empty<SkillBatchLearn>()).FirstOrDefault(l => l != null && l.id == learned.id);
                        if (inherited == null || inherited.level > learned.level)
                            plan.notes.Add("CẦN DUYỆT kế thừa: " + group.Key + " " + previous.form.formId + "→" + next.form.formId + ": " + learned.id);
                    }
                }
            }
        }

        static void AddKeys(Plan plan, Dictionary<string, PetSkillDefinition> map, PetSkillDefinition skill) {
            foreach (string value in new[] { skill.Id }.Concat(skill.legacyIds ?? Array.Empty<string>())) {
                if (string.IsNullOrWhiteSpace(value)) continue;
                string key = value.Trim();
                if (map.TryGetValue(key, out var other) && other != skill) plan.errors.Add("ID/alias trùng: " + key);
                else map[key] = skill;
            }
        }
        static void ValidateRow(Plan plan, SkillBatchSet row, Dictionary<string, PetSkillDefinition> map) {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in row.learnset ?? Array.Empty<SkillBatchLearn>()) {
                if (item == null || !map.TryGetValue(item.id ?? "", out var skill) || item.level < 1 || item.level > 100 || !seen.Add(skill.Id))
                    plan.errors.Add("Learnset thiếu skill/trùng/level ngoài 1–100: " + row.speciesId);
            }
            seen.Clear();
            if (row.defaultLoadout == null || row.defaultLoadout.Length == 0 || row.defaultLoadout.Length > 4) plan.errors.Add("Default phải có 1–4 skill: " + row.speciesId);
            foreach (string id in row.defaultLoadout ?? Array.Empty<string>()) {
                if (!map.TryGetValue(id ?? "", out var skill) || !seen.Add(skill.Id)
                    || !(row.learnset ?? Array.Empty<SkillBatchLearn>()).Any(l => l != null && map.TryGetValue(l.id ?? "", out var s) && s == skill && l.level <= row.newPetLevel))
                    plan.errors.Add("Default chưa học/đúp ở cấp thú mới: " + row.speciesId + " / " + id);
            }
        }
        static void Merge(PetSpeciesSkillSet set, SkillBatchSet row, Dictionary<string, PetSkillDefinition> map, bool newSet) {
            foreach (var item in row.learnset ?? Array.Empty<SkillBatchLearn>()) {
                var skill = map[item.id];
                if (!set.learnset.Any(e => e != null && e.skill == skill)) set.learnset.Add(new PetSpeciesSkillEntry { skill = skill, learnAtLevel = item.level });
            }
            if (newSet) foreach (string id in row.defaultLoadout ?? Array.Empty<string>()) set.defaultLoadout.Add(map[id]);
            set.Invalidate();
        }
        static void BindForm(Plan plan, SkillBatchForm link, PetSpeciesSkillSet set) {
            var family = Load<CreatureFamilyDefinition>(link.family);
            var form = family?.forms.Find(f => f.formId == link.formId);
            if (form == null) { plan.errors.Add("Không tìm thấy Family/form: " + link.family + "::" + link.formId); return; }
            if (form.skillSet != null && form.skillSet != set) { plan.errors.Add("Không đè binding Family đã có: " + family.name + "::" + form.formId); return; }
            if (form.skillSet == null) Draft<CreatureFamilyDefinition>(plan, link.family).forms.Find(f => f.formId == link.formId).skillSet = set;
            if (form.petPrefab == null) plan.notes.Add("THIẾU prefab pet runtime: " + family.definitionId + "::" + form.formId);
            foreach (var prefab in new[] { form.petPrefab, form.wildPrefab, form.enemyPrefab, form.visualPrefab }.Where(p => p != null).Distinct()) {
                string path = AssetDatabase.GetAssetPath(prefab);
                if (!path.StartsWith("Assets/Game/CreatureSystem/Runtime/", StringComparison.Ordinal)) { plan.notes.Add("Không sửa prefab ngoài Runtime: " + path); continue; }
                if (plan.prefabs.TryGetValue(path, out var other) && other != set) { plan.errors.Add("Prefab bị gán hai set: " + path); continue; }
                bool missing = false;
                foreach (var stats in prefab.GetComponentsInChildren<PetHudRuntimeStats>(true)) {
                    if (stats.SpeciesSkillSet != null && stats.SpeciesSkillSet != set) plan.errors.Add("Không đè Stats binding: " + path);
                    missing |= stats.SpeciesSkillSet == null;
                }
                foreach (var binding in prefab.GetComponentsInChildren<CreatureVisualBinding>(true)) {
                    if (binding.skillSet != null && binding.skillSet != set) plan.errors.Add("Không đè Visual binding: " + path);
                    missing |= binding.skillSet == null;
                    if (binding.sockets == null || binding.sockets.Count == 0) plan.notes.Add("Socket chưa có; cần đặt tay: " + path);
                    if (binding.animationProfile == null) plan.notes.Add("Thiếu CreatureAnimationProfile: " + path);
                }
                if (missing) plan.prefabs[path] = set;
            }
        }
        static void Commit(Plan plan) {
            var changed = plan.Changes.ToList();
            var backups = changed.Where(plan.originals.ContainsKey).ToDictionary(p => p, p => EditorJsonUtility.ToJson(plan.originals[p]));
            var prefabBackups = plan.prefabs.Keys.ToDictionary(p => p, File.ReadAllBytes);
            var created = new List<string>();
            try {
                // New definitions before sets, then existing objects/catalogs. All references resolve before saving.
                foreach (string path in changed.Where(p => !plan.originals.ContainsKey(p)).OrderBy(p => plan.desired[p] is PetSkillDefinition ? 0 : 1)) {
                    Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
                    AssetDatabase.CreateAsset(plan.desired[path], path); created.Add(path);
                }
                foreach (string path in changed.Where(plan.originals.ContainsKey)) {
                    EditorUtility.CopySerialized(plan.desired[path], plan.originals[path]);
                    EditorUtility.SetDirty(plan.originals[path]); AssetDatabase.SaveAssetIfDirty(plan.originals[path]);
                }
                foreach (var pair in plan.prefabs) {
                    var root = PrefabUtility.LoadPrefabContents(pair.Key);
                    try {
                        foreach (var stats in root.GetComponentsInChildren<PetHudRuntimeStats>(true)) {
                            var serialized = new SerializedObject(stats); var property = serialized.FindProperty("speciesSkillSet");
                            if (property == null) throw new MissingFieldException("PetHudRuntimeStats.speciesSkillSet");
                            if (property.objectReferenceValue == null) { property.objectReferenceValue = pair.Value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                        }
                        foreach (var binding in root.GetComponentsInChildren<CreatureVisualBinding>(true)) if (binding.skillSet == null) binding.skillSet = pair.Value;
                        if (PrefabUtility.SaveAsPrefabAsset(root, pair.Key) == null) throw new IOException("Không lưu được " + pair.Key);
                    } finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            } catch {
                foreach (var backup in backups) { EditorJsonUtility.FromJsonOverwrite(backup.Value, plan.originals[backup.Key]); EditorUtility.SetDirty(plan.originals[backup.Key]); AssetDatabase.SaveAssetIfDirty(plan.originals[backup.Key]); }
                foreach (var backup in prefabBackups) { File.WriteAllBytes(backup.Key, backup.Value); AssetDatabase.ImportAsset(backup.Key); }
                foreach (string path in created.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
                throw;
            }
        }
        static void Folder(string path) {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static void WriteReport(Plan plan, string name, IEnumerable<string> extra = null) {
            Directory.CreateDirectory(ReportRoot);
            var lines = new List<string> { "Pet skill batch — " + DateTime.UtcNow.ToString("O"), "Input: " + Input,
                "Errors: " + plan.errors.Count, "Asset changes: " + plan.Changes.Count(), "Prefab changes: " + plan.prefabs.Count,
                "Pending rows are proposals, not runtime content. Codex numbers are review-only; no catalog renumbering here." };
            lines.AddRange(plan.errors.Select(e => "ERROR " + e)); lines.AddRange(plan.Changes.Select(p => "CHANGE " + p));
            lines.AddRange(plan.prefabs.Keys.Select(p => "BIND " + p)); lines.AddRange(plan.notes);
            if (extra != null) lines.AddRange(extra);
            File.WriteAllLines(ReportRoot + name + ".txt", lines);
            Debug.Log("Pet skill batch: " + plan.errors.Count + " lỗi; xem " + ReportRoot + name + ".txt");
        }
    }
}

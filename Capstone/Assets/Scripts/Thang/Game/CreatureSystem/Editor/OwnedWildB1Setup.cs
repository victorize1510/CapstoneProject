using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Skills;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor
{
    // Chi B1. Khong co InitializeOnLoad, khong reset scene/save, khong tao Enemy rieng.
    public static class OwnedWildB1Setup
    {
        const string ConfigPath = "Assets/Game/CreatureSystem/Editor/Configuration/OwnedWildB1.json";
        const string Shared = "Assets/Game/Skills/Data/B1";
        [Serializable] public sealed class Recipe
        {
            public int schema;
            public bool trial;
            public string[] scope, tables;
            public Form[] forms;
            public Skill[] skills;
            public string catalog, skillCatalog, ownedTemplate, wildTemplate, aiTemplate;
        }
        [Serializable] public sealed class Box
        {
            public float radius, height, visualY;
            public float[] center;
        }
        [Serializable] public sealed class Entry
        {
            public string id, state, motion, presentation;
            public int level, action;
            public float duration, release;
        }
        [Serializable] public sealed class Form
        {
            public string family, form, name, visual, profile, controller, source;
            public string iconGuid, stat, set, owned, wild, headPath, bodyPath;
            public int[] baseStats;
            public int catchRate, primary, secondary;
            public Box box;
            public Entry[] entries;
        }
        [Serializable] public sealed class Skill
        {
            public string id, name, element, delivery, offense, action, icon;
            public string effect, effectSource, definition, combat, presentation;
            public float power, cooldown, range, speed, radius, lifetime, retreat, preferred, release;
        }

        static Recipe Load()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Dừng Play Mode trước khi chạy B1.");
            var data = JsonUtility.FromJson<Recipe>(File.ReadAllText(ConfigPath));
            if (data == null || data.schema != 1 || !data.trial || data.forms?.Length != 6
                || data.scope?.Length != 2 || !data.scope.Contains("chick") || !data.scope.Contains("cat-electric"))
                throw new InvalidOperationException("Cấu hình chỉ được chứa sáu form B1 đã duyệt.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in data.forms)
            {
                if (!data.scope.Contains(f.family) || !new[] { "01", "02", "03" }.Contains(f.form)
                    || !keys.Add(f.family + "/" + f.form) || f.baseStats?.Length != 6
                    || f.baseStats.Any(v => v < 1 || v > 255) || f.catchRate < 1 || f.catchRate > 255)
                    throw new InvalidOperationException("Form/Stats/Capture Rate không hợp lệ.");
                var family = Need<CreatureFamilyDefinition>("Assets/Game/CreatureSystem/Data/Families/Family_" + f.family + ".asset");
                var form = family.forms.Single(x => x.formId == f.form);
                if (!form.TryGetTyping(out var primary, out var secondary))
                    throw new InvalidOperationException("Family chưa có cấu hình hệ hợp lệ: " + f.family + "/" + f.form);
                f.primary = (int)primary;
                f.secondary = (int)secondary;
                var visual = Need<GameObject>(f.visual);
                var animator = visual.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || animator.avatar.isHuman
                    || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("Thiếu Generic Avatar/controller: " + f.visual);
                var binding = animator.GetComponent<CreatureVisualBinding>();
                if (binding == null || binding.animationProfile != Need<CreatureAnimationProfile>(f.profile))
                    throw new InvalidOperationException("Visual/Profile không khớp: " + f.visual);
                if (visual.transform.Find(f.headPath) == null || visual.transform.Find(f.bodyPath) == null)
                    throw new InvalidOperationException("Đường xương không tồn tại: " + f.visual);
                var controller = animator.runtimeAnimatorController as AnimatorController;
                if (controller == null) throw new InvalidOperationException("Cần AnimatorController thật.");
                foreach (var e in f.entries)
                {
                    var state = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == e.state).state;
                    if (state == null || !(state.motion is AnimationClip clip) || clip.length <= 0 || clip.isLooping)
                        throw new InvalidOperationException("Thiếu motion one-shot thật: " + f.visual + "/" + e.state);
                    if (e.level < 1 || e.duration <= 0 || e.release <= 0 || e.release >= .85f)
                        throw new InvalidOperationException("Thiếu thông số skill: " + e.id);
                }
            }
            return data;
        }

        [MenuItem("Tools/Capstone/Creatures/B1/1. Build missing Chick and Electric Cat assets")]
        public static void BuildMissing()
        {
            var data = Load();
            int writes = 0;
            var library = Need<PetSkillCatalog>(data.skillCatalog);
            foreach (var s in data.skills)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(s.effect) == null)
                {
                    var go = PrefabUtility.LoadPrefabContents(s.effectSource);
                    try
                    {
                        foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true))
                            if (script != null && script.GetType().FullName != "UnityEngine.Rendering.Universal.UniversalAdditionalLightData")
                                Object.DestroyImmediate(script);
                        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
                        foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
                        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                        { var main = ps.main; main.stopAction = ParticleSystemStopAction.None; }
                        go.name = s.id;
                        Folder(Path.GetDirectoryName(s.effect));
                        PrefabUtility.SaveAsPrefabAsset(go, s.effect); writes++;
                    }
                    finally { PrefabUtility.UnloadPrefabContents(go); }
                }
                var impact = Impact(data, s.id);
                var presentation = Presentation(s.presentation, s.release, impact, ref writes);
                var combat = NewAsset<PetSkillCombatProfile>(s.combat, ref writes, out bool newCombat);
                if (newCombat)
                {
                    combat.delivery = (PetSkillDelivery)Enum.Parse(typeof(PetSkillDelivery), s.delivery);
                    combat.damageKind = s.offense == "ATK" ? CreatureDamageKind.Physical : CreatureDamageKind.Magical;
                    combat.power = s.power; combat.range = s.range; combat.projectileSpeed = s.speed;
                    combat.projectileRadius = s.radius; combat.lifetime = s.lifetime;
                    combat.projectileVisual = combat.delivery == PetSkillDelivery.Projectile ? Need<GameObject>(s.effect) : null;
                    combat.releaseSocket = "Head"; combat.retreatDistance = s.retreat;
                    combat.retreatSpeed = 12; combat.preferredDistance = s.preferred; combat.visualScale = .35f;
                    Save(combat);
                }
                var skill = NewAsset<PetSkillDefinition>(s.definition, ref writes, out bool newSkill);
                if (newSkill)
                {
                    skill.skillId = s.id; skill.displayName = s.name;
                    skill.description = "Tấn công một mục tiêu hợp lệ trong tầm thi triển.";
                    skill.element = s.element == "Bay" ? PetElement.Flying : PetElement.Electric;
                    skill.icon = Need<Sprite>(s.icon); skill.cooldownSeconds = s.cooldown;
                    skill.animatorStates = new[] { s.id == "peck.flying" ? "Bite Attack" : s.id == "orb.electric" ? "Projectile Attack" : "Cast Spell" };
                    skill.animationDuration = 1; skill.animationFade = .08f;
                    skill.presentation = presentation; skill.combat = combat; Save(skill);
                }
                if (!library.skills.Contains(skill)) { library.skills.Add(skill); Save(library); writes++; }
            }
            library.Invalidate();
            foreach (var f in data.forms)
            {
                var stat = NewAsset<PetStatProfile>(f.stat, ref writes, out bool newStat);
                if (newStat)
                {
                    var so = new SerializedObject(stat);
                    Set(so, "definitionId", f.family); Set(so, "formId", f.form);
                    var axes = new[] { "hp", "attack", "magicAttack", "defense", "magicDefense", "speed" };
                    for (int i = 0; i < 6; i++) Set(so, axes[i], f.baseStats[i]);
                    Set(so, "criticalRate", 6.25f); Set(so, "criticalDamage", 50f);
                    Set(so, "isCatchable", true); Set(so, "baseCatchRate", f.catchRate);
                    so.ApplyModifiedPropertiesWithoutUndo(); Save(stat);
                }
                foreach (var e in f.entries) Presentation(e.presentation, e.release, Impact(data, e.id), ref writes);
                var set = NewAsset<PetSpeciesSkillSet>(f.set, ref writes, out bool newSet);
                if (newSet)
                {
                    set.speciesId = f.family + (f.family == "chick" && f.form == "03" ? "/03" : "");
                    set.catalog = library; set.initializeFreshWildLoadout = true;
                    foreach (var e in f.entries)
                    {
                        var definition = Definition(data, e.id);
                        set.learnset.Add(new PetSpeciesSkillEntry { skill = definition, learnAtLevel = e.level,
                            overrideAnimation = true, animatorStates = new[] { e.state }, animationDuration = e.duration,
                            animationFade = .08f, presentationOverride = Need<PetSkillPresentationProfile>(e.presentation) });
                        set.defaultLoadout.Add(definition);
                    }
                    Save(set);
                }
                if (!library.speciesSets.Contains(set)) { library.speciesSets.Add(set); Save(library); writes++; }
                ConfigureVisual(f, set, ref writes);
            }
            foreach (var f in data.forms)
            {
                Actor(data, f, false, ref writes);
                Actor(data, f, true, ref writes);
            }
            Connect(data, ref writes);
            library.Invalidate();
            Debug.Log("B1 build: " + writes + " thay đổi. Không sửa scene/save hoặc sáu form Rồng/Dơi. Chạy Verify tiếp theo.");
        }

        static void ConfigureVisual(Form f, PetSpeciesSkillSet set, ref int writes)
        {
            var asset = Need<GameObject>(f.visual);
            var b = asset.GetComponentInChildren<CreatureVisualBinding>(true);
            if (b.skillSet != null && b.skillSet != set) throw new InvalidOperationException("Giữ SkillSet chỉnh tay: " + f.visual);
            var expected = new[] { ("Head", f.headPath), ("Body", f.bodyPath), ("Ground", "") };
            if (b.skillSet == set && expected.All(v => b.sockets.Any(s => s.key == v.Item1 && s.anchor != null))) { }
            else
            {
                var root = PrefabUtility.LoadPrefabContents(f.visual);
                try
                {
                    var binding = root.GetComponentInChildren<CreatureVisualBinding>(true); binding.skillSet = set;
                    foreach (var tuple in expected)
                        if (!binding.sockets.Any(s => s.key == tuple.Item1 && s.anchor != null))
                        {
                            binding.sockets.RemoveAll(s => s.key == tuple.Item1);
                            binding.sockets.Add(new PetSkillSocket { key = tuple.Item1,
                                anchor = tuple.Item2.Length == 0 ? root.transform : root.transform.Find(tuple.Item2) });
                        }
                    PrefabUtility.SaveAsPrefabAsset(root, f.visual); writes++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var profile = Need<CreatureAnimationProfile>(f.profile); bool dirty = false;
            foreach (var e in f.entries)
            {
                var mapping = profile.skills.FirstOrDefault(s => s.skillId == e.id);
                if (mapping != null && mapping.action != (CreatureAction)e.action)
                    throw new InvalidOperationException("Mapping chỉnh tay không khớp: " + f.profile + "/" + e.id);
                if (mapping == null) { profile.skills.Add(new CreatureSkillAction { skillId = e.id, action = (CreatureAction)e.action }); dirty = true; }
            }
            if (dirty) { Save(profile); writes++; }
        }

        static void Actor(Recipe data, Form f, bool wild, ref int writes)
        {
            string target = wild ? f.wild : f.owned;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(target) != null) return;
            var root = PrefabUtility.LoadPrefabContents(wild ? data.wildTemplate : data.ownedTemplate);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(Need<GameObject>(f.visual), root.scene);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                var animator = visual.GetComponentInChildren<Animator>(true);
                var pet = root.GetComponent<PetController>(); var ps = new SerializedObject(pet);
                Set(ps, "owner", null); Set(ps, "wildEncounter", wild); Set(ps, "useSelfInput", false); Set(ps, "animator", animator);
                Strings(ps.FindProperty("spawnStates"), new[] { "Cast Spell", "Idle" });
                var profile = Need<CreatureAnimationProfile>(f.profile);
                Strings(ps.FindProperty("idleStates"), new[] { profile.actions.First(a => a.action == CreatureAction.Idle).state });
                Strings(ps.FindProperty("moveStates"), new[] { profile.actions.First(a => a.action == CreatureAction.Move).state });
                Strings(ps.FindProperty("attackStates"), new[] { "Bite Attack" }); ps.ApplyModifiedPropertiesWithoutUndo();
                var metadata = root.GetComponent<PetCollectionMetadata>(); var ms = new SerializedObject(metadata);
                foreach (string k in new[] { "petId", "nickname", "captureDate", "captureLocation", "captureBallId", "trainerName", "codexNumber", "codexDescription" }) Set(ms, k, "");
                foreach (string k in new[] { "favorite", "released", "geneticsInitialized", "captureGeneticsRolled", "codexRegistered", "hasNextEvolution" }) Set(ms, k, false);
                Set(ms, "definitionId", f.family); Set(ms, "species", f.name); Set(ms, "currentFormId", f.form);
                Set(ms, "evolutionStage", int.Parse(f.form) - 1); Set(ms, "element", f.primary); Set(ms, "secondaryElement", f.secondary);
                Set(ms, "experience", 0); Set(ms, "experienceToNextLevel", 100); Set(ms, "obtainedOrder", 0); Set(ms, "nature", "Serious");
                ms.FindProperty("promptedEvolutionIds").ClearArray(); ms.FindProperty("resourceInvestments").ClearArray();
                foreach (string k in new[] { "individualValues", "effortValues" })
                    foreach (string axis in new[] { "hp", "attack", "defense", "magicAttack", "magicDefense", "speed" })
                        ms.FindProperty(k).FindPropertyRelative(axis).intValue = 0;
                var family = data.forms.Where(x => x.family == f.family).OrderBy(x => x.form).ToArray();
                var rules = ms.FindProperty("evolutionRules"); rules.arraySize = 2;
                for (int i = 0; i < 2; i++)
                {
                    var to = family[i + 1]; var r = rules.GetArrayElementAtIndex(i);
                    Set(r, "evolutionId", f.family + ".0" + (i + 1) + "-to-0" + (i + 2) + ".level");
                    Set(r, "sourceStage", i); Set(r, "targetFormId", to.form); Set(r, "targetDisplayName", to.name); Set(r, "targetSpecies", to.name);
                    Set(r, "targetIcon", Need<Sprite>(AssetDatabase.GUIDToAssetPath(to.iconGuid)));
                    Set(r, "targetVisualPrefab", Need<GameObject>(to.visual)); Set(r, "targetElement", to.primary); Set(r, "targetSecondaryElement", to.secondary);
                    Set(r, "replaceSecondaryElement", true);
                    Set(r, "trigger", 0); Set(r, "requiredLevel", i == 0 ? 18 : 36); Set(r, "requiredItemId", ""); Set(r, "requiredItemQuantity", 1);
                    Set(r, "maxHealthMultiplier", 1f);
                    foreach (string axis in new[] { "attackBonus", "defenseBonus", "magicAttackBonus", "magicDefenseBonus", "speedBonus" }) Set(r, axis, 0);
                }
                ms.ApplyModifiedPropertiesWithoutUndo();
                var stats = root.GetComponent<PetHudRuntimeStats>(); var ss = new SerializedObject(stats);
                Set(ss, "displayName", f.name); Set(ss, "icon", Need<Sprite>(AssetDatabase.GUIDToAssetPath(f.iconGuid)));
                Set(ss, "level", 1); Set(ss, "speciesSkillSet", Need<PetSpeciesSkillSet>(f.set));
                Set(ss, "autoResolveSpeciesSkillSet", false); Set(ss, "usePrototypeSkillsWhenEmpty", false);
                Set(ss, "animator", animator); Set(ss, "petController", pet);
                float initialHealth = PetStatCalculator.CalculateMaxHealth(f.baseStats[0], 0, 0, 1);
                Set(ss, "health", initialHealth); Set(ss, "maxHealth", initialHealth); Set(ss, "energy", 0f); Set(ss, "maxEnergy", 0f);
                ss.FindProperty("skills").ClearArray(); ss.FindProperty("learnedSkills").ClearArray(); ss.ApplyModifiedPropertiesWithoutUndo();
                var ev = new SerializedObject(root.GetComponent<PetEvolutionVisualController>());
                Set(ev, "visualRoot", root.transform); Set(ev, "baseVisual", visual); Set(ev, "baseVisualPrefab", Need<GameObject>(f.visual)); ev.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<CreatureRolePresentation>().role = wild ? CreatureRole.Wild : CreatureRole.OwnedPet;
                root.GetComponent<PetSkillPresentationPlayer>().sockets = new List<PetSkillSocket>(visual.GetComponentInChildren<CreatureVisualBinding>(true).sockets);
                if (root.GetComponent<PetCombatActionDriver>() == null) root.AddComponent<PetCombatActionDriver>();
                if (wild)
                {
                    var health = root.GetComponent<DummyEnemy>(); health.autoRevive = false; health.trainingDummy = health.isBoss = false;
                    if (root.GetComponent<BasicCreatureEnemy>() == null) root.AddComponent<BasicCreatureEnemy>();
                    if (root.GetComponent<EncounterLevel>() == null) root.AddComponent<EncounterLevel>();
                }
                var capsule = root.GetComponent<CapsuleCollider>(); capsule.isTrigger = false;
                capsule.radius = f.box.radius; capsule.height = f.box.height; capsule.center = new Vector3(f.box.center[0], f.box.center[1], f.box.center[2]);
                var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>(); agent.enabled = false;
                agent.radius = capsule.radius; agent.height = capsule.height; agent.baseOffset = 0;
                root.name = (wild ? "Wild_" : "OwnedPet_") + f.family + "_" + f.form;
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
                Folder(Path.GetDirectoryName(target)); PrefabUtility.SaveAsPrefabAsset(root, target); writes++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Connect(Recipe data, ref int writes)
        {
            var catalog = Need<PetPrefabCatalog>(data.catalog); var cs = new SerializedObject(catalog); var entries = cs.FindProperty("entries");
            foreach (var f in data.forms)
            {
                var family = Need<CreatureFamilyDefinition>("Assets/Game/CreatureSystem/Data/Families/Family_" + f.family + ".asset");
                var form = family.forms.Single(x => x.formId == f.form);
                var owned = Need<GameObject>(f.owned); var wild = Need<GameObject>(f.wild); var stat = Need<PetStatProfile>(f.stat); var set = Need<PetSpeciesSkillSet>(f.set);
                if ((form.petPrefab != null && form.petPrefab != owned) || (form.wildPrefab != null && form.wildPrefab != wild))
                    throw new InvalidOperationException("Giữ prefab đã gắn khác recipe: " + f.family + "/" + f.form);
                if (form.petPrefab != owned || form.wildPrefab != wild || form.statProfile != stat || form.skillSet != set || form.typingStatus != CreatureTypingStatus.Configured)
                {
                    form.petPrefab = owned; form.wildPrefab = wild; form.statProfile = stat; form.skillSet = set;
                    form.typingStatus = CreatureTypingStatus.Configured; family.evolutionRulesReviewed = true; Save(family); writes++;
                }
                SerializedProperty entry = null;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var e = entries.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("definitionId").stringValue == f.family && e.FindPropertyRelative("formId").stringValue == f.form) entry = e;
                }
                if (entry == null) { entries.arraySize++; entry = entries.GetArrayElementAtIndex(entries.arraySize - 1); Set(entry, "definitionId", f.family); Set(entry, "formId", f.form); }
                if (entry.FindPropertyRelative("prefab").objectReferenceValue != owned.GetComponent<PetController>())
                { Set(entry, "prefab", owned.GetComponent<PetController>()); writes++; }
            }
            if (cs.ApplyModifiedPropertiesWithoutUndo()) Save(catalog);
            foreach (var p in data.tables)
            {
                var table = Need<CreatureSpawnTable>(p); bool dirty = false;
                foreach (var species in table.species.Where(s => data.scope.Contains(s.definitionId)))
                    foreach (var f in species.forms)
                    {
                        var recipe = data.forms.Single(x => x.family == species.definitionId && x.form == f.formId);
                        var prefab = Need<GameObject>(recipe.wild);
                        if (f.prefab == null) { f.prefab = prefab; dirty = true; }
                        else if (f.prefab != prefab) throw new InvalidOperationException("Table có prefab chỉnh tay khác recipe.");
                        if (!string.IsNullOrEmpty(f.configurationBlocker)) { f.configurationBlocker = ""; dirty = true; }
                    }
                if (dirty) { Save(table); writes++; }
            }
        }

        static PetSkillDefinition Definition(Recipe data, string id) => Need<PetSkillDefinition>(id == "bite.normal"
            ? "Assets/Game/CreatureSystem/Runtime/Bats/Skills/Skill_bite.normal.asset" : id == "wing strike"
            ? "Assets/Game/Skills/Data/Definitions/Skill_wing_strike.asset" : data.skills.Single(s => s.id == id).definition);
        static string Impact(Recipe data, string id) => data.skills.Single(s => s.id == (id == "bolt.electric" ? "bolt.electric" : id.Contains("electric") ? "spark.electric" : "peck.flying")).effect;
        static PetSkillPresentationProfile Presentation(string p, float release, string effect, ref int writes)
        {
            var value = NewAsset<PetSkillPresentationProfile>(p, ref writes, out bool created);
            if (!created) return value;
            value.releaseMode = PetSkillReleaseMode.NormalizedAnimation; value.releaseNormalized = release; value.chainNormalized = .85f;
            value.releaseDelaySeconds = 0; value.playbackSpeed = 1; value.impactWindowSeconds = 5;
            AudioClip Audio(string name) => Need<AudioClip>("Assets/UnityAssset/Trung/KriptoFX/Realistic Effects Pack v4/Effects/Audio/" + name + ".wav");
            value.cast = new PetSkillCue { socket = "Head", followSocket = true, actorSpace = true, sound = Audio("Ray2"), scale = .35f, volume = .3f, lifetimeSeconds = 1.2f };
            value.release = new PetSkillCue { socket = "Head", actorSpace = true, sound = Audio("Hand1"), scale = .35f, volume = .3f, lifetimeSeconds = 1.2f };
            value.impact = new PetSkillCue { effectPrefab = Need<GameObject>(effect), actorSpace = true, sound = Audio("Explosion1"), scale = .35f, volume = .3f, lifetimeSeconds = 1.2f };
            Save(value); return value;
        }
        static T NewAsset<T>(string p, ref int writes, out bool created) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(p); created = a == null;
            if (!created) return a;
            Folder(Path.GetDirectoryName(p)); a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, p); writes++; return a;
        }
        static T Need<T>(string p) where T : Object => AssetDatabase.LoadAssetAtPath<T>(p) ?? throw new InvalidOperationException("Thiếu asset thật: " + p);
        static void Folder(string p)
        {
            string current = "Assets";
            foreach (var part in p.Replace('\\', '/').Split('/').Skip(1))
            { string next = current + "/" + part; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part); current = next; }
        }
        static void Save(Object o) { EditorUtility.SetDirty(o); AssetDatabase.SaveAssetIfDirty(o); }
        static void Strings(SerializedProperty p, string[] values) { p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i]; }
        static void Set(SerializedObject so, string key, object value) => Assign(so.FindProperty(key), value);
        static void Set(SerializedProperty so, string key, object value) => Assign(so.FindPropertyRelative(key), value);
        static void Assign(SerializedProperty p, object v)
        {
            if (p == null) throw new InvalidOperationException("Thiếu serialized field.");
            if (v is string s) p.stringValue = s; else if (v is bool b) p.boolValue = b;
            else if (v is int i) { if (p.propertyType == SerializedPropertyType.Integer) p.longValue = i; else p.intValue = i; }
            else if (v is float f) p.floatValue = f; else p.objectReferenceValue = v as Object;
        }

        static void VerifySpawnTables(Recipe data, PetPrefabCatalog catalog, UnityEngine.SceneManagement.Scene preview,
            Action<bool, string> check)
        {
            foreach (string path in data.tables)
            {
                var table = Need<CreatureSpawnTable>(path);
                string zonePath = Path.GetDirectoryName(path).Replace('\\', '/') + "/"
                    + Path.GetFileNameWithoutExtension(path).Replace("SpawnTable_", "Zone_") + ".prefab";
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(Need<GameObject>(zonePath), preview);
                var zone = instance.GetComponent<CreatureEncounterZone>();
                var b1Table = Object.Instantiate(table);
                try
                {
                    check(zone.spawnTable == table, zonePath + ": sai Spawn Table");
                    check(table.ownedPetCatalog == catalog, path + ": sai OwnedPet catalog");
                    // Chi loc ban sao de loi cua Family chua lam khong che loi B1.
                    b1Table.species = table.species.Where(s => s != null && s.enabled && s.weight > 0
                        && data.scope.Contains(s.definitionId)).ToList();
                    check(b1Table.species.Count > 0, path + ": thieu Species B1 dang bat");
                    foreach (var species in b1Table.species)
                    {
                        foreach (var form in species.forms.Where(f => f != null && f.enabled && f.weight > 0))
                        {
                            var recipe = data.forms.Single(f => f.family == species.definitionId && f.form == form.formId);
                            check(form.prefab == Need<GameObject>(recipe.wild), path + ": sai prefab " + species.definitionId + "/" + form.formId);
                        }
                    }
                    zone.spawnTable = b1Table;
                    bool valid = zone.ValidateConfiguration(out string reason);
                    check(valid && zone.EligibleSpeciesCount == b1Table.species.Count && zone.ConfigurationIssues.Count == 0,
                        zonePath + ": " + reason + " " + string.Join(" | ", zone.ConfigurationIssues));
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                    Object.DestroyImmediate(b1Table);
                }
            }
        }

        [MenuItem("Tools/Capstone/Creatures/B1/2. Verify assets and isolated Editor snapshots")]
        public static void Verify()
        {
            var data = Load(); var issues = new List<string>(); int checks = 0;
            var library = Need<PetSkillCatalog>(data.skillCatalog);
            foreach (var issue in library.ValidateCatalog()) issues.Add("Catalog: " + issue);
            checks++;
            foreach (string path in data.forms.Select(f => f.set).Distinct())
            {
                foreach (var issue in Need<PetSpeciesSkillSet>(path).ValidateSet()) issues.Add(path + ": " + issue);
                checks++;
            }
            // Clear only a cached authoring lookup; no save data or live scene actor is touched.
            typeof(PetStatCalculator).GetMethod("ResetProfiles", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var catalog = Need<PetPrefabCatalog>(data.catalog);
                void CheckAsset(bool ok, string detail) { checks++; if (!ok) issues.Add(detail); }
                VerifySpawnTables(data, catalog, preview, CheckAsset);
                foreach (var f in data.forms)
                {
                    CheckAsset(catalog.TryGetPrefab(f.family, f.form, out var ownedPrefab)
                        && ownedPrefab == Need<GameObject>(f.owned).GetComponent<PetController>(),
                        "Catalog khong resolve dung OwnedPet " + f.family + "/" + f.form);
                    foreach (string p in new[] { f.owned, f.wild })
                    {
                        var prefab = Need<GameObject>(p);
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                        try
                        {
                            var pet = instance.GetComponent<PetController>(); var stats = instance.GetComponent<PetHudRuntimeStats>();
                            var meta = instance.GetComponent<PetCollectionMetadata>(); var animator = instance.GetComponentInChildren<Animator>(true);
                            var driver = instance.GetComponent<CreatureAnimationDriver>(); bool wild = p == f.wild;
                            void Check(bool ok, string detail) { checks++; if (!ok) issues.Add(p + ": " + detail); }
                            Check(pet != null && stats != null && meta != null && driver != null, "Thiếu component root");
                            Check(pet.IsWildEncounter == wild && meta.DefinitionId == f.family && meta.CurrentFormId == f.form, "Sai role/ID");
                            Check(meta.Element == (PetElement)f.primary && meta.SecondaryElement == (PetElement)f.secondary, "Sai hệ");
                            Check(PetStatCalculator.TryGetProfile(meta, out var stat) && stat.BaseCatchRate == f.catchRate, "Sai StatProfile/capture");
                            Check(animator != null && animator.avatar != null && animator.avatar.isValid && !animator.avatar.isHuman, "Avatar Generic không hợp lệ");
                            Check(instance.GetComponentsInChildren<Animator>(true).Length == 1, "Animator trùng");
                            animator.Rebind(); animator.Update(0); driver.Bind(animator);
                            foreach (var action in new[] { CreatureAction.Idle, CreatureAction.Move, CreatureAction.Attack, CreatureAction.Hit, CreatureAction.Death })
                                Check(driver.TryGetStates(action, out _), "HasState " + action);
                            foreach (var entry in f.entries) Check(driver.TryGetSkillStates(entry.id, out _), "HasState/mapping " + entry.id);
                            if (wild)
                            {
                                Check(instance.GetComponent<BasicCreatureEnemy>() != null && instance.GetComponent<WildPetCaptureTarget>() != null, "Thiếu AI/capture");
                                Check(instance.GetComponent<WildPetCaptureTarget>()?.CanEnterDowned == true, "Không đủ điều kiện capture khi gục");
                                meta.AssignPersistentId("b1-check-" + f.family + f.form);
                                typeof(PetCollectionMetadata).GetMethod("InitializeWildIndividual", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(meta, null);
                                foreach (int level in new[] { 1, 4, 5, 10, 12, 18, 30, 36 })
                                {
                                    stats.SetIdentity(f.name, level, stats.Icon);
                                    typeof(PetHudRuntimeStats).GetMethod("InitializeFreshWildLoadout", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stats, null);
                                    var actual = stats.GetSkills().Select(s => s.skillId).ToArray();
                                    var expected = f.entries.Where(e => e.level <= level).Take(4).Select(e => e.id).ToArray();
                                    Check(actual.SequenceEqual(expected), "Loadout mới Lv" + level);
                                }
                                var before = stats.CreateSaveData(); var metadata = meta.CreateSaveData();
                                bool factoryLoaded = catalog.TryInstantiate(f.family, f.form, instance.transform, out var factoryPet);
                                Check(factoryLoaded, "Factory không tạo được OwnedPet theo ID/form");
                                if (!factoryLoaded) continue;
                                var other = factoryPet.gameObject;
                                try
                                {
                                    var target = other.GetComponent<PetHudRuntimeStats>(); var targetMeta = other.GetComponent<PetCollectionMetadata>();
                                    targetMeta.AssignPersistentId(meta.PersistentId); targetMeta.RestoreFromSaveData(metadata); target.RestoreFromSaveData(before);
                                    Check(JsonUtility.ToJson(before) == JsonUtility.ToJson(target.CreateSaveData()), "Snapshot stats/loadout roundtrip");
                                    Check(JsonUtility.ToJson(metadata) == JsonUtility.ToJson(targetMeta.CreateSaveData()), "Snapshot genetics/form roundtrip");
                                    targetMeta.RestoreEvolutionPresentation(target);
                                    Check(targetMeta.Element == (PetElement)f.primary && targetMeta.SecondaryElement == (PetElement)f.secondary, "Typing sau restore form");
                                    var empty = target.CreateSaveData(); empty.equippedSkillIds.Clear();
                                    target.RestoreFromSaveData(empty);
                                    Check(target.GetSkills().Count == 0, "Loadout rong da luu bi tu dien lai");
                                    bool initializedOwned = (bool)typeof(PetHudRuntimeStats).GetMethod("InitializeFreshWildLoadout", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
                                    Check(!initializedOwned && target.GetSkills().Count == 0, "Fresh Wild initializer chay tren Owned");
                                    if (f.family == "chick" && f.form == "01")
                                    {
                                        foreach (var rule in targetMeta.EvolutionRules.OrderBy(r => r.SourceStage))
                                            Check(targetMeta.ApplyEvolution(rule), "Thu logic metadata tien hoa");
                                        var final = data.forms.Single(x => x.family == f.family && x.form == "03");
                                        Check(targetMeta.Element == (PetElement)final.primary && targetMeta.SecondaryElement == (PetElement)final.secondary,
                                            "Chim03 khong dung he chinh thuc");
                                    }
                                }
                                finally { Object.DestroyImmediate(other); }
                            }
                        }
                        finally { Object.DestroyImmediate(instance); }
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            string root = Path.Combine(Capstone.Game.UISystem.QaArtifactPaths.Root, "owned-wild-b1-chick-cat-2026-11-19");
            Directory.CreateDirectory(root);
            string output = Path.Combine(root, "editor-verification.txt");
            File.AppendAllText(Path.Combine(root, "editor-created-paths.txt"), output + Environment.NewLine);
            File.WriteAllText(output, "EDITOR ONLY; PLAY UNVERIFIED\nChecks=" + checks + "\n" + string.Join("\n", issues));
            if (issues.Count > 0) throw new InvalidOperationException("B1: " + issues.Count + " lỗi. Xem " + output);
            Debug.Log("B1 Editor checks=" + checks + ". Chưa kiểm Play/capture transaction/respawn/20 summon cycles. " + output);
        }
    }
}

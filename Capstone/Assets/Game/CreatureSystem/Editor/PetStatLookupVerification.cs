using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.HudSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor {
    /// <summary>Editor-only regression checks. Fixtures stay in memory; no source asset, scene or save is written.</summary>
    public static class PetStatLookupVerification {
        public const string Root = QaArtifactPaths.Root + "stat-profile-bat-proposal-2026-11-06/";
        static readonly FieldInfo Cache = typeof(PetStatCalculator).GetField("profiles", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly List<string> Checks = new List<string>();

        [MenuItem("Tools/Capstone/Creatures/8. Verify scoped stat profile lookup")]
        public static void Run() {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Kiểm lookup ở Edit Mode sau khi compile xong.");
            Directory.CreateDirectory(Root);
            Checks.Clear();
            File.WriteAllText(Root + "status.txt", "RUNNING: Unity Editor in-memory regression; no Play Mode.");
            var temporary = new List<Object>();
            PetStatProfile[] previous = (PetStatProfile[])Cache.GetValue(null);
            try {
                var go = new GameObject("Stat lookup regression (temporary)") { hideFlags = HideFlags.HideAndDontSave };
                temporary.Add(go);
                go.SetActive(false);
                var metadata = go.AddComponent<PetCollectionMetadata>();
                var actual = Resources.LoadAll<PetStatProfile>("PetStats");
                var originals = actual.ToDictionary(p => p, p => EditorJsonUtility.ToJson(p));
                Check(actual.Length >= 3, "Actual stat resources load in Unity");
                Cache.SetValue(null, actual);

                string[] dragonForms = { "dragon-dusk-base", "dragon-dusk-winged", "dragon-dusk-elder" };
                int[] dragonHp = { 55, 70, 90 }, dragonCatch = { 255, 120, 45 };
                for (int i = 0; i < 3; i++) {
                    var expected = actual.Single(p => p.DefinitionId == "Dragon Dusk" && p.FormId == dragonForms[i]);
                    foreach (string id in new[] { "Dragon Dusk", "Dragon Nightfall", "Dragon Darkness", "Dragon Dusk Winged", "Dragon Dusk Elder" }) {
                        Bind(metadata, id, string.Empty, (i + 1).ToString("00"));
                        Expect(metadata, expected, "Existing dragon numeric form and alias: " + id + "/" + (i + 1));
                        Bind(metadata, id, string.Empty, dragonForms[i]);
                        Expect(metadata, expected, "Existing dragon named form and alias: " + id + "/" + dragonForms[i]);
                    }
                    Check(expected.BaseStats.hp == dragonHp[i] && expected.BaseCatchRate == dragonCatch[i] && expected.IsCatchable,
                        "Existing dragon HP/capture configuration unchanged: " + dragonForms[i]);
                    Bind(metadata, "Dragon Dusk", "Dragon Dusk", (i + 1).ToString("00"));
                    var numeric = PetStatCalculator.Calculate(metadata, 20);
                    Bind(metadata, "Dragon Dusk", "Dragon Dusk", dragonForms[i]);
                    var legacy = PetStatCalculator.Calculate(metadata, 20);
                    Check(Equal(numeric, legacy), "All eight derived stats equal across dragon form aliases: " + dragonForms[i]);
                }
                Bind(metadata, "Dragon Dusk", string.Empty, "base");
                Expect(metadata, actual.Single(p => p.DefinitionId == "Dragon Dusk" && p.FormId == dragonForms[0]), "Legacy dragon base remains supported");
                Bind(metadata, "Dragon Dusk", "Dragon Dusk", "99");
                Check(!PetStatCalculator.TryGetProfile(metadata, out _), "Unknown dragon form does not return form 01");

                var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>(CreatureLibraryAuthoring.LibraryPath);
                Check(library != null, "Current family authoring library exists");
                var synthetic = new List<PetStatProfile>(actual);
                var profilesByKey = new Dictionary<string, PetStatProfile>();
                int index = 0;
                foreach (var family in library.families.Where(f => f != null && f.definitionId != "Dragon Dusk")) {
                    foreach (var form in family.forms) {
                        // Synthetic sentinel values prove identity isolation; these are not proposed balance data.
                        var fixture = Fixture(family.definitionId, form.formId, 20 + index++, temporary);
                        synthetic.Add(fixture);
                        profilesByKey.Add(family.definitionId + "/" + form.formId, fixture);
                    }
                }
                Check(profilesByKey.Count == 30, "Thirty independent in-memory family/form fixtures, no authored stat assets created");
                Cache.SetValue(null, synthetic.ToArray());
                foreach (var pair in profilesByKey) {
                    var fixture = pair.Value;
                    Bind(metadata, fixture.DefinitionId, string.Empty, fixture.FormId);
                    Expect(metadata, fixture, "Generic family/form resolves exact profile: " + pair.Key);
                    var result = PetStatCalculator.Calculate(metadata, 20);
                    Check(result.MaxHealth == PetStatCalculator.CalculateMaxHealth(fixture.BaseStats.hp, 0, 0, 20),
                        "Calculation reads this family's HP, not neutral/dragon: " + pair.Key);
                    Bind(metadata, " " + fixture.DefinitionId.ToUpperInvariant() + " ", string.Empty, fixture.FormId);
                    Expect(metadata, fixture, "Case/outer whitespace do not alter identity: " + pair.Key);
                }

                var bat = profilesByKey["bat/01"];
                var wolf = profilesByKey["wolf/01"];
                // Put the display-species profile first to expose order-dependent fallback bugs.
                Cache.SetValue(null, new[] { wolf, bat });
                Bind(metadata, "bat", "wolf", "01");
                Expect(metadata, bat, "Persistent definition ID wins over an earlier display-species profile");
                Bind(metadata, "legacy-bat-name", "bat", "01");
                Expect(metadata, bat, "Legacy species fallback retains its own numeric form");
                Bind(metadata, string.Empty, "bat", "01");
                Expect(metadata, bat, "Legacy species-only metadata remains supported");
                Bind(metadata, "bat", string.Empty, "02");
                Check(!PetStatCalculator.TryGetProfile(metadata, out _), "Missing form never reuses form 01");
                Bind(metadata, "not-a-family", string.Empty, "01");
                Check(!PetStatCalculator.TryGetProfile(metadata, out _), "Unknown family never borrows a different family's form");
                Check(!PetStatCalculator.TryGetProfile(null, out _), "Null metadata has no configured profile");

                var batBase = Fixture("bat", "base", 31, temporary);
                Cache.SetValue(null, new[] { batBase, bat });
                Bind(metadata, "bat", string.Empty, "base");
                Expect(metadata, batBase, "Generic base is preserved instead of becoming dragon-dusk-base");
                Bind(metadata, "bat", string.Empty, "dragon-dusk-base");
                Check(!PetStatCalculator.TryGetProfile(metadata, out _), "Dragon form names do not alias generic numeric/base forms");

                Cache.SetValue(null, actual);
                Bind(metadata, "renamed-dragon-display", "Dragon Nightfall", "02");
                Expect(metadata, actual.Single(p => p.FormId == dragonForms[1]), "Dragon alias fallback canonicalizes its own ID/form pair");
                foreach (var entry in originals)
                    Check(EditorJsonUtility.ToJson(entry.Key) == entry.Value, "Actual resource remains unmodified: " + entry.Key.name);
                File.WriteAllText(Root + "status.txt", "PASS: " + Checks.Count + " Unity Editor checks. In-memory fixtures only; no Play Mode, scene, prefab, save, build or UI changes.");
                Debug.Log("Stat lookup regression passed: " + Checks.Count + ". " + Root);
            } catch (Exception exception) {
                File.WriteAllText(Root + "status.txt", "FAIL: " + exception);
                throw;
            } finally {
                Cache.SetValue(null, previous);
                for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
                File.WriteAllLines(Root + "checks.txt", Checks);
                File.WriteAllText(Root + "cache-restored.txt", ReferenceEquals(Cache.GetValue(null), previous) ? "PASS: original profile cache restored; temporary objects destroyed." : "FAIL: cache mismatch");
            }
        }

        static void Check(bool valid, string message) {
            Checks.Add((valid ? "PASS " : "FAIL ") + message);
            if (!valid) throw new InvalidOperationException(message);
        }
        static void Expect(PetCollectionMetadata metadata, PetStatProfile expected, string message) =>
            Check(PetStatCalculator.TryGetProfile(metadata, out var found) && found == expected, message);

        static void Bind(PetCollectionMetadata metadata, string id, string species, string form) {
            var serialized = new SerializedObject(metadata);
            serialized.FindProperty("definitionId").stringValue = id;
            serialized.FindProperty("species").stringValue = species;
            serialized.FindProperty("currentFormId").stringValue = form;
            serialized.FindProperty("geneticsInitialized").boolValue = true;
            serialized.FindProperty("captureGeneticsRolled").boolValue = true;
            serialized.FindProperty("nature").stringValue = "Serious";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static PetStatProfile Fixture(string id, string form, int hp, List<Object> temporary) {
            var profile = ScriptableObject.CreateInstance<PetStatProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.name = "in-memory sentinel " + id + "/" + form;
            temporary.Add(profile);
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("definitionId").stringValue = id;
            serialized.FindProperty("formId").stringValue = form;
            serialized.FindProperty("hp").intValue = hp;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }
        static bool Equal(PetStatSnapshot a, PetStatSnapshot b) =>
            a.Hp == b.Hp && a.MaxHealth == b.MaxHealth && a.Attack == b.Attack && a.Defense == b.Defense && a.MagicAttack == b.MagicAttack && a.MagicDefense == b.MagicDefense && a.Speed == b.Speed && a.CriticalRate == b.CriticalRate && a.CriticalDamage == b.CriticalDamage;
    }
}

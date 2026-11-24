using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Capstone.Game.CreatureSystem;
using Capstone.Game.Elements;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace Capstone.Game.Skills.Editor {
    static class PetSkillAudit {
        const string TrungRoot = "Assets/Prefabs/Trung/Skills";
        const string Verified = "Đã xác minh";
        const string Unverified = "Chưa xác minh";
        const string Missing = "Thiếu asset";
        const string Conflicting = "Có xung đột";
        const string Multiple = "Có nhiều prefab cần người dùng lựa chọn";
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        static readonly Dictionary<string, string> AuthoringNames = new(StringComparer.OrdinalIgnoreCase) {
            ["gust.flying"] = "Chém Gió",
            ["leafstorm.grass"] = "Bão Lá",
            ["orb.electric"] = "Điện Cầu",
            ["spark.electric"] = "Tia Lửa"
        };
        static string ReportFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/SkillAudit-2026-11-23"));
        static string VerificationFolder => Path.Combine(QaArtifactPaths.Root, "SkillAudit-2026-11-23");

        [MenuItem("Tools/Capstone/Pet Skills/Export current skill and Trung VFX audit")]
        public static void Export() {
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Game/CreatureSystem/Data/CreatureLibrary.asset");
            var catalog = Resources.Load<PetSkillCatalog>("PetSkills/PetSkillCatalog");
            var definitions = Assets<PetSkillDefinition>().OrderBy(skill => skill.Id, StringComparer.Ordinal).ToArray();
            var forms = library.families.SelectMany(family => family.forms.Select(form => new FormInfo(family, form))).ToArray();
            var trung = Assets<GameObject>("t:Prefab", TrungRoot).OrderBy(PathOf).ToArray();
            var used = new Dictionary<GameObject, HashSet<string>>();
            var dependencyOwners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            Directory.CreateDirectory(ReportFolder);

            using var stats = new Csv("04_CHON_STATS_27", "Skill ID", "Tên", "Hệ runtime", "Hệ trong asset", "Loại sát thương runtime",
                "Delivery", "Power asset", "Range", "Cooldown giây", "Mana runtime", "Mana asset", "Số hit tối đa mỗi cast",
                "Số mục tiêu", "Bán kính Targeted", "Bán kính projectile", "Tốc độ projectile", "Lifetime projectile",
                "Crit Rate bonus", "Crit Damage bonus", "Buff/Debuff/DoT chạy thật", "Tỷ lệ hiệu ứng phụ",
                "Thông số khác", "Mô tả", "Mô tả hiệu ứng UI", "Icon", "Loài/Form: cấp Pet học", "Legacy ID",
                "Definition", "Combat profile", "Có trong Catalog", "Trạng thái", "Gợi ý kiểm thử cân bằng",
                "Chọn Power", "Chọn Range", "Chọn Cooldown", "Chọn MP", "Chọn Radius", "Chọn Speed", "Chọn hiệu ứng");
            using var vfx = new Csv("05_VFX_TRUNG_27", "Skill ID", "Tên", "Source Definition", "Presentation mặc định", "Cast prefab",
                "Release prefab", "Projectile visual", "Trail Renderer nằm trong prefab", "Impact prefab", "Buff prefab",
                "Cast: socket/offset/rotation/follow/timing/SFX", "Release: socket/offset/rotation/follow/timing/SFX",
                "Impact: vị trí/offset/rotation/timing/SFX", "Release mode và timing", "Projectile socket",
                "Projectile speed", "Homing", "Theo dõi đích", "Root Scale prefab theo vai trò", "Child/Particle/Graph",
                "Scale runtime", "Prefab Trung đã nối qua GUID/dependency", "Prefab Trung ứng viên", "Nguồn ứng viên",
                "Override Form", "Trạng thái", "Điểm cần xem trong Play Mode", "Chọn prefab", "Chọn Spawn", "Chọn Offset XYZ",
                "Chọn Rotation XYZ", "Chọn Scale XYZ", "Chọn Timing", "Chọn Speed", "Chọn SFX");
            using var formTable = new Csv("02_CHIA_33_FORM", "Family ID", "Form ID", "Tên Form", "Hệ 1", "Hệ 2", "Skill Set", "Species ID",
                "Skill ID @ cấp Pet học", "Loadout mặc định (thứ tự ưu tiên)", "Animation profile", "Animator Controller",
                "Bindings theo kỹ năng", "Override Presentation", "Visual runtime", "Owned prefab", "Wild prefab",
                "Scale Form", "Socket: đường dẫn và pose prefab", "Trạng thái");
            using var perForm = new Csv("PHU_LUC_FORM_SKILL", "Family ID", "Form ID", "Tên Form", "Skill ID", "Cấp Pet học",
                "Ô mặc định", "Animation nguồn", "State/Clip", "Animation timing asset", "Presentation thực dùng", "Cast cue",
                "Release cue", "Impact cue", "Projectile hoặc buff", "Socket đã resolve", "Scale runtime từng pha",
                "Release ngưỡng: giây gần đúng theo clip asset", "Trạng thái");
            using var prefabs = new Csv("PHU_LUC_PREFAB", "Prefab", "GUID", "Loại", "Skill và vai trò qua reference",
                "Skill qua dependency", "Prefab nguồn variant", "Root scale", "Transform con: local và lossy prefab",
                "Particle Start Size và module", "VFX Graph và tham số expose", "Trail", "MonoBehaviour / Embedded Audio", "Trạng thái");

            foreach (var skill in definitions) {
                var profile = skill.combat;
                var presentation = skill.presentation;
                var entries = forms.SelectMany(form => form.Entries.Where(entry => entry.skill == skill).Select(entry => (form, entry))).ToArray();
                var profiles = entries.Select(item => item.entry.Presentation).Append(presentation).Where(value => value != null).Distinct().ToArray();
                var visuals = profiles.SelectMany(CueAssets).Concat(CombatAssets(profile)).ToArray();
                foreach (var visual in visuals) {
                    if (!used.TryGetValue(visual.prefab, out var owner)) used[visual.prefab] = owner = new HashSet<string>();
                    owner.Add(skill.Id + " / " + visual.phase);
                    foreach (string dependency in AssetDatabase.GetDependencies(PathOf(visual.prefab), true).Where(path => path.StartsWith(TrungRoot + "/") && path.EndsWith(".prefab"))) {
                        if (!dependencyOwners.TryGetValue(dependency, out var owners)) dependencyOwners[dependency] = owners = new HashSet<string>();
                        owners.Add(skill.Id + " / " + visual.phase + " / " + PathOf(visual.prefab));
                    }
                }
                string kind = profile == null ? "" : profile.delivery == PetSkillDelivery.SelfBuff ? "Support" :
                    profile.damageKind == CreatureDamageKind.Physical ? "Physical" : "Special (Magical)";
                var uiKind = skill.uiMetadata?.damageKind ?? SkillDamageLabel.Unspecified;
                bool conflict = profile != null && uiKind != SkillDamageLabel.Unspecified && (kind == "Support" ? uiKind != SkillDamageLabel.None :
                    kind == "Physical" ? uiKind != SkillDamageLabel.Physical : uiKind != SkillDamageLabel.Magical);
                bool projectile = profile != null && profile.delivery == PetSkillDelivery.Projectile;
                bool volley = projectile && profile.weightedWindVolley;
                bool buff = profile != null && profile.delivery == PetSkillDelivery.SelfBuff;
                string effects = Join(new[] {
                    buff ? $"Buff công +{N(profile.offenseBuffPercent)}%, {N(profile.buffDuration)}s, làm mới, không cộng dồn" : "",
                    profile != null && !buff && profile.bleedPower > 0 ? $"DoT vật lý ×{N(profile.bleedPower)}/tick; 3 tick cách 1s, làm mới theo nguồn" : ""
                });
                stats.Row(skill.Id, VietnameseUI.Skill(skill.displayName), SkillElementAssignments.Resolve(skill.Id, skill.element), skill.element,
                    kind, profile?.delivery, profile?.power, profile?.range, skill.cooldownSeconds, skill.ManaCost, skill.manaCost,
                    volley ? "2/3/4/5 đạn; Power chia cho số đạn" : profile == null ? "" : buff ? "0" : "1",
                    volley ? "Tối đa 1/đạn; có thể khác mục tiêu" : profile == null ? "" : buff ? "0" : "Tối đa 1",
                    profile?.delivery == PetSkillDelivery.Targeted ? profile.targetedImpactRadius : null,
                    projectile ? profile.projectileRadius : null, projectile ? profile.projectileSpeed : null, projectile ? profile.lifetime : null,
                    skill.criticalRateBonus, skill.criticalDamageBonus, effects,
                    volley ? "2/3/4/5 đạn: 10/40/40/10%" : profile != null && !buff && profile.bleedPower > 0 ? "Bleed khi hit hợp lệ; không roll riêng" : "",
                    profile == null ? "" : $"Auto target={profile.autoAcquireTarget}; half angle melee={N(profile.meleeHalfAngle)}°; retreat={N(profile.retreatDistance)}m @{N(profile.retreatSpeed)}m/s; preferred={N(profile.preferredDistance)}m",
                    VietnameseUI.SkillDescription(skill.description), Join(skill.uiMetadata?.effects?.Select(e => e.role + " " + e.description)),
                    Ref(skill.icon), Join(entries.Select(item => item.form.Key + "@" + item.entry.learnAtLevel)), Join(skill.legacyIds),
                    Ref(skill), Ref(profile), catalog.skills.Contains(skill), profile == null || skill.icon == null ? Missing : conflict ? Conflicting : Verified,
                    Recommendation(profile, skill));

                var activeTrung = visuals.SelectMany(item => AssetDatabase.GetDependencies(PathOf(item.prefab), true))
                    .Where(path => path.StartsWith(TrungRoot + "/") && path.EndsWith(".prefab")).Distinct().ToArray();
                var candidates = activeTrung.Length == 0
                    ? trung.Where(prefab => NameKey(prefab.name) == NameKey(skill.displayName)).ToArray() : Array.Empty<GameObject>();
                string candidateSource = candidates.Length > 0 ? "Trùng tên hiển thị, chưa nối bằng GUID" : "";
                if (activeTrung.Length == 0 && candidates.Length == 0 && AuthoringNames.TryGetValue(skill.Id, out string authoringName)) {
                    candidates = trung.Where(prefab => NameKey(Path.GetFileNameWithoutExtension(PathOf(prefab))) == NameKey(authoringName)).ToArray();
                    if (candidates.Length > 0) candidateSource = "Tên đối chiếu trong hồ sơ authoring, asset/GUID có thật, chưa nối runtime";
                }
                if (activeTrung.Length == 0 && candidates.Length == 0) {
                    var element = SkillElementAssignments.Resolve(skill.Id, skill.element);
                    string familyFolder = element == PetElement.Nature ? "Grass" : element.ToString();
                    candidates = trung.Where(prefab => PathOf(prefab).Contains("/" + familyFolder + "/")).ToArray();
                    if (candidates.Length > 0) candidateSource = "Cùng thư mục hệ, chưa nối bằng GUID";
                }
                string overrides = Join(entries.Where(item => item.entry.presentationOverride != null)
                    .Select(item => item.form.Key + " = " + Ref(item.entry.presentationOverride)));
                var defaultVisuals = CueAssets(presentation).Concat(CombatAssets(profile)).ToArray();
                vfx.Row(skill.Id, VietnameseUI.Skill(skill.displayName), Ref(skill), Ref(presentation),
                    Ref(presentation?.cast?.effectPrefab), Ref(presentation?.release?.effectPrefab), Ref(profile?.projectileVisual),
                    Join(visuals.Where(item => item.prefab.GetComponentInChildren<TrailRenderer>(true) != null)
                        .Select(item => item.phase + ": " + Ref(item.prefab)).Distinct()), Ref(presentation?.impact?.effectPrefab), Ref(profile?.buffVisual),
                    Cue(presentation?.cast), Cue(presentation?.release), Cue(presentation?.impact, true), Timing(presentation),
                    projectile ? profile.releaseSocket : "", projectile ? profile.projectileSpeed : null,
                    projectile ? "Không; hướng cố định sau release" : "Không áp dụng",
                    profile != null && profile.trackTargetUntilRelease ? "Trong chuẩn bị đến release" : "Không có cập nhật theo đích đến release",
                    Join(defaultVisuals.Select(item => item.phase + " " + V(item.prefab.transform.localScale))),
                    Join(visuals.Select(item => item.prefab).Distinct().Select(prefab => PathOf(prefab) +
                        $" ({prefab.GetComponentsInChildren<ParticleSystem>(true).Length} PS, {prefab.GetComponentsInChildren<VisualEffect>(true).Length} graph); xem PHU_LUC_PREFAB")),
                    "Cue: root prefab × max(.01, cue.scale) × Scale Form; Projectile: ghi đè root = visualScale × Scale Form; Buff: ghi đè root = clamp(body extents.x × 1.6, .75, 2), theo transform Pet",
                    Join(activeTrung.Select(path => path + " [" + AssetDatabase.AssetPathToGUID(path) + "]")), Join(candidates.Select(Ref)),
                    activeTrung.Length > 0 ? "AssetDatabase GUID/dependency" : candidateSource,
                    overrides, presentation == null ? Missing : activeTrung.Length > 0 ? Verified : candidates.Length > 1 ? Multiple : Unverified,
                    "So hướng ở thời điểm release và kích thước trên camera thật; chỉ số transform không đo kích thước render");
            }

            foreach (var info in forms) {
                var form = info.Form;
                var set = info.Set;
                var animator = info.Animator;
                var animation = info.Animation;
                var states = ReadStates(animator?.runtimeAnimatorController);
                bool issue = false;
                foreach (var entry in info.Entries) {
                    var skill = entry.skill;
                    var data = entry.CreateHudData(entry.learnAtLevel);
                    var profile = skill.combat;
                    var p = entry.Presentation;
                    var action = animation?.skills.FirstOrDefault(binding => string.Equals(binding.skillId, skill.Id, StringComparison.OrdinalIgnoreCase));
                    string actionState = action != null ? animation.actions.FirstOrDefault(binding => binding.action == action.action)?.state : null;
                    var requested = animation != null ? new[] { actionState } : data.animatorStates;
                    var found = (requested ?? Array.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => states.FirstOrDefault(state => state.name == name || state.name == "Base Layer." + name))
                        .Where(state => state.name != null).ToArray();
                    bool projectile = profile?.delivery == PetSkillDelivery.Projectile;
                    var sockets = new [] { p?.cast?.socket, p?.release?.socket, projectile ? profile.releaseSocket : null }
                        .Where(socket => !string.IsNullOrWhiteSpace(socket)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    bool missingSocket = sockets.Any(socket => info.Anchor(socket) == null);
                    issue |= found.Length == 0 || p == null || missingSocket;
                    perForm.Row(info.Family.definitionId, form.formId, form.displayName, skill.Id, entry.learnAtLevel,
                        set.defaultLoadout.IndexOf(skill) is int index && index >= 0 ? index + 1 : null,
                        Ref(animation) + (action != null ? " / " + action.action : entry.overrideAnimation ? " / Override learnset" : " / Definition"),
                        found.Length > 0 ? Join(found.Select(state => state.name + " = " + Ref(state.clip) + " @" + N(state.speed) + "x"))
                            : Join(requested) + " (chưa resolve)",
                        $"duration={N(data.animationDuration)}; fade={N(data.animationFade)}; windup={N(data.windupSeconds)}; recovery={N(data.recoverySeconds)}",
                        Ref(p), Cue(p?.cast), Cue(p?.release), Cue(p?.impact, true), Join(CombatAssets(profile).Select(value => value.phase + ": " + Ref(value.prefab))),
                        Join(sockets.Select(socket => socket + "=" + (info.Anchor(socket) is Transform anchor ? Hierarchy(anchor) : "fallback root tại runtime"))),
                        RuntimeScale(info, p, profile),
                        found.Length == 1 && found[0].clip != null && p != null && p.playbackSpeed > 0 && found[0].speed > 0
                            ? N(found[0].clip.length * p.releaseNormalized / (p.playbackSpeed * found[0].speed)) : "",
                        p == null || animator == null ? Missing : found.Length == 0 || missingSocket ? Conflicting : Verified);
                }
                formTable.Row(info.Family.definitionId, form.formId, form.displayName, form.primaryElement, form.secondaryElement,
                    Ref(set), set?.speciesId, Join(info.Entries.Select(entry => entry.skill.Id + "@" + entry.learnAtLevel)),
                    Join(set?.defaultLoadout.Where(skill => skill != null).Select(skill => skill.Id)), Ref(animation),
                    Ref(animator?.runtimeAnimatorController), Join(animation?.skills.Select(binding => binding.skillId + "=" + binding.action)),
                    Join(info.Entries.Where(entry => entry.presentationOverride != null).Select(entry => entry.skill.Id + "=" + Ref(entry.presentationOverride))),
                    Ref(form.visualPrefab), Ref(form.petPrefab), Ref(form.wildPrefab), info.Scale,
                    Join(info.Sockets.Where(socket => socket?.anchor != null).Select(socket => socket.key + "=" + Hierarchy(socket.anchor) +
                        " pos=" + V(socket.anchor.localPosition) + " rot=" + V(socket.anchor.localEulerAngles) + " scale=" + V(socket.anchor.localScale))),
                    set == null || animator == null || form.petPrefab == null ? Missing : issue ? Conflicting : Verified);
            }

            foreach (var prefab in used.Keys.Concat(trung).Distinct().GroupBy(PathOf).Select(group => group.First()).OrderBy(PathOf)) {
                string path = PathOf(prefab);
                used.TryGetValue(prefab, out var owners);
                dependencyOwners.TryGetValue(path, out var nested);
                var children = prefab.GetComponentsInChildren<Transform>(true).Where(child => child != prefab.transform);
                var behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
                prefabs.Row(path, AssetDatabase.AssetPathToGUID(path), path.StartsWith(TrungRoot + "/") ? "Trung" : "Runtime",
                    Join(owners?.OrderBy(s => s)), Join(nested?.OrderBy(s => s)), Ref(PrefabUtility.GetCorrespondingObjectFromSource(prefab)),
                    V(prefab.transform.localScale),
                    Join(children.Select(child => Hierarchy(child) + " local=" + V(child.localScale) + " lossy(prefab)=" + V(child.lossyScale))),
                    Join(prefab.GetComponentsInChildren<ParticleSystem>(true).Select(Particle)),
                    Join(prefab.GetComponentsInChildren<VisualEffect>(true).Select(Graph)),
                    Join(prefab.GetComponentsInChildren<TrailRenderer>(true).Select(trail => Hierarchy(trail.transform) +
                        $" width×{N(trail.widthMultiplier)}; time={N(trail.time)}s")),
                    Join(behaviours.Select(behaviour => behaviour == null ? "Missing Script" : behaviour.GetType().Name)
                        .Concat(prefab.GetComponentsInChildren<AudioSource>(true).Select(audio => "Audio=" + Ref(audio.clip))).Distinct()),
                    behaviours.Any(behaviour => behaviour == null) ? Conflicting : Verified);
            }
            Debug.Log($"Skill audit: {definitions.Length} definitions, {forms.Length} forms, {trung.Length} Trung prefabs, {used.Count} runtime VFX references.");
            VerifyCompatibility(definitions, forms.Length);
        }

        static IEnumerable<T> Assets<T>(string filter = null, params string[] folders) where T : Object =>
            (folders.Length == 0 ? AssetDatabase.FindAssets(filter ?? "t:" + typeof(T).Name) : AssetDatabase.FindAssets(filter, folders))
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(asset => asset != null);

        static IEnumerable<(string phase, GameObject prefab)> CueAssets(PetSkillPresentationProfile p) =>
            p == null ? Array.Empty<(string, GameObject)>() : new[] {
                (phase: "Cast", prefab: p.cast?.effectPrefab), ("Release", p.release?.effectPrefab), ("Impact", p.impact?.effectPrefab)
            }.Where(value => value.Item2 != null);
        static IEnumerable<(string phase, GameObject prefab)> CombatAssets(PetSkillCombatProfile p) =>
            p == null ? Array.Empty<(string, GameObject)>() : new[] {
                (phase: "Projectile", prefab: p.projectileVisual), ("Buff", p.buffVisual)
            }.Where(value => value.Item2 != null);

        static string Cue(PetSkillCue cue, bool impact = false) => cue == null || (cue.effectPrefab == null && cue.sound == null) ? "" :
            $"{Ref(cue.effectPrefab)}; spawn={(impact ? "điểm impact, rotation từ normal" : string.IsNullOrEmpty(cue.socket) ? "root" : cue.socket)}; " +
            $"offset={V(cue.localOffset)}; rot={V(cue.localEulerAngles)}°; actorSpace={cue.actorSpace}; " +
            $"followSocket={(impact ? false : cue.followSocket)}; scale={N(cue.scale)}; life={N(cue.lifetimeSeconds)}s; " +
            $"SFX={Ref(cue.sound)}; volume={N(cue.volume)}; pitch={N(cue.pitch)}";
        static string Timing(PetSkillPresentationProfile p) => p == null ? "" :
            $"{p.releaseMode}; normalized={N(p.releaseNormalized)}; delay Timed={N(p.releaseDelaySeconds)}s;" +
            $" chain={N(p.chainNormalized)}; playbackSpeed={N(p.playbackSpeed)}; impactWindow={N(p.impactWindowSeconds)}s";
        static string RuntimeScale(FormInfo form, PetSkillPresentationProfile p, PetSkillCombatProfile profile) {
            string Scale(PetSkillCue cue) => cue?.effectPrefab != null
                ? V(cue.effectPrefab.transform.localScale * (Mathf.Max(.01f, cue.scale) * form.Scale)) : "";
            return Join(new[] {
                "Cast=" + Scale(p?.cast), "Release=" + Scale(p?.release), "Impact=" + Scale(p?.impact),
                profile?.projectileVisual != null ? "Projectile=" + V(Vector3.one * profile.visualScale * form.Scale) : "",
                profile?.buffVisual != null ? "Buff=clamp(body.bounds.extents.x×1.6,.75,2) × parent Pet" : ""
            });
        }

        static List<(string name, AnimationClip clip, float speed)> ReadStates(RuntimeAnimatorController controller) {
            var result = new List<(string, AnimationClip, float)>();
            var overrides = controller as AnimatorOverrideController;
            if ((overrides != null ? overrides.runtimeAnimatorController : controller) is not AnimatorController animator) return result;
            void Collect(AnimatorStateMachine machine, string prefix) {
                foreach (var entry in machine.states) {
                    var clip = entry.state.motion as AnimationClip;
                    if (clip != null && overrides != null) clip = overrides[clip] ?? clip;
                    result.Add((prefix + "." + entry.state.name, clip, entry.state.speed));
                }
                foreach (var child in machine.stateMachines) Collect(child.stateMachine, prefix + "." + child.stateMachine.name);
            }
            if (animator.layers.Length != 0) Collect(animator.layers[0].stateMachine, animator.layers[0].name);
            return result;
        }

        static string Particle(ParticleSystem particle) {
            var m = particle.main;
            var shape = particle.shape;
            return $"{Hierarchy(particle.transform)}: mode={m.scalingMode}; sim={m.simulationSpace}; size=" +
                (m.startSize3D ? $"XYZ({Curve(m.startSizeX)};{Curve(m.startSizeY)};{Curve(m.startSizeZ)})" : Curve(m.startSize)) +
                $"; shape={(shape.enabled ? shape.shapeType + " scale=" + V(shape.scale) + " radius=" + N(shape.radius) : "off")};" +
                $" life={Curve(m.startLifetime)}; speed={Curve(m.startSpeed)}; delay={Curve(m.startDelay)}; duration={N(m.duration)};" +
                $" loop={m.loop}; sizeOverLifetime={particle.sizeOverLifetime.enabled}; sizeBySpeed={particle.sizeBySpeed.enabled}; maxParticles={m.maxParticles}";
        }
        static string Curve(ParticleSystem.MinMaxCurve curve) {
            string Keys(AnimationCurve data) => data == null ? "" : string.Join("/", data.keys.Select(key => N(key.time) + ":" + N(key.value)));
            return curve.mode switch {
                ParticleSystemCurveMode.Constant => N(curve.constant),
                ParticleSystemCurveMode.TwoConstants => N(curve.constantMin) + "–" + N(curve.constantMax),
                ParticleSystemCurveMode.Curve => $"Curve×{N(curve.curveMultiplier)} [{Keys(curve.curve)}]",
                _ => $"TwoCurves×{N(curve.curveMultiplier)} [{Keys(curve.curveMin)}|{Keys(curve.curveMax)}]"
            };
        }
        static string Graph(VisualEffect effect) {
            if (effect.visualEffectAsset == null) return Hierarchy(effect.transform) + " " + Missing;
            var properties = new List<VFXExposedProperty>();
            effect.visualEffectAsset.GetExposedProperties(properties);
            string Value(VFXExposedProperty property) {
                string name = property.name;
                if (property.type == typeof(float) && effect.HasFloat(name)) return N(effect.GetFloat(name));
                if (property.type == typeof(Vector2) && effect.HasVector2(name)) return effect.GetVector2(name).ToString();
                if (property.type == typeof(Vector3) && effect.HasVector3(name)) return V(effect.GetVector3(name));
                if (property.type == typeof(Vector4) && effect.HasVector4(name)) return effect.GetVector4(name).ToString();
                return "(" + property.type.Name + ")";
            }
            return $"{Hierarchy(effect.transform)}: {Ref(effect.visualEffectAsset)}; exposed=" +
                Join(properties.Select(property => property.name + "=" + Value(property))) +
                "; node size nội bộ/chuyển động render: Chưa xác minh";
        }

        static string Recommendation(PetSkillCombatProfile profile, PetSkillDefinition skill) {
            if (profile == null) return "Xác minh profile.";
            if (profile.delivery == PetSkillDelivery.SelfBuff) return "So uptime buff với hồi chiêu và MP.";
            if (profile.weightedWindVolley) return "So Power tổng/loạt, số hit thực tế và độ phân tán.";
            if (profile.delivery == PetSkillDelivery.Melee) return "Test tầm và góc với hai cỡ hitbox, đối chiếu Physical/Special trên cùng bộ Stats.";
            if (profile.delivery == PetSkillDelivery.Targeted) return "Đo khả năng né theo bán kính và thời gian báo trước; executor hiện chỉ xác nhận một mục tiêu.";
            return "Đo thời gian bay range/speed và độ lệch socket; so Power mỗi giây và mỗi MP.";
        }

        static void VerifyCompatibility(PetSkillDefinition[] definitions, int forms) {
            Directory.CreateDirectory(VerificationFolder);
            var report = new List<string>();
            void Check(bool pass, string label) {
                report.Add((pass ? "PASS " : "FAIL ") + label);
                File.WriteAllLines(Path.Combine(VerificationFolder, "editor.txt"), report);
                if (!pass) throw new InvalidOperationException(label);
            }
            Check(definitions.Length == 27 && definitions.Select(value => value.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 27 && forms == 33,
                "27 unique skill IDs and 33 library forms");
            Check(definitions.Any(definition => definition.combat?.damageKind == CreatureDamageKind.Physical) &&
                definitions.Any(definition => definition.combat?.damageKind == CreatureDamageKind.Magical),
                "Physical and Special authored per skill");
            var parent = new GameObject("Skill audit save fixture") { hideFlags = HideFlags.HideAndDontSave };
            try {
                var set = Assets<PetSpeciesSkillSet>().First(value => value.learnset.Count > 1);
                var stats = parent.AddComponent<PetHudRuntimeStats>();
                stats.ConfigureSkillSet(set, false);
                var preview = set.learnset.First(entry => entry.learnAtLevel > 1);
                stats.SetIdentity("Audit fixture", preview.learnAtLevel);
                stats.UnlockSkillsUpToLevel(preview.learnAtLevel, out _);
                Check(stats.GetLearnedSkills().Any(value => value.skillId == preview.skill.Id), "pet level grants a learned skill");
                var save = stats.CreateSaveData();
                var progress = save.learnedSkillProgress.First(value => value.skillId == preview.skill.Id);
                int progressIndex = save.learnedSkillProgress.IndexOf(progress);
                var legacy = preview.skill.legacyIds.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id) && set.TryGet(id, out var entry) && entry.skill == preview.skill);
                string savedId = legacy ?? preview.skill.Id;
                progress.skillId = savedId;
                save.equippedSkillSlotCount = 4;
                save.equippedSkillIds = new List<string> { "", savedId, "", "" };
                string json = JsonUtility.ToJson(save).Replace(JsonUtility.ToJson(progress),
                    JsonUtility.ToJson(progress).Insert(1, "\"level\":17,\"exp\":941,"));
                var read = JsonUtility.FromJson<PetRuntimeStatsSaveData>(json);
                Check(read.learnedSkillProgress[progressIndex].unlocked, "old skill-level/EXP fields do not affect the saved learn flag");
                stats.RestoreFromSaveData(read);
                var restored = stats.CreateSaveData();
                Check(restored.level == save.level && restored.equippedSkillIds.SequenceEqual(new[] { "", preview.skill.Id, "", "" }),
                    "pet level plus four ordered slots/empty slots survive old save");
                Check(restored.learnedSkillProgress.Any(value => value.skillId == preview.skill.Id && value.unlocked)
                    && restored.learnedSkillProgress.All(value => !JsonUtility.ToJson(value).Contains("\"level\"")),
                    "new save preserves learn flag and omits skill level");
                var hud = stats.GetSkills()[1];
                Check(hud.combatProfile == preview.skill.combat && Mathf.Approximately(hud.manaCost, preview.skill.ManaCost)
                    && Mathf.Approximately(hud.cooldownSeconds, preview.skill.cooldownSeconds),
                    "HUD keeps authored combat profile, mana and cooldown");
            } finally { Object.DestroyImmediate(parent); }
        }

        sealed class FormInfo {
            public readonly CreatureFamilyDefinition Family;
            public readonly CreatureFormDefinition Form;
            readonly CreatureVisualBinding binding;
            readonly PetSkillPresentationPlayer player;
            readonly PetHudRuntimeStats stats;
            public readonly Animator Animator;
            public string Key => Family.definitionId + "/" + Form.formId;
            public PetSpeciesSkillSet Set => binding?.skillSet ?? stats?.SpeciesSkillSet ?? Form.skillSet;
            public CreatureAnimationProfile Animation => binding?.animationProfile ?? Form.animationProfile;
            public float Scale => binding == null ? 1f : Mathf.Clamp(binding.presentationScale, .25f, 3f);
            public IEnumerable<PetSkillSocket> Sockets => binding?.sockets ?? player?.sockets ?? Enumerable.Empty<PetSkillSocket>();
            public IEnumerable<PetSpeciesSkillEntry> Entries => Set != null
                ? Set.learnset.Where(value => value?.skill != null) : Enumerable.Empty<PetSpeciesSkillEntry>();
            public Transform Anchor(string key) => Sockets.FirstOrDefault(socket => socket != null && socket.anchor != null
                && string.Equals(socket.key, key, StringComparison.OrdinalIgnoreCase))?.anchor;
            public FormInfo(CreatureFamilyDefinition family, CreatureFormDefinition form) {
                Family = family;
                Form = form;
                var actor = form.petPrefab ?? form.wildPrefab ?? form.sourcePrefab;
                var visual = form.visualPrefab ?? actor;
                binding = visual?.GetComponentInChildren<CreatureVisualBinding>(true);
                player = actor?.GetComponentInChildren<PetSkillPresentationPlayer>(true);
                stats = actor?.GetComponentInChildren<PetHudRuntimeStats>(true);
                Animator = binding != null ? binding.GetComponent<Animator>() : visual?.GetComponentInChildren<Animator>(true);
            }
        }

        static string Ref(Object asset) {
            string path = PathOf(asset);
            return path.Length == 0 ? "" : path + " [" + AssetDatabase.AssetPathToGUID(path) + "]" +
                (AssetDatabase.IsSubAsset(asset) ? "/" + asset.name : "");
        }
        static string PathOf(Object asset) => asset != null ? AssetDatabase.GetAssetPath(asset) : "";
        static string V(Vector3 vector) => $"({N(vector.x)}, {N(vector.y)}, {N(vector.z)})";
        static string N(float number) => number.ToString("0.#####", Invariant);
        static string Join(IEnumerable<string> values) => values == null ? "" : string.Join(" | ", values.Where(value => !string.IsNullOrEmpty(value)));
        static string Hierarchy(Transform child) => child.parent == null ? child.name : Hierarchy(child.parent) + "/" + child.name;
        static string NameKey(string value) =>
            string.Concat(value.Normalize(NormalizationForm.FormD).Where(character => CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)).Replace('đ', 'd').Replace('Đ', 'D').ToUpperInvariant();

        sealed class Csv : IDisposable {
            readonly StreamWriter writer;
            readonly int columns;
            public Csv(string fileName, params string[] header) {
                columns = header.Length;
                writer = new StreamWriter(Path.Combine(ReportFolder, fileName + ".csv"), false, new UTF8Encoding(true));
                Row(header);
            }
            public void Row(params object[] values) =>
                writer.WriteLine(string.Join(",", Enumerable.Range(0, columns).Select(index => Quote(index < values.Length ? values[index] : null))));
            static string Quote(object value) => "\"" + (Convert.ToString(value, Invariant) ?? "").Replace("\"", "\"\"") + "\"";
            public void Dispose() => writer.Dispose();
        }
    }
}

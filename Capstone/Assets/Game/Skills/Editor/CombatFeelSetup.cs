using System;
using System.IO;
using Capstone.Game.CreatureSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.Skills.Editor {
    /// <summary>Idempotent, native asset authoring. Never edits vendor assets, scenes or saves.</summary>
    public static class CombatFeelSetup {
        public const string Root = "Assets/Game/Skills/Data/CombatFeel/";
        public const string Qa = UISystem.QaArtifactPaths.Root + "combat-feel-playtest-2026-11-08/";
        [MenuItem("Tools/Capstone/Creatures/14. Apply combat feel and skill balance")]
        public static void Apply() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dừng Play Mode trước.");
            Folder(Root.TrimEnd('/'));
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Skills/Data/Tactics/WindBuff.mat");
            if (original == null) throw new InvalidOperationException("Thiếu material kỹ năng hiện có.");
            string matPath = Root + "CombatGlow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null) { material = new Material(original); AssetDatabase.CreateAsset(material, matPath); }
            material.SetColor("_BaseColor", Color.white); Save(material);
            var bite = Effect("BiteRelease", material, 0);
            var biteHit = Effect("BiteImpact", material, 1);
            var charge = Effect("EnergyCharge", material, 2);
            var orb = Effect("EnergyOrb", material, 3);
            var energyHit = Effect("EnergyImpact", material, 4);
            var wind = Effect("WindBlade", material, 5);
            var windHit = Effect("WindImpact", material, 6);
            var roar = Effect("RoarRelease", material, 7);
            var aura = Effect("RoarAura", material, 8);
            var bleed = Effect("BleedTick", material, 9);
            string[] names = { "bite", "blast", "projectile", "wing_strike" };
            float[] cooldown = { 2, 10, 3, 4.2f }, power = { 1.2f, 1, 1.6f, 1.8f }, marker = { .36f, .43f, .30f, .45f };
            for (int i = 0; i < names.Length; i++) {
                var definition = AssetDatabase.LoadAssetAtPath<PetSkillDefinition>("Assets/Game/Skills/Data/Definitions/Skill_" + names[i] + ".asset");
                if (definition == null || definition.combat == null || definition.presentation == null) throw new InvalidOperationException("Thiếu skill " + names[i]);
                definition.cooldownSeconds = cooldown[i]; definition.animationFade = .09f; definition.windupSeconds = definition.recoverySeconds = 0;
                definition.combat.power = power[i]; definition.combat.bleedPower = i == 0 ? .1f : 0;
                var p = definition.presentation;
                p.releaseMode = PetSkillReleaseMode.NormalizedAnimation; p.releaseNormalized = marker[i]; p.chainNormalized = i == 3 ? .9f : .85f;
                p.playbackSpeed = i == 1 ? 1.25f : 1; p.impactWindowSeconds = 4;
                p.cast = Cue(i == 2 ? charge : null, i == 2 ? "Mouth" : "Body", true, i == 2 ? 1.2f : .8f, .3f);
                p.release = Cue(i == 0 ? bite : i == 1 ? roar : null, i == 1 || i == 3 ? "Body" : "Mouth", i == 0, i == 1 ? 1.6f : .3f, 1);
                p.release.sound = Audio(i == 1 ? "shout_frostbreath_006" : i == 3 ? "Wind" : "Cry2");
                p.release.volume = i == 1 ? .38f : .2f; p.release.pitch = i == 1 ? .8f : 1;
                if (i == 0) p.release.localOffset = new Vector3(0, 0, .4f);
                p.release.actorSpace = true;
                p.impact = Cue(i == 0 ? biteHit : i == 2 ? energyHit : i == 3 ? windHit : null, "", false, .3f, 1);
                p.impact.sound = i == 1 ? null : Audio(i == 2 ? "LightningImpact1" : "RockImpact2"); p.impact.volume = .18f;
                if (i == 1) {
                    definition.combat.delivery = PetSkillDelivery.SelfBuff; definition.combat.offenseBuffPercent = 20;
                    definition.combat.buffDuration = 6; definition.combat.buffVisual = aura;
                    definition.description = "Gầm lên, tăng 20% ATK và SP.ATK trong 6 giây. Không cộng dồn. Hồi chiêu 10 giây.";
                } else if (i == 0) {
                    definition.description = "Cắn gây 120% ATK sát thương vật lý. Gây chảy máu 10% ATK mỗi giây trong 3 giây; cùng nguồn làm mới, không cộng dồn.";
                } else {
                    definition.combat.projectileVisual = i == 2 ? orb : wind; definition.combat.visualScale = 1;
                    definition.combat.projectileSpeed = i == 2 ? 12 : 16;
                    definition.combat.retreatDistance = 2.4f; definition.combat.retreatSpeed = 12; definition.combat.preferredDistance = 5;
                    definition.description = i == 2 ? "Phóng đạn gây 160% SP.ATK sát thương phép. Lùi nhanh khi địch áp sát; đạn dừng ở mục tiêu đầu tiên hoặc vật cản."
                        : "Vỗ cánh phóng lưỡi gió gây 180% ATK sát thương vật lý. Lùi nhanh khi địch áp sát; một mục tiêu mỗi đòn.";
                }
                definition.uiMetadata.effects.Clear();
                definition.uiMetadata.effects.Add(new SkillEffectDescription { role = i == 1 ? SkillEffectLabel.Buff : SkillEffectLabel.Damage, description = definition.description });
                if (i == 0) definition.uiMetadata.effects.Add(new SkillEffectDescription { role = SkillEffectLabel.Dot, description = "Chảy máu 10% ATK × 3 nhịp; không chí mạng." });
                Save(definition); Save(definition.combat); Save(p);
            }
            for (int form = 1; form <= 3; form++) {
                var set = AssetDatabase.LoadAssetAtPath<PetSpeciesSkillSet>("Assets/Game/Skills/Data/Species/Dragon" + form.ToString("00") + ".asset");
                foreach (var item in set.learnset) { item.windupSeconds = item.recoverySeconds = 0; item.animationFade = .09f; }
                set.Invalidate(); Save(set); ConfigureController(form);
            }
            Folder("Assets/Game/CreatureSystem/Resources");
            const string settingsPath = "Assets/Game/CreatureSystem/Resources/CombatFeelSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<CombatFeelSettings>(settingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<CombatFeelSettings>(); AssetDatabase.CreateAsset(settings, settingsPath); }
            settings.basicImpact = biteHit; settings.bleedImpact = bleed;
            settings.basicSound = Audio("RockImpact2"); settings.bleedSound = Audio("RockImpact"); Save(settings);
            Debug.Log("Combat feel: ATK baseline, 120/160/180% skills, three wound ticks, normalized releases, runtime controllers. Scenes/saves unchanged.");
        }
        static void ConfigureController(int form) {
            string visualPath = "Assets/Game/CreatureSystem/Runtime/Dragons/Visual/Visual_Dragon" + form.ToString("00") + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(visualPath);
            try {
                var animator = root.GetComponentInChildren<Animator>(true);
                string current = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                if (form > 1 && current.Contains("/UnityAssset/")) {
                    string destination = "Assets/Game/CreatureSystem/Runtime/Dragons/Animation/Dragon" + form.ToString("00") + ".controller";
                    if (AssetDatabase.LoadAssetAtPath<AnimatorController>(destination) == null && !AssetDatabase.CopyAsset(current, destination)) throw new IOException("Không tạo được controller runtime.");
                    animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(destination);
                }
                var controller = animator.runtimeAnimatorController as AnimatorController;
                if (controller == null || AssetDatabase.GetAssetPath(controller).Contains("/UnityAssset/")) throw new InvalidOperationException("Không sửa controller nguồn.");
                bool parameter = false;
                foreach (var value in controller.parameters) if (value.name == "CombatSpeed") parameter = true;
                if (!parameter) controller.AddParameter(new AnimatorControllerParameter { name = "CombatSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
                ConfigureStates(controller.layers[0].stateMachine); Save(controller);
                PrefabUtility.SaveAsPrefabAsset(root, visualPath);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void ConfigureStates(AnimatorStateMachine machine) {
            foreach (var child in machine.states) {
                var state = child.state;
                if (state.name == "Bite Attack" || state.name == "Blast Attack" || state.name == "Projectile Attack" || state.name == "Wing Attack") {
                    state.speedParameter = "CombatSpeed"; state.speedParameterActive = true; EditorUtility.SetDirty(state);
                }
            }
            foreach (var child in machine.stateMachines) ConfigureStates(child.stateMachine);
        }
        static PetSkillCue Cue(GameObject effect, string socket, bool follow, float lifetime, float scale) => new PetSkillCue {
            effectPrefab = effect, socket = socket, followSocket = follow, lifetimeSeconds = lifetime, scale = scale, actorSpace = true
        };
        static GameObject Effect(string name, Material material, int style) {
            var root = new GameObject(name);
            try {
                Color violet = new Color(.76f, .43f, 1, .8f), cyan = new Color(.48f, .86f, 1, .85f), gold = new Color(1, .86f, .55f, .9f), red = new Color(.85f, .23f, .35f, .8f);
                Color color = style == 9 ? red : style == 5 || style == 6 ? cyan : violet;
                bool loop = style == 2 || style == 3 || style == 5 || style == 8;
                var particles = root.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main; main.loop = loop; main.playOnAwake = false; main.duration = loop ? 1 : .3f;
                main.startLifetime = loop ? .25f : .2f; main.startSpeed = style == 7 ? 2 : style == 2 || style == 3 ? .15f : .7f;
                main.startSize = style == 3 ? .24f : style == 2 ? .16f : .055f; main.maxParticles = 48; main.startColor = color;
                var emission = particles.emission; emission.rateOverTime = loop ? 16 : 0;
                if (!loop) emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)(style == 9 ? 5 : 12)) });
                var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = style == 8 ? .6f : style == 7 ? .4f : .12f;
                var size = particles.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
                var life = particles.colorOverLifetime; life.enabled = true;
                var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(0, 1) }); life.color = gradient;
                particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                if (style == 0) {
                    for (int jaw = 0; jaw < 2; jaw++) {
                        var line = Line(root, material, jaw == 0 ? gold : violet, 21, .055f);
                        for (int i = 0; i < 21; i++) { float a = Mathf.PI * i / 20; line.SetPosition(i, new Vector3(Mathf.Cos(a) * .35f, (jaw == 0 ? 1 : -1) * Mathf.Sin(a) * .22f, 0)); }
                    }
                    for (int side = -1; side <= 1; side += 2) {
                        var line = Line(root, material, gold, 3, .045f);
                        line.SetPositions(new[] { new Vector3(side * .18f - .05f, .1f, 0), new Vector3(side * .18f, -.11f, 0), new Vector3(side * .18f + .05f, .1f, 0) });
                    }
                }
                if (style == 5 || style == 6 || style == 7 || style == 8) {
                    int layers = style == 5 || style == 8 ? 2 : 1;
                    for (int layer = 0; layer < layers; layer++) {
                        bool ring = style == 7 || style == 8;
                        var line = Line(root, material, layer == 1 ? gold : color, ring ? 49 : 29, style == 5 ? .075f : .035f);
                        for (int p = 0; p < line.positionCount; p++) {
                            float t = p / (float)(line.positionCount - 1), angle = (ring ? t * 360 : -75 + t * 150) * Mathf.Deg2Rad;
                            float radius = ring ? .6f + layer * .16f : .65f + layer * .1f;
                            line.SetPosition(p, ring ? new Vector3(Mathf.Cos(angle) * radius, .1f + layer * .25f, Mathf.Sin(angle) * radius)
                                : new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius - .3f, layer * .1f));
                        }
                        if (style == 5 || style == 8) line.gameObject.AddComponent<PetSkillEffectMotion>().degreesPerSecond = style == 5 ? new Vector3(0, 0, (layer == 0 ? 1 : -1) * 90) : new Vector3(0, (layer == 0 ? 1 : -1) * 60, 0);
                    }
                }
                if (style == 3 || style == 5) {
                    var trail = root.AddComponent<TrailRenderer>(); trail.sharedMaterial = material; trail.time = .14f; trail.minVertexDistance = .08f;
                    trail.widthMultiplier = style == 3 ? .14f : .08f; trail.startColor = color; trail.endColor = new Color(color.r, color.g, color.b, 0);
                }
                var envelope = root.AddComponent<CombatFxEnvelope>(); envelope.loop = loop; envelope.duration = loop ? 1 : .3f;
                return PrefabUtility.SaveAsPrefabAsset(root, Root + name + ".prefab");
            } finally { Object.DestroyImmediate(root); }
        }
        static LineRenderer Line(GameObject root, Material material, Color color, int count, float width) {
            var line = new GameObject("Ribbon").AddComponent<LineRenderer>(); line.transform.SetParent(root.transform, false);
            line.useWorldSpace = false; line.sharedMaterial = material; line.positionCount = count; line.widthMultiplier = width;
            line.startColor = line.endColor = color; line.widthCurve = new AnimationCurve(new Keyframe(0, .1f), new Keyframe(.5f, 1), new Keyframe(1, .1f)); return line;
        }
        static AudioClip Audio(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/UnityAssset/Trung/KriptoFX/Realistic Effects Pack v4/Effects/Audio/" + name + ".wav");
        static void Save(Object asset) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
        static void Folder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    }
}

using System;
using System.IO;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Capstone.Game.Skills.Editor {
    /// <summary>Edits the existing skill assets in place; never rebuilds source models or individuals.</summary>
    public static class DragonSkillTacticsSetup {
        public const string Root = "Assets/Game/Skills/Data/Tactics/";
        const string Definitions = "Assets/Game/Skills/Data/Definitions/Skill_";
        [MenuItem("Tools/Capstone/Creatures/12. Apply dragon wind, roar buff and retreat")]
        public static void Build() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dừng Play Mode trước khi cấu hình.");
            Folder(Root.TrimEnd('/'));
            Material material = MaterialAsset();
            GameObject wind = Effect("WindSlash", material, false), aura = Effect("RoarBuffAura", material, true);
            var wing = AssetDatabase.LoadAssetAtPath<PetSkillDefinition>(Definitions + "wing_strike.asset");
            var blast = AssetDatabase.LoadAssetAtPath<PetSkillDefinition>(Definitions + "blast.asset");
            var projectile = AssetDatabase.LoadAssetAtPath<PetSkillDefinition>(Definitions + "projectile.asset");
            if (wing == null || blast == null || projectile == null) throw new InvalidOperationException("Thiếu định nghĩa kỹ năng gốc.");
            wing.displayName = "Chém gió"; wing.description = "Vỗ cánh phóng lưỡi gió vào mục tiêu đã khóa. Lùi nhanh khi địch áp sát.";
            wing.animationDuration = .667f; wing.windupSeconds = .1f; wing.recoverySeconds = .2f;
            var w = wing.combat; w.delivery = PetSkillDelivery.Projectile; w.damageKind = CreatureSystem.CreatureDamageKind.Physical;
            w.power = 1.2f; w.range = 10; w.projectileSpeed = 16; w.projectileRadius = .38f; w.lifetime = 1.2f;
            w.projectileVisual = wind; w.visualScale = 1; w.releaseSocket = "Body";
            w.retreatDistance = 2.4f; w.retreatSpeed = 12; w.preferredDistance = 5;
            var wp = wing.presentation; wp.releaseDelaySeconds = .3f;
            wp.cast = new PetSkillCue { socket = "Body", followSocket = true, lifetimeSeconds = .4f };
            wp.release = new PetSkillCue { socket = "Body", sound = Audio("Wind"), volume = .35f, pitch = 1.3f, lifetimeSeconds = .7f };
            // Keep the existing impact art but make the slash readable instead of an oversized burst.
            wp.impact.scale = .18f; wp.impact.volume = .2f;
            wing.uiMetadata.damageKind = SkillDamageLabel.Physical;
            wing.uiMetadata.effects.Clear(); wing.uiMetadata.effects.Add(new SkillEffectDescription { role = SkillEffectLabel.Damage, description = "Lưỡi gió bay, một hit vật lý khi trúng." });
            blast.displayName = "Gầm cường hóa"; blast.description = "Gầm lên, tăng 20% ATK và SP.ATK trong 6 giây. Dùng lại làm mới thời gian, không cộng dồn. Không gây sát thương.";
            blast.animationDuration = 1.5f; blast.windupSeconds = .1f; blast.recoverySeconds = .2f;
            var b = blast.combat; b.delivery = PetSkillDelivery.SelfBuff; b.offenseBuffPercent = 20; b.buffDuration = 6; b.buffVisual = aura; b.retreatDistance = 0;
            var bp = blast.presentation; bp.releaseDelaySeconds = .65f;
            bp.cast = new PetSkillCue { socket = "Body", followSocket = true, lifetimeSeconds = .8f };
            bp.release = new PetSkillCue { socket = "Mouth", sound = Audio("shout_frostbreath_006"), volume = .5f, pitch = .75f, lifetimeSeconds = 2.5f };
            bp.impact = new PetSkillCue { lifetimeSeconds = .1f };
            blast.uiMetadata.damageKind = SkillDamageLabel.None; blast.uiMetadata.effects.Clear();
            blast.uiMetadata.effects.Add(new SkillEffectDescription { role = SkillEffectLabel.Buff, description = "ATK và SP.ATK +20% trong 6 giây; làm mới, không cộng dồn." });
            projectile.combat.retreatDistance = 2.4f; projectile.combat.retreatSpeed = 12; projectile.combat.preferredDistance = 5;
            foreach (var asset in new Object[] { wing, w, wp, blast, b, bp, projectile.combat }) Save(asset);
            for (int form = 1; form <= 3; form++) {
                var set = AssetDatabase.LoadAssetAtPath<PetSpeciesSkillSet>("Assets/Game/Skills/Data/Species/Dragon" + form.ToString("00") + ".asset");
                if (set == null) throw new InvalidOperationException("Thiếu learnset rồng " + form);
                if (set.TryGet("blast", out var entry)) { entry.animationDuration = 1.5f; entry.recoverySeconds = .2f; }
                if (set.TryGet("wing strike", out entry)) { entry.animationDuration = .667f; entry.recoverySeconds = .2f; }
                set.Invalidate(); Save(set);
            }
            Debug.Log("Dragon skills: wind slash, 20% six-second roar buff, bounded fast retreat. IDs/save unchanged.");
        }
        static Material MaterialAsset() {
            string path = Root + "WindBuff.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) throw new InvalidOperationException("Thiếu shader URP Particles/Unlit.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            string texturePath = Root + "SoftParticle.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) {
                texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "SoftParticle", wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[64 * 64];
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) {
                    float a = Mathf.Clamp01(1 - new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude);
                    pixels[y * 64 + x] = new Color(1, 1, 1, a * a);
                }
                texture.SetPixels(pixels); texture.Apply(false); AssetDatabase.CreateAsset(texture, texturePath);
            }
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 2);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", 0); material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000; Save(material); return material;
        }
        static GameObject Effect(string name, Material material, bool aura) {
            var root = new GameObject(name);
            try {
                for (int layer = 0; layer < (aura ? 2 : 3); layer++) {
                    var ring = new GameObject("Ribbon" + layer); ring.transform.SetParent(root.transform, false);
                    ring.transform.localPosition = new Vector3(0, aura ? layer * .3f : 0, aura ? 0 : layer * -.12f);
                    if (!aura) ring.transform.localRotation = Quaternion.Euler(0, layer == 1 ? 65 : layer == 2 ? -65 : 0, 0);
                    var line = ring.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.sharedMaterial = material;
                    line.positionCount = aura ? 49 : 25; line.widthMultiplier = aura ? .055f : .14f - layer * .03f;
                    Color color = aura ? (layer == 0 ? new Color(1.2f, .45f, 1.6f, .85f) : new Color(1.4f, 1, .35f, .8f)) : new Color(.65f, 1.5f, 1.3f, 1);
                    line.startColor = color; line.endColor = color; line.widthCurve = aura ? AnimationCurve.Linear(0, 1, 1, 1) : new AnimationCurve(new Keyframe(0, 0), new Keyframe(.5f, 1), new Keyframe(1, 0));
                    for (int p = 0; p < line.positionCount; p++) {
                        float t = p / (float)(line.positionCount - 1), angle = (aura ? t * 360 : -70 + t * 140) * Mathf.Deg2Rad;
                        float r = aura ? .75f + layer * .16f : .8f + layer * .16f;
                        line.SetPosition(p, aura ? new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle * 3) * .07f, Mathf.Sin(angle) * r) : new Vector3(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r - .4f, 0));
                    }
                    if (aura) ring.AddComponent<PetSkillEffectMotion>().degreesPerSecond = new Vector3(0, layer == 0 ? 90 : -60, 0);
                }
                var particles = root.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main; main.loop = true; main.duration = 1; main.startLifetime = aura ? .8f : .2f; main.startSpeed = aura ? .8f : .2f;
                main.startSize = aura ? .09f : .1f; main.maxParticles = 32; main.startColor = aura ? new Color(.8f, .55f, 1, .7f) : new Color(.5f, 1, .9f, .6f);
                var emission = particles.emission; emission.rateOverTime = aura ? 12 : 18;
                var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = aura ? .6f : .4f;
                var colorLife = particles.colorOverLifetime; colorLife.enabled = true;
                var gradient = new Gradient(); gradient.SetKeys(new[] {new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1)}, new[] {new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(0, 1)}); colorLife.color = gradient;
                particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(root, Root + name + ".prefab");
            } finally { Object.DestroyImmediate(root); }
        }
        static AudioClip Audio(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/UnityAssset/Trung/KriptoFX/Realistic Effects Pack v4/Effects/Audio/" + name + ".wav");
        static void Save(Object asset) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
        static void Folder(string path) { string parent = Path.GetDirectoryName(path).Replace('\\', '/'); if (!AssetDatabase.IsValidFolder(parent)) Folder(parent); if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    }
}

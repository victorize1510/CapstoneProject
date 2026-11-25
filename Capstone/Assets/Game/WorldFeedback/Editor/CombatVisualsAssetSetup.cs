using System;
using System.IO;
using System.Text;
using Capstone.Game.CreatureSystem;
using Capstone.Game.Skills;
using UnityEditor;
using UnityEngine;

namespace Capstone.Game.WorldFeedback.Editor {
    /// <summary>Scoped presentation assets only; no scene, save, source controller or gameplay balance writes.</summary>
    public static class CombatVisualsAssetSetup {
        const string Art = "Assets/Game/WorldFeedback/Resources/WorldFeedback/";
        [MenuItem("Tools/Capstone/Advanced/Combat visuals/Apply presentation assets")]
        public static void Apply() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var report = new StringBuilder("Presentation asset setup\n");
            foreach (string id in new[] { "bite", "blast", "projectile", "wing_strike" }) {
                string path = Art + "Icons/Skill_" + id + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                var definition = AssetDatabase.LoadAssetAtPath<PetSkillDefinition>("Assets/Game/Skills/Data/Definitions/Skill_" + id + ".asset");
                definition.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                report.AppendLine(id + " -> " + path + "; semantic icon; alpha; no gameplay fields changed");
            }
            string texPath = Art + "CombatSoftDisc.png";
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) {
                float radius = new Vector2((x + .5f - 32) / 31, (y + .5f - 32) / 31).magnitude;
                pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius * radius), 2));
            }
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(texPath, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
            const string sourceMat = "Assets/Game/Skills/Data/CombatFeel/CombatGlow.mat", newMat = "Assets/Game/Skills/Data/CombatFeel/CombatSoftParticle.mat";
            var particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(newMat);
            if (particleMaterial == null) { particleMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>(sourceMat)); AssetDatabase.CreateAsset(particleMaterial, newMat); }
            particleMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            EditorUtility.SetDirty(particleMaterial); AssetDatabase.SaveAssetIfDirty(particleMaterial);
            foreach (string path in Directory.GetFiles("Assets/Game/Skills/Data/CombatFeel", "*.prefab")) {
                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    foreach (var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true)) renderer.sharedMaterial = particleMaterial;
                    // Ribbons/trails retain their existing untextured material.
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            for (int form = 1; form <= 3; form++) {
                string path = "Assets/Game/CreatureSystem/Runtime/Dragons/Visual/Visual_Dragon" + form.ToString("00") + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var binding = root.GetComponentInChildren<CreatureVisualBinding>(true);
                    if (binding == null) throw new InvalidOperationException("Missing visual binding: " + path);
                    binding.presentationScale = form == 1 ? 1 : form == 2 ? 1.15f : 1.35f;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine("form " + form + " visual scale=" + binding.presentationScale + " sockets=" + binding.sockets.Count);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            CombatVisualsVerification.Write("asset-setup.txt", report.ToString());
        }
    }
}

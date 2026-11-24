using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor
{
    static class CartoonHitSetup
    {
        const string Source = "Assets/UnityAssset/Trung/JMO Assets/Cartoon FX (legacy)/CFX Prefabs/Hits/";
        const string Effects = "Assets/Game/Skills/Data/CombatFeel/";
        const string Settings = "Assets/Game/CreatureSystem/Resources/CombatFeelSettings.asset";

        [MenuItem("Tools/Capstone/Pet Skills/Build combined cartoon hit")]
        static void Build()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CombatFeelSettings>(Settings);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (settings == null || shader == null)
                throw new InvalidOperationException("Combat feel settings or the URP particle shader are missing.");

            var effect = new GameObject("Cartoon Hit");
            try
            {
                AddLayer(effect.transform, shader, "CFX_Hit_A Red+RandomText", "Red impact", .66f);
                AddLayer(effect.transform, shader, "CFX_Hit_C White", "White sparks", .85f);
                settings.directImpact = PrefabUtility.SaveAsPrefabAsset(effect, Effects + "CartoonHit.prefab");
                if (settings.directImpact == null)
                    throw new InvalidOperationException("Could not save the combined hit prefab.");
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
            finally
            {
                Object.DestroyImmediate(effect);
            }
        }

        static void AddLayer(Transform parent, Shader shader, string sourceName, string layerName, float scale)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source + sourceName + ".prefab");
            if (source == null) throw new InvalidOperationException("Missing source hit prefab " + sourceName);
            var layer = Object.Instantiate(source, parent, false);
            layer.name = layerName;
            layer.transform.localScale *= scale;
            var text = layer.transform.Find("Text Random");
            if (text != null) Object.DestroyImmediate(text.gameObject);
            foreach (var script in layer.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(script);

            foreach (var particle in layer.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.loop = false;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.maxParticles = Mathf.Min(main.maxParticles, 24);

                var renderer = particle.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = ConvertMaterial(shader, renderer.sharedMaterial);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        static Material ConvertMaterial(Shader shader, Material source)
        {
            if (source == null || source.GetTexture("_MainTex") == null)
                throw new InvalidOperationException("A cartoon hit source is missing its particle texture.");
            string path = Effects + source.name + "_URP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(shader);
            material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            material.SetTextureScale("_BaseMap", source.GetTextureScale("_MainTex"));
            material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_MainTex"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 2);
            material.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (int)BlendMode.One);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (int)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}

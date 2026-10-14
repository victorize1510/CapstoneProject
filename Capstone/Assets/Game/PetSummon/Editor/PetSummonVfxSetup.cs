using UnityEditor;
using UnityEngine;

namespace Capstone.Game.PetSummon.Editor
{
    public static class PetSummonVfxSetup
    {
        public const string ProfilePath = "Assets/Game/PetSummon/PetVFX_Default.asset";
        const string Root = "Assets/Game/PetSummon";

        // Explicit installation only. Domain reload never rebuilds profiles or scenes.
        [MenuItem("Game Tools/Pets/Install 3D Summon VFX")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                Debug.LogWarning("Stop Play Mode before installing pet VFX resources."); return;
            }
            PetVFXProfile profile = AssetDatabase.LoadAssetAtPath<PetVFXProfile>(ProfilePath);
            if (profile == null) {
                profile = ScriptableObject.CreateInstance<PetVFXProfile>();
                AssetDatabase.CreateAsset(profile,ProfilePath);
            }
            profile.spawnPrefab = BuildPrefab(false);
            profile.recallPrefab = BuildPrefab(true);
            if (profile.bodyShader == null) profile.bodyShader = Shader.Find("Capstone/PetSummonBodyGlow");
            if (profile.energyShader == null) profile.energyShader = Shader.Find("Capstone/PetSummonEnergy");
            if (profile.dissolveNoise == null) profile.dissolveNoise = BuildNoise();
            EditorUtility.SetDirty(profile);
            ConfigurePlayer("Assets/Prefabs/Thắng/Player.prefab",profile);
            ConfigurePlayer("Assets/Prefabs/Thắng/Vexa/Vefects_Vexa.prefab",profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            Debug.Log("3D pet VFX installed: reusable Spawn/Recall prefabs, profile, shader references and both player prefabs. Existing profile settings preserved.");
        }

        static PetSummonVfxInstance BuildPrefab(bool recall)
        {
            string path = Root + "/Prefabs/" + (recall ? "RecallVFX" : "SpawnVFX") + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) {
                var contents=PrefabUtility.LoadPrefabContents(path);
                try {
                    if (contents.GetComponent<PetSummonVfxInstance>().BuildLayout(recall))
                        PrefabUtility.SaveAsPrefabAsset(contents,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<PetSummonVfxInstance>();
            }
            var root = new GameObject(recall ? "RecallVFX" : "SpawnVFX");
            try {
                root.AddComponent<PetSummonVfxInstance>().BuildLayout(recall);
                return PrefabUtility.SaveAsPrefabAsset(root,path).GetComponent<PetSummonVfxInstance>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Texture2D BuildNoise()
        {
            string path = Root + "/Materials/MaterializeNoise.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 64;
            var texture = new Texture2D(size,size,TextureFormat.RGBA32,false,true) {
                name = "MaterializeNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size*size];
            for (int y=0;y<size;y++) for (int x=0;x<size;x++) {
                float u=x/(float)size,v=y/(float)size;
                float x0=Mathf.PerlinNoise(u*5+17,v*5+31),x1=Mathf.PerlinNoise(u*5+12,v*5+31);
                float y0=Mathf.PerlinNoise(u*5+17,v*5+26),xy=Mathf.PerlinNoise(u*5+12,v*5+26);
                float value=Mathf.Lerp(Mathf.Lerp(x0,x1,u),Mathf.Lerp(y0,xy,u),v);
                pixels[y*size+x]=new Color(value,value,value,1);
            }
            texture.SetPixels(pixels); texture.Apply(false,true);
            AssetDatabase.CreateAsset(texture,path); return texture;
        }

        static void ConfigurePlayer(string path,PetVFXProfile profile)
        {
            GameObject root=PrefabUtility.LoadPrefabContents(path);
            try {
                var effect=root.GetComponent<PetSummonReleaseEffect>();
                if (effect == null) throw new System.InvalidOperationException("Player is missing PetSummonReleaseEffect: " + path);
                if (effect.Profile == null) {
                    effect.ConfigureProfile(profile);
                    EditorUtility.SetDirty(effect); PrefabUtility.SaveAsPrefabAsset(root,path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}

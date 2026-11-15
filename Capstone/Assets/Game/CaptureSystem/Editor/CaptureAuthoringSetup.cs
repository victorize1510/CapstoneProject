using System;
using System.IO;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Capstone.Game.CaptureSystem.Editor {
    public static class CaptureAuthoringSetup {
        const string Root=QaArtifactPaths.Root+"capture-stats-visibility-2026-11-04/";
        const string Resource="Assets/Resources/Capture/",Prefabs="Assets/Game/CaptureSystem/Prefabs/";
        [MenuItem("Tools/Capstone/Capture/Prepare capture assets")]
        public static void Build(){
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Resource);Directory.CreateDirectory(Prefabs);AssetDatabase.Refresh();
            var ball=AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>(Resource+"BasicCaptureBall.asset");
            if(ball==null){ball=ScriptableObject.CreateInstance<MonsterItemDefinition>();AssetDatabase.CreateAsset(ball,Resource+"BasicCaptureBall.asset");}
            var data=new SerializedObject(ball);
            data.FindProperty("Name").stringValue="Bóng thu phục cơ bản";data.FindProperty("stableId").stringValue="capture-ball-basic-01";
            data.FindProperty("Stackable").boolValue=true;data.FindProperty("MaxStackSize").intValue=99;
            data.FindProperty("category").intValue=(int)GameItemCategory.CaptureBall;data.FindProperty("rarity").intValue=0;
            data.FindProperty("description").stringValue="Dụng cụ thu phục thú hoang khi đang chiến đấu.";
            data.FindProperty("effect").stringValue="Khóa một thú hoang rồi sử dụng bóng để thu phục.";
            data.FindProperty("source").stringValue="";data.FindProperty("usableFromInventory").boolValue=true;data.FindProperty("consumable").boolValue=true;
            data.FindProperty("isCaptureBall").boolValue=true;data.FindProperty("captureMultiplier").floatValue=1;data.FindProperty("guaranteedCapture").boolValue=false;data.FindProperty("captureCondition").intValue=0;data.ApplyModifiedPropertiesWithoutUndo();
            var settings=AssetDatabase.LoadAssetAtPath<CaptureSettings>(Resource+"CaptureSettings.asset");
            if(settings==null){settings=ScriptableObject.CreateInstance<CaptureSettings>();AssetDatabase.CreateAsset(settings,Resource+"CaptureSettings.asset");}
            settings.enableCriticalCapture=false;settings.ballVisual=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/PetSummon/Prefabs/BallGreenClose.prefab");EditorUtility.SetDirty(settings);
            var catalog=AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>("Assets/Game/HudSystem/Prefabs/DragonEvolutionCatalog.asset");
            if(catalog==null)throw new InvalidOperationException("Missing existing dragon prefab catalog.");
            foreach(var form in new[]{"01","02","03"}){
                if(!catalog.TryGetPrefab("Dragon Dusk",form,out var template))throw new InvalidOperationException("Missing dragon form "+form);
                var go=Object.Instantiate(template.gameObject);go.name="WildDragon"+form;go.SetActive(false);
                try{
                    var pet=go.GetComponent<PetController>();var so=new SerializedObject(pet);so.FindProperty("wildEncounter").boolValue=true;so.FindProperty("useSelfInput").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();pet.owner=null;
                    var metadata=go.GetComponentInChildren<PetCollectionMetadata>(true);var m=new SerializedObject(metadata);
                    foreach(string key in new[]{"petId","nickname","captureDate","captureLocation","captureBallId","trainerName"})m.FindProperty(key).stringValue="";
                    m.FindProperty("favorite").boolValue=false;m.FindProperty("geneticsInitialized").boolValue=false;m.FindProperty("captureGeneticsRolled").boolValue=false;m.FindProperty("released").boolValue=false;m.ApplyModifiedPropertiesWithoutUndo();
                    // A fresh wild spawn starts at full health. HUD resolves the real maximum
                    // from the existing formula, then the spawn rolls its one-time genetics.
                    var runtime=new SerializedObject(go.GetComponentInChildren<PetHudRuntimeStats>(true));runtime.FindProperty("health").floatValue=1;runtime.FindProperty("maxHealth").floatValue=1;runtime.ApplyModifiedPropertiesWithoutUndo();
                    var enemy=go.GetComponent<DummyEnemy>()??go.AddComponent<DummyEnemy>();enemy.autoRevive=false;enemy.isBoss=false;enemy.writeHealthToName=false;
                    if(go.GetComponent<WildPetCaptureTarget>()==null)go.AddComponent<WildPetCaptureTarget>();
                    var agent=go.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent!=null)agent.enabled=false;
                    // Owned templates can be serialized while dormant. A wild wrapper
                    // must start visible; do not enable particle/map-marker renderers.
                    foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
                        if((renderer is SkinnedMeshRenderer||renderer is MeshRenderer)&&renderer.GetComponentInParent<AAMAP.MapIcon>(true)==null)renderer.enabled=true;
                    foreach(var animator in go.GetComponentsInChildren<Animator>(true))animator.enabled=true;
                    go.SetActive(true);PrefabUtility.SaveAsPrefabAsset(go,Prefabs+go.name+".prefab");
                }finally{Object.DestroyImmediate(go);}
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            if(settings.ballVisual!=null)RenderBallIcon(ball,settings.ballVisual);
            File.WriteAllText(Root+"authoring-assets.txt",Resource+"BasicCaptureBall.asset\n"+Resource+"CaptureSettings.asset\n"+Prefabs+"WildDragon01.prefab\n"+Prefabs+"WildDragon02.prefab\n"+Prefabs+"WildDragon03.prefab\n");
        }
        static void RenderBallIcon(MonsterItemDefinition ball,GameObject prefab){
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try{
                var model=Object.Instantiate(prefab);foreach(var script in model.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
                preview.AddSingleGO(model);var renderers=model.GetComponentsInChildren<MeshRenderer>();if(renderers.Length==0)return;
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float extent=Mathf.Max(bounds.extents.x,bounds.extents.y,bounds.extents.z);
                if(extent>0){model.transform.localScale*=.5f/extent;bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);}
                File.WriteAllText(Root+"ball-preview-bounds.txt",bounds+"\n"+string.Join("\n",Array.ConvertAll(renderers,r=>r.name+" "+r.bounds)));
                preview.BeginStaticPreview(new Rect(0,0,128,128));
                preview.camera.fieldOfView=30;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=Color.clear;
                preview.camera.transform.position=bounds.center+new Vector3(1,.65f,-2).normalized*Mathf.Max(.5f,bounds.extents.magnitude*4.5f);preview.camera.transform.LookAt(bounds.center);
                preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(40,40,0);preview.lights[1].intensity=.8f;preview.ambientColor=new Color(.45f,.45f,.45f);
                preview.Render(true);image=preview.EndStaticPreview();
                // EndStaticPreview returns RGB24. Recover coverage with black/white
                // renders, then explicitly encode RGBA32 rather than keying out genuine
                // dark details on the ball or writing alpha back into an RGB texture.
                preview.BeginStaticPreview(new Rect(0,0,128,128));preview.camera.backgroundColor=Color.white;preview.Render(true);
                var white=preview.EndStaticPreview();
                try{var blackPixels=image.GetPixels();var whitePixels=white.GetPixels();for(int i=0;i<blackPixels.Length;i++){var b=blackPixels[i];var w=whitePixels[i];float alpha=1-Mathf.Clamp01(Mathf.Max(w.r-b.r,w.g-b.g,w.b-b.b));blackPixels[i]=alpha<.005f?Color.clear:new Color(Mathf.Clamp01(b.r/alpha),Mathf.Clamp01(b.g/alpha),Mathf.Clamp01(b.b/alpha),alpha);}var rgba=new Texture2D(image.width,image.height,TextureFormat.RGBA32,false,false);rgba.SetPixels(blackPixels);rgba.Apply();Object.DestroyImmediate(image);image=rgba;}
                finally{Object.DestroyImmediate(white);}
                File.WriteAllBytes(Resource+"BasicCaptureBall.png",image.EncodeToPNG());AssetDatabase.ImportAsset(Resource+"BasicCaptureBall.png");
                var importer=(TextureImporter)AssetImporter.GetAtPath(Resource+"BasicCaptureBall.png");importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                ball.Icon=AssetDatabase.LoadAssetAtPath<Sprite>(Resource+"BasicCaptureBall.png");EditorUtility.SetDirty(ball);AssetDatabase.SaveAssets();
            }finally{preview.Cleanup();if(image!=null)Object.DestroyImmediate(image);}
        }
    }
}


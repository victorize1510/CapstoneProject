using System;
using System.IO;
using System.Linq;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.ShopSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor {
    /// <summary>Explicit, repeatable authoring. Never runs on import/domain reload.</summary>
    public static class DemoSceneOneGameplaySetup {
        public static string Qa => QaArtifactPaths.Root+"demoscene1-starter-shop-respawn-2026-11-16/";
        const string Art="Assets/Game/CreatureSystem/Runtime/DemoSceneOne";
        const string SmokeSource="Assets/UnityAssset/Trung/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR3 Hit Misc F Smoke.prefab";
        public static void Write(string file,string text) {
            Directory.CreateDirectory(Qa);File.WriteAllText(Qa+file,text);File.AppendAllText(Qa+"created-paths.txt",Qa+file+"\n");
        }
        [MenuItem("Tools/Capstone/PvE/3. Apply starter Shop and respawn (DemoScene 1)")]
        public static void Apply() {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=DemoSceneOneSetup.ScenePath)throw new InvalidOperationException("Mở DemoScene 1 ngoài Play Mode.");
            string original=DemoSceneOneSetup.OriginalHash();
            Directory.CreateDirectory(Qa+"backups");
            string backup=Qa+"backups/before-gameplay-authoring-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity";
            EditorSceneManager.SaveScene(scene,backup,true);File.AppendAllText(Qa+"created-paths.txt",backup+"\n");
            Directory.CreateDirectory(Art);
            var roots=scene.GetRootGameObjects();
            var save=roots.SelectMany(r=>r.GetComponentsInChildren<PlayerSaveController>(true)).Single();
            var input=roots.SelectMany(r=>r.GetComponentsInChildren<PetCommandInput>(true)).Single();
            var basePet=input.activePet;
            if(basePet==null)throw new InvalidOperationException("Thiếu tham chiếu starter gốc.");
            var grant=save.GetComponent<DemoSceneOneStarterGrant>()??Undo.AddComponent<DemoSceneOneStarterGrant>(save.gameObject);
            Undo.RecordObject(grant,"DemoScene 1 starter receipt");
            grant.baseStarter=basePet;
            grant.legacyBasePetId=basePet.GetComponent<PetCollectionMetadata>().PetId;
            grant.catalog=AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>("Assets/Game/CreatureSystem/Runtime/Dragons/DemoDragonCatalog.asset");
            if(grant.catalog==null)throw new InvalidOperationException("Thiếu catalog ba dạng rồng.");
            var saveData=new SerializedObject(save);saveData.FindProperty("loadOnStart").boolValue=true;
            saveData.FindProperty("seedDragonPreviewPartyOnFreshSave").boolValue=false;saveData.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(save);EditorUtility.SetDirty(grant);
            var shop=BuildShop();int shops=0;
            foreach(var npc in roots.SelectMany(r=>r.GetComponentsInChildren<NpcPanelInteractable>(true))) {
                if(new SerializedObject(npc).FindProperty("targetPanel").enumValueIndex!=1)continue;
                var source=npc.GetComponent<ShopSource>()??Undo.AddComponent<ShopSource>(npc.gameObject);
                Undo.RecordObject(source,"DemoScene 1 merchant offers");source.Configure(shop);EditorUtility.SetDirty(source);
                PrefabUtility.RecordPrefabInstancePropertyModifications(source);shops++;
            }
            if(shops==0)throw new InvalidOperationException("Không tìm thấy NPC Shop; không dùng catalog toàn game làm dự phòng.");
            var smoke=BuildSmoke();
            var stars=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/DownedStars_CapstoneFallback.prefab");
            var existing=roots.SelectMany(r=>r.GetComponentsInChildren<EncounterSpawnPoint>(true)).ToArray();
            var enemies=roots.SelectMany(r=>r.GetComponentsInChildren<BasicCreatureEnemy>(true)).Where(e=>e.enabled&&e.GetComponent<DummyEnemy>() is var h&&h!=null&&!h.IsTrainingDummy&&!h.IsBoss).ToArray();
            int count=0;
            foreach(var enemy in enemies) {
                var health=enemy.GetComponent<DummyEnemy>();
                var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(enemy.gameObject);
                if(source==null||source.GetComponent<DummyEnemy>()==null)throw new InvalidOperationException("Thiếu prefab nguồn cho "+enemy.name);
                var owner=existing.FirstOrDefault(p=>p.initialActor==health);
                if(owner==null) {
                    var go=new GameObject(enemy.name+" - encounter home");Undo.RegisterCreatedObjectUndo(go,"Encounter home");
                    go.transform.SetPositionAndRotation(enemy.transform.position,enemy.transform.rotation);owner=go.AddComponent<EncounterSpawnPoint>();
                }
                Undo.RecordObject(owner,"Encounter lifecycle");owner.encounterPrefab=source;owner.initialActor=health;
                owner.actorScale=enemy.transform.lossyScale;owner.smokePrefab=smoke;owner.downedStarsPrefab=stars;owner.respawnDelay=5;
                owner.openingEncounter=enemy.GetComponent<EncounterLevel>()?.openingEncounter==true;owner.settings.Read(enemy);
                Undo.RecordObject(health,"Managed encounter has no same-individual revive");health.autoRevive=false;
                EditorUtility.SetDirty(owner);EditorUtility.SetDirty(health);PrefabUtility.RecordPrefabInstancePropertyModifications(health);count++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene,DemoSceneOneSetup.ScenePath))throw new IOException("Không lưu được DemoScene 1.");
            if(DemoSceneOneSetup.OriginalHash()!=original)throw new InvalidOperationException("DemoScene gốc đã thay đổi.");
            Write("authoring.txt","APPLIED\noriginal SHA256="+original+"\nstarter base ID="+grant.legacyBasePetId+"\nshop NPCs="+shops+"\nencounter owners="+count+"\nSmoke source="+SmokeSource+"\nNo original scene, global shop catalog or real save changed by authoring.");
        }
        static ShopCatalog BuildShop() {
            string path=Art+"/Shop.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>(path);
            if(catalog==null){catalog=ScriptableObject.CreateInstance<ShopCatalog>();AssetDatabase.CreateAsset(catalog,path);}
            var defaults = Resources.Load<ShopCatalog>("Shop/DefaultShopCatalog");
            if (defaults == null) throw new InvalidOperationException("Thiếu danh mục cửa hàng mặc định.");
            catalog.sharedCatalog = defaults;
            catalog.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            return catalog;
        }
        static GameObject BuildSmoke() {
            string path=Art+"/DeathSmoke.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var go=PrefabUtility.LoadPrefabContents(SmokeSource);
            try {
                go.name="DeathSmoke - pooled URP variant";
                foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
                int i=0;
                foreach(var renderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true)) {
                    var materials=renderer.sharedMaterials;
                    for(int j=0;j<materials.Length;j++) {
                        if(materials[j]==null)continue;
                        string materialPath=Art+"/DeathSmoke_"+(i++)+".mat";
                        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if(material==null) {
                            material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                            material.name="DeathSmoke URP";material.SetTexture("_BaseMap",materials[j].GetTexture("_MainTex"));
                            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
                            material.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                            material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                            material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;AssetDatabase.CreateAsset(material,materialPath);
                        }
                        materials[j]=material;
                    }
                    renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                }
                foreach(var particle in go.GetComponentsInChildren<ParticleSystem>(true)) {
                    var main=particle.main;main.loop=false;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;main.maxParticles=Mathf.Min(main.maxParticles,64);
                    main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                }
                return PrefabUtility.SaveAsPrefabAsset(go,path);
            } finally {PrefabUtility.UnloadPrefabContents(go);}
        }
    }
}

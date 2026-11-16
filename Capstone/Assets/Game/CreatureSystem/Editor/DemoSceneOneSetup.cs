using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using GDS.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor {
    public static class DemoSceneOneSetup {
        public const string ScenePath = "Assets/Scenes/DemoScene 1.unity";
        public const string Qa = UISystem.QaArtifactPaths.Root + "pve-items-capture-demoscene1-2026-11-16/";
        const string Art = "Assets/Game/CreatureSystem/Runtime/DemoSceneOne";
        public static void Report(string file, string text) {
            Directory.CreateDirectory(Qa);
            string path = Qa + file;
            File.WriteAllText(path, text);
            File.AppendAllText(Qa + "created-paths.txt", path + "\n");
        }
        public static string OriginalHash() {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes("Assets/Scenes/DemoScene.unity"))).Replace("-", "");
        }
        [MenuItem("Tools/Capstone/PvE/1. Prepare DemoScene 1 (preserve original)")]
        public static void Apply() {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath) throw new InvalidOperationException("Chỉ triển khai trên DemoScene 1 ngoài Play Mode.");
            string original = OriginalHash();
            Directory.CreateDirectory(Qa + "backups");
            EditorSceneManager.SaveScene(scene, Qa + "backups/before-authoring.unity", true);
            Directory.CreateDirectory(Art); Directory.CreateDirectory("Assets/Resources/Inventory"); AssetDatabase.Refresh();
            BuildCatalog();
            var stun = BuildStars();
            ConfigureDemoBat(scene);
            var loot = AssetDatabase.LoadAssetAtPath<PveLootCatalog>(Art + "/Loot.asset");
            if (loot == null) {
                loot = Object.Instantiate(Resources.Load<PveLootCatalog>("Pve/LootCatalog"));
                loot.name = "DemoSceneOneLoot"; loot.entries.Clear();
                foreach (string path in new[] {"Assets/Game/Inventory/Potion.asset", "Assets/Game/Inventory/HealingApple.asset"})
                    loot.entries.Add(new PveLootCatalog.Entry { item = AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>(path), weight = 1, quantity = 1 });
                AssetDatabase.CreateAsset(loot, Art + "/Loot.asset");
            }
            var roots = scene.GetRootGameObjects();
            var isolation = roots.SelectMany(r => r.GetComponentsInChildren<DemoSceneOneSaveIsolation>(true)).FirstOrDefault();
            if (isolation == null) {
                var go = new GameObject("DemoScene 1 - independent development profile");
                Undo.RegisterCreatedObjectUndo(go, "DemoScene 1 development profile");
                isolation = go.AddComponent<DemoSceneOneSaveIsolation>();
            }
            foreach (var root in roots) {
                foreach (var seeder in root.GetComponentsInChildren<InventoryDebugSeeder>(true)) {
                    var data = new SerializedObject(seeder); data.FindProperty("seedOnStart").boolValue = false; data.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(seeder);
                }
                foreach (var save in root.GetComponentsInChildren<PlayerSaveController>(true)) {
                    SetSlot(save);
                    var rewards = save.GetComponent<PveRewardService>() ?? Undo.AddComponent<PveRewardService>(save.gameObject);
                    Undo.RecordObject(rewards, "DemoScene 1 loot"); rewards.ConfigureLoot(loot); EditorUtility.SetDirty(rewards);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rewards);
                }
                foreach (var save in root.GetComponentsInChildren<QuestSaveController>(true)) SetSlot(save);
            }
            var enemies = roots.SelectMany(r => r.GetComponentsInChildren<BasicCreatureEnemy>(true)).Where(e => e.enabled && !e.GetComponent<DummyEnemy>().IsTrainingDummy && !e.GetComponent<DummyEnemy>().IsBoss).ToArray();
            var player = roots.SelectMany(r => r.GetComponentsInChildren<PetCommandInput>(true)).FirstOrDefault();
            var opening = enemies.OrderBy(e => player != null ? (e.transform.position-player.transform.position).sqrMagnitude : 0).FirstOrDefault();
            foreach (var enemy in enemies) {
                var level = enemy.GetComponent<EncounterLevel>() ?? Undo.AddComponent<EncounterLevel>(enemy.gameObject);
                Undo.RecordObject(level, "Encounter level"); level.openingEncounter = enemy == opening;
                var status = enemy.GetComponent<DownedStatusVisual>() ?? Undo.AddComponent<DownedStatusVisual>(enemy.gameObject);
                Undo.RecordObject(status, "Downed status art"); status.effectPrefab = stun;
                EditorUtility.SetDirty(level); EditorUtility.SetDirty(status);
                PrefabUtility.RecordPrefabInstancePropertyModifications(level); PrefabUtility.RecordPrefabInstancePropertyModifications(status);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Không lưu được DemoScene 1.");
            if (OriginalHash() != original) throw new InvalidOperationException("Hash scene gốc đã đổi.");
            Report("authoring.txt", "APPLIED DemoScene 1\noriginal SHA256="+original+"\nenemies="+enemies.Length+"\nmain save untouched; isolated profile on E\nStatus Effects FREE not imported: explicit Capstone fallback stars used.");
        }
        static void SetSlot(Object component) {
            var data = new SerializedObject(component);
            var slot = data.FindProperty("slotId"); if (slot != null) slot.stringValue = "demoscene-1";
            var migrate = data.FindProperty("migrateLegacyQuestSave"); if (migrate != null) migrate.boolValue = false;
            data.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        static void ConfigureDemoBat(UnityEngine.SceneManagement.Scene scene) {
            var old=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BasicCreatureEnemy>(true))
                .FirstOrDefault(e=>e.name=="Enemy_Bat01_Demo"&&e.GetComponent<PetController>()==null);
            if(old==null)return;
            const string path=Art+"/WildEnemy_Bat01.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null){
                var contents=PrefabUtility.LoadPrefabContents("Assets/Game/CreatureSystem/Runtime/Bats/Wild/Wild_Bat01.prefab");
                try{
                    contents.name="WildEnemy_Bat01";contents.AddComponent<BasicCreatureEnemy>();
                    prefab=PrefabUtility.SaveAsPrefabAsset(contents,path);
                }finally{PrefabUtility.UnloadPrefabContents(contents);}
            }
            var replacement=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            Undo.RegisterCreatedObjectUndo(replacement,"DemoScene 1 wild bat encounter");
            replacement.name="Wild_Bat01_DemoScene1";replacement.transform.SetParent(old.transform.parent,false);
            replacement.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);replacement.transform.localScale=old.transform.localScale;
            var next=replacement.GetComponent<BasicCreatureEnemy>();
            next.aggroRadius=old.aggroRadius;next.leashRadius=old.leashRadius;next.patrolRadius=old.patrolRadius;next.territory=old.territory;
            next.moveSpeed=old.moveSpeed;next.attackRange=old.attackRange;next.attackInterval=old.attackInterval;next.hitDelay=old.hitDelay;next.attackDuration=old.attackDuration;
            PrefabUtility.RecordPrefabInstancePropertyModifications(next);PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
            Undo.DestroyObjectImmediate(old.gameObject);
        }
        static void BuildCatalog() {
            const string path = "Assets/Resources/Inventory/ItemCatalog.asset";
            var items = AssetDatabase.FindAssets("t:MonsterItemDefinition", new[] {"Assets/Game", "Assets/Resources"})
                .Select(g => AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).Where(i=>i!=null).OrderBy(i=>i.StableId).ToArray();
            if (items.Select(i=>i.StableId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=items.Length) throw new InvalidOperationException("Danh mục có ID trùng, không ghi dữ liệu.");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemBaseCatalogSO>(path);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<ItemBaseCatalogSO>(); AssetDatabase.CreateAsset(catalog,path); }
            var data = new SerializedObject(catalog); var list = data.FindProperty("items"); list.arraySize=items.Length;
            for(int i=0;i<items.Length;i++) list.GetArrayElementAtIndex(i).objectReferenceValue=items[i];
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(catalog);
            Report("item-catalog.csv", "id,name,category,hp,legacyMP\n"+string.Join("\n",items.Select(i=>$"{i.StableId},{i.Name},{i.Category},{i.HealAmount},{i.EnergyRestoreAmount}")));
        }
        static GameObject BuildStars() {
            string path=Art+"/DownedStars_CapstoneFallback.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(existing!=null)return existing;
            var shader=Shader.Find("Universal Render Pipeline/Unlit"); if(shader==null)throw new InvalidOperationException("Thiếu URP Unlit.");
            var material=new Material(shader);material.name="DownedChampagne";material.SetColor("_BaseColor",new Color(1,.76f,.28f,1));material.SetFloat("_Cull",0);
            AssetDatabase.CreateAsset(material,Art+"/DownedChampagne.mat");
            var vertices=new Vector3[11];var triangles=new int[30];
            for(int i=0;i<10;i++){float angle=(90+i*36)*Mathf.Deg2Rad;float radius=i%2==0?.19f:.085f;vertices[i+1]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0);triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i==9?1:i+2;}
            var mesh=new Mesh{name="DownedStar"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Art+"/DownedStar.asset");
            var root=new GameObject("Downed stars - Capstone fallback");
            try {
                var orbit=root.AddComponent<OrbitingStatusStars>();orbit.stars=new Transform[3];
                for(int i=0;i<3;i++){var star=new GameObject("Star "+i);star.transform.SetParent(root.transform,false);star.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=star.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;orbit.stars[i]=star.transform;}
                var line=root.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;line.loop=true;line.widthMultiplier=.012f;line.positionCount=40;line.shadowCastingMode=ShadowCastingMode.Off;
                for(int i=0;i<40;i++){float angle=i*Mathf.PI*2/40;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*.65f,0,Mathf.Sin(angle)*.45f));}
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            } finally { Object.DestroyImmediate(root); }
        }
    }
}

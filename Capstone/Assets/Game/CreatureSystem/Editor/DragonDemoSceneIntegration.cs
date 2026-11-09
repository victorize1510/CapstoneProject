using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor {
    public static class DragonDemoSceneIntegration {
        public const string Root="Assets/Game/CreatureSystem/Runtime/Dragons/";
        public const string CatalogPath=Root+"DemoDragonCatalog.asset";
        const string Pilot="Assets/Game/CreatureSystem/Pilot/",Demo="Assets/Scenes/DemoScene.unity";
        const string Family="Assets/Game/CreatureSystem/Data/Families/Family_Dragon_Dusk.asset";
        public const string Qa=Capstone.Game.UISystem.QaArtifactPaths.Root+"dragon-demoscene-runtime-2026-11-06/";
        static readonly List<string> changes=new();
        static void Record(string value){changes.Add(value);Directory.CreateDirectory(Qa);File.WriteAllLines(Qa+"integration.txt",changes);}
        static void Folder(string path){string current="Assets";foreach(string part in path.Split('/').Skip(1)){if(!AssetDatabase.IsValidFolder(current+"/"+part))AssetDatabase.CreateFolder(current,part);current+="/"+part;}}
        static T Copy<T>(string source,string target)where T:Object {
            if(!AssetDatabase.LoadAssetAtPath<T>(target)){Folder(Path.GetDirectoryName(target).Replace('\\','/'));if(!AssetDatabase.CopyAsset(source,target))throw new InvalidOperationException("Copy failed: "+source);Record("Created "+target);}
            return AssetDatabase.LoadAssetAtPath<T>(target)??throw new InvalidOperationException("Missing asset: "+target);
        }
        static GameObject Visual(int stage)=>AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Visual/Visual_Dragon"+stage.ToString("00")+".prefab");
        static void WireRules(PetCollectionMetadata metadata){var so=new SerializedObject(metadata);var rules=so.FindProperty("evolutionRules");for(int i=0;i<rules.arraySize;i++){var rule=rules.GetArrayElementAtIndex(i);int target=rule.FindPropertyRelative("sourceStage").intValue+2;if(target>=1&&target<=3)rule.FindPropertyRelative("targetVisualPrefab").objectReferenceValue=Visual(target);}so.ApplyModifiedPropertiesWithoutUndo();}
        [MenuItem("Tools/Capstone/Creatures/7. Integrate dragon runtime into DemoScene")]
        public static void Apply(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before integration.");
            var loaded=SceneManager.GetSceneByPath(Demo);if(loaded.isLoaded&&loaded.isDirty)throw new InvalidOperationException("DemoScene has unsaved user changes. Integration did not save or overwrite them.");
            changes.Clear();var family=AssetDatabase.LoadAssetAtPath<CreatureFamilyDefinition>(Family);if(family==null||family.forms.Count!=3)throw new InvalidOperationException("Missing verified dragon family.");
            for(int stage=1;stage<=3;stage++){
                string form=stage.ToString("00");
                Copy<CreatureAnimationProfile>(Pilot+"Animation/Dragon"+form+".asset",Root+"Animation/Dragon"+form+".asset");
                var visual=Copy<GameObject>(Pilot+"Visual/Visual_Dragon"+form+".prefab",Root+"Visual/Visual_Dragon"+form+".prefab");
                var contents=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(visual));
                try{var binding=contents.GetComponentInChildren<CreatureVisualBinding>(true);binding.animationProfile=AssetDatabase.LoadAssetAtPath<CreatureAnimationProfile>(Root+"Animation/Dragon"+form+".asset");PrefabUtility.SaveAsPrefabAsset(contents,Root+"Visual/Visual_Dragon"+form+".prefab");}finally{PrefabUtility.UnloadPrefabContents(contents);}
            }
            for(int stage=1;stage<=3;stage++)foreach(string role in new[]{"OwnedPet","Wild"}){
                string form=stage.ToString("00"),path=Root+role+"/"+role+"_Dragon"+form+".prefab";
                Copy<GameObject>(Pilot+role+"/"+role+"_Dragon"+form+".prefab",path);
                var go=PrefabUtility.LoadPrefabContents(path);
                try{
                    var evolution=go.GetComponent<PetEvolutionVisualController>();
                    var old=go.GetComponentInChildren<CreatureVisualBinding>(true).transform.root==go.transform
                        ?go.GetComponentInChildren<CreatureVisualBinding>(true).gameObject:null;
                    // The binding is on the shared visual's Animator, never the gameplay root.
                    if(old==null||old==go)throw new InvalidOperationException("Visual boundary missing: "+path);
                    var parent=old.transform.parent;int index=old.transform.GetSiblingIndex();var scale=old.transform.localScale;
                    var next=(GameObject)PrefabUtility.InstantiatePrefab(Visual(stage),go.scene);next.transform.SetParent(parent,false);next.transform.SetSiblingIndex(index);next.transform.localScale=scale;
                    var animator=next.GetComponentInChildren<Animator>(true);go.GetComponent<PetController>().animator=animator;
                    var player=go.GetComponent<PetSkillPresentationPlayer>();if(player!=null)player.sockets=new List<PetSkillSocket>(next.GetComponentInChildren<CreatureVisualBinding>(true).sockets);
                    var stats=new SerializedObject(go.GetComponent<PetHudRuntimeStats>());stats.FindProperty("animator").objectReferenceValue=animator;stats.ApplyModifiedPropertiesWithoutUndo();
                    if(evolution!=null){var so=new SerializedObject(evolution);so.FindProperty("visualRoot").objectReferenceValue=go.transform;so.FindProperty("baseVisual").objectReferenceValue=next;so.FindProperty("baseVisualPrefab").objectReferenceValue=Visual(stage);so.ApplyModifiedPropertiesWithoutUndo();}
                    WireRules(go.GetComponent<PetCollectionMetadata>());Object.DestroyImmediate(old);
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                }finally{PrefabUtility.UnloadPrefabContents(go);}
            }
            NormalizeProductionScales();
            var catalog=Copy<PetPrefabCatalog>(Pilot+"DragonPilotCatalog.asset",CatalogPath);var entries=new SerializedObject(catalog);var list=entries.FindProperty("entries");
            for(int i=0;i<3;i++)list.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"OwnedPet/OwnedPet_Dragon"+(i+1).ToString("00")+".prefab").GetComponent<PetController>();
            entries.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(catalog);
            var active=SceneManager.GetActiveScene();bool opened=!loaded.isLoaded;var scene=opened?EditorSceneManager.OpenScene(Demo,OpenSceneMode.Additive):loaded;
            try{
                var roots=scene.GetRootGameObjects();
                var pets=roots.SelectMany(r=>r.GetComponentsInChildren<PetController>(true)).Where(p=>p.GetComponent<PetCollectionMetadata>()?.DefinitionId=="Dragon Dusk").ToArray();
                if(pets.Length!=1)throw new InvalidOperationException("Expected exactly one authored dragon gameplay actor; found "+pets.Length+". No scene saved.");
                var pet=pets[0];string source=AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(pet.gameObject));bool changed=false;
                if(!source.StartsWith(Root,StringComparison.Ordinal)){ReplaceActor(scene,pet);changed=true;}
                foreach(var save in roots.SelectMany(r=>r.GetComponentsInChildren<PlayerSaveController>(true))){var so=new SerializedObject(save);var property=so.FindProperty("petPrefabCatalog");if(property.objectReferenceValue==catalog)continue;
                    // A later creature-family integration may extend the catalog.
                    // Preserve it when all production dragon mappings remain correct.
                    var current=property.objectReferenceValue as PetPrefabCatalog;
                    bool complete=current!=null;
                    for(int i=1;complete&&i<=3;i++) complete=current.TryGetPrefab("Dragon Dusk",i.ToString("00"),out var existing)&&existing==AssetDatabase.LoadAssetAtPath<GameObject>(Root+"OwnedPet/OwnedPet_Dragon"+i.ToString("00")+".prefab").GetComponent<PetController>();
                    if(complete)continue;
                    property.objectReferenceValue=catalog;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(save);changed=true;}
                if(changed){if(!EditorSceneManager.SaveScene(scene,Demo))throw new InvalidOperationException("Could not save DemoScene.");Record("Saved DemoScene only. Authored inactive model references, source prefabs and other scenes preserved.");}
                else Record("DemoScene already uses runtime actor/catalog; not resaved.");
            }finally{if(opened)EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}
            for(int i=0;i<3;i++){var form=family.forms[i];string id=(i+1).ToString("00");form.visualPrefab=Visual(i+1);form.animationProfile=AssetDatabase.LoadAssetAtPath<CreatureAnimationProfile>(Root+"Animation/Dragon"+id+".asset");form.petPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"OwnedPet/OwnedPet_Dragon"+id+".prefab");form.wildPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Wild/Wild_Dragon"+id+".prefab");}
            EditorUtility.SetDirty(family);AssetDatabase.SaveAssetIfDirty(family);Record("COMPLETE: production roots/catalog wired; no wild encounter, enemy AI or NPC added.");
        }
        public static void NormalizeProductionScales(){
            // Evolution replaces a model with localScale=one. All form roots must use
            // that same model-only contract, otherwise a restored form changes size.
            var family=AssetDatabase.LoadAssetAtPath<CreatureFamilyDefinition>(Family);
            Vector3 actorScale=family.forms[0].sourcePrefab.transform.localScale;
            for(int stage=1;stage<=3;stage++){
                string path=Root+"Visual/Visual_Dragon"+stage.ToString("00")+".prefab";var visual=PrefabUtility.LoadPrefabContents(path);
                try{visual.transform.localScale=Vector3.one;PrefabUtility.SaveAsPrefabAsset(visual,path);}finally{PrefabUtility.UnloadPrefabContents(visual);}
                foreach(string role in new[]{"OwnedPet","Wild"}){
                    path=Root+role+"/"+role+"_Dragon"+stage.ToString("00")+".prefab";var actor=PrefabUtility.LoadPrefabContents(path);
                    try{
                        actor.transform.localScale=actorScale;var model=actor.GetComponentInChildren<CreatureVisualBinding>(true);model.transform.localScale=Vector3.one;
                        var collider=actor.GetComponent<CapsuleCollider>();if(collider!=null){var skins=model.GetComponentsInChildren<Renderer>(true);var bounds=skins[0].bounds;foreach(var skin in skins.Skip(1))bounds.Encapsulate(skin.bounds);var scale=actor.transform.lossyScale;collider.center=actor.transform.InverseTransformPoint(bounds.center);collider.radius=Mathf.Max(.1f,Mathf.Min(bounds.size.x,bounds.size.z)*.22f/Mathf.Max(.001f,Mathf.Abs(scale.x)));collider.height=Mathf.Max(collider.radius*2,bounds.size.y/Mathf.Max(.001f,Mathf.Abs(scale.y)));}
                        PrefabUtility.SaveAsPrefabAsset(actor,path);
                    }finally{PrefabUtility.UnloadPrefabContents(actor);}
                }
            }
            Record("Normalized model roots to 1 and all form actor scales to authored base "+actorScale+"; evolution/load retain world scale. Wild colliders refit in local space.");
        }
        static void ReplaceActor(Scene scene,PetController oldPet){
            var metadata=oldPet.GetComponent<PetCollectionMetadata>();var before=new SerializedObject(metadata);string id=before.FindProperty("petId").stringValue;
            int stage=metadata.EvolutionStage+1;var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"OwnedPet/OwnedPet_Dragon"+stage.ToString("00")+".prefab");
            var old=oldPet.gameObject;var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);go.SetActive(false);go.name=old.name;go.layer=old.layer;go.tag=old.tag;
            go.transform.SetParent(old.transform.parent,false);go.transform.localPosition=old.transform.localPosition;go.transform.localRotation=old.transform.localRotation;go.transform.localScale=old.transform.localScale;go.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
            foreach(var component in old.GetComponents<Component>()){
                if(component==null||component is Transform||component is Animator||component is Renderer||component is MeshFilter||component is PetEvolutionVisualController||component is PetEvolutionTestButton)continue;
                var target=go.GetComponent(component.GetType());if(target==null)target=go.AddComponent(component.GetType());EditorUtility.CopySerialized(component,target);PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            var pet=go.GetComponent<PetController>();pet.animator=go.GetComponentInChildren<Animator>(true);pet.agent=go.GetComponent<UnityEngine.AI.NavMeshAgent>();pet.useSelfInput=false;
            var stats=new SerializedObject(go.GetComponent<PetHudRuntimeStats>());stats.FindProperty("animator").objectReferenceValue=pet.animator;stats.FindProperty("speciesSkillSet").objectReferenceValue=AssetDatabase.LoadAssetAtPath<CreatureFamilyDefinition>(Family).forms[stage-1].skillSet;stats.FindProperty("usePrototypeSkillsWhenEmpty").boolValue=false;
            if(stats.FindProperty("maxHealth").floatValue<=0){stats.FindProperty("health").floatValue=1;stats.FindProperty("maxHealth").floatValue=1;Record("Normalized missing authored HP 0/0 to full ratio; actual stats still calculated by existing formula.");}stats.ApplyModifiedPropertiesWithoutUndo();WireRules(go.GetComponent<PetCollectionMetadata>());
            var map=new Dictionary<Object,Object>{{old,go},{old.transform,go.transform}};
            foreach(var c in old.GetComponents<Component>()){if(c is Transform||c==null||c is PetEvolutionTestButton)continue;var replacement=c is Animator?pet.animator:go.GetComponent(c.GetType());if(replacement!=null)map[c]=replacement;}
            var named=go.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var t in old.GetComponentsInChildren<Transform>(true)){if(t==old.transform||!named.TryGetValue(t.name,out var next))continue;map[t]=next;map[t.gameObject]=next.gameObject;foreach(var c in t.GetComponents<Component>())if(c!=null&&next.GetComponent(c.GetType()) is Component target)map[c]=target;}
            int references=0;
            foreach(var component in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true))){
                if(component==null||component is Transform||component.transform.IsChildOf(old.transform))continue;
                var so=new SerializedObject(component);var prop=so.GetIterator();bool changed=false;
                while(prop.Next(true))if(prop.propertyType==SerializedPropertyType.ObjectReference&&prop.objectReferenceValue!=null){var value=prop.objectReferenceValue;
                    if(map.TryGetValue(value,out var replacement)){prop.objectReferenceValue=replacement;changed=true;references++;}
                    else if(value is Component c&&c.transform.IsChildOf(old.transform)||value is GameObject g&&g.transform.IsChildOf(old.transform))throw new InvalidOperationException("Unmapped actor reference: "+component.name+" / "+prop.propertyPath+". No scene saved.");
                }
                if(changed){so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(component);if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
            }
            var after=new SerializedObject(go.GetComponent<PetCollectionMetadata>());if(after.FindProperty("petId").stringValue!=id)throw new InvalidOperationException("Persistent ID changed.");
            bool active=old.activeSelf;Object.DestroyImmediate(old);go.SetActive(active);PrefabUtility.RecordPrefabInstancePropertyModifications(go);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(pet);PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<PetHudRuntimeStats>());PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<PetCollectionMetadata>());
            Record("Replaced owned form "+stage+"; preserved ID "+id+", name/parent/local pose/scale, metadata and controller settings; remapped "+references+" references. Removed runtime test-key bypass only from replacement.");
        }
    }
}

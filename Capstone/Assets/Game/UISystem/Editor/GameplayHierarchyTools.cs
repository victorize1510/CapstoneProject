using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.HudSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Organize existing scene objects without rebuilding gameplay or changing references.</summary>
    public static class GameplayHierarchyTools {
        public const string RootName="CapstoneGameplay";
        public const string PrefabPath="Assets/Game/SceneBundles/CapstoneGameplay.prefab";
        static readonly string[] Groups={"01_Player","02_Pets","03_UI","04_Cameras","05_Systems"};
        const string UndoLabel="Group gameplay for scene copy";

        [MenuItem("Game Tools/Scene/Group Gameplay for Copy")]
        public static void GroupMenu(){var root=GroupCurrentScene();Selection.activeGameObject=root;}

        public static GameObject GroupCurrentScene(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before organizing the scene.");
            var scene=SceneManager.GetActiveScene();
            var roots=scene.GetRootGameObjects();
            var existing=roots.FirstOrDefault(x=>x.name==RootName);
            if(existing!=null)return existing;
            // Snapshot all original transforms, including inactive models and nested camera targets.
            var transforms=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var matrices=transforms.Select(t=>t.localToWorldMatrix).ToArray();
            var active=transforms.Select(t=>t.gameObject.activeSelf).ToArray();
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(UndoLabel);
            try{
                foreach(var metadata in roots.SelectMany(r=>r.GetComponentsInChildren<PetCollectionMetadata>(true))){
                    var serialized=new SerializedObject(metadata);
                    if(!string.IsNullOrWhiteSpace(serialized.FindProperty("petId").stringValue))continue;
                    Undo.RecordObject(metadata,UndoLabel);_ = metadata.PersistentId;EditorUtility.SetDirty(metadata);
                }
                var gameplay=Create(RootName,null,scene);
                var groups=Groups.ToDictionary(n=>n,n=>Create(n,gameplay.transform,scene).transform);
                // This rig was nested beneath a shop model. Move the rig itself, preserving its targets.
                foreach(var rig in transforms.Where(t=>t.name=="TestChar Camera Rig"))
                    Undo.SetTransformParent(rig,groups["04_Cameras"],UndoLabel);
                foreach(var root in roots){
                    string group=GroupFor(root);
                    if(group!=null)Undo.SetTransformParent(root.transform,groups[group],UndoLabel);
                }
                for(int i=0;i<transforms.Length;i++){
                    var current=transforms[i].localToWorldMatrix;float delta=0;
                    for(int n=0;n<16;n++)delta=Mathf.Max(delta,Mathf.Abs(matrices[i][n]-current[n]));
                    if(delta>.002f||active[i]!=transforms[i].gameObject.activeSelf)
                        throw new InvalidOperationException("Grouping changed transform/state: "+transforms[i].name);
                }
                Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);
                return gameplay;
            }catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        static GameObject Create(string name,Transform parent,Scene scene){
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,UndoLabel);
            SceneManager.MoveGameObjectToScene(go,scene);if(parent!=null)go.transform.SetParent(parent,false);
            return go;
        }
        static string GroupFor(GameObject root){
            if(root.GetComponent<PetCommandInput>()!=null||root.name=="Player")return "01_Player";
            if(root.GetComponent<PetController>()!=null||root.name=="Dragon Darkness"||root.name=="Dragon Nightfall")return "02_Pets";
            if(root.GetComponent<Camera>()!=null||root.name=="TestChar Camera Rig")return "04_Cameras";
            if(root.GetComponent<Canvas>()!=null||root.GetComponent<UIDocument>()!=null||root.GetComponent<UnityEngine.EventSystems.EventSystem>()!=null||new[]{"GameplayHUD","CompassHUD","GameMenu"}.Contains(root.name))return "03_UI";
            if(new[]{"QuestManager","MapSystem","Map Icon","DragonEvolution"}.Contains(root.name))return "05_Systems";
            return null;
        }

        public static List<string> ExternalSceneReferences(GameObject root){
            var references=new List<string>();
            foreach(var component in root.GetComponentsInChildren<Component>(true)){
                if(component==null)continue;
                var serialized=new SerializedObject(component);var it=serialized.GetIterator();
                while(it.Next(true)){
                    if(it.propertyType!=SerializedPropertyType.ObjectReference)continue;
                    var obj=it.objectReferenceValue;
                    Transform target=obj is GameObject g?g.transform:obj is Component c?c.transform:null;
                    if(target==null||EditorUtility.IsPersistent(obj)||!target.gameObject.scene.IsValid()||target.IsChildOf(root.transform)||target==root.transform)continue;
                    references.Add(component.name+" ["+component.GetType().Name+"]."+it.propertyPath+" -> "+target.name);
                }
            }
            return references;
        }

        [MenuItem("Game Tools/Scene/Export Gameplay Bundle Prefab")]
        public static void ExportMenu(){var root=GroupCurrentScene();Export(root);Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);}
        public static void Export(GameObject root){
            if(root==null||root.name!=RootName)throw new InvalidOperationException("Missing grouped gameplay root.");
            var external=ExternalSceneReferences(root);
            if(external.Count>0)throw new InvalidOperationException("Gameplay still references scene objects outside the bundle:\n"+string.Join("\n",external));
            if(!AssetDatabase.IsValidFolder("Assets/Game/SceneBundles"))AssetDatabase.CreateFolder("Assets/Game","SceneBundles");
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath,out bool success);
            if(!success)throw new InvalidOperationException("Unable to save gameplay bundle prefab.");
            AssetDatabase.SaveAssets();
        }
    }
}

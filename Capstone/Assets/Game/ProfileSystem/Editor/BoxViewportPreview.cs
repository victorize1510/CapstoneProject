using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.ProfileSystem.Editor {
    // Loads the real composed document and styles without inventory controllers or saves.
    public sealed class BoxViewportPreview : EditorWindow {
        [MenuItem("Game Tools/GameToolThang/HUD/Preview BOX Viewport")]
        public static void Open() {
            var window=GetWindow<BoxViewportPreview>(true,"BOX viewport preview",true);
            window.position=new Rect(60,80,1366,700);window.Show();
        }
        public void CreateGUI() {
            var asset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ThangTest/UIThang/Inventory/UI/MonsterInventory.uxml");
            if(asset==null) throw new InvalidOperationException("Missing composed inventory UXML.");
            var tree=asset.CloneTree(); rootVisualElement.Add(tree);
            tree.style.position=Position.Absolute;tree.style.left=0;tree.style.top=0;tree.style.right=0;tree.style.bottom=0;
            var root=tree.Q("monster-inventory-root");root.style.display=DisplayStyle.Flex;
            foreach(string name in new[]{"quest-inventory-shell","inventory-panel","pets-panel","menu-placeholder-panel"}) {
                var element=tree.Q(name);if(element!=null) element.style.display=DisplayStyle.None;
            }
            var box=tree.Q("box-panel");box.style.display=DisplayStyle.Flex;
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(_=>CheckLater(box));
            CheckLater(box);
        }
        void CheckLater(VisualElement box) {
            rootVisualElement.schedule.Execute(()=>{
                Rect viewport=rootVisualElement.worldBound,bounds=box.worldBound;
                if(viewport.width<1 || bounds.width<1) return;
                bool full=Mathf.Abs(bounds.xMin-viewport.xMin)<1 && Mathf.Abs(bounds.xMax-viewport.xMax)<1
                    && Mathf.Abs(bounds.yMin-viewport.yMin)<1 && Mathf.Abs(bounds.yMax-viewport.yMax)<1;
                string result=(full?"PASS":"FAIL")+$" actual composed BOX viewport {viewport.size}: {bounds.size}";
                Directory.CreateDirectory("Temp/ProfileRefresh");File.AppendAllText("Temp/ProfileRefresh/box-viewport.txt",result+Environment.NewLine);
                if(full) Debug.Log(result);else Debug.LogError(result);
            }).StartingIn(150);
        }
    }
}

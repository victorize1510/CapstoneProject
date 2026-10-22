using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using Capstone.Game.MapSystem.UI;
using Capstone.Game.ProfileSystem;
using Capstone.Game.QuestSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UITK = UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    // Exercise actual scene owners and their disable/enable paths. No item use, rename,
    // evolution, quest completion or direct save writes are performed by this check.
    public static class PanelLifecycleValidation {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static bool running;
        static readonly List<string> results = new List<string>();

        [MenuItem("Game Tools/GameToolThang/HUD/Validate Panel Lifecycle in Play Mode")]
        public static async void Run() {
            if (!Application.isPlaying || running) { Debug.LogWarning("Enter Play Mode; run one panel check at a time."); return; }
            running = true; results.Clear();
            var menu = Object.FindFirstObjectByType<GameMenuController>();
            var inventory = Object.FindFirstObjectByType<MonsterInventoryController>();
            var profile = Object.FindFirstObjectByType<ProfilePanelController>();
            var map = Object.FindFirstObjectByType<MapInputController>();
            var controlLock = Object.FindFirstObjectByType<LocalPlayerControlLock>();
            try {
                Check(menu != null && inventory != null && profile != null && map != null && controlLock != null, "all scene panel owners exist");
                inventory.Close(); profile.Close(); map.CloseMap(); menu.CloseMenu(); await Settle();
                Unlocked(menu, controlLock, "initial gameplay");
                menu.OpenMenu(); await Settle();
                await Capture("TrainerMenu");
                Check(menu.IsMenuOpen && menu.AllowsCameraTracking && InventoryInputController.GameplayInputBlocked, "Trainer Menu keeps tracking while blocking gameplay");
                var panel = Field<RectTransform>(menu,"panel");
                Check(panel.parent.name == "TrainerMenuDesign" && panel.anchoredPosition.x >= TrainerMenuView.DesignWidth * .5f, "Trainer Menu v3 keeps the left world view clear");
                menu.OpenProfileDirect(); await Settle();
                await Capture("Profile");
                Check(profile.IsOpen && !menu.IsMenuOpen && !menu.AllowsCameraTracking, "Profile replaces Menu and blocks camera input");
                foreach (var selectable in Field<RectTransform>(profile,"root").GetComponentsInChildren<Selectable>(true))
                    Check(selectable.targetGraphic != null, "Profile graphic: " + selectable.name);
                Invoke(profile,"RequestBack"); await Settle();
                Check(menu.IsMenuOpen && !profile.IsOpen, "Profile Back returns to Trainer Menu");
                menu.OpenProfileDirect(); await Settle();
                profile.enabled = false; await Settle();
                Unlocked(menu, controlLock, "disabled Profile"); profile.enabled = true;
                menu.OpenMenu(); await Settle(); menu.OpenCodexDirect(); await Settle();
                Check(inventory.IsCodexOpen && !menu.IsMenuOpen && !menu.AllowsCameraTracking,"Codex replaces Menu and blocks camera input");
                Check(inventory.GetComponent<UIDocument>().rootVisualElement.Q<Label>("codex-empty").text=="Cẩm nang chưa có thông tin sinh vật.","runtime Codex has no Editor fixture");
                await Capture("Codex");
                Invoke(inventory,"HandleCodexBack"); await Settle();
                Check(menu.IsMenuOpen && !inventory.IsOpen,"Codex Back returns to Menu"); menu.CloseMenu(); await Settle();
                menu.OpenMenu(); await Settle(); menu.OpenInventoryDirect(); await Settle();
                inventory.RequestClose(); await Settle();
                Check(menu.IsMenuOpen && !inventory.IsOpen,"BAG Back returns to opening Menu"); menu.CloseMenu(); await Settle();
                menu.SetDialogueOpen(new object(),true); menu.enabled = false; await Settle();
                Check(!InventoryInputController.GameplayInputBlocked && !controlLock.IsLocked, "disabled router releases its dialogue lock");
                menu.enabled = true; await Settle();

                var document = inventory.GetComponent<UITK.UIDocument>();
                var callbacks = Field<IDictionary>(inventory,"controlCallbacks"); int callbackCount = callbacks.Count;
                Check(callbackCount > 0, "BAG controls are registered");
                for (int i=0; i<3; i++) {
                    menu.OpenInventoryDirect(); await Settle();
                    Check(inventory.IsOpen && InventoryInputController.GameplayInputBlocked, "BAG opens and locks, cycle " + i);
                    inventory.enabled = false; await Settle();
                    Check(!inventory.IsOpen && callbacks.Count == 0, "disabled BAG closes and detaches callbacks, cycle " + i);
                    Unlocked(menu,controlLock,"disabled BAG cycle " + i);
                    inventory.enabled = true; await Settle();
                    Check(callbacks.Count == callbackCount, "BAG callback count stays stable, cycle " + i);
                }
                menu.OpenInventoryDirect(); await Settle();
                await Capture("BAG");
                var search = document.rootVisualElement.Q<UITK.TextField>("inventory-search-field");
                string oldSearch = search.value;
                search.value = "__audit_no_matching_item__"; await Settle();
                Check(Field<IList>(inventory,"filteredItems").Count == 0, "BAG search still works after enable cycles");
                inventory.enabled=false; inventory.enabled=true; await Settle();
                Check(search.value == "__audit_no_matching_item__", "BAG search value survives controller rebind");
                search.value = oldSearch;
                inventory.Close(); await Settle();
                menu.OpenInventoryDirect(); await Settle();
                var inventoryInput = inventory.GetComponent<InventoryInputController>();
                inventoryInput.enabled = false; await Settle();
                Check(!inventory.IsOpen, "disabled inventory input closes BAG");
                Unlocked(menu,controlLock,"disabled inventory input"); inventoryInput.enabled=true;
                var party=inventory.GetComponent<PetPartyPanelController>();
                int tooltipCount=Field<IDictionary>(party,"tooltipCallbacks").Count;
                for(int i=0;i<3;i++) {
                    party.enabled=false;
                    Check(Field<IDictionary>(party,"tooltipCallbacks").Count==0,"PETS hover callbacks detach, cycle "+i);
                    party.enabled=true;
                    Check(Field<IDictionary>(party,"tooltipCallbacks").Count==tooltipCount,"PETS hover callback count stays stable, cycle "+i);
                }

                foreach (var entry in new[] { ("PETS", "pets-panel", (Action)menu.OpenPetsDirect), ("BOX", "box-panel", (Action)menu.OpenBoxDirect), ("QUEST", "quest-panel", (Action)menu.OpenQuestDirect) }) {
                    entry.Item3(); await Settle();
                    await Capture(entry.Item1);
                    var section = document.rootVisualElement.Q<UITK.VisualElement>(entry.Item2);
                    Check(section != null && section.resolvedStyle.display != UITK.DisplayStyle.None && inventory.IsOpen, entry.Item1 + " section is visible");
                    Check(InventoryInputController.GameplayInputBlocked && controlLock.IsLocked, entry.Item1 + " locks gameplay");
                    if (entry.Item1 == "PETS") {
                        Check(section.Q<UITK.VisualElement>("pets-party-row").childCount == 6, "PETS has six party cards");
                        var preview=Field<PetPortraitPreview>(party,"portraitPreview");
                        Check(preview.Texture!=null,"PETS owns a separate preview texture");
                        var previewCamera=Field<Camera>(preview,"camera"); int renders=0;
                        void CountPreview(UnityEngine.Rendering.ScriptableRenderContext context,Camera camera) { if(camera==previewCamera) renders++; }
                        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering+=CountPreview;
                        try { await Task.Delay(650); }
                        finally { UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=CountPreview; }
                        Check(renders>0 && renders<=9,"PETS preview renders at pose cadence: "+renders+" frames / 650 ms");
                    }
                    if (entry.Item1 == "BOX") {
                        var box = inventory.GetComponent<PetBoxPanelController>();
                        var firstCard = Field<IList>(box,"storageCards")[0];
                        inventory.Close();
                        Invoke(box,"HandleProviderChanged");
                        Check(ReferenceEquals(firstCard,Field<IList>(box,"storageCards")[0]), "hidden BOX does not rebuild its cards");
                    }
                    inventory.Close(); await Settle(); Unlocked(menu,controlLock,entry.Item1 + " closed");
                }
                map.OpenMap(); await Settle();
                await Capture("MAP");
                Check(map.IsOpen && InventoryInputController.GameplayInputBlocked, "MAP opens and locks gameplay");
                var presentation=Object.FindFirstObjectByType<WorldMapUIController>();
                var marker=presentation.RenderMarker(new MapMarkerData { id="audit-marker",displayName="Audit",type=MapMarkerType.Enemy,normalizedPosition=new Vector2(.8f,.8f) });
                Invoke(presentation,"SelectMarker",marker.Data); await Settle();
                presentation.SetFilter(MapFilterGroup.Enemy,false); await Settle();
                Check(!Field<TMPro.TMP_Text>(presentation,"selectedLabel").transform.parent.gameObject.activeSelf, "filtered marker hides its selection label");
                presentation.SetFilter(MapFilterGroup.Enemy,true); presentation.RemoveMarker("audit-marker");
                var testA=presentation.RenderMarker(new MapMarkerData { id="ui-audit-A",displayName="Kiểm tra A",type=MapMarkerType.Enemy,normalizedPosition=new Vector2(.12f,.94f) });
                presentation.RenderMarker(new MapMarkerData { id="ui-audit-B",displayName="Kiểm tra B",type=MapMarkerType.Enemy,normalizedPosition=new Vector2(.125f,.94f) });
                await Settle();
                var clusters=Field<object>(presentation,"clusters"); var pool=Field<IList>(clusters,"pool"); object chosen=null;
                foreach(var group in pool) foreach(var member in (List<MapMarkerView>)group.GetType().GetField("Members").GetValue(group))
                    if(member.Data.id=="ui-audit-A") chosen=group;
                Check(chosen!=null,"MAP overlapping markers share a group");
                Invoke(clusters,"Open",chosen); await Settle();
                var popup=Field<RectTransform>(clusters,"popup"); UnityEngine.UI.Button choice=null;
                foreach(var button in popup.GetComponentsInChildren<UnityEngine.UI.Button>()) if(button.GetComponentInChildren<TMPro.TMP_Text>()?.text=="Kiểm tra A") choice=button;
                Check(choice!=null,"MAP group lists exact marker name"); choice.onClick.Invoke(); await Settle();
                Check(Field<string>(presentation,"selectedId")=="ui-audit-A" && !popup.gameObject.activeSelf,"MAP group selects requested marker and closes");
                Check(Field<TMPro.TMP_Text>(presentation,"selectedLabel").transform.parent.gameObject.activeSelf,"grouped selected MAP marker retains its selection label");
                Invoke(clusters,"Open",chosen); await Settle(); presentation.SetFilter(MapFilterGroup.Enemy,false); await Settle();
                popup=Field<RectTransform>(clusters,"popup");
                Check(!popup.gameObject.activeSelf,"MAP filter closes group popup");
                presentation.SetFilter(MapFilterGroup.Enemy,true); presentation.RemoveMarker("ui-audit-A"); presentation.RemoveMarker("ui-audit-B");
                map.enabled=false; await Settle();
                Check(!map.IsOpen,"disabled MAP closes its screen");
                Unlocked(menu,controlLock,"disabled MAP"); map.enabled=true;
                Check(Object.FindFirstObjectByType<PetSummon.PetSummonHandSocket>().HeldBall == null,"idle player has no ball after panel transitions");
                var quest=AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/ThangTest/UIThang/Inventory/Q_Test_KillCube.asset");
                foreach (var reward in quest.Rewards) if (reward.RewardType==QuestRewardType.Item)
                    Check(reward.ItemDefinition != null && inventory.GetComponent<MonsterInventoryAdapter>().FindItemBase(reward.TargetId)!=null,"quest item reward has a persistent definition");
                Debug.Log("Panel lifecycle validation passed: " + results.Count + " checks; Menu/Profile/BAG/PETS/BOX/QUEST/MAP, rebinds, hidden updates and input restoration.");
            } catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); }
            finally {
                if (Application.isPlaying) {
                    if (profile != null) { profile.enabled=true; profile.Close(); }
                    if (inventory != null) { inventory.enabled=true; inventory.Close(); }
                    if (map != null) { map.enabled=true; map.CloseMap(); }
                    if (menu != null) { menu.enabled=true; menu.CloseMenu(); }
                }
                Directory.CreateDirectory("Temp/PanelAudit"); File.WriteAllLines("Temp/PanelAudit/validation.txt",results); running=false;
            }
        }
        static async Task Settle() { await Task.Delay(220); if (!Application.isPlaying) throw new InvalidOperationException("Play Mode ended during panel validation."); }
        static async Task Capture(string name) {
            Directory.CreateDirectory("../artifacts/ui-completion/screenshots");
            ScreenCapture.CaptureScreenshot("../artifacts/ui-completion/screenshots/"+name+".png"); await Settle();
        }
        static T Field<T>(object owner,string name) => (T)owner.GetType().GetField(name,Private).GetValue(owner);
        static void Invoke(object owner,string name,params object[] args) => owner.GetType().GetMethod(name,Private).Invoke(owner,args);
        static void Check(bool valid,string message) { if (!valid) throw new InvalidOperationException(message); results.Add("PASS " + message); }
        static void Unlocked(GameMenuController menu,LocalPlayerControlLock controlLock,string context) => Check(!InventoryInputController.GameplayInputBlocked && !controlLock.IsLocked && menu.CurrentMode==GameInputMode.Gameplay, context + " restores gameplay input");
    }
}

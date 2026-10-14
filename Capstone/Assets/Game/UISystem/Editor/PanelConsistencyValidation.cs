using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
using Capstone.Game.MapSystem.UI;
using Capstone.Game.QuestSystem.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Runtime layout regression; no inventory or save mutations.</summary>
    public static class PanelConsistencyValidation {
        const string Output = "../artifacts/panel-consistency/";
        const string AutoStop = "Capstone.PanelConsistencyValidation.AutoStop";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly List<string> results = new List<string>();
        static bool running;
        static void Check(bool valid,string label) {
            if(!valid) throw new InvalidOperationException(label);
            results.Add("PASS " + label);
        }
        static bool Same(Color a,Color b) => Mathf.Abs(a.r-b.r)<.006f && Mathf.Abs(a.g-b.g)<.006f && Mathf.Abs(a.b-b.b)<.006f && Mathf.Abs(a.a-b.a)<.006f;
        static void White(VisualElement root,string selector,string label) {
            var element=root.Q(className:selector);
            Check(element!=null && Same(element.resolvedStyle.backgroundColor,MenuPalette.Surface),label+" white "+selector);
        }
        static async Task Capture(string name) {
            Directory.CreateDirectory(Output+"screenshots");
            ScreenCapture.CaptureScreenshot(Output+"screenshots/"+name+".png");
            await Task.Delay(250);
        }

        [MenuItem("Game Tools/GameToolThang/HUD/Validate Panel Visual Consistency")]
        public static async void RunPlay() {
            if(running || !Application.isPlaying) return;
            running=true; results.Clear();
            var menu=Object.FindFirstObjectByType<GameMenuController>();
            var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();
            var doc=inventory.GetComponent<UIDocument>().rootVisualElement;
            var oldWidth=doc.style.width; var oldHeight=doc.style.height;
            try {
                var cases=new (string id,Action open,string[] surfaces)[] {
                    ("quest-panel",menu.OpenQuestDirect,new[]{"quest-list-column","quest-detail-column"}),
                    ("inventory-panel",menu.OpenInventoryDirect,new[]{"category-panel","inventory-grid-frame","inventory-detail-column"}),
                    ("pets-panel",menu.OpenPetsDirect,new[]{"pets-main-panel","pets-preview-panel","pets-information-panel","pets-stats-card"}),
                    ("box-panel",menu.OpenBoxDirect,new[]{"box-surface","box-selected-panel","box-storage-panel"}),
                    ("shop-panel",()=>menu.OpenShopFromWorld(),new[]{"shop-column"}),
                    ("settings-panel",menu.OpenSettingsDirect,new[]{"settings-group"}),
                    ("codex-panel",menu.OpenCodexDirect,new[]{"codex-column"}),
                };
                // Explicit logical viewports test flex layout independently of Game View scaling.
                foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1366,768)}) {
                    doc.style.width=size.x; doc.style.height=size.y;
                    foreach(var item in cases) {
                        inventory.Close(); menu.CloseMenu(); await Task.Delay(180);
                        item.open(); await Task.Delay(400);
                        var panel=doc.Q(item.id); var header=panel.Q(className:"screen-header");
                        Check(Same(panel.resolvedStyle.backgroundColor,MenuPalette.Background),size+" "+item.id+" warm outer background");
                        foreach(var surface in item.surfaces) White(panel,surface,size+" "+item.id);
                        var mark=header.Q(className:"screen-heading-mark");
                        var title=header.Q<Label>(className:"screen-title");
                        Check(Mathf.Abs(mark.resolvedStyle.width-24)<1 && Mathf.Abs(title.worldBound.x-header.worldBound.x-66)<1,size+" "+item.id+" aligned heading");
                        Check(header.Q<Button>(className:"screen-back").worldBound.xMax<=panel.worldBound.xMax+1,size+" "+item.id+" Back fits screen");
                        if(item.id=="quest-panel") {
                            var quest=inventory.GetComponent<QuestPanelController>();
                            typeof(QuestPanelController).GetMethod("SetEmptyDetail",Private).Invoke(quest,null);
                            await Task.Delay(100);
                            Check(panel.Q("quest-detail-scroll").resolvedStyle.display==DisplayStyle.None,size+" empty Quest hides complete scroll surface");
                            Check(panel.Q("quest-detail-empty").worldBound.height>=panel.Q("quest-detail-column").worldBound.height-4,size+" empty Quest fills white column");
                        }
                        if(item.id=="box-panel") {
                            var empty=panel.Q<Label>("box-empty-label");
                            Check(empty.resolvedStyle.display==DisplayStyle.None || empty.text.Contains("Không tìm"),size+" Box has no redundant empty-storage caption");
                        }
                        if(item.id=="pets-panel") {
                            var main=panel.Q(className:"pets-main-panel");
                            var preview=panel.Q("pets-selected-portrait");
                            var actions=panel.Q(className:"pets-actions-row");
                            Check(preview.worldBound.xMax<=panel.Q(className:"pets-information-panel").worldBound.x+1,size+" Pets columns do not overlap");
                            Check(actions.worldBound.yMax<=main.worldBound.yMax+1,size+" Pets actions remain visible");
                            Check(panel.Q(className:"pets-skills-header").worldBound.yMin>=panel.Q(className:"pets-stats-card").worldBound.yMax-1,size+" Pets stats do not overlap skills");
                            var statRows=panel.Query(className:"pets-stat-row").ToList();
                            Check(statRows.Count==8 && statRows.TrueForAll(row=>Mathf.Abs(row.worldBound.x-statRows[0].worldBound.x)<1),size+" Pets eight stats stay in one column");
                            Check(Mathf.Abs(panel.Q(className:"pet-party-card").resolvedStyle.height-(size.y<900?164:205))<1,size+" Pets party cards 20 percent taller");
                            var partyRow=panel.Q(className:"pets-party-row");
                            var partyHint=panel.Q(className:"pets-party-hint-row");
                            Check(partyRow.worldBound.yMax<=partyHint.worldBound.yMin+1
                                && partyHint.worldBound.yMax<=main.worldBound.yMin+1,size+" Pets entire party row and hints stay above main panel");
                            Check(actions.parent==panel.Q(className:"pets-information-panel"),size+" Pets action footer stays outside scrolling content");
                            var rt=preview.resolvedStyle.backgroundImage.renderTexture;
                            var party=inventory.GetComponent<PetPartyPanelController>();
                            Check(rt!=null && Mathf.Abs(rt.width/(float)rt.height-preview.resolvedStyle.width/preview.resolvedStyle.height)<.01f,size+" preview texture matches viewport aspect: texture="+(rt!=null?rt.width+"x"+rt.height:"null")+", viewport="+preview.resolvedStyle.width+"x"+preview.resolvedStyle.height);
                            VerifyPortrait(party,size.ToString());
                        }
                        if(size.x==1920) await Capture(item.id);
                    }
                }
                doc.style.width=oldWidth; doc.style.height=oldHeight;
                inventory.Close(); menu.CloseMenu(); await Task.Delay(180);
                menu.OpenProfileDirect(); await Task.Delay(400);
                var profile=Object.FindFirstObjectByType<ProfilePanelController>();
                var profileRoot=(RectTransform)typeof(ProfilePanelController).GetField("root",Private).GetValue(profile);
                Check(Same(profileRoot.GetComponent<UnityEngine.UI.Image>().color,MenuPalette.Background),"Profile warm outer background");
                var slots=profileRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                int teamCount=0;
                foreach(var slot in slots) {
                    if(!slot.name.StartsWith("TeamPetSlot_")) continue;
                    teamCount++;
                    Check(slot.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length==0,"Profile "+slot.name+" avatar only, no name or info");
                    Check(slot.GetComponent<UnityEngine.UI.RectMask2D>()!=null
                        && slot.GetComponentInChildren<UnityEngine.UI.AspectRatioFitter>(true).aspectMode==UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent,
                        "Profile "+slot.name+" avatar fills card without distortion");
                }
                Check(teamCount==6,"Profile has six avatar cards");
                await Capture("profile-panel");
                profile.Close(); await Task.Delay(180);
                menu.OpenInventoryDirect(); await Task.Delay(300);
                var heal=inventory.GetComponent<PetHealPanelController>();
                var bag=inventory.GetComponent<MonsterInventoryAdapter>();
                foreach(var item in bag.GetItems()) {
                    if(!item.IsRecoveryItem) continue;
                    string resource=item.EnergyRestoreAmount>0?"MP":"HP";
                    Check(heal.TryOpenFromInventory(item),item.Name+" opens recovery dialog from Bag");
                    await Task.Delay(180);
                    Check(doc.Q<Label>("heal-resource-title").text.StartsWith(resource),item.Name+" dialog shows correct resource");
                    Check(doc.Q<Label>("heal-item-effect").text.Contains(resource),item.Name+" dialog shows correct effect");
                    heal.Close();
                }
                inventory.Close(); await Task.Delay(180);
                menu.OpenMapDirect(); await Task.Delay(400);
                var map=Object.FindFirstObjectByType<WorldMapUIController>();
                Check(map!=null && Same(map.Theme.background,MenuPalette.Background),"Map shares warm background palette");
                var mapScreen=(RectTransform)typeof(WorldMapUIController).GetField("screen",Private).GetValue(map);
                var mapMark=(RectTransform)mapScreen.Find("TopHeader/Emblem");
                var mapTitle=(RectTransform)mapScreen.Find("TopHeader/Title");
                Check(mapMark.rect.width==24 && mapTitle.anchoredPosition.x==66,"Map heading mark and title align with panels");
                await Capture("map-panel");
                menu.OpenMenu(); await Task.Delay(180);
                var trainer=(RectTransform)typeof(GameMenuController).GetField("panel",Private).GetValue(menu);
                Check(Same(trainer.GetComponent<UnityEngine.UI.Image>().color,MenuPalette.Background),"Trainer Menu shares warm background palette");
                await Capture("trainer-menu");
                Debug.Log("Panel consistency: "+results.Count+" checks passed.");
            } catch(Exception error) { results.Add("FAIL "+error); Debug.LogException(error); await Capture("failure"); }
            finally {
                doc.style.width=oldWidth; doc.style.height=oldHeight;
                inventory.Close(); menu.CloseMenu();
                Directory.CreateDirectory(Output); File.WriteAllLines(Output+"runtime-validation.txt",results);
                running=false;
                if(SessionState.GetBool(AutoStop,false)) { SessionState.SetBool(AutoStop,false); EditorApplication.isPlaying=false; }
            }
        }
        static void VerifyPortrait(PetPartyPanelController party,string label) {
            var preview=(PetPortraitPreview)typeof(PetPartyPanelController).GetField("portraitPreview",Private).GetValue(party);
            var camera=(Camera)typeof(PetPortraitPreview).GetField("camera",Private).GetValue(preview);
            Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
            foreach(var filter in camera.transform.parent.GetComponentsInChildren<MeshFilter>()) {
                foreach(var vertex in filter.sharedMesh.vertices) {
                    Vector3 point=camera.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                    min=Vector2.Min(min,point); max=Vector2.Max(max,point);
                }
            }
            Check(min.x>=.01f && min.y>=.01f && max.x<=.99f && max.y<=.99f,label+" preview contains complete animated silhouette");
            Check(Vector2.Distance((min+max)*.5f,Vector2.one*.5f)<.02f,label+" preview silhouette centered");
        }
        [InitializeOnLoadMethod] static void Initialize() { EditorApplication.update-=Poll; EditorApplication.update+=Poll; }
        static void Poll() {
            if(running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            const string start="Temp/StartPanelConsistency.request",run="Temp/RunPanelConsistency.request";
            if(!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(start)) {
                try { File.Delete(start); }
                catch (IOException) { return; }
                SessionState.SetBool(AutoStop,true); File.WriteAllText(run,"Run"); EditorApplication.isPlaying=true;
            }
            if(Application.isPlaying && File.Exists(run)) {
                try { File.Delete(run); }
                catch (IOException) { return; }
                RunPlay();
            }
        }
    }
}

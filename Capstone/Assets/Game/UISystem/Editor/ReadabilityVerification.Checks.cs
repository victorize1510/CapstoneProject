using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.Inventory.Bag.Editor;
using Capstone.Game.ProfileSystem;
using Capstone.Game.QuestSystem.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    public static partial class ReadabilityVerification {
        static bool Visible(VisualElement element) { for(var e=element;e!=null;e=e.parent)if(e.resolvedStyle.display==DisplayStyle.None||e.resolvedStyle.visibility==Visibility.Hidden)return false;return element!=null; }
        static readonly List<string> hits=new List<string>();
        static void AuditToolkit(VisualElement root,string panel) {
            int tested=0,misses=0;
            foreach(var button in root.Query<Button>().ToList()) {
                if(!Visible(button)||!button.enabledInHierarchy||button.worldBound.width<10||button.worldBound.height<10)continue;
                foreach(float y in new[]{.2f,.5f,.8f})foreach(float x in new[]{.2f,.5f,.8f}) {
                    var local=new Vector2(button.layout.width*x,button.layout.height*y);if(!button.ContainsPoint(local))continue;
                    var world=button.LocalToWorld(local);bool clipped=false;
                    for(var p=button.parent;p!=null;p=p.parent)if((p is ScrollView sv&&!sv.contentViewport.worldBound.Contains(world))||((p.style.overflow.value==Overflow.Hidden||p.ClassListContains("unity-scroll-view__content-viewport"))&&!p.worldBound.Contains(world)))clipped=true;
                    if(clipped||!root.worldBound.Contains(world))continue;
                    var picked=button.panel.Pick(world);bool pass=picked==button||button.Contains(picked);tested++;if(!pass)misses++;
                    hits.Add(FormattableString.Invariant($"{panel},{button.name},{x},{y},{pass},\"{PathOf(picked)}\""));
                }
            }
            checks.Add("AUDIT "+panel+" Toolkit samples="+tested+" misses="+misses);
            File.WriteAllLines(Root+"button-audit.csv",hits);
        }
        static void AuditNative(ProfilePanelController profile) {
            var root=(RectTransform)Get(profile,"root");var canvas=root.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var results=new List<RaycastResult>();int tested=0,misses=0;
            foreach(var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(false)) {
                if(!button.IsInteractable())continue;var rect=(RectTransform)button.transform;var corners=new Vector3[4];rect.GetWorldCorners(corners);
                foreach(float y in new[]{.2f,.5f,.8f})foreach(float x in new[]{.2f,.5f,.8f}) {
                    var p=RectTransformUtility.WorldToScreenPoint(camera,Vector3.Lerp(Vector3.Lerp(corners[0],corners[3],x),Vector3.Lerp(corners[1],corners[2],x),y));
                    var contour=button.GetComponent<ProfileR2HitArea>();if(contour!=null&&!contour.IsRaycastLocationValid(p,camera))continue;
                    var data=new PointerEventData(EventSystem.current){position=p};results.Clear();EventSystem.current.RaycastAll(data,results);
                    var picked=results.FirstOrDefault(r=>r.gameObject!=null).gameObject;bool pass=picked!=null&&picked.GetComponentInParent<UnityEngine.UI.Button>()==button;tested++;if(!pass)misses++;
                    hits.Add(FormattableString.Invariant($"Profile,{button.name},{x},{y},{pass},\"{(picked!=null?picked.name:"null")}\""));
                }
            }
            checks.Add("AUDIT Profile uGUI samples="+tested+" misses="+misses);
        }
        static async Task AfterChecks(GameMenuController menu,MonsterInventoryController bag,ProfilePanelController profile,VisualElement ui) {
            hits.Clear();hits.Add("panel,button,fx,fy,picks_button,path");var party=Object.FindFirstObjectByType<PetPartyPanelController>();
            menu.OpenPetsDirect();await Task.Delay(250);
            Check(ui.Q("pets-summoned-badge")==null,"view-only badge removed");
            Check(ui.Q("pets-mode-2d")==null&&ui.Q("pets-mode-3d")==null&&ui.Q<Button>("pets-mode-toggle")!=null,"one preview-mode control");
            Check(!party.IsPreview3D,"default 2D after open");
            await StateBounds(ui);
            var toggle=ui.Q<Button>("pets-mode-toggle");var rect=toggle.worldBound;BagVerification.Click(ui,"pets-mode-toggle");await Task.Delay(450);
            Check(party.IsPreview3D&&party.HasPreviewTexture&&ui.Q<Label>("pets-preview-mode-text").text=="3D","single control enables explicit 3D");
            await Shot("after","pet-preview3d");
            BagVerification.Click(ui,"pets-mode-toggle");await Task.Delay(220);Check(!party.HasPreviewTexture&&!party.IsPreview3D,"same control returns to 2D and releases preview");Check(toggle.worldBound==rect,"mode geometry stable");
            var preview=Get(party,"portraitPreview");var viewed=(PetController)Get(party,"viewedPet");var metadata=viewed.GetComponentInChildren<PetCollectionMetadata>(true);
            preview.GetType().GetField("source",Any).SetValue(preview,viewed);preview.GetType().GetField("metadata",Any).SetValue(preview,metadata);preview.GetType().GetField("form",Any).SetValue(preview,metadata.CurrentFormId);preview.GetType().GetField("stage",Any).SetValue(preview,new GameObject("QA empty render stage"){hideFlags=HideFlags.HideAndDontSave});party.SetPreview3D(true);await Task.Delay(250);
            Check(!party.IsPreview3D&&!party.HasPreviewTexture,"injected empty renderer returns to 2D and releases stage");
            await PreservationChecks(ui,party);
            var name=ui.Q<Label>("pets-selected-name");string originalName=name.text;name.text=new string('W',60);await Task.Delay(120);Check(name.worldBound.xMax<=ui.Q("pets-rename-button").worldBound.xMin,"long name stays clear of rename/favorite");name.text=originalName;
            BagVerification.Click(ui,"pets-tab-skills");await Task.Delay(250);var rail=ui.Q("companion-filter-rail");Check(rail!=null&&ui.Q<ScrollView>("green-filter-viewport")!=null,"skill filters have framed viewport");
            var viewport=ui.Q<ScrollView>("green-filter-viewport");float before=viewport.scrollOffset.x;BagVerification.Click(ui,"skills-filter-next");await Task.Delay(180);Check(viewport.scrollOffset.x>before,"filter rail next scrolls");
            BagVerification.Click(ui,"skills-filter-dark");await Task.Delay(150);Check(ui.Q("skills-filter-dark").ClassListContains("is-selected"),"filter preserves existing selection callback");AuditToolkit(ui.Q("pets-panel"),"Pet-skills");
            BagVerification.Click(ui,"pets-tab-growth");await Task.Delay(200);
            foreach(string id in new[]{"pets-growth-level","pets-growth-heal","pets-growth-evolution"}) {
                if(!ui.Q<Button>(id).enabledInHierarchy){checks.Add("UNAVAILABLE "+id+" preview: current runtime pet has no configured next evolution; disabled state preserved");continue;}
                ui.Q<ScrollView>("pets-tab-body-growth").ScrollTo(ui.Q(id));await Task.Delay(200);BagVerification.Click(ui,id);await Task.Delay(180);
                bool opened=id.EndsWith("level")?Object.FindFirstObjectByType<PetLevelUpPanelController>().IsOpen:id.EndsWith("heal")?Object.FindFirstObjectByType<PetHealPanelController>().IsOpen:Object.FindFirstObjectByType<PetEvolutionPanelController>().IsOpen;
                Check(opened,id+" existing preview callback opens its popup");menu.HandleShortcut(KeyCode.Escape);await Task.Delay(180);
            }
            BagVerification.Click(ui,"pets-tab-overview");await Task.Delay(220);BagVerification.Click(ui,"pets-details-button");await Task.Delay(220);
            Check(ui.Q("pet-details-genetics-table").childCount==3,"genetics table contains heading plus IV/EV rows");
            Check(ui.Q<Label>("pet-details-nature").text!="Serious","Nature value localized");Check(ui.Q<Label>("pet-details-id").tooltip==ui.Q<Label>("pet-details-id").text,"full ID kept in tooltip");
            Check(!ui.Q("pet-details-dialog").Query<Label>().ToList().Any(l=>l.text.Contains("CODEX / EVOLUTION")||l.text.Contains("A young dusk dragon")),"details headings and lore localized");
            AuditToolkit(ui.Q("pet-details-dialog"),"Pet-details");party.CloseDetails();bag.Close();
            menu.OpenInventoryDirect();await Task.Delay(220);Call(bag,"SelectItem",-1,false);await Task.Delay(180);Check(Visible(ui.Q("bag-detail-empty-group")),"Bag empty inspector cluster visible without selection");
            var search=ui.Q<TextField>("inventory-search-field");var clear=ui.Q<Button>("inventory-search-field-clear");Check(!Visible(clear),"search clear hidden for empty query");search.value="QA-no-item-match";await Task.Delay(180);
            Check(Visible(clear)&&ui.Q("bag-search-group").worldBound.Contains(clear.worldBound.center),"search clear is inside one search field");BagVerification.Click(ui,"inventory-search-field-clear");await Task.Delay(180);Check(search.value=="","search clear uses existing callback");
            bag.SelectNextItem();await Task.Delay(150);var selection=bag.SelectedItem;BagVerification.Click(ui,"inventory-view-button");await Task.Delay(180);Check(bag.SelectedItem==selection,"grid/list selection preserved");AuditToolkit(ui.Q("inventory-panel"),"Bag-list");BagVerification.Click(ui,"inventory-view-button");await Task.Delay(180);AuditToolkit(ui.Q("inventory-panel"),"Bag-grid");bag.Close();
            menu.OpenQuestDirect();await Task.Delay(220);AuditToolkit(ui.Q("quest-panel"),"Quest");bag.Close();
            menu.OpenQuestDirect();var quest=Object.FindFirstObjectByType<QuestPanelController>();Call(quest,"SelectTab",Enum.Parse(Get(quest,"currentTab").GetType(),"Main"));await Shot("after","quest-main");bag.Close();
            menu.OpenProfileDirect();await Task.Delay(200);var profileRoot=(RectTransform)Get(profile,"root");Check(profileRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true).All(b=>b.name!="ChangeAvatarButton"),"duplicate avatar action removed from generator");
            var cameraButton=profileRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="AvatarCameraButton");cameraButton.onClick.Invoke();await Task.Delay(180);Check(((RectTransform)Get(profile,"avatarDialog")).gameObject.activeSelf,"camera opens existing picker");Call(profile,"HideAvatarDialog");AuditNative(profile);profile.Close();
            foreach(var resolution in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
            var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var size=view.GetType().GetProperty("selectedSizeIndex",Any);TrainerMenuPolishCapture.SelectSize(view,size,resolution);await Task.Delay(200);
            foreach(string panel in new[]{"Box","Codex","Shop","Settings"}) {
                bag.Close();if(panel=="Box")bag.OpenPetBoxPanel();else if(panel=="Codex")bag.OpenCodexPanel();else if(panel=="Shop")bag.OpenShopPanel();else bag.OpenSettingsPanel(()=>"Hướng dẫn kiểm thử");
                await Task.Delay(250);var root=ui.Q(panel.ToLowerInvariant()+"-panel");Check(Visible(root),panel+" runtime panel opens");AuditToolkit(root,panel);await Shot("after",panel.ToLowerInvariant());
            }
            bag.Close();menu.OpenMapDirect();await Shot("after","map");Call(menu,"CloseMap");menu.CloseMenu();await Shot("after","hud");menu.OpenMenu();await Shot("after","menu");menu.CloseMenu();
            }
            int elements=ui.Query<VisualElement>().ToList().Count;
            for(int cycle=0;cycle<5;cycle++){menu.OpenPetsDirect();await Task.Delay(120);party.SetPreview3D(true);await Task.Delay(120);bag.Close();menu.CloseMenu();await Task.Delay(120);Check(!party.HasPreviewTexture&&!party.IsPreview3D,"close cycle "+cycle+" releases 3D");}
            Check(ui.Query<VisualElement>().ToList().Count==elements,"UI element count stable after five open/close cycles");
            foreach(var hud in Object.FindObjectsByType<GameplayHudController>(FindObjectsInactive.Include,FindObjectsSortMode.None)){var group=hud.PresentationRoot.GetComponent<CanvasGroup>();Check(group==null||group.alpha>0,"gameplay HUD alpha restored after close cycles");}
            File.WriteAllLines(Root+"button-audit.csv",hits);
        }
        static async Task PreservationChecks(VisualElement ui,PetPartyPanelController party){
            var provider=Object.FindFirstObjectByType<PetCommandHudProvider>();var command=Get(provider,"petCommandInput");var active=Get(command,"activePet");var first=provider.GetPetControllerAt(0);var second=provider.GetPetControllerAt(1);var cards=(System.Collections.Generic.List<Button>)Get(party,"partyCards");
            void Click(Button b){using(var click=ClickEvent.GetPooled()){click.target=b;b.SendEvent(click);}}
            Click(cards[1]);await Task.Delay(180);Check(ReferenceEquals(Get(party,"viewedPet"),second)&&ReferenceEquals(Get(command,"activePet"),active),"avatar click changes viewed pet without summon");
            BagVerification.Click(ui,"pets-swap-button");Click(cards[0]);await Task.Delay(180);Check(provider.GetPetControllerAt(0)==first&&provider.GetPetControllerAt(1)==second,"swap preview does not change roster");BagVerification.Click(ui,"r3-swap-confirm");await Task.Delay(300);Check(provider.GetPetControllerAt(0)==second&&provider.GetPetControllerAt(1)==first,"swap confirmation saves isolated roster");
            BagVerification.Click(ui,"pets-swap-button");Click(cards[1]);await Task.Delay(180);BagVerification.Click(ui,"r3-swap-confirm");await Task.Delay(300);Check(provider.GetPetControllerAt(0)==first&&provider.GetPetControllerAt(1)==second,"swap test restores roster through same confirmation");
            BagVerification.Click(ui,"pets-tab-skills");await Task.Delay(250);var skills=Object.FindFirstObjectByType<PetSkillsPanelController>();string Id()=>((System.Collections.Generic.List<SkillHudData>)Get(skills,"equippedSnapshot"))[0].skillId;string original=Id();
            Call(skills,"SelectLearnedSkill",1);Call(skills,"EquipSelectedInto",0);await Task.Delay(150);Check(Id()==original,"equip preview leaves current skill unchanged");BagVerification.Click(ui,"r3-equip-confirm");await Task.Delay(300);Check(Id()!=original,"equip confirmation changes isolated loadout");
            Call(skills,"SelectLearnedSkill",0);Call(skills,"EquipSelectedInto",0);await Task.Delay(150);BagVerification.Click(ui,"r3-equip-confirm");await Task.Delay(300);Check(Id()==original,"equip test restores original first skill");BagVerification.Click(ui,"pets-tab-overview");await Task.Delay(220);
        }
        static async Task StateBounds(VisualElement ui){
            var rows=new List<string>{"control,state,part,x,y,w,h,font_size,font_style"};float maxDelta=0;float scale=Screen.width/ui.panel.visualTree.worldBound.width;
            foreach(string id in new[]{"pets-mode-toggle","pets-rename-button","pets-favorite-button","pets-tab-overview","pets-tab-skills","pets-tab-growth"}){
                var b=ui.Q<AngularButton>(id);var nodes=new List<VisualElement>{b};nodes.AddRange(b.Query<VisualElement>().ToList().Where(e=>e is Label||e.ClassListContains("companion-icon")));var normal=nodes.Select(e=>e.worldBound).ToArray();bool selected=b.ClassListContains("is-selected");
                foreach(string state in new[]{"normal","hover","pressed","selected","focus"}){b.Surface.Hovered=state=="hover";b.Surface.Pressed=state=="pressed";b.Surface.IsFocused=state=="focus";b.EnableInClassList("is-selected",state=="selected");b.Surface.InvalidateFeedback();await Task.Delay(160);
                    for(int i=0;i<nodes.Count;i++){var e=nodes[i];var r=e.worldBound;var n=normal[i];maxDelta=Mathf.Max(maxDelta,Mathf.Abs(r.x-n.x)*scale,Mathf.Abs(r.y-n.y)*scale,Mathf.Abs(r.width-n.width)*scale,Mathf.Abs(r.height-n.height)*scale);rows.Add(FormattableString.Invariant($"{id},{state},{i},{r.x*scale},{r.y*scale},{r.width*scale},{r.height*scale},{e.resolvedStyle.fontSize},{e.resolvedStyle.unityFontStyleAndWeight}"));}}
                b.Surface.Hovered=b.Surface.Pressed=b.Surface.IsFocused=false;b.EnableInClassList("is-selected",selected);b.Surface.InvalidateFeedback();
            }
            File.WriteAllLines(Root+"interaction-bounds.csv",rows);Check(maxDelta<.01f,"normal/hover/pressed/selected/focus maximum bounds delta px="+maxDelta);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;
using Button=UnityEngine.UIElements.Button;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Explicit file-request QA only. Captures the actual engine, never a mockup renderer.</summary>
    public static class GreenAngularVerification {
        public const string Output="../artifacts/ui-r3-fixes-2026-10-20/geometry/";
        const string Request="Temp/VerifyGreenAngular.request",OwnPlay="GreenAngularVerification.OwnPlay";
        const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        static bool running;
        static readonly List<string> log=new List<string>();
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll(){
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            if(!EditorApplication.isPlaying){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(OwnPlay,true);EditorApplication.EnterPlaymode();return;}
            if(Object.FindFirstObjectByType<GameMenuController>()==null)return;File.Delete(Request);Run();
        }
        static void Check(bool condition,string message){log.Add((condition?"PASS ":"FAIL ")+message);File.WriteAllLines(Output+"validation-progress.txt",log);}
        static void Click(Button b){if(b==null||!b.enabledInHierarchy)throw new Exception("Button unavailable: "+b?.name);b.Focus();typeof(Clickable).GetMethod("Invoke",Any).Invoke(b.clickable,new object[]{null});}
        [Serializable]sealed class BoundsEntry{public string id;public float x,y,width,height;}
        [Serializable]sealed class BoundsDocument{public int width,height;public string scene,mode,provider,selectedPet,eightStats;public BoundsEntry[] boxes;}
        static List<BoundsEntry> PopupBounds(VisualElement root,VisualElement dialog,string screen){float s=Screen.width/root.worldBound.width;var list=new List<BoundsEntry>();void Add(string id,VisualElement e){if(e==null)return;var r=e.worldBound;list.Add(new BoundsEntry{id=id,x=r.x*s,y=r.y*s,width=r.width*s,height=r.height*s});}Add("modal",dialog);
            var map=new Dictionary<string,string>();if(screen.StartsWith("05")){map.Add("summary","ga-details-summary");map.Add("metadata","ga-details-basic");map.Add("stats","ga-details-stats");map.Add("advanced","ga-details-advanced");map.Add("skills","ga-details-skills");}
            else if(screen.StartsWith("06")){map.Add("summary","ga-summary");map.Add("stats-before-after","ga-level-stats");map.Add("new-skills","ga-level-new");map.Add("evolution","ga-level-evolution");map.Add("resources","ga-level-item");map.Add("confirm","ga-level-confirm");}
            else if(screen.StartsWith("07")){map.Add("summary","ga-summary");map.Add("preview","ga-heal-preview");map.Add("item","ga-heal-item");map.Add("cancel","ga-heal-cancel");map.Add("confirm","ga-heal-confirm");}
            else{map.Add("summary","ga-summary");map.Add("input","ga-rename-input");map.Add("helper","ga-rename-helper");map.Add("cancel","ga-rename-cancel");map.Add("confirm","ga-rename-save");}foreach(var p in map)Add(p.Key,dialog.Q(className:p.Value));return list;
        }
        static List<BoundsEntry> ToolkitBounds(VisualElement root,VisualElement panel){float s=Screen.width/root.worldBound.width;var list=new List<BoundsEntry>();
            var ids=new Dictionary<string,string>{{"companion-hero","hero"},{"green-inspector-union","inspector-fixed"},{"companion-tabs","navigation-rail"},{"pets-tab-overview","nav-overview"},{"pets-tab-skills","nav-skills"},{"pets-tab-growth","nav-growth"},{"companion-dock","roster"},{"pets-mode-2d","mode-2d"},{"pets-mode-3d","mode-3d"},{"companion-stats","stats"},{"companion-skill-summary","skills"},{"companion-actions","actions"},{"companion-growth-level","growth-0"},{"companion-growth-heal","growth-1"},{"companion-growth-evolution","growth-2"},{"green-learned-panel","learned"}};
            foreach(var item in ids){var e=panel.Q(item.Key);if(e==null||e.resolvedStyle.display==DisplayStyle.None)continue;var r=e.worldBound;list.Add(new BoundsEntry{id=item.Value,x=r.x*s,y=r.y*s,width=r.width*s,height=r.height*s});}
            int i=0;foreach(var e in panel.Query<Button>(className:"pet-party-card").ToList()){var r=e.worldBound;list.Add(new BoundsEntry{id="slot-"+(++i),x=r.x*s,y=r.y*s,width=r.width*s,height=r.height*s});}return list;
        }
        static List<BoundsEntry> MenuBounds(GameMenuController menu){var ids=new Dictionary<string,string>{{"MenuShell","shell"},{"TrainerSummary","profile"},{"PetsButton","pet-card"},{"InventoryButton","bag-card"},{"ProfileButton","profile-button"},{"QuestButton","quest-button"},{"MapButton","map-button"},{"CodexButton","codex-button"},{"QuickInfo","status"},{"SettingsButton","settings"},{"BackButton","back"}};
            var list=new List<BoundsEntry>();var root=(RectTransform)typeof(GameMenuController).GetField("root",Any).GetValue(menu);foreach(var e in root.GetComponentsInChildren<RectTransform>()){if(!ids.TryGetValue(e.name,out var id))continue;var c=new Vector3[4];e.GetWorldCorners(c);var a=RectTransformUtility.WorldToScreenPoint(null,c[1]);var b=RectTransformUtility.WorldToScreenPoint(null,c[3]);list.Add(new BoundsEntry{id=id,x=a.x,y=Screen.height-a.y,width=b.x-a.x,height=a.y-b.y});}return list;
        }
        static async Task Capture(string name,List<BoundsEntry> boxes,string mode="2D"){
            await Task.Delay(250);string stem=name+"-"+Screen.width+"x"+Screen.height;
            ScreenCapture.CaptureScreenshot(Output+"captures/"+stem+".png");await Task.Delay(300);
            var owner=Object.FindFirstObjectByType<MonsterInventoryController>()?.GetComponent<PetPartyPanelController>();var provider=owner!=null?typeof(PetPartyPanelController).GetField("petHudProvider",Any)?.GetValue(owner) as MonoBehaviour:null;var document=Object.FindFirstObjectByType<MonsterInventoryController>()?.GetComponent<UIDocument>()?.rootVisualElement;
            File.WriteAllText(Output+"captures/"+stem+".json",JsonUtility.ToJson(new BoundsDocument{width=Screen.width,height=Screen.height,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,mode=mode,provider=provider!=null?provider.GetType().Name:"missing",selectedPet=document?.Q<Label>("pets-selected-name")?.text,eightStats=string.Join(";",new[]{"health","attack","magic-attack","defense","magic-defense","speed","critical-rate","critical-damage"}.Select(k=>k+"="+document?.Q<Label>("pets-stat-"+k)?.text)),boxes=boxes.ToArray()},true));log.Add("CAPTURE "+stem+" actual Game view; "+mode);
        }
        static void CornerTests(VisualElement root){foreach(var b in root.Query<AngularButton>().ToList()){
            if(b.resolvedStyle.display==DisplayStyle.None||b.worldBound.width<=0||b.worldBound.height<=0)continue;
            Check(!b.ContainsPoint(new Vector2(1,1)),"transparent corner rejected "+b.name);
            var corner=b.panel.Pick(b.LocalToWorld(new Vector2(1,1)));Check(corner!=b&&corner?.GetFirstAncestorOfType<Button>()!=b,"native Pick rejects transparent corner "+b.name);
            Check(b.resolvedStyle.backgroundColor.a<.01f&&b.resolvedStyle.borderTopWidth==0,"no native rectangular background/focus "+b.name);
            if(b.enabledInHierarchy&&b.worldBound.Overlaps(root.worldBound)){var picked=b.panel.Pick(b.worldBound.center);Check(picked==b||picked?.GetFirstAncestorOfType<Button>()==b,"center native Pick "+b.name);}
        }}
        static async Task StateFixture(PetPartyPanelController party,VisualElement root,VisualElement panel){
            var field=typeof(PetPartyPanelController).GetField("petHudProvider",Any);var auto=typeof(PetPartyPanelController).GetField("autoFindProvider",Any);var saved=field.GetValue(party);var savedAuto=auto.GetValue(party);var live=saved as IPetHudProvider;if(live==null)return;
            var slots=new List<PetSlotHudData>(live.GetPetSlots());if(slots.Count<2||!slots[0].occupied||!slots[1].occupied)return;
            var fixture=new GameObject("GreenAngularReadonlyStateFixture"){hideFlags=HideFlags.DontSave};try{
                var provider=fixture.AddComponent<GreenAngularReadonlyHudProvider>();provider.enabled=false;var first=slots[0];first.selected=false;first.summoned=true;first.favorite=false;slots[0]=first;var second=slots[1];second.selected=true;second.summoned=false;second.favorite=true;slots[1]=second;
                provider.Slots=slots;provider.Skills=live.GetSkills();provider.Status=new PetStatusHudData{hasPet=second.occupied,displayName=second.displayName,level=second.level,health=second.health,maxHealth=second.maxHealth,icon=second.icon};auto.SetValue(party,false);field.SetValue(party,provider);party.Refresh();await Task.Delay(150);
                var cards=panel.Query<Button>(className:"pet-party-card").ToList();Check(cards[0].ClassListContains("is-summoned")&&!cards[0].ClassListContains("is-selected")&&cards[1].ClassListContains("is-selected"),"read-only view fixture separates summoned, selected and favorite");Check(cards[0].Q<AngularSurface>("companion-active")?.resolvedStyle.display==DisplayStyle.Flex,"summoned uses visible polygon/check marker");
                await Capture("fixture_selected_summoned_favorite",ToolkitBounds(root,panel),"2D VIEW-ONLY fixture, copied roster; no provider/save commands");
            }finally{field.SetValue(party,saved);auto.SetValue(party,savedAuto);party.Refresh();Object.Destroy(fixture);}
        }
        static async void Run(){running=true;log.Clear();Directory.CreateDirectory(Output+"captures");
            var menu=Object.FindFirstObjectByType<GameMenuController>();var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();var party=inventory.GetComponent<PetPartyPanelController>();var doc=inventory.GetComponent<UIDocument>();var root=doc.rootVisualElement;var panel=root.Q("pets-panel");
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",Any);int previous=(int)selected.GetValue(view);
            var hudFixture=new GameObject("GreenAngularHUDVisibilityFixture",typeof(TextMesh));var hudRenderer=hudFixture.GetComponent<Renderer>();
            try{
                await Task.Delay(1000);inventory.Close();menu.CloseMenu();
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    TrainerMenuPolishCapture.SelectSize(view,selected,size);view.Show();view.Focus();await Task.Delay(600);Check(Screen.width==size.x&&Screen.height==size.y,"actual resolution "+size);
                    menu.HandleShortcut(KeyCode.Tab);await Task.Delay(250);Check(menu.IsMenuOpen,"Tab opens Trainer Menu");menu.HandleShortcut(KeyCode.Tab);await Task.Delay(250);Check(!menu.IsMenuOpen,"second Tab closes Trainer Menu");
                    menu.OpenMenu();await Task.Delay(350);EventSystem.current?.SetSelectedGameObject(null);Check(hudRenderer.forceRenderingOff,"Trainer Menu suppresses world HUD");
                    var menuRoot=(RectTransform)typeof(GameMenuController).GetField("root",Any).GetValue(menu);
                    Check(!menuRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="EditNameButton"||t.name=="BoxButton"),"Menu has no player rename or BOX");
                    Check(!menuRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="AvatarPlaceholder"),"display-only avatar has no representative glyph");
                    var avatarImage=menuRoot.GetComponentsInChildren<UnityEngine.UI.Image>().FirstOrDefault(i=>i.name=="Avatar");
                    Check(avatarImage!=null&&!avatarImage.raycastTarget&&avatarImage.GetComponent<UnityEngine.UI.Selectable>()==null,"avatar cannot click/focus/edit");
                    for(int active=0;active<3;active++){var contour=GreenAngularGeometry.Polygon("union",864,620,3,active);Check(Enumerable.Range(0,3).All(j=>GreenAngularGeometry.Contains(contour,new Vector2(152+j*280,60))),"all three tab feet inside common contour active="+active);}
                    var oct=GreenAngularGeometry.Polygon("skill-oct",66,66);Check(oct.Length==8&&oct[0].x<16&&oct[1].x>50,"skill is square with four small corner cuts");
                    foreach(var g in menuRoot.GetComponentsInChildren<GreenAngularGraphic>()){if(!g.raycastTarget)continue;Vector3[] c=new Vector3[4];g.rectTransform.GetWorldCorners(c);var q=RectTransformUtility.WorldToScreenPoint(null,c[1])+new Vector2(1,-1);Check(!g.IsRaycastLocationValid(q,null),"uGUI transparent corner rejects "+g.name);}
                    await Capture("01_Menu",MenuBounds(menu));menu.CloseMenu();menu.OpenPetsDirect();await Task.Delay(400);
                    Check(!party.IsPreview3D&&!party.HasPreviewTexture,"default 2D allocates no preview RT "+size);
                    Check(panel.Query<Button>(className:"pet-party-card").ToList().Count==6,"six roster slots");
                    var statNames=panel.Query<Label>(className:"companion-stat-name").ToList().Select(l=>l.text).ToArray();Check(statNames.Contains("CRIT RATE")&&statNames.Contains("CRIT DMG"),"current CRIT RATE / CRIT DMG display labels");
                    for(int tab=0;tab<3;tab++){Click(root.Q<Button>(new[]{"pets-tab-overview","pets-tab-skills","pets-tab-growth"}[tab]));await Task.Delay(300);Check(party.CompanionTab==tab,"tab route "+tab);Check(panel.Q<AngularSurface>("green-inspector-union").ActiveTab==tab,"union contour family "+tab);await Capture(new[]{"02_Tong_Quan","03_Ky_Nang","04_Phat_Trien"}[tab],ToolkitBounds(root,panel));}
                    var legacySkills=root.Q("skills-modal-dialog");Check(legacySkills!=null&&legacySkills.Query<AngularButton>().ToList().All(b=>b.Surface.style.display.value==DisplayStyle.None&&b.Q<Label>(className:"angular-button-caption").style.display.value==DisplayStyle.None),"non-embedded skills keep native text/background without duplicate caption");
                    foreach(var action in new[]{new[]{"pets-growth-level","level-up-modal-overlay","06_Tang_Cap"},new[]{"pets-growth-heal","heal-modal-overlay","07_Hoi_Phuc"}}){Click(root.Q<Button>(action[0]));await Task.Delay(300);var ov=root.Q(action[1]);Check(ov.resolvedStyle.display!=DisplayStyle.None,"popup opens "+action[2]);var d=ov.Q(className:"ga-popup");float s=Screen.width/root.worldBound.width;var r=d.worldBound;
                        if(action[2].StartsWith("06")){var owner=inventory.GetComponent<PetLevelUpPanelController>();var target=(PetController)typeof(PetLevelUpPanelController).GetField("targetPet",Any).GetValue(owner);var preview=inventory.GetComponent<PetLevelUpService>().CreatePreview(target);if(preview.OwnedItemQuantity<preview.RequiredItemQuantity)Check(!ov.Q<Button>("level-up-confirm-button").enabledInHierarchy&&!string.IsNullOrWhiteSpace(ov.Q<Label>("level-up-feedback").text),"actual missing level item disables confirm with reason");}
                        if(action[2].StartsWith("07")){var owner=inventory.GetComponent<PetHealPanelController>();var target=(PetController)typeof(PetHealPanelController).GetField("targetPet",Any).GetValue(owner);var preview=inventory.GetComponent<PetHealService>().CreatePreview(target);if(preview.IsFullHealth)Check(!ov.Q<Button>("heal-confirm-button").enabledInHierarchy&&ov.Q<Label>("heal-feedback").text==preview.DisabledReason,"actual full HP shows one primary reason and disables confirm");}
                        await Capture(action[2],PopupBounds(root,d,action[2]));Check(ov.panel.Pick(new Vector2(2,2))==ov,"popup blocks background "+action[2]);menu.HandleShortcut(KeyCode.Escape);await Task.Delay(250);Check(ov.resolvedStyle.display==DisplayStyle.None,"Esc closes "+action[2]);}
                    Click(root.Q<Button>("pets-tab-overview"));await Task.Delay(250);
                    foreach(var action in new[]{new[]{"pets-details-button","pet-details-overlay","05_Thong_Tin_Thu"},new[]{"pets-rename-button","pets-rename-overlay","08_Doi_Ten_Thu"}}){Click(root.Q<Button>(action[0]));await Task.Delay(300);var ov=root.Q(action[1]);Check(ov.resolvedStyle.display!=DisplayStyle.None,"popup opens "+action[2]);var d=ov.Q(className:"ga-popup");float s=Screen.width/root.worldBound.width;var r=d.worldBound;await Capture(action[2],PopupBounds(root,d,action[2]));
                        if(action[2].StartsWith("08")){var field=ov.Q<TextField>("pets-rename-input");Check(field.maxLength==16,"rename max16 retained");foreach(var value in new[]{"","   ","A","Tên thú dài123456"}){field.value=value;await Task.Delay(90);Check(ov.Q<Button>("pets-rename-save").enabledInHierarchy==(!string.IsNullOrWhiteSpace(value)&&value.Length<=16),"rename validity length="+value.Length);}field.value="abcdefghijklmnopq";Check(!ov.Q<Button>("pets-rename-save").enabledInHierarchy,"rename rejects programmatic17");}
                        menu.HandleShortcut(KeyCode.Escape);await Task.Delay(200);Check(ov.resolvedStyle.display==DisplayStyle.None,"Esc closes "+action[2]);var focused=root.focusController.focusedElement as VisualElement;Check(focused==root.Q(action[0])||root.Q(action[0]).Contains(focused),"popup restores opener focus "+action[2]);}
                    CornerTests(panel.Q("pets-design"));Click(root.Q<Button>("pets-mode-3d"));await Task.Delay(400);Check(party.HasPreviewTexture,"explicit3D allocates renderer");await Capture("preview_3D",ToolkitBounds(root,panel),"3D");Click(root.Q<Button>("pets-mode-2d"));await Task.Delay(200);Check(!party.HasPreviewTexture,"2D releases renderer");
                    await GreenAngularStateBoard.Capture(root);
                    await StateFixture(party,root,panel);
                    Click(root.Q<Button>("pets-back-button"));await Task.Delay(250);Check(!inventory.IsPetsSectionOpen&&!party.HasPreviewTexture,"Back closes and releases");Check(!hudRenderer.forceRenderingOff,"closing restores world HUD visibility");
                }
                log.Add("NOT RUN consuming positive transactions, external gamepad, standalone build. Real Profiler is a separate capture.");
            }catch(Exception e){log.Add("ERROR "+e);Debug.LogException(e);}finally{
                inventory.Close();menu.CloseMenu();Object.Destroy(hudFixture);selected.SetValue(view,previous);File.WriteAllLines(Output+"validation.txt",log);running=false;if(SessionState.GetBool(OwnPlay,false)){SessionState.SetBool(OwnPlay,false);EditorApplication.ExitPlaymode();}
            }
        }
    }
}



using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;
namespace Capstone.Game.HudSystem.Editor {
    public static class CompanionVerification {
        public const string Output=Capstone.Game.UISystem.QaArtifactPaths.Root + "companion-ui/";
        const string Request="Temp/VerifyCompanion.request",OwnPlay="CompanionVerification.OwnPlay";
        static bool running;
        const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        [MenuItem("Game Tools/Companion UI/Capture three tabs")]
        static void RequestCapture()=>Queue("after");
        [MenuItem("Game Tools/Companion UI/Validate interactions")]
        static void RequestValidation()=>Queue("test");
        static void Queue(string command){
            if(running||File.Exists(Request)||File.Exists("Temp/ProfileCompanion.request")){Debug.LogWarning("Companion QA is already queued or running.");return;}
            Directory.CreateDirectory("Temp");File.WriteAllText(Request,command);
        }
        [InitializeOnLoadMethod] static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll(){
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            if(!EditorApplication.isPlaying){if(EditorApplication.isPlayingOrWillChangePlaymode)return;running=true;try{EnsureAssets();SessionState.SetBool(OwnPlay,true);EditorApplication.EnterPlaymode();}finally{running=false;}return;}
            if(Object.FindFirstObjectByType<GameMenuController>()==null)return;
            string command=File.ReadAllText(Request).Trim();File.Delete(Request);if(command=="test")Test();else Capture(command);
        }
        static readonly List<string> checks=new List<string>();
        static void Check(bool condition,string message){checks.Add((condition?"PASS ":"FAIL ")+message);if(!condition)throw new Exception(message);}
        static void Click(Button button){Check(button!=null&&button.enabledInHierarchy,"enabled button "+button?.name);typeof(Clickable).GetMethod("Invoke",Any).Invoke(button.clickable,new object[]{null});}
        [Serializable]sealed class BoundsEntry{public string id;public float x,y,width,height;}
        [Serializable]sealed class BoundsDocument{public int width,height;public BoundsEntry[] boxes;}
        static void SaveBounds(UIDocument document,string suffix){
            var root=document.rootVisualElement;var panel=root.Q("pets-panel");float sx=Screen.width/root.worldBound.width,sy=Screen.height/root.worldBound.height;
            string[] names={"companion-header","companion-hero","companion-tabs","companion-inspector","companion-dock","pets-swap-button","companion-resources","companion-stats","companion-skill-summary","companion-actions"};
            var entries=new List<BoundsEntry>();foreach(var id in names){var e=panel.Q(id);if(e==null)continue;var r=e.worldBound;entries.Add(new BoundsEntry{id=id,x=(r.x-root.worldBound.x)*sx,y=(r.y-root.worldBound.y)*sy,width=r.width*sx,height=r.height*sy});}
            foreach(var e in panel.Query<Button>(className:"pet-party-card").ToList()){var r=e.worldBound;entries.Add(new BoundsEntry{id=e.name,x=r.x*sx,y=r.y*sy,width=r.width*sx,height=r.height*sy});}
            File.WriteAllText(Output+"bounds-"+suffix+".json",JsonUtility.ToJson(new BoundsDocument{width=Screen.width,height=Screen.height,boxes=entries.ToArray()},true));
        }
        static async void Test(){
            running=true;checks.Clear();Directory.CreateDirectory(Output);
            var menu=Object.FindFirstObjectByType<GameMenuController>();var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();
            var party=inventory.GetComponent<PetPartyPanelController>();var provider=Object.FindFirstObjectByType<PetCommandHudProvider>();var input=Object.FindFirstObjectByType<PetCommandInput>();
            var doc=inventory.GetComponent<UIDocument>();var ui=doc.rootVisualElement;
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",Any);int previous=(int)selected.GetValue(view);
            var originalPet=provider.GetSelectedPetController();var labelObject=new GameObject("CompanionHUDVisibilityFixture",typeof(TextMesh));var worldRenderer=labelObject.GetComponent<Renderer>();
            try{
                await Task.Delay(1200);menu.CloseMenu();inventory.Close();await Task.Delay(300);
                bool priorCursor=Cursor.visible;var priorLock=Cursor.lockState;
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,selected,size);view.Show();view.Focus();await Task.Delay(600);menu.OpenPetsDirect();await Task.Delay(500);
                    Check(inventory.IsPetsSectionOpen&&InventoryInputController.GameplayInputBlocked,"party owns input at "+size);
                    Check(!party.IsPreview3D&&!party.HasPreviewTexture,"default 2D without renderer "+size);
                    Check(ui.Q<Label>("pets-selected-portrait-fallback").resolvedStyle.display==DisplayStyle.Flex,"missing large art stays 2D "+size);
                    Check(worldRenderer.forceRenderingOff,"world HUD labels suppressed "+size);
                    var panel=ui.Q("pets-panel");var cards=panel.Query<Button>(className:"pet-party-card").ToList();Check(cards.Count==6,"six dock slots "+size);
                    SaveBounds(doc,size.x+"x"+size.y);
                    foreach(var id in new[]{"pets-tab-overview","pets-tab-skills","pets-tab-growth","pets-back-button","pets-level-up-button","pets-heal-button","pets-swap-button"}){
                        var button=ui.Q<Button>(id);var bounds=button.worldBound;
                        Check(bounds.width>0&&bounds.height>0&&ui.worldBound.Contains(bounds.min)&&ui.worldBound.Contains(bounds.max),"visible hitbox "+id+" "+size);
                        var picked=button.panel.Pick(bounds.center);Check(picked==button||picked?.GetFirstAncestorOfType<Button>()==button,"native panel Pick reaches "+id+" "+size);
                    }
                    var stats=originalPet.GetComponentInChildren<PetCollectionMetadata>(true).CurrentStats;
                    Check(ui.Q<Label>("pets-stat-health").text==stats.Hp.ToString()&&ui.Q<Label>("pets-stat-magic-attack").text==stats.MagicAttack.ToString()&&ui.Q<Label>("pets-stat-magic-defense").text==stats.MagicDefense.ToString(),"real stat values, including HP stat not current HP "+size);
                    foreach(var key in new[]{"health","attack","magic-attack","defense","magic-defense","speed","critical-rate","critical-damage"})Check(ui.Q<Label>("pets-stat-"+key)!=null,"stat binding "+key+" "+size);
                    Click(ui.Q<Button>("pets-tab-skills"));await Task.Delay(250);
                    var skills=inventory.GetComponent<PetSkillsPanelController>();Check(party.CompanionTab==1&&skills.IsEmbedded&&!skills.IsOpen,"single embedded skill controller "+size);
                    int count=provider.GetEquippedSkillSlotCount();for(int i=0;i<4;i++){var slot=ui.Q<Button>("skills-equipped-slot-"+(i+1));Check(slot.resolvedStyle.display==DisplayStyle.Flex&&slot.enabledInHierarchy==(i<count),"provider-authorized equipped slot "+i+" "+size);}
                    var learned=ui.Query<Button>(className:"skills-learned-card").ToList();if(learned.Count>0){Click(learned[0]);await Task.Delay(200);Check(ui.Q<Label>("skills-tooltip-name").text!="Chi tiết kỹ năng","learned selection populates inspector "+size);}
                    Click(ui.Q<Button>("pets-tab-growth"));await Task.Delay(250);Check(party.CompanionTab==2&&!skills.IsEmbedded,"growth detaches skill dialog "+size);
                    Check(ui.Q<Label>("pets-growth-level-summary").text.Contains("Lv."),"growth is service-backed preview "+size);
                    foreach(var pair in new[]{new[]{"pets-growth-level","level-up-modal-overlay"},new[]{"pets-growth-heal","heal-modal-overlay"}}){
                        Click(ui.Q<Button>(pair[0]));await Task.Delay(300);Check(ui.Q(pair[1]).resolvedStyle.display!=DisplayStyle.None,"existing preview popup "+pair[1]+" "+size);
                        ScreenCapture.CaptureScreenshot(Output+"popup-"+pair[1]+"-"+size.x+"x"+size.y+".png");await Task.Delay(200);menu.HandleShortcut(KeyCode.Escape);await Task.Delay(250);Check(inventory.IsPetsSectionOpen&&party.CompanionTab==2,"Esc cancels popup and keeps tab/pet "+size);
                    }
                    Click(ui.Q<Button>("pets-tab-overview"));await Task.Delay(200);Click(ui.Q<Button>("pets-rename-button"));await Task.Delay(220);Check(party.IsRenameOpen,"rename uses existing modal "+size);
                    var field=ui.Q<TextField>("pets-rename-input");Check(field.maxLength==16,"rename 16-character validation "+size);field.value="   ";await Task.Delay(180);Check(!ui.Q<Button>("pets-rename-save").enabledInHierarchy,"invalid rename cannot confirm "+size);menu.HandleShortcut(KeyCode.Escape);await Task.Delay(250);Check(!party.IsRenameOpen&&inventory.IsPetsSectionOpen,"rename cancel restores party "+size);
                    Click(ui.Q<Button>("pets-swap-button"));await Task.Delay(200);Check(panel.Query<Button>(className:"is-swap-source").ToList().Count==1,"swap source is distinct marker "+size);Click(ui.Q<Button>("pets-swap-button"));await Task.Delay(200);Check(panel.Query<Button>(className:"is-swap-source").ToList().Count==0,"swap cancel "+size);
                    var summonBefore=provider.GetPetSlots().Select(s=>s.summoned).ToArray();var other=provider.GetPetControllerAt(1);
                    if(other!=null){input.SetActivePet(other);provider.NotifyHudDataChanged();await Task.Delay(250);Check(provider.GetPetSlots()[1].selected&&summonBefore.SequenceEqual(provider.GetPetSlots().Select(s=>s.summoned)),"viewing different pet preserves summoned flags (read-only input/provider notification) "+size);input.SetActivePet(originalPet);provider.NotifyHudDataChanged();await Task.Delay(200);}
                    for(int cycle=0;cycle<3;cycle++){Click(ui.Q<Button>("pets-mode-3d"));await Task.Delay(350);Check(party.IsPreview3D&&party.HasPreviewTexture,"3D stage allocated only on request "+cycle+" "+size);if(cycle==0){ScreenCapture.CaptureScreenshot(Output+"preview-3d-"+size.x+"x"+size.y+".png");await Task.Delay(180);}Click(ui.Q<Button>("pets-mode-2d"));await Task.Delay(250);Check(!party.HasPreviewTexture&&Resources.FindObjectsOfTypeAll<GameObject>().Count(g=>g.name=="PetPortraitRenderStage"&&g.activeInHierarchy)==0,"3D stage released "+cycle+" "+size);}
                    Click(ui.Q<Button>("pets-details-button"));await Task.Delay(200);Check(party.IsDetailsOpen,"existing details popup "+size);menu.HandleShortcut(KeyCode.Escape);await Task.Delay(200);
                    var name=ui.Q<Label>("pets-selected-name");string originalName=name.text;name.text="Tên thú cực kỳ dài kiểm tra ellipsis và vùng thao tác";await Task.Delay(100);Check(name.resolvedStyle.textOverflow==TextOverflow.Ellipsis,"long name ellipsis "+size);ScreenCapture.CaptureScreenshot(Output+"long-name-"+size.x+"x"+size.y+".png");await Task.Delay(200);name.text=originalName;
                    var providerField=typeof(PetPartyPanelController).GetField("petHudProvider",Any);var autoField=typeof(PetPartyPanelController).GetField("autoFindProvider",Any);
                    var savedProvider=providerField.GetValue(party);var savedAuto=autoField.GetValue(party);GameObject fixture=null;
                    try{
                        autoField.SetValue(party,false);providerField.SetValue(party,null);party.Refresh();await Task.Delay(180);
                        Check(ui.Q<Label>("pets-stat-health").text=="—"&&ui.Q<Label>("pets-health-value").text=="— / —","missing provider shows unknown values "+size);
                        foreach(var id in new[]{"pets-level-up-button","pets-heal-button","pets-mode-3d","pets-swap-button"})Check(!ui.Q<Button>(id).enabledInHierarchy,"missing provider disables "+id+" "+size);
                        fixture=new GameObject("CompanionEmptyRosterFixture");var placeholder=fixture.AddComponent<PlaceholderPetHudProvider>();placeholder.enabled=false;
                        typeof(PlaceholderPetHudProvider).GetField("petSlots",Any).SetValue(placeholder,new List<PetSlotHudData>(new PetSlotHudData[6]));
                        providerField.SetValue(party,placeholder);party.Refresh();await Task.Delay(180);
                        Check(panel.Query<Button>(className:"pet-party-card").ToList().Count==6&&panel.Query<Button>(className:"is-selected").ToList().All(b=>!b.ClassListContains("pet-party-card")),"empty roster keeps six unselected dock cells "+size);
                        Check(ui.Q<Label>("pets-selected-name").text=="Chọn một thú","empty roster has readable empty state "+size);
                        Check(ui.Q("pets-health-fill").resolvedStyle.width<1&&panel.Query<VisualElement>(className:"pet-skill-card").ToList().All(e=>e.resolvedStyle.display==DisplayStyle.None),"empty roster clears HP and skill summary even if provider supplies residual values "+size);
                        ScreenCapture.CaptureScreenshot(Output+"empty-roster-"+size.x+"x"+size.y+".png");await Task.Delay(180);
                    }finally{providerField.SetValue(party,savedProvider);autoField.SetValue(party,savedAuto);party.Refresh();if(fixture!=null)Object.Destroy(fixture);}
                    Click(ui.Q<Button>("pets-back-button"));await Task.Delay(300);Check(!inventory.IsPetsSectionOpen&&!party.HasPreviewTexture&&!worldRenderer.forceRenderingOff,"Back releases preview/HUD "+size);Check(Cursor.visible==priorCursor&&Cursor.lockState==priorLock,"cursor restored "+size);
                }
                checks.Add("NOTE Positive resource-consuming confirmations, persistent rename/favorite/swap and equip save were not executed on user data.");
            }catch(Exception e){checks.Add("ERROR "+e);Debug.LogException(e);}
            finally{if(input!=null&&originalPet!=null){input.SetActivePet(originalPet);provider.NotifyHudDataChanged();}inventory.Close();Object.Destroy(labelObject);selected.SetValue(view,previous);File.WriteAllLines(Output+"validation.txt",checks);running=false;if(SessionState.GetBool(OwnPlay,false)){SessionState.SetBool(OwnPlay,false);EditorApplication.ExitPlaymode();}}
        }
        static void EnsureAssets(){
            foreach(string path in Directory.GetFiles(CompanionAssetImporter.Root,"*.png",SearchOption.AllDirectories))if(((TextureImporter)AssetImporter.GetAtPath(path)).textureType!=TextureImporterType.Sprite)AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            string catalog=CompanionAssetImporter.Root+"Portraits.asset";
            if(!File.Exists(catalog))AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PetCompanionArtCatalog>(),catalog);
        }
        static async void Capture(string stage){
            running=true;Directory.CreateDirectory(Output);var log=new List<string>();
            var menu=Object.FindFirstObjectByType<GameMenuController>();
            var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);
            var selected=type.GetProperty("selectedSizeIndex",Any);int previous=(int)selected.GetValue(view);
            try{
                await Task.Delay(1200); menu.CloseMenu(); inventory.Close();
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,selected,size);view.Show();view.Focus();await Task.Delay(700);
                    menu.OpenPetsDirect();await Task.Delay(700);
                    if(Screen.width!=size.x||Screen.height!=size.y)throw new Exception("Wrong Game view size "+Screen.width+"x"+Screen.height);
                    ScreenCapture.CaptureScreenshot(Output+stage+"-overview-"+size.x+"x"+size.y+".png");await Task.Delay(400);
                    if(stage!="before"){
                        var party=inventory.GetComponent<PetPartyPanelController>();
                        foreach(int tab in new[]{1,2}){party.ShowCompanionTab(tab);await Task.Delay(500);ScreenCapture.CaptureScreenshot(Output+stage+"-"+(tab==1?"skills":"growth")+"-"+size.x+"x"+size.y+".png");await Task.Delay(400);}
                    }
                    log.Add("PASS actual Game view "+size+" stage="+stage);inventory.Close();await Task.Delay(200);
                }
            }catch(Exception e){log.Add("ERROR "+e);Debug.LogException(e);}
            finally{inventory?.Close();selected.SetValue(view,previous);File.WriteAllLines(Output+stage+"-capture.txt",log);running=false;
                if(SessionState.GetBool(OwnPlay,false)){SessionState.SetBool(OwnPlay,false);EditorApplication.ExitPlaymode();}}
        }
    }
}

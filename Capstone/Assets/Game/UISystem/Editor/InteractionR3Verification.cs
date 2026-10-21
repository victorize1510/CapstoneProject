using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Actual engine + real file transactions against opt-in isolated saves. Input here is programmatic, not mouse proof.</summary>
    static class InteractionR3Verification {
        const string Request="Temp/VerifyInteractionR3.request";
        const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        static bool running;
        static readonly List<string> log=new List<string>();
        static string Output=>Path.Combine(InteractionR3SaveIsolation.Output,"transactions");
        static object Get(object o,string field)=>o.GetType().GetField(field,Any)?.GetValue(o);
        static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Any).Invoke(o,args);
        static void Check(bool valid,string message){log.Add((valid?"PASS ":"FAIL ")+message);File.WriteAllLines(Path.Combine(Output,"checks.txt"),log);if(!valid)throw new Exception(message);}
        static void Click(VisualElement root,string name){var b=root.Q<Button>(name);Check(b!=null&&b.enabledInHierarchy,"enabled "+name);typeof(Clickable).GetMethod("Invoke",Any).Invoke(b.clickable,new object[]{null});}
        static string Id(PetController pet)=>pet!=null?pet.GetComponentInChildren<PetCollectionMetadata>(true)?.PersistentId:null;
        static string Roster(PetCommandHudProvider p)=>string.Join("|",Enumerable.Range(0,6).Select(i=>Id(p.GetPetControllerAt(i))??"empty"));
        static string Skills(PetController pet)=>string.Join("|",pet.GetComponentInChildren<PetHudRuntimeStats>(true).GetSkills().Select(s=>s.skillId));
        // Fault injection restores the previous, already loaded store without re-instantiating
        // the roster mid-test. No production path bypasses LoadNow.
        static void RestoreTestStore(PlayerSaveController save,PlayerJsonFileSaveStore store){
            string path=store.GetPath(save.SlotId);var data=PlayerSaveMigration.Read(File.ReadAllText(path),out _);
            save.SetSaveStore(store);typeof(PlayerSaveController).GetField("loadedSlotPath",Any).SetValue(save,path);typeof(PlayerSaveController).GetField("retainedData",Any).SetValue(save,data);
        }
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll(){
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            if(!File.Exists(InteractionR3SaveIsolation.Flag)){File.Delete(Request);Debug.LogError("R3 QA requires isolated save flag.");return;}
            if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.EnterPlaymode();return;}
            if(Object.FindFirstObjectByType<GameMenuController>()==null)return;File.Delete(Request);Run();
        }
        static async void Run(){
            running=true;Directory.CreateDirectory(Output);log.Clear();var menu=Object.FindFirstObjectByType<GameMenuController>();var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();var party=inventory.GetComponent<PetPartyPanelController>();var skills=inventory.GetComponent<PetSkillsPanelController>();var provider=Object.FindFirstObjectByType<PetCommandHudProvider>();var save=Object.FindFirstObjectByType<PlayerSaveController>();var ui=inventory.GetComponent<UIDocument>().rootVisualElement;int writes=0,failures=0;Action<PlayerSaveData> saved=_=>writes++;Action<string> failed=_=>failures++;save.SaveCompleted+=saved;save.SaveFailed+=failed;
            try{
                await Task.Delay(1400);Check(Path.GetFullPath(save.SavePath).StartsWith(InteractionR3SaveIsolation.SaveRoot,StringComparison.OrdinalIgnoreCase),"store uses dedicated test snapshot before any transaction");
                menu.CloseMenu();inventory.Close();menu.OpenPetsDirect();await Task.Delay(300);save.SaveNow();await Task.Delay(800);
                string active=Id(provider.GetSelectedPetController());int viewed=Enumerable.Range(0,6).First(i=>provider.GetPetControllerAt(i)!=null&&Id(provider.GetPetControllerAt(i))!=active);
                string roster=Roster(provider),disk=File.ReadAllText(save.SavePath);int before=writes;
                Call(party,"HandlePartyCardClick",viewed,1);Call(party,"HandlePartyCardClick",viewed,2);await Task.Delay(650);
                Check(Id(provider.GetSelectedPetController())==active&&writes==before&&File.ReadAllText(save.SavePath)==disk,"avatar single/double-click views only: active, save count and file unchanged");
                var target=provider.GetPetControllerAt(viewed);string targetId=Id(target);
                Check(Get(party,"viewedPet")==target,"viewed context points at clicked identity");
                bool favoriteBefore=target.GetComponentInChildren<PetCollectionMetadata>(true).IsFavorite;before=writes;Click(ui,"pets-favorite-button");Check(writes==before+1&&target.GetComponentInChildren<PetCollectionMetadata>(true).IsFavorite!=favoriteBefore&&Id(provider.GetSelectedPetController())==active,"favorite targets viewed and saves once without selecting active");
                foreach(var action in new[]{new[]{"pets-level-up-button","PetLevelUpPanelController"},new[]{"pets-heal-button","PetHealPanelController"}}){
                    Click(ui,action[0]);await Task.Delay(80);var c=inventory.GetComponents<MonoBehaviour>().First(x=>x.GetType().Name==action[1]);Check(Get(c,"targetPet")==target,action[1]+" targets viewed, not active");Call(c,"Close");
                }
                Click(ui,"pets-details-button");Check(Get(party,"detailsPet")==target,"details targets viewed");party.CloseDetails();
                Click(ui,"pets-rename-button");Check(Get(party,"renameTarget")==target,"rename pins viewed identity");
                var input=ui.Q<TextField>("pets-rename-input");input.value=" ";Check(!ui.Q<Button>("pets-rename-save").enabledInHierarchy,"blank rename rejected");input.value="R3 Đặng Thú";before=writes;Click(ui,"pets-rename-save");Check(writes==before+1,"rename commits one save");Check(target.GetComponentInChildren<PetCollectionMetadata>(true).Nickname=="R3 Đặng Thú","rename edits viewed pet only");Check(Id(provider.GetSelectedPetController())==active,"rename leaves active unchanged");
                // View/preview/commit swap, occupied then empty. Same public service owns save and rollback.
                before=writes;Click(ui,"pets-swap-button");Call(party,"HandlePartyCardClick",viewed==0?1:0,1);Check(Roster(provider)==roster&&writes==before,"occupied destination is preview only");Check(Get(party,"viewedPet")==target,"swap destination does not select target");
                Click(ui,"r3-swap-cancel");Check(Roster(provider)==roster&&writes==before,"swap cancel performs no write");
                Click(ui,"pets-swap-button");int destination=viewed==0?1:0;Call(party,"HandlePartyCardClick",destination,1);before=writes;Click(ui,"r3-swap-confirm");Call(party,"ConfirmPartySwap");
                Check(writes==before+1&&provider.GetPetControllerAt(destination)==target,"occupied swap commits exactly once, including duplicate confirm");Check(Get(party,"viewedPet")==target&&Id(provider.GetSelectedPetController())==active,"swap follows viewed identity and preserves active");
                await Task.Delay(200);int empty=Enumerable.Range(0,6).First(i=>provider.GetPetControllerAt(i)==null);Click(ui,"pets-swap-button");Call(party,"HandlePartyCardClick",empty,1);before=writes;Click(ui,"r3-swap-confirm");Check(writes==before+1&&provider.GetPetControllerAt(empty)==target,"empty swap commits once");
                var savedRoster=PlayerSaveMigration.Read(File.ReadAllText(save.SavePath),out _);Check(savedRoster.pets.partyPetIds[empty]==targetId,"party destination persisted by actual save serializer");
                // Native grid elements survive data refresh. Both selection orders preview only.
                party.ShowCompanionTab(1);await Task.Delay(300);var grid=ui.Q("skills-learned-grid");var cards=grid.Query<Button>(className:"skills-learned-card").ToList();Check(cards.Count>=2,"real learned skill list available");var first=cards[0];string oldSkills=Skills(target);before=writes;
                Call(skills,"EquipSelectedInto",1);Call(skills,"SelectLearnedSkill",0);Check(writes==before&&Skills(target)==oldSkills,"destination then learned skill previews without saving");
                skills.Refresh();Check(ReferenceEquals(first,grid.Query<Button>(className:"skills-learned-card").ToList()[0]),"learned grid keeps element identity through data refresh");
                Check(ui.Q<Button>("r3-equip-confirm").enabledInHierarchy,"valid preview enables Equip");Click(ui,"r3-equip-confirm");Call(skills,"ConfirmEquipPreview");Check(writes==before+1&&Skills(target)!=oldSkills,"Equip + duplicate confirm saves exactly once");Check(Id(provider.GetSelectedPetController())==active,"Equip leaves active pet unchanged");
                Call(skills,"SelectLearnedSkill",0);Call(skills,"EquipSelectedInto",1);Check(!ui.Q<Button>("r3-equip-confirm").enabledInHierarchy,"same skill destination disabled");
                Call(skills,"CancelEquipPreview");Call(skills,"SelectLearnedSkill",1);Call(skills,"EquipSelectedInto",0);before=writes;party.ShowCompanionTab(2);await Task.Delay(200);Check(!(bool)Get(skills,"equipSaving")&&Get(skills,"selectedSkillId")==null&&writes==before,"tab change cancels equip preview without write");
                // Failure is an actual filesystem failure (a file used as parent directory), not a mocked return value.
                party.ShowCompanionTab(1);await Task.Delay(250);Call(skills,"SelectLearnedSkill",0);Call(skills,"EquipSelectedInto",0);
                string bad=Path.Combine(Output,"not-a-directory");File.WriteAllText(bad,"failure fixture");var goodStore=new PlayerJsonFileSaveStore(InteractionR3SaveIsolation.SaveRoot);save.SetSaveStore(new PlayerJsonFileSaveStore(bad));oldSkills=Skills(target);before=writes;Call(skills,"ConfirmEquipPreview");Check(writes==before&&Skills(target)==oldSkills,"real save failure rolls skill loadout back");RestoreTestStore(save,goodStore);
                Call(skills,"CancelEquipPreview");party.ShowCompanionTab(0);await Task.Delay(250);Click(ui,"pets-swap-button");Call(party,"HandlePartyCardClick",0,1);roster=Roster(provider);save.SetSaveStore(new PlayerJsonFileSaveStore(bad));before=writes;Call(party,"ConfirmPartySwap");Check(writes==before&&Roster(provider)==roster,"real save failure rolls occupied/empty party transaction back");RestoreTestStore(save,goodStore);Call(party,"ClearSwapSelection");
                // Healing uses real item definitions on the isolated test save. No asset or live save edit.
                var adapter=inventory.GetComponent<MonsterInventoryAdapter>();var stats=target.GetComponentInChildren<PetHudRuntimeStats>(true);var potion=AssetDatabase.LoadAssetAtPath<GDS.Core.ItemBase>("Assets/Game/Inventory/Potion.asset");Check(potion!=null,"existing recovery item definition");adapter.AddItem(potion,2);stats.SetStatus(Mathf.Max(1,stats.MaxHealth-20),stats.MaxHealth,stats.Energy,stats.MaxEnergy);save.SaveNow();await Task.Delay(500);
                int count=adapter.GetQuantity(potion);float hp=stats.Health;var heal=inventory.GetComponent<PetHealPanelController>();heal.Open(target,potion);before=writes;Call(heal,"ConfirmHeal");Call(heal,"ConfirmHeal");log.Add($"HEAL writes {before}→{writes}, quantity {count}→{adapter.GetQuantity(potion)}, HP {hp}→{stats.Health}, message={Get(heal,"transientFeedback")}");Check(writes==before+1&&adapter.GetQuantity(potion)==count-1&&stats.Health>hp,"heal consumes exactly once for viewed pet; duplicate confirm guarded");heal.Close();
                await Task.Delay(200);stats.SetStatus(Mathf.Max(1,stats.MaxHealth-20),stats.MaxHealth,stats.Energy,stats.MaxEnergy);count=adapter.GetQuantity(potion);hp=stats.Health;save.SetSaveStore(new PlayerJsonFileSaveStore(bad));heal.Open(target,potion);before=writes;int failureBefore=failures;Call(heal,"ConfirmHeal");Check(failures==failureBefore+1&&writes==before&&adapter.GetQuantity(potion)==count&&Mathf.Approximately(stats.Health,hp),"real save failure restores both recovery item and HP");heal.Close();RestoreTestStore(save,goodStore);
                // The configured XP item is absent from the normal save. A transient QA-only
                // item uses that existing configured ID; no asset, rule or formula is edited.
                var levelService=inventory.GetComponent<PetLevelUpService>();var levelPanel=inventory.GetComponent<PetLevelUpPanelController>();var levelPreview=levelService.CreatePreview(target);Check(!levelPreview.CanLevelUp&&levelPreview.Item==null,"normal isolated snapshot reports missing configured level item");
                string xpId=(string)Get(levelService,"levelUpItemId");var xpItem=MonsterItemDefinition.CreateRuntime(xpId,GameItemCategory.Material,"QA fixture for existing configured item",string.Empty);adapter.AddItem(xpItem,levelPreview.RequiredItemQuantity*3);save.SaveNow();await Task.Delay(200);
                levelPreview=levelService.CreatePreview(target);int levelBefore=stats.Level,xpCount=adapter.GetQuantity(xpItem),required=levelPreview.RequiredItemQuantity;before=writes;levelPanel.Open(target);Call(levelPanel,"ConfirmLevelUp");Call(levelPanel,"ConfirmLevelUp");Check(writes==before+1&&stats.Level==levelBefore+1&&adapter.GetQuantity(xpItem)==xpCount-required,"level-up fixture targets viewed, consumes configured quantity and saves once");levelPanel.Close();
                await Task.Delay(200);levelPreview=levelService.CreatePreview(target);adapter.AddItem(xpItem,levelPreview.RequiredItemQuantity);xpCount=adapter.GetQuantity(xpItem);levelBefore=stats.Level;int xpBefore=target.GetComponentInChildren<PetCollectionMetadata>(true).Experience;save.SetSaveStore(new PlayerJsonFileSaveStore(bad));levelPanel.Open(target);before=writes;failureBefore=failures;Call(levelPanel,"ConfirmLevelUp");Check(failures==failureBefore+1&&writes==before&&stats.Level==levelBefore&&target.GetComponentInChildren<PetCollectionMetadata>(true).Experience==xpBefore&&adapter.GetQuantity(xpItem)==xpCount,"real level save failure restores level, XP and item quantity");levelPanel.Close();RestoreTestStore(save,goodStore);
                var metadata=target.GetComponentInChildren<PetCollectionMetadata>(true);var evoService=inventory.GetComponent<PetEvolutionService>();Check(!evoService.CreatePreview(target).CanEvolve,"current real roster is final form and cannot evolve");
                var rule=metadata.EvolutionRules.First(r=>r!=null&&r.Trigger!=PetEvolutionTrigger.Item);var evoFixture=metadata.CreateSaveData();evoFixture.evolutionStage=rule.SourceStage;evoFixture.currentFormId=metadata.EvolutionRules.FirstOrDefault(r=>r.TargetStage==rule.SourceStage)?.TargetFormId??string.Empty;metadata.RestoreFromSaveData(evoFixture);stats.SetIdentity(stats.DisplayName,Mathf.Max(stats.Level,rule.RequiredLevel),stats.Icon);metadata.SyncRuntimeHealth(stats);save.SaveNow();await Task.Delay(200);
                party.Refresh();Call(party,"OpenEvolutionPanel",new object[]{null});var evoPanel=inventory.GetComponent<PetEvolutionPanelController>();Check(Get(evoPanel,"targetPet")==target,"evolution pins viewed identity before leaving Companion");before=writes;Call(evoPanel,"ConfirmEvolution");Call(evoPanel,"ConfirmEvolution");Check(writes==before+1&&metadata.EvolutionStage==rule.TargetStage,"existing catalog evolution fixture confirms once");evoPanel.Close();
                save.SaveNow();var persisted=PlayerSaveMigration.Read(File.ReadAllText(save.SavePath),out _);string expectedRoster=Roster(provider),expectedSkills=Skills(target);int expectedCount=adapter.GetQuantity(potion);Check(save.LoadNow(),"reload actual test save");await Task.Delay(600);target=Enumerable.Range(0,6).Select(provider.GetPetControllerAt).First(p=>Id(p)==targetId);Check(Roster(provider)==expectedRoster&&Skills(target)==expectedSkills&&adapter.GetQuantity(potion)==expectedCount,"reload retains confirmed roster, skills and item count");
                // 50 cycles: element/callback/RT counts must return to stable baselines.
                inventory.Close();menu.CloseMenu();menu.OpenPetsDirect();party.ShowCompanionTab(1);await Task.Delay(250);int buttonCount=ui.Query<Button>().ToList().Count;int callbacks=((System.Collections.IDictionary)Get(party,"buttonCallbacks")).Count;
                for(int i=0;i<50;i++){inventory.Close();menu.OpenMenu();menu.CloseMenu();menu.OpenPetsDirect();party.ShowCompanionTab(i%3);await Task.Delay(170);party.SetPreview3D(true);await Task.Delay(30);party.SetPreview3D(false);}
                party.ShowCompanionTab(1);await Task.Delay(250);Check(ui.Query<Button>().ToList().Count==buttonCount,"50 cycles retain stable UI element count");Check(((System.Collections.IDictionary)Get(party,"buttonCallbacks")).Count==callbacks,"50 cycles retain stable registered action count");Check(!party.HasPreviewTexture,"50 cycles leave zero owned preview RT");inventory.Close();Check(!InventoryInputController.GameplayInputBlocked,"closing releases gameplay input ownership");
                log.Add("COMPLETE actual Unity Play; programmatic controller input with real isolated save transactions. Mouse proof is recorded separately.");
            }catch(Exception e){log.Add("ERROR "+e);Debug.LogException(e);}finally{save.SaveCompleted-=saved;save.SaveFailed-=failed;inventory.Close();menu.CloseMenu();File.WriteAllLines(Path.Combine(Output,"checks.txt"),log);running=false;EditorApplication.ExitPlaymode();}
        }
    }
}

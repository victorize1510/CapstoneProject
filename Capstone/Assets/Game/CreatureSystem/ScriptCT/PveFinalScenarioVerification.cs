#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.PetSummon;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using Capstone.Game.WorldFeedback;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Capstone.Game.CreatureSystem {
    internal static class PveFinalScenarioVerification {
        const string Qa=QaArtifactPaths.Root+"pve-combat-hub-2026-11-11/";
        const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        static async Task Gap(int ms=100){await Task.Delay(ms);if(!EditorApplication.isPlaying)throw new OperationCanceledException("Play ended");}
        static void Write(string file,string value){File.WriteAllText(Qa+file,value);File.AppendAllText(Qa+"created-paths.txt",Qa+file+"\n");}
        static async Task Shot(string name){string path=Qa+name+".png";File.AppendAllText(Qa+"created-paths.txt",path+"\n");ScreenCapture.CaptureScreenshot(path);await Gap(150);}
        internal static async Task Run(PetCommandInput input,Action<bool,string> check){
            var director=input.GetComponent<PetSummonDirector>();check(director.RequestSummonSlot(0),"final visual pet summon accepted");for(int i=0;i<150&&!input.CanUsePetSkills;i++)await Gap();
            var pet=input.activePet;check(pet!=null&&pet.CanReceiveCommands,"final visual pet ready");pet.ReturnToOwner();pet.roamAroundOwner=false;
            var inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();inventory.TryAddItemAtomic(CaptureAssets.LoadBall(),3);
            var wallet=Object.FindFirstObjectByType<PlayerCurrencyWallet>();var rewards=Object.FindFirstObjectByType<PveRewardService>();
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);var size=type.GetProperty("selectedSizeIndex",Any);int original=(int)size.GetValue(view);
            var helper=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture")).First(t=>t!=null).GetMethod("SelectSize",Any);
            string profile="environment,condition,samples,main_thread_ms,gc_bytes_per_observed_frame\n";
            try{
                profile+=await Measure("idle",check);Write("final-profiler.csv",profile);
                foreach(var resolution in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    helper.Invoke(null,new object[]{view,size,resolution});view.Focus();await Gap(400);check(Screen.width==resolution.x&&Screen.height==resolution.y,"final actual Game view "+resolution);
                    var picker=Object.FindFirstObjectByType<HubCaptureHud>();picker.OpenPicker();await Gap();
                    check(picker.IsPickerOpen,"real G picker open for final screenshot");await Shot("hud-ball-picker-"+resolution.x);picker.ClosePicker();await Gap(150);
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab");
                    Vector3 point=input.transform.position+input.transform.right*1.5f+input.transform.forward;
                    if(Physics.Raycast(point+Vector3.up*2,Vector3.down,out var floor,6,~0,QueryTriggerInteraction.Ignore))point.y=floor.point.y;
                    var go=Object.Instantiate(prefab,point,Quaternion.identity);go.GetComponent<BasicCreatureEnemy>().enabled=false;var nav=go.GetComponent<UnityEngine.AI.NavMeshAgent>();if(nav!=null)nav.enabled=false;
                    var enemy=go.GetComponent<DummyEnemy>();enemy.autoRevive=false;await Gap();
                    var combat=pet.GetComponent<CreatureCombatant>();
                    enemy.ApplyCombatHit(new CombatHit(pet.gameObject,"qa-two-factions",992,5,CreatureDamageKind.Physical,false,false,enemy.transform.position+Vector3.up*.55f));
                    combat.ReceiveHit(new CombatHit(go,"qa-two-factions",993,5,CreatureDamageKind.Physical,false,false,pet.transform.position+Vector3.up*.55f));
                    await Gap(120);var numbers=Object.FindFirstObjectByType<FloatingCombatText>();check(numbers!=null&&numbers.ActiveCount>=2,"both faction damage labels actually visible "+resolution);await Shot("hud-two-factions-"+resolution.x);
                    int before=rewards.CompletedCount,gold=wallet.Gold;
                    enemy.ApplyCombatHit(new CombatHit(pet.gameObject,"qa-reward",994,10000,CreatureDamageKind.Physical,false,false,enemy.TargetPosition));
                    string id=rewards.LastStagedId;check(!string.IsNullOrEmpty(id),"real defeat staged durable reward "+resolution);
                    for(int i=0;i<30&&rewards.CompletedCount==before;i++)await Gap();
                    check(rewards.CompletedCount==before+1&&wallet.Gold>=gold+2&&wallet.Gold<=gold+5,"actual nearby loot flight commits one reward "+resolution);
                    check(Object.FindFirstObjectByType<AcquisitionToastView>().VisibleCount>0,"saved receipt is rendered as real acquisition toast "+resolution);await Shot("loot-receipt-"+resolution.x);
                    profile+=await Measure("after-reward-"+resolution.x,check);Write("final-profiler.csv",profile);Object.Destroy(go);
                }
                // Explicitly bounded native input window; no recorder and no real player save.
                var switcher=input.GetComponent<PlayerVisualSwitcher>();var capture=Object.FindFirstObjectByType<HubCaptureHud>();int visual=switcher.ActiveVisualIndex,throws=capture.ThrowIntents;
                Write("manual-ready.txt","READY 60 seconds: F8, G tap, P tap/open/close, 1; Play-only E save");
                for(int i=0;i<600&&!File.Exists(Qa+"manual.done");i++){Write("manual-state.txt","visual="+switcher.ActiveVisualIndex+" initialVisual="+visual+" throwIntents="+capture.ThrowIntents+" initialThrows="+throws+" partyOpen="+GameplayHudController.Live.IsPartyOpen+" summoned="+pet.IsSummoned);await Gap();}
                if(File.Exists(Qa+"manual.done")){File.Delete(Qa+"manual.done");check(switcher.ActiveVisualIndex!=visual,"native F8 input changes visual through runtime router");check(capture.ThrowIntents==throws+1,"native G tap produces exactly one throw intent");Write("manual-ready.txt","COMPLETE physical tap checks; physical hold remains unverified");}
                else Write("manual-ready.txt","TIMEOUT physical input not verified; engine checks complete");
            }finally{size.SetValue(view,original);}
        }
        static async Task<string> Measure(string condition,Action<bool,string> check){
            using(var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",32))using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",32)){
                long main=0,allocated=0;for(int i=0;i<20;i++){await Gap(50);main+=cpu.LastValue;allocated+=gc.LastValue;}
                check(cpu.Valid&&gc.Valid,"actual Editor ProfilerRecorder "+condition);
                return FormattableString.Invariant($"Editor,{condition},20,{main/20d/1000000},{allocated/20d}\n");
            }
        }
    }
}
#endif

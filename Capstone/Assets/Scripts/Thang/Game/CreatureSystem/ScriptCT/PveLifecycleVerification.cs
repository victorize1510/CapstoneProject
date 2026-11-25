#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.PetSummon;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using Capstone.Game.WorldFeedback;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Opt-in Play-only fixtures. Real saves and scene assets are never changed by these checks.</summary>
    public static class PveLifecycleVerification {
        static string Qa => QaArtifactPaths.Root+"pve-combat-hub-2026-11-11/";
        const string Session = "Capstone.PveLifecycle.Qa";
        public const string Version="pve-life-2026-11-12-v4";
        static readonly List<string> checks=new List<string>();static double pollAt;static bool running;
        static void Write(string file,string value){Directory.CreateDirectory(Qa);File.WriteAllText(Qa+file,value);File.AppendAllText(Qa+"created-paths.txt",Qa+file+"\n");}
        static void Check(bool pass,string name){checks.Add((pass?"PASS ":"FAIL ")+name);Write("life-checks.txt",string.Join("\n",checks));if(!pass)throw new InvalidOperationException(name);}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;Write("life-loaded.txt",Version);}
        static void Poll(){if(EditorApplication.timeSinceStartup<pollAt||EditorApplication.isUpdating||EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)return;pollAt=EditorApplication.timeSinceStartup+1;if(!File.Exists(Qa+"life.request")||File.ReadAllText(Qa+"life.request").Trim()!=Version)return;File.Delete(Qa+"life.request");Begin();}
        [MenuItem("Tools/Capstone/Advanced/PvE/4. Verify isolated lifecycle rewards and navigation")]
        public static void Begin(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;checks.Clear();
            SessionState.SetBool(Session,true);SessionState.SetString(Session+".save",Qa+"test-saves/life-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            SessionState.SetBool(Session+".reload",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+".options",(int)EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();Write("life-status.txt","ENTERING_PLAY");EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){
            if(!SessionState.GetBool(Session,false)||running)return;string root=SessionState.GetString(Session+".save","");
            foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new PlayerJsonFileSaveStore(root));
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(root));Run();
        }
        sealed class FixedRandom:ICaptureRandom{readonly double value;public FixedRandom(double value){this.value=value;}public double NextFloat01()=>value;}
        static async Task Gap(int ms=100){await Task.Delay(ms);if(!EditorApplication.isPlaying)throw new OperationCanceledException("Play ended");}
        static GameObject Spawn(string path,Vector3 point){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new InvalidOperationException(path);var go=Object.Instantiate(prefab,point,Quaternion.identity);go.name="QA lifecycle "+prefab.name;go.GetComponent<DummyEnemy>().autoRevive=false;var brain=go.GetComponent<BasicCreatureEnemy>();if(brain!=null)brain.enabled=false;return go;}
        static CombatDamageResult Hit(DummyEnemy enemy,PetController pet,float raw,int token=77)=>enemy.ApplyCombatHit(new CombatHit(pet.gameObject,"qa-lifecycle",token,raw,CreatureDamageKind.Physical,false,false,enemy.TargetPosition));
        static async void Run(){
            running=true;var fixtures=new List<GameObject>();var brains=new List<(BasicCreatureEnemy brain,bool enabled)>();PveRewardService rewards=null;
            try{
                await Gap(650);Write("life-status.txt","RUNNING real HP/capture/save lifecycle");
                var player=Object.FindFirstObjectByType<PetCommandInput>();var save=Object.FindFirstObjectByType<PlayerSaveController>();
                var inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();var wallet=Object.FindFirstObjectByType<PlayerCurrencyWallet>();
                rewards=save.GetComponent<PveRewardService>();rewards.enabled=false;
                Check(save.SavePath.StartsWith(Qa,StringComparison.OrdinalIgnoreCase)&&!save.IsSaveBlocked,"isolated writable player save on E");
                foreach(var ai in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None)){brains.Add((ai,ai.enabled));ai.enabled=false;}
                var director=player.GetComponent<PetSummonDirector>();var movement=player.GetComponent<BasicPlayerMovement>();
                for(int i=0;i<40&&movement!=null&&!movement.CanStartPetThrow;i++)await Gap();
                Check(director.RequestSummonSlot(0),"real source pet summon accepted");for(int i=0;i<120&&director.IsSequenceRunning;i++)await Gap();
                var source=player.activePet;Check(source!=null&&source.CanReceiveCommands,"source pet ready");source.roamAroundOwner=false;
                var sourceStats=source.GetComponent<PetHudRuntimeStats>();var ball=CaptureAssets.LoadBall();inventory.TryAddItemAtomic(ball,6);
                var coordinator=Object.FindFirstObjectByType<PetCaptureCoordinator>();int beforePending=rewards.PendingCount;
                var wildGo=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/Wild/Wild_Dragon01.prefab",player.transform.position+player.transform.forward*5);fixtures.Add(wildGo);await Gap();
                var wild=wildGo.GetComponent<WildPetCaptureTarget>();int defeated=0;wild.Enemy.Defeated+=_=>defeated++;
                var terminal=Hit(wild.Enemy,source,100000);
                Check(wild.Enemy.IsDowned&&wild.Enemy.CurrentHealth==0&&!terminal.Killed&&defeated==0&&rewards.PendingCount==beforePending,"lethal catchable hit enters downed at 0 HP without death/rewards");
                Check(!wildGo.GetComponent<CreatureCombatant>().CanBeAttacked&&wild.Enemy.IsSelectable,"downed target selectable but immune to further damage");
                Check(!CaptureCalculator.TryOdds(new CaptureContext(0,100,255,new CaptureBallRules(1)),out _,out _)&&CaptureCalculator.TryOdds(new CaptureContext(0,100,255,new CaptureBallRules(1),CaptureMajorStatus.None,true,0,false,-1,false,true),out _,out _),"zero HP calculator requires explicit valid downed context");
                coordinator.RandomSource=new FixedRandom(0);int quantity=inventory.GetQuantity(ball);string id=wild.Metadata.PersistentId;
                Check(coordinator.TryCaptureTarget(ball,inventory,wild,out var success,out string error)&&success.Success,"real zero HP capture transaction succeeds: "+error);
                Check(inventory.GetQuantity(ball)==quantity-1&&Mathf.Abs(wild.Stats.Health-wild.Stats.MaxHealth*.25f)<.001f&&!wild.IsWild&&defeated==0&&rewards.PendingCount==beforePending,"capture consumes once restores 25 percent HP and suppresses kill reward");
                var disk=PlayerSaveMigration.Read(File.ReadAllText(save.SavePath),out _);
                Check(disk.pets.petStates.Count(p=>p.petId==id)==1,"captured individual persisted exactly once");
                var missGo=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/Wild/Wild_Dragon03.prefab",player.transform.position+player.transform.right*5);fixtures.Add(missGo);await Gap();
                var miss=missGo.GetComponent<WildPetCaptureTarget>();int missDefeated=0;miss.Enemy.Defeated+=_=>missDefeated++;Hit(miss.Enemy,source,100000);
                coordinator.RandomSource=new FixedRandom(.999999);quantity=inventory.GetQuantity(ball);
                Check(coordinator.TryCaptureTarget(ball,inventory,miss,out var failed,out error)&&!failed.Success&&inventory.GetQuantity(ball)==quantity-1,"real failed downed throw consumes one ball");
                await Gap(5300);Check(miss.Enemy.IsDowned&&missDefeated==0,"15-second capture window remains available after five seconds");
                miss.EndPresentationHold();await Gap(10100);
                Check(!miss.Enemy.IsDowned&&!miss.Enemy.IsAlive&&missDefeated==1&&rewards.PendingCount==beforePending+1,"failed presentation ends expired window and finalizes death once");
                int pending=rewards.PendingCount;Hit(miss.Enemy,source,100000);await Gap();Check(missDefeated==1&&rewards.PendingCount==pending,"duplicate late hit cannot duplicate death or reward journal");
                var record=rewards.CreateSaveData().pending.Last();
                Check(record.gold>=2&&record.gold<=5&&record.items.Count<=1&&record.experienceCommitted,"rolled reward journal has approved loot and committed pet EXP");
                int receipts=0;Action<ItemAcquisitionReceipt> listener=_=>receipts++;ItemAcquisitionFeedback.Received+=listener;
                int gold=wallet.Gold;string inventoryBefore=JsonUtility.ToJson(inventory.CreateSaveData());
                bool collected;
                using(var locked=new FileStream(save.SavePath,FileMode.Open,FileAccess.Read,FileShare.None))collected=rewards.TryCollect(record.transactionId);
                Check(!collected&&wallet.Gold==gold&&JsonUtility.ToJson(inventory.CreateSaveData())==inventoryBefore&&rewards.PendingCount==pending&&receipts==0,"real file IO failure rolls back inventory gold and retains pending reward without receipt");
                Check(rewards.TryCollect(record.transactionId)&&wallet.Gold==gold+record.gold&&receipts==1+record.items.Count,"retry commits wallet inventory and receipt exactly once");
                Check(!rewards.TryCollect(record.transactionId)&&wallet.Gold==gold+record.gold,"duplicate pickup callback is rejected");
                ItemAcquisitionFeedback.Received-=listener;
                disk=PlayerSaveMigration.Read(File.ReadAllText(save.SavePath),out _);rewards.RestoreFromSaveData(disk.pveRewards);
                Check(!rewards.CreateSaveData().pending.Any(p=>p.transactionId==record.transactionId)&&!rewards.TryCollect(record.transactionId),"saved completed reward does not reappear after journal reload");
                string old=PlayerSaveMigration.Write(new PlayerSaveData()).Replace("\"version\": 14","\"version\": 13");var migrated=PlayerSaveMigration.Read(old,out int loaded);
                Check(loaded==13&&migrated.version==PlayerSaveData.CurrentVersion,"v13 save migration remains readable with optional reward journal");
                // Check both recovery clocks using real elapsed time.
                source.ReturnToOwner();var combat=source.GetComponent<CreatureCombatant>();combat.ReceiveDamage(10,CreatureDamageKind.Physical,missGo);
                sourceStats.SetStatus(sourceStats.Health,sourceStats.MaxHealth,sourceStats.MaxEnergy,sourceStats.MaxEnergy);
                sourceStats.SpendMana(sourceStats.MaxEnergy-20);float hp=sourceStats.Health,mp=sourceStats.Energy;
                await Gap(500);Check(Mathf.Abs(sourceStats.Health-hp)<.001f&&sourceStats.Energy==mp,"pet recovery waits one second after HP loss and MP use");
                await Gap(750);Check(sourceStats.Health>hp&&sourceStats.Energy>mp,"living summoned pet regenerates HP and MP after their cooldowns");
                Write("life-status.txt","RUNNING bounded navigation chase/return/regen");
                Check(NavMesh.SamplePosition(source.transform.position,out var petGround,3,NavMesh.AllAreas),"summoned pet is over baked DemoScene NavMesh");
                Vector3 spawn=default;bool found=false;var path=new NavMeshPath();
                for(int n=0;n<16;n++){
                    float angle=n*Mathf.PI/8;Vector3 point=petGround.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*6;
                    if(NavMesh.SamplePosition(point,out var sample,1.5f,NavMesh.AllAreas)&&NavMesh.CalculatePath(sample.position,petGround.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete&&(sample.position-petGround.position).sqrMagnitude>16){spawn=sample.position;found=true;break;}
                }
                Check(found,"real complete path found for moving enemy fixture");
                var moving=Spawn("Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab",spawn);fixtures.Add(moving);await Gap(300);
                var enemy=moving.GetComponent<DummyEnemy>();var brain=moving.GetComponent<BasicCreatureEnemy>();brain.patrolRadius=0;brain.enabled=true;
                Vector3 start=moving.transform.position;await Gap(1400);
                var navAgent=moving.GetComponent<NavMeshAgent>();Write("navigation-runtime.txt","start="+start+" end="+moving.transform.position+" pet="+source.transform.position+" sourceReady="+source.CanReceiveCommands+" active="+(player.activePet==source)+" state="+brain.State+" hasTarget="+(brain.Target!=null)+" agent="+navAgent.enabled+" onNav="+navAgent.isOnNavMesh+(navAgent.enabled&&navAgent.isOnNavMesh?" path="+navAgent.pathStatus+" stopped="+navAgent.isStopped+" velocity="+navAgent.velocity+" destination="+navAgent.destination:""));
                Check((moving.transform.position-start).sqrMagnitude>.04f&&brain.Target==combat,"real NavMesh enemy moves and pursues summoned pet");
                source.ReturnToOwner();await Gap(200);Check(brain.Target==combat,"return pet command does not clear enemy aggro");
                Hit(enemy,source,10);float enemyHp=enemy.CurrentHealth;Vector3 ownerBefore=player.transform.position;
                player.transform.position+=Vector3.right*40;source.transform.position=player.transform.position+Vector3.forward;
                await Gap(600);Check(brain.State=="Return"&&brain.Target==null,"leash exit enters Return without reacquiring outside target");
                for(int n=0;n<150&&!brain.AtHome;n++)await Gap();
                Check(brain.AtHome&&brain.Target==null,"enemy returns to its actual NavMesh home");
                await Gap(8500);Check(enemy.CurrentHealth>enemyHp,"enemy at home regenerates real HP after safe period");
                player.transform.position=ownerBefore;source.transform.position=ownerBefore+Vector3.forward;
                Write("life-status.txt","COMPLETE "+checks.Count+" engine checks. Navigation and moving-target combat are separate checks.");
            }catch(Exception error){Write("life-status.txt","FAILED\n"+error);Debug.LogException(error);}
            finally{foreach(var go in fixtures)if(go!=null)Object.Destroy(go);foreach(var pair in brains)if(pair.brain!=null)pair.brain.enabled=pair.enabled;if(rewards!=null)rewards.enabled=true;SessionState.SetBool(Session,false);running=false;EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+".reload",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+".options",0);if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();}
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Explicit bounded engine checks. Scene edits never occur in this verifier.</summary>
    public static class PveItemsCaptureVerification {
        const string Qa=UISystem.QaArtifactPaths.Root+"pve-items-capture-demoscene1-2026-11-16/";
        const string Session="Capstone.PveItemsCapture20261116";
        static readonly List<string> checks=new List<string>();
        static bool running;
        static double deadline;
        static EditorWindow gameView;
        static PropertyInfo sizeProperty;
        static int originalSize=-1;
        static void Write(string file,string text){Directory.CreateDirectory(Qa);File.WriteAllText(Qa+file,text);File.AppendAllText(Qa+"created-paths.txt",Qa+file+"\n");}
        static void Check(bool pass,string name){checks.Add((pass?"PASS ":"FAIL ")+name);Write("engine-checks.txt",string.Join("\n",checks));if(!pass)throw new InvalidOperationException(name);}
        [MenuItem("Tools/Capstone/PvE/2. Verify DemoScene 1 with isolated saves")]
        public static void Begin(){
            if(EditorApplication.isPlayingOrWillChangePlaymode||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/DemoScene 1.unity")throw new InvalidOperationException("Mở DemoScene 1 ngoài Play Mode.");
            checks.Clear();SessionState.SetBool(Session,true);
            SessionState.SetBool(Session+".reload",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+".options",(int)EditorSettings.enterPlayModeOptions);
            SessionState.SetString(Session+".save",Qa+"test-saves/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            deadline=EditorApplication.timeSinceStartup+65;EditorApplication.update+=Watchdog;
            Write("engine-status.txt","ENTERING_PLAY; timeout 65 seconds");EditorApplication.EnterPlaymode();
        }
        static void Watchdog(){if(EditorApplication.timeSinceStartup<deadline)return;Write("engine-status.txt","TIMEOUT; verification stopped, do not restart Unity");Finish();}
        static void Finish(){if(originalSize>=0&&gameView!=null){sizeProperty.SetValue(gameView,originalSize);originalSize=-1;}SessionState.SetBool(Session,false);running=false;EditorApplication.update-=Watchdog;EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+".reload",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+".options",0);if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){
            if(!SessionState.GetBool(Session,false)||running)return;
            string root=SessionState.GetString(Session+".save","");
            foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new PlayerJsonFileSaveStore(root));
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(root));
            Run();
        }
        static async Task Gap(int milliseconds=100){await Task.Delay(milliseconds);if(!EditorApplication.isPlaying||!SessionState.GetBool(Session,false))throw new OperationCanceledException("Verification stopped");}
        sealed class FixedRandom:ICaptureRandom{readonly double value;public FixedRandom(double value){this.value=value;}public double NextFloat01()=>value;}
        static GameObject Spawn(string path,Vector3 point,List<GameObject> created){var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),point,Quaternion.identity);created.Add(go);var brain=go.GetComponent<BasicCreatureEnemy>();if(brain!=null)brain.enabled=false;return go;}
        static CombatDamageResult Hit(DummyEnemy enemy,PetController source,float raw)=>enemy.ApplyCombatHit(new CombatHit(source.gameObject,"qa-pve-items",1,raw,CreatureDamageKind.Physical,false,false,enemy.TargetPosition));
        static async void Run(){
            running=true;var created=new List<GameObject>();var brains=new List<(BasicCreatureEnemy brain,bool enabled)>();PetController[] originalParty=null;PetController originalActive=null;PetCommandInput player=null;
            try{
                await Gap(800);
                player=Object.FindFirstObjectByType<PetCommandInput>();var save=Object.FindFirstObjectByType<PlayerSaveController>();var inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();
                Check(player!=null&&save!=null&&inventory!=null&&save.SavePath.StartsWith(Qa,StringComparison.OrdinalIgnoreCase),"DemoScene 1 test save isolated on E");
                foreach(var brain in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None)){brains.Add((brain,brain.enabled));brain.enabled=false;}
                Check(InventoryDefinitionCatalog.Definitions.Count>=5&&InventoryDefinitionCatalog.Find("healing-apple")?.HealAmount==30,"canonical catalog resolves real definitions");
                foreach(double unit in new[]{0,.099,.1,.299,.3,.699,.7,.899,.9,.999})Check(PveBalanceRules.EncounterLevel(unit)>=3&&PveBalanceRules.EncounterLevel(unit)<=7,"level boundary "+unit);
                Check(PveBalanceRules.CapturedHealth(0,100)==25&&PveBalanceRules.CapturedHealth(10,100)==25&&PveBalanceRules.CapturedHealth(70,100)==70,"capture health floor preserves higher HP");
                originalParty=(PetController[])player.petSlots.Clone();originalActive=player.activePet;player.petSlots=new PetController[6];
                for(int i=0;i<6;i++){
                    var go=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon01.prefab",player.transform.position+Vector3.right*(i+2),created);
                    var pet=go.GetComponent<PetController>();pet.owner=player.transform;player.petSlots[i]=pet;
                    go.GetComponent<PetCollectionMetadata>().AssignPersistentId(Guid.NewGuid().ToString("N"));
                    var stats=go.GetComponent<PetHudRuntimeStats>();stats.SetIdentity(stats.DisplayName,5,stats.Icon);stats.SetStatus(i==3?0:stats.MaxHealth,stats.MaxHealth,0,0);
                }
                var source=player.petSlots[0];var helper=player.petSlots[1];player.activePet=source;
                var roster=Object.FindFirstObjectByType<PetBoxRuntimeProvider>();roster.Bind(player);
                var rewards=save.GetComponent<PveRewardService>();rewards.enabled=false;
                var targetGo=Spawn("Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab",player.transform.position+player.transform.forward*4,created);await Gap();
                var enemy=targetGo.GetComponent<DummyEnemy>();Hit(enemy,helper,1);Hit(enemy,source,100000);
                var reward=rewards.CreateSaveData().pending.Last();
                Func<int,int> xp=i=>reward.pets.FirstOrDefault(p=>p.petId==player.petSlots[i].GetComponent<PetCollectionMetadata>().PetId)?.experience??0;
                Check(xp(0)==30&&xp(1)==23&&xp(2)==15&&xp(3)==0,"real reward transaction: finisher 100 helper 75 reserve 50 fainted 0");
                Check(reward.pets.Count==5&&reward.experienceCommitted,"eligible party captured once and EXP saved");
                int pending=rewards.PendingCount;Hit(enemy,source,100000);Check(rewards.PendingCount==pending,"late repeated hit cannot duplicate reward");
                await VerifyAnimationAndResolutions(player,created,enemy.gameObject);
                var ball=CaptureAssets.LoadBall();inventory.TryAddItemAtomic(ball,5);var coordinator=Object.FindFirstObjectByType<PetCaptureCoordinator>();coordinator.RandomSource=new FixedRandom(0);
                var wildGo=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/Wild/Wild_Dragon03.prefab",player.transform.position+player.transform.forward*5,created);await Gap();
                var wild=wildGo.GetComponent<WildPetCaptureTarget>();int deaths=0;wild.Enemy.Defeated+=_=>deaths++;Hit(wild.Enemy,source,100000);
                Check(wild.Enemy.IsDowned&&wild.Enemy.DownedSecondsRemaining>14&&deaths==0,"zero HP enters 15-second capture window without death reward");
                int quantity=inventory.GetQuantity(ball);string individual=wild.Metadata.PetId;
                Check(coordinator.TryCaptureTarget(ball,inventory,wild,out var result,out var error)&&result.Success,"real zero-HP capture succeeds: "+error);
                Check(Mathf.Abs(wild.Stats.Health-wild.Stats.MaxHealth*.25f)<.001f&&inventory.GetQuantity(ball)==quantity-1,"capture raises HP to 25 percent and consumes one ball");
                Check(deaths==0&&rewards.PendingCount==pending&&!wild.IsWild,"capture suppresses defeat and loot");
                var persisted=PlayerSaveMigration.Read(File.ReadAllText(save.SavePath),out _).pets.petStates.Single(p=>p.petId==individual);
                Check(persisted.runtimeStats!=null,"captured individual exists once in saved roster");
                Check(Mathf.Abs(persisted.runtimeStats.health-wild.Stats.MaxHealth*.25f)<.001f,"25 percent HP persisted in the capture transaction");
                var healthyGo=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/Wild/Wild_Dragon03.prefab",player.transform.position-player.transform.right*5,created);await Gap();
                var healthy=healthyGo.GetComponent<WildPetCaptureTarget>();float healthyBefore=healthy.Stats.Health;
                Check(coordinator.TryCaptureTarget(ball,inventory,healthy,out var healthyResult,out error)&&healthyResult.Success&&healthy.Stats.Health==healthyBefore,"capture above 25 percent preserves original HP");
                var expiredGo=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/Wild/Wild_Dragon02.prefab",player.transform.position+player.transform.forward*6,created);await Gap();
                var expired=expiredGo.GetComponent<WildPetCaptureTarget>();int expiredDeaths=0;expired.Enemy.Defeated+=_=>expiredDeaths++;
                Hit(expired.Enemy,source,100000);await Gap(15300);
                Check(!expired.Enemy.IsDowned&&!expired.Enemy.IsAlive&&expiredDeaths==1,"real 15-second deadline finalizes defeat exactly once");
                var nextGo=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/Wild/Wild_Dragon03.prefab",player.transform.position+player.transform.right*5,created);await Gap();
                var next=nextGo.GetComponent<WildPetCaptureTarget>();float before=next.Stats.Health;quantity=inventory.GetQuantity(ball);save.SaveNow();
                using(var locked=new FileStream(save.SavePath,FileMode.Open,FileAccess.Read,FileShare.None))Check(!coordinator.TryCaptureTarget(ball,inventory,next,out _,out _),"real file lock rejects save");
                Check(next.IsWild&&next.Stats.Health==before&&inventory.GetQuantity(ball)==quantity&&!roster.Contains(next.Pet),"save failure rolls back HP item and placement");
                Write("engine-status.txt","COMPLETE "+checks.Count+" checks; physical controls, visual review and Profiler remain separate");
            }catch(Exception e){Write("engine-status.txt","FAILED\n"+e);Debug.LogException(e);}
            finally{
                if(player!=null&&originalParty!=null){player.petSlots=originalParty;player.activePet=originalActive;}
                foreach(var go in created)if(go!=null)Object.Destroy(go);
                foreach(var item in brains)if(item.brain!=null)item.brain.enabled=item.enabled;
                Finish();
            }
        }
        static async Task VerifyAnimationAndResolutions(PetCommandInput player,List<GameObject> created,GameObject hostile){
            for(int form=1;form<=3;form++){
                var go=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon"+form.ToString("00")+".prefab",player.transform.position+player.transform.forward*(2+form),created);
                var pet=go.GetComponent<PetController>();pet.owner=player.transform;pet.roamAroundOwner=false;
                var stats=go.GetComponent<PetHudRuntimeStats>();stats.SetIdentity(stats.DisplayName,5,stats.Icon);stats.SetStatus(stats.MaxHealth,stats.MaxHealth,0,0);
                Check(stats.GetSkills().All(s=>s.manaCost==0)&&stats.MaxEnergy==0,"form "+form+" has cooldown-only skills");
                Check(pet.TrySummonAt(go.transform.position,player.transform.forward),"form "+form+" real summon placement");
                for(int i=0;i<35&&!pet.CanReceiveCommands;i++)await Gap();
                Check(pet.CanReceiveCommands,"form "+form+" ready after spawn animation");
                var combat=go.GetComponent<CreatureCombatant>();
                Check(combat.ReceiveHit(new CombatHit(hostile,"qa-hit",0,1,CreatureDamageKind.Physical,false,false,combat.Center)),"form "+form+" receives real damage");
                await Gap(100);var animator=stats.VisualAnimator;
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Take Damage")||animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Take Damage"),"form "+form+" plays Take Damage clip");
                await Gap(300);
                combat.ReceiveHit(new CombatHit(hostile,"qa-ko",1,100000,CreatureDamageKind.Physical,false,false,combat.Center));await Gap(100);
                Check(!pet.IsSummoned&&animator.enabled&&animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Die"),"form "+form+" KO plays Die before visual hide");
                if(form==1){await Resolution(1920,1080);await Resolution(1366,768);}
                for(int i=0;i<32&&animator.enabled;i++)await Gap();
                Check(!animator.enabled&&!pet.CanReceiveCommands,"form "+form+" KO finishes and hides safely");
            }
        }
        static async Task Resolution(int width,int height){
            var assembly=typeof(UnityEditor.Editor).Assembly;const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            var viewType=assembly.GetType("UnityEditor.GameView");gameView=EditorWindow.GetWindow(viewType,false,"Game",false);
            sizeProperty=viewType.GetProperty("selectedSizeIndex",flags);if(originalSize<0)originalSize=(int)sizeProperty.GetValue(gameView);
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
            var groupMethod=sizesType.GetMethod("GetGroup",flags);var kind=Enum.Parse(groupMethod.GetParameters()[0].ParameterType,"Standalone");var group=groupMethod.Invoke(singleton,new[]{kind});
            int count=(int)group.GetType().GetMethod("GetTotalCount",flags).Invoke(group,null),index=-1;
            for(int i=0;i<count;i++){var size=group.GetType().GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});var type=size.GetType();if((int)type.GetProperty("width",flags).GetValue(size)==width&&(int)type.GetProperty("height",flags).GetValue(size)==height){index=i;break;}}
            if(index<0)throw new InvalidOperationException("Game view thiếu preset "+width+"x"+height);
            sizeProperty.SetValue(gameView,index);gameView.Show();gameView.Focus();await Gap(300);
            Check(Screen.width==width&&Screen.height==height,"actual Game view "+width+"x"+height);
            string path=Qa+"runtime-"+width+"x"+height+".png";ScreenCapture.CaptureScreenshot(path);File.AppendAllText(Qa+"created-paths.txt",path+"\n");await Gap(150);
        }
    }
}
#endif

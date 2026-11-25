#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.CaptureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using Object=UnityEngine.Object;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Kiem engine co gioi han tu menu; arena va actor tam khong luu vao scene asset.</summary>
    public static class EnemyAiVerification {
        static string DefaultRoot => QaArtifactPaths.Root+"enemy-ai-retaliation-skills-2026-11-16/";
        static string Root=>SessionState.GetString(Session+".root",DefaultRoot);
        const string Session="Capstone.EnemyAI.20261116";
        static readonly List<string> checks=new List<string>();
        static readonly List<CombatDamageResult> damage=new List<CombatDamageResult>();
        static bool running;static double deadline;static Scene arena;static NavMeshDataInstance navigation;
        static Vector3 origin;static PetCommandInput input;static GameMenuController menu;
        static EditorWindow gameView;static PropertyInfo sizeProperty;static int originalSize=-1;
        static string Mode=>SessionState.GetBool(Session+".girlAim",false)?"girl-aim":SessionState.GetBool(Session+".captureFeel",false)?"capturefeel":SessionState.GetBool(Session+".followup",false)?"followup":SessionState.GetBool(Session+".elements",false)?"elements":SessionState.GetBool(Session+".regression",false)?"lifecycle":SessionState.GetBool(Session+".edges",false)?"navigation":"combat";
        static void Write(string file,string content){Directory.CreateDirectory(Root);File.WriteAllText(Root+file,content);File.AppendAllText(Root+"created-paths.txt",Root+file+"\n");}
        static void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);Write(Mode+"-checks.txt",string.Join("\n",checks));if(!ok)throw new InvalidOperationException(label);}
        [MenuItem("Tools/Capstone/PvE/9. Verify retaliation skills and menu pause")]
        public static void Begin()=>BeginRun(false);
        [MenuItem("Tools/Capstone/PvE/10. Verify navigation lifecycle and 20-enemy budget")]
        public static void BeginNavigation()=>BeginRun(true);
        [MenuItem("Tools/Capstone/PvE/11. Verify wild buff capture respawn and owned skills")]
        public static void BeginLifecycle()=>BeginRun(false,true);
        public static void BeginAtRoot(string artifactRoot,bool navigation=false,bool lifecycle=false,bool capture=true){
            string full=Path.GetFullPath(artifactRoot),allowed=Path.GetFullPath(QaArtifactPaths.Root);
            if(!full.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Thu muc QA phai nam tren E ngoai Git.");
            BeginRun(navigation,lifecycle,full.Replace('\\','/').TrimEnd('/')+"/",false,capture);
        }
        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/1. Baseline combat")]
        public static void BaselineCombat()=>BeginAtRoot(QaArtifactPaths.Root+"gameplay-cleanup-elements-2026-11-17/baseline/");
        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/2. Baseline navigation and profiler")]
        public static void BaselineNavigation()=>BeginAtRoot(QaArtifactPaths.Root+"gameplay-cleanup-elements-2026-11-17/baseline/",true);
        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/3. Refactor combat regression")]
        public static void RefactorCombat()=>BeginAtRoot(QaArtifactPaths.Root+"gameplay-cleanup-elements-2026-11-17/refactor/");
        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/4. Refactor navigation regression")]
        public static void RefactorNavigation()=>BeginAtRoot(QaArtifactPaths.Root+"gameplay-cleanup-elements-2026-11-17/refactor/",true);
        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/7. Post-elements combat regression")]
        public static void PostElementCombat()=>BeginAtRoot(QaArtifactPaths.Root+"gameplay-cleanup-elements-2026-11-17/post-elements/",capture:false);
        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/8. Post-elements navigation and profiler")]
        public static void PostElementNavigation()=>BeginAtRoot(QaArtifactPaths.Root+"gameplay-cleanup-elements-2026-11-17/post-elements/",true,capture:false);
        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/9. Post-elements lifecycle regression")]
        public static void PostElementLifecycle()=>BeginAtRoot(QaArtifactPaths.Root+"gameplay-cleanup-elements-2026-11-17/post-elements/",lifecycle:true,capture:false);
        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/10. Run bounded post-element suite")]
        public static async void PostElementSuite(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Elements.ElementGameplayVerification.Contracts();
            if(!File.ReadAllText(Elements.ElementGameplayVerification.Root+"element-contract-status.txt").StartsWith("COMPLETE"))return;
            string root=QaArtifactPaths.Root+"gameplay-cleanup-elements-2026-11-17/post-elements/";
            for(int stage=0;stage<3;stage++){
                BeginAtRoot(root,stage==1,stage==2,false);
                string mode=stage==0?"combat":stage==1?"navigation":"lifecycle";
                double end=EditorApplication.timeSinceStartup+155;
                while(EditorApplication.timeSinceStartup<end&&(SessionState.GetBool(Session,false)||EditorApplication.isPlayingOrWillChangePlaymode))await Task.Delay(200);
                if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(root+mode+"-status.txt")||!File.ReadAllText(root+mode+"-status.txt").StartsWith("COMPLETE"))return;
                await Task.Delay(300);
            }
        }
        public static void BeginElementRun(string root)=>BeginRun(false,false,root,true);
        public static void BeginFollowupRun(string root)=>BeginRun(false,false,root,false,false,true);
        public static void BeginCaptureFeelRun(string root)=>BeginRun(false,false,root,false,false,false,true);
        public static void BeginGirlAimRun(string root)=>BeginRun(false,false,root,girlAim:true);
        static void BeginRun(bool edges,bool regression=false,string artifactRoot=null,bool elements=false,bool capture=true,bool followup=false,bool captureFeel=false,bool girlAim=false){
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!="Assets/Scenes/DemoScene 1.unity")throw new InvalidOperationException("Dừng Play và mở DemoScene 1.");
            SessionState.SetString(Session+".root",artifactRoot ?? DefaultRoot);
            SessionState.SetBool(Session+".elements",elements);
            SessionState.SetBool(Session+".followup",followup);
            SessionState.SetBool(Session+".captureFeel",captureFeel);
            SessionState.SetBool(Session+".girlAim",girlAim);
            SessionState.SetBool(Session+".capture",capture);
            checks.Clear();damage.Clear();running=false;SessionState.SetBool(Session,true);SessionState.SetBool(Session+".edges",edges);
            SessionState.SetBool(Session+".regression",regression);
            SessionState.SetBool(Session+".reload",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+".options",(int)EditorSettings.enterPlayModeOptions);
            SessionState.SetString(Session+".save",Root+"test-saves/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Watchdog;
            Write(Mode+"-status.txt","ENTERING_PLAY; bounded 150 seconds");EditorApplication.EnterPlaymode();
        }
        static void Watchdog(){if(EditorApplication.timeSinceStartup<deadline)return;Write(Mode+"-status.txt","TIMEOUT; do not restart Unity automatically");Finish();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){
            if(!SessionState.GetBool(Session,false)||running)return;
            string root=SessionState.GetString(Session+".save","");
            foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new PlayerJsonFileSaveStore(root));
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(root));
            Run();
        }
        static async Task Gap(int ms=100){await Task.Delay(ms);if(!EditorApplication.isPlaying||!SessionState.GetBool(Session,false))throw new OperationCanceledException("Verification stopped");}
        static async Task Until(Func<bool> predicate,float seconds,string label){float end=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<end)await Gap(40);Check(predicate(),label);}
        static void Record(CombatDamageResult result)=>damage.Add(result);
        static void Finish(){
            SessionState.SetBool(Session,false);running=false;EditorApplication.update-=Watchdog;CombatDamageEvents.Applied-=Record;GameplayPause.ReleaseAll();
            if(navigation.valid)navigation.Remove();if(arena.IsValid()&&arena.isLoaded)SceneManager.UnloadSceneAsync(arena);
            if(originalSize>=0&&gameView!=null){sizeProperty.SetValue(gameView,originalSize);originalSize=-1;}
            EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+".reload",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+".options",0);
            string save=SessionState.GetString(Session+".save","");if(Directory.Exists(save))foreach(string file in Directory.GetFiles(save,"*",SearchOption.AllDirectories))File.AppendAllText(Root+"created-paths.txt",file+"\n");
            if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
        }
        static async void Run(){
            running=true;bool background=Application.runInBackground;Application.runInBackground=true;
            try{
                await Gap(1200);input=Object.FindFirstObjectByType<PetCommandInput>();menu=Object.FindFirstObjectByType<GameMenuController>();
                var save=Object.FindFirstObjectByType<PlayerSaveController>();Check(save!=null&&save.SavePath.StartsWith(Root,StringComparison.OrdinalIgnoreCase),"isolated test save on E");
                foreach(var brain in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None))brain.enabled=false;
                foreach(var point in Object.FindObjectsByType<EncounterSpawnPoint>(FindObjectsSortMode.None))point.enabled=false;
                foreach(var pet in input.petSlots)if(pet!=null)pet.Withdraw();
                BuildArena();CombatDamageEvents.Applied+=Record;
                if(SessionState.GetBool("Capstone.R2",false)){SessionState.SetBool("Capstone.R2",false);await R2Verification.Run(input,arena,origin,Check,Gap);}else if(SessionState.GetBool(Session+".girlAim",false))await GirlAimVerification.Run(input,arena,origin,Check,Gap);else if(SessionState.GetBool(Session+".captureFeel",false))await CapturePresentationVerification.RunRuntime(input,arena,origin,Check,Gap);else if(SessionState.GetBool(Session+".followup",false))await Elements.GameplayCleanupFollowupVerification.RunRuntime(input,arena,origin,Check,Gap);else if(SessionState.GetBool(Session+".elements",false))await Elements.ElementGameplayVerification.RunRuntime(input,arena,origin,Check,Gap);else if(SessionState.GetBool(Session+".regression",false))await LifecycleChecks();else if(SessionState.GetBool(Session+".edges",false))await NavigationChecks();else await CombatChecks();
                Write(Mode+"-status.txt","COMPLETE "+checks.Count+" engine checks; "+DateTime.UtcNow.ToString("O"));
            }catch(Exception ex){Write(Mode+"-status.txt","FAILED\n"+ex);Debug.LogException(ex);}
            finally{Application.runInBackground=background;Finish();}
        }
        static void BuildArena(){
            arena=SceneManager.CreateScene("QA retaliation arena (temporary)");origin=input.transform.position+Vector3.right*1000;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="QA floor";floor.transform.position=origin-Vector3.up*.5f;floor.transform.localScale=new Vector3(70,1,70);SceneManager.MoveGameObjectToScene(floor,arena);
            var settings=NavMesh.GetSettingsByIndex(0);var source=new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(70,1,70),transform=floor.transform.localToWorldMatrix*Matrix4x4.Scale(new Vector3(1f/70,1,1f/70))};
            var data=NavMeshBuilder.BuildNavMeshData(settings,new List<NavMeshBuildSource>{source},new Bounds(origin,new Vector3(72,6,72)),Vector3.zero,Quaternion.identity);
            navigation=NavMesh.AddNavMeshData(data);Check(navigation.valid,"temporary NavMesh built without an asset bake");
            var movement=input.GetComponent<BasicPlayerMovement>();if(movement!=null)movement.enabled=false;
            var controller=input.GetComponent<CharacterController>();if(controller!=null)controller.enabled=false;input.transform.position=origin-Vector3.forward*3;if(controller!=null)controller.enabled=true;
            Physics.SyncTransforms();
        }
        static GameObject Spawn(string asset,Vector3 offset){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(asset);if(prefab==null)throw new InvalidOperationException(asset);var go=Object.Instantiate(prefab,origin+offset,Quaternion.identity);SceneManager.MoveGameObjectToScene(go,arena);return go;}
        static async Task<PetController> Owned(int form,Vector3 offset){
            var go=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon"+form.ToString("00")+".prefab",offset);var pet=go.GetComponent<PetController>();pet.AssignOwner(input.transform);pet.roamAroundOwner=false;
            pet.GetComponent<PetHudRuntimeStats>().SetStatus(10000,10000,0,0);
            Check(pet.TrySummonAt(origin+offset,Vector3.forward),"owned form "+form+" real summon begins");await Until(()=>pet.CanReceiveCommands,5,"owned form "+form+" summon confirmed");pet.enabled=false;
            input.petSlots[form-1]=pet;input.SetActivePet(pet);return pet;
        }
        static BasicCreatureEnemy Enemy(Vector3 offset,bool skills=false){
            var go=Spawn("Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab",offset);
            var brain=go.GetComponent<BasicCreatureEnemy>()??go.AddComponent<BasicCreatureEnemy>();brain.patrolRadius=0;brain.useSkills=skills;brain.skillChance=1;brain.DecisionRandomSource=()=>0;
            go.GetComponent<DummyEnemy>().autoRevive=false;return brain;
        }
        static void Provoke(BasicCreatureEnemy brain,PetController pet)=>brain.GetComponent<DummyEnemy>().ApplyCombatHit(new CombatHit(pet.gameObject,"qa-provocation",1,1,CreatureDamageKind.Physical,false,false,brain.transform.position));
        static int HitsFrom(GameObject source,string skill=null)=>damage.Count(d=>d.Hit.Source==source&&(skill==null||d.Hit.SkillId==skill));
        static void Place(GameObject go,Vector3 point){var agent=go.GetComponent<NavMeshAgent>();if(agent!=null&&agent.enabled&&agent.isOnNavMesh)agent.Warp(point);else go.transform.position=point;Physics.SyncTransforms();}
        static async Task CombatChecks(){
            var pet=await Owned(1,Vector3.zero);var brain=Enemy(Vector3.forward*1.5f);await Gap(1200);
            Check(brain.Target==null&&HitsFrom(brain.gameObject)==0,"nearby owned pet does not provoke enemy");
            Provoke(brain,pet);await Until(()=>brain.HasCommittedAttack,3,"actual HP damage provokes committed basic attack");
            menu.OpenInventoryDirect();await Until(()=>GameplayPause.IsPaused,2,"real Bag callback owns gameplay pause");
            float hp=pet.GetComponent<PetHudRuntimeStats>().Health,clock=Time.time,play=GameplayPause.PlayTime;int hits=HitsFrom(brain.gameObject);Vector3 position=brain.transform.position;
            var animator=brain.GetComponent<CreatureAnimationDriver>().BoundAnimator;float normalized=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            await Gap(650);
            Check(Time.time==clock&&Mathf.Abs(GameplayPause.PlayTime-play)<.005f&&hp==pet.GetComponent<PetHudRuntimeStats>().Health&&HitsFrom(brain.gameObject)==hits&&Vector3.Distance(position,brain.transform.position)<.001f,"menu freezes HP AI movement and both gameplay clocks");
            Check(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime-normalized)<.01f,"menu freezes committed attack animation");
            menu.OpenSettingsDirect();await Gap(180);Check(GameplayPause.IsPaused,"Bag to Settings handoff remains paused");
            menu.HandleShortcut(KeyCode.Escape);await Until(()=>!GameplayPause.IsPaused,2,"Back resumes gameplay");
            await Until(()=>HitsFrom(brain.gameObject,"basic")>hits,5,"paused windup resumes and delivers real basic damage");
            Check(damage.Where(d=>d.Hit.Source==brain.gameObject).GroupBy(d=>d.Hit.SkillId+":"+d.Hit.CastId+":"+d.Hit.EmissionIndex).All(g=>g.Count()==1),"one damage result per basic token");
            brain.enabled=false;Object.Destroy(brain.gameObject);await Gap();
            var ranged=Enemy(Vector3.forward*7,true);await Gap(100);Provoke(ranged,pet);
            var trace=new List<string>();float rangedUntil=Time.realtimeSinceStartup+8;
            while(HitsFrom(ranged.gameObject,"orb.dark")==0&&Time.realtimeSinceStartup<rangedUntil){
                var act=ranged.GetComponent<PetCombatActionDriver>();var exe=ranged.GetComponent<PetSkillCombatExecutor>();var pres=ranged.GetComponent<PetSkillPresentationPlayer>();var hud=ranged.GetComponent<PetHudRuntimeStats>();
                trace.Add($"{Time.time:F3} {ranged.State} target={ranged.Target?.name} reason={ranged.LastReason} phase={act.Phase} starts={act.StartedCount} cancel={act.CancelledCount} reject={act.LastRejection} norm={act.Normalized:F3} released={pres?.ReleasedCount} projectiles={exe?.ActiveProjectileCount} hits={exe?.HitCount} exec={exe?.LastRejection}"+" mouth="+pres?.SocketPosition("Mouth")+" slots="+string.Join(",",hud.GetSkills().Select(s=>s.skillId+"/"+s.unlocked+"/"+s.usable)));await Gap(100);
            }
            Write("ranged-trace.txt",string.Join("\n",trace));Check(HitsFrom(ranged.gameObject,"orb.dark")>0,"enemy equipped ranged skill uses shared projectile and actual damage");
            Check(ranged.GetComponent<PetCombatActionDriver>().LastReleaseNormalized>=.3f,"enemy skill release follows animation progress");
            pet.enabled=true;input.transform.position=origin-Vector3.forward;
            var petStats=pet.GetComponent<PetHudRuntimeStats>();petStats.RequestSkill(1);var petAction=pet.GetComponent<PetCombatActionDriver>();
            await Until(()=>petAction!=null&&petAction.IsSkillBusy,2,"owned pet starts real buff windup");
            int cancelled=petAction.CancelledCount,cast=petAction.CastToken;var cooldown=petStats.SnapshotCooldowns();
            menu.OpenInventoryDirect();await Until(()=>GameplayPause.IsPaused,1,"full menu pauses owned skill");await Gap(550);
            Check(petAction.IsSkillBusy&&petAction.CastToken==cast&&petAction.CancelledCount==cancelled&&petStats.SnapshotCooldowns().Zip(cooldown,(a,b)=>Mathf.Abs(a-b)<.02f).All(equal=>equal),"menu preserves owned cast token and cooldown without cancelling");
            menu.HandleShortcut(KeyCode.Escape);await Until(()=>!GameplayPause.IsPaused,2,"Back resumes owned skill");
            await Until(()=>pet.GetComponent<CreatureCombatant>().OffenseBuffRemaining>0,4,"owned buff releases once after resume");pet.enabled=false;
            await Resolution(1920,1080,"combat");await Resolution(1366,768,"combat");
            var second=await Owned(2,Vector3.right*2);pet.Withdraw();input.SetActivePet(second);
            await Until(()=>ranged.Target==second.GetComponent<CreatureCombatant>(),3,"provoked enemy changes target to same team's newly summoned pet");
            second.Withdraw();await Until(()=>ranged.Target==null&&ranged.CurrentState==EnemyAiState.Return,3,"withdrawing all pets disengages and returns home");
            await Until(()=>ranged.AtHome,8,"enemy navigates home");ranged.enabled=false;Object.Destroy(ranged.gameObject);await Gap();
            var token=new GameObject("QA nested pause owner");SceneManager.MoveGameObjectToScene(token,arena);Time.timeScale=.65f;GameplayPause.SetPaused(token,true);menu.OpenInventoryDirect();GameplayPause.SetPaused(token,false);await Gap(100);
            Check(GameplayPause.IsPaused&&Time.timeScale==0,"nested menu owner retains pause");menu.HandleShortcut(KeyCode.Escape);await Until(()=>!GameplayPause.IsPaused,2,"final owner releases nested pause");Check(Mathf.Approximately(Time.timeScale,.65f),"resume restores previous time scale");Time.timeScale=1;
        }
        static async Task NavigationChecks(){
            var pet=await Owned(1,Vector3.forward*8);var brain=Enemy(Vector3.zero);await Gap(100);Provoke(brain,pet);Vector3 start=brain.transform.position;
            await Until(()=>Vector3.Distance(start,brain.transform.position)>1,4,"retaliating enemy follows a complete NavMesh path");
            Place(pet.gameObject,origin+Vector3.forward*25);await Until(()=>brain.Target==null,3,"target outside leash is dropped");await Until(()=>brain.AtHome,8,"enemy returns inside territory without teleporting");
            Place(pet.gameObject,origin+Vector3.forward*7);Provoke(brain,pet);await Until(()=>brain.Target!=null,2,"new damage reengages returned enemy");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="QA opaque wall";wall.transform.position=origin+Vector3.forward*4+Vector3.up*2;wall.transform.localScale=new Vector3(30,4,1);SceneManager.MoveGameObjectToScene(wall,arena);var obstacle=wall.AddComponent<NavMeshObstacle>();obstacle.carving=true;Physics.SyncTransforms();
            await Until(()=>brain.Target==null,5,"blocked line of sight times out search and disengages");Check(brain.transform.position.z<wall.transform.position.z+.5f,"enemy does not cross physical wall");
            Object.Destroy(wall);await Gap(200);brain.enabled=false;Object.Destroy(brain.gameObject);await Gap();
            var stranded=Enemy(Vector3.right*80);await Gap(100);Provoke(stranded,pet);Vector3 outside=stranded.transform.position;await Gap(1400);
            Check(Vector3.Distance(outside,stranded.transform.position)<.1f&&stranded.LandedHits==0,"missing NavMesh has no transform-chase or teleport fallback");Object.Destroy(stranded.gameObject);await Gap();
            int registryBefore=CreatureCombatant.Active.Count;
            int CallbackCount()=>((Delegate)typeof(CombatDamageEvents).GetField("Applied",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null))?.GetInvocationList().Length??0;
            int callbacksBefore=CallbackCount();
            var many=new List<BasicCreatureEnemy>();for(int i=0;i<20;i++){var next=Enemy(new Vector3((i%5)*4-8,0,(i/5)*4-8));next.patrolRadius=3.5f;many.Add(next);}await Gap(300);
            using(var recorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Capstone.EnemyAI.Tick",120))
            using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",120)){
                var samples=new List<long>();var gcSamples=new List<long>();int paths=many.Sum(b=>b.PathRequests);float until=Time.realtimeSinceStartup+3;
                while(Time.realtimeSinceStartup<until){await Gap(50);if(recorder.Valid)samples.Add(recorder.LastValue);if(gc.Valid)gcSamples.Add(gc.LastValue);}
                Write("performance.txt","20 temporary neutral enemies; 3 seconds wall time\nRecorder valid="+recorder.Valid+"; samples="+samples.Count+"; max ns="+(samples.Count>0?samples.Max():0)+"; mean ns="+(samples.Count>0?samples.Average():0)+"\nPath requests="+(many.Sum(b=>b.PathRequests)-paths)+"\nGlobal frame GC valid="+gc.Valid+"; mean bytes="+(gcSamples.Count>0?gcSamples.Average():0)+"; max bytes="+(gcSamples.Count>0?gcSamples.Max():0)+"; not exclusive AI allocations");
                Check(many.All(b=>b.Target==null&&b.LandedHits==0),"20 neutral enemies patrol without proximity attacks");
                Check(many.Sum(b=>b.PathRequests)-paths<=20*4*4,"path requests remain within configured budget");
            }
            await Resolution(1920,1080,"navigation");await Resolution(1366,768,"navigation");
            foreach(var next in many)Object.Destroy(next.gameObject);await Gap(300);Check(many.All(b=>b==null),"temporary enemy actors are destroyed without pending AI callbacks");
            for(int cycle=0;cycle<2;cycle++){
                many.Clear();for(int i=0;i<20;i++)many.Add(Enemy(new Vector3(i%5*4-8,0,i/5*4-8)));
                await Gap(250);foreach(var actor in many)Object.Destroy(actor.gameObject);await Gap(250);
            }
            Check(CreatureCombatant.Active.Count==registryBefore,"combat registry returns to baseline after three cycles of 20 enemies");
            Check(CallbackCount()==callbacksBefore,"damage event subscriber count returns after three spawn destroy cycles");
            Write("lifecycle-counters.txt","combat registry before="+registryBefore+" after="+CreatureCombatant.Active.Count+"; event callbacks before="+callbacksBefore+" after="+CallbackCount()+"; temporary enemy cycles=3 x 20");
        }
        sealed class FixedRandom:ICaptureRandom{public double NextFloat01()=>0;}
        static async Task LifecycleChecks(){
            var pet=await Owned(1,Vector3.zero);var box=Object.FindFirstObjectByType<PetBoxRuntimeProvider>();box.Bind(input);
            var wildGo=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/Wild/Wild_Dragon01.prefab",Vector3.forward*6);
            var brain=wildGo.GetComponent<BasicCreatureEnemy>()??wildGo.AddComponent<BasicCreatureEnemy>();brain.patrolRadius=0;brain.useSkills=false;brain.moveSpeed=.1f;brain.attackRange=.2f;
            await Gap(150);Provoke(brain,pet);await Until(()=>brain.Target!=null,2,"wild dragon accepts retaliation target");
            var actions=wildGo.GetComponent<PetCombatActionDriver>();Check(actions.Submit(1),"wild dragon starts equipped roar buff through shared driver");
            var combat=wildGo.GetComponent<CreatureCombatant>();await Until(()=>combat.OffenseBuffRemaining>0,4,"wild dragon buff releases from animation");await Gap(500);
            Check(combat.OffenseMultiplier>1.19f&&combat.OffenseBuffRemaining>4,"wild role retains six-second offense buff");
            pet.Withdraw();await Until(()=>brain.Target==null,2,"wild enemy enters return when team is withdrawn");
            var health=wildGo.GetComponent<DummyEnemy>();float before=health.CurrentHealth;Provoke(brain,pet);Check(health.CurrentHealth<before&&brain.Target==null,"returning enemy takes damage without an eligible summoned target");
            var pointGo=new GameObject("QA lifecycle owner");pointGo.SetActive(false);SceneManager.MoveGameObjectToScene(pointGo,arena);pointGo.transform.position=wildGo.transform.position;
            var point=pointGo.AddComponent<EncounterSpawnPoint>();point.initialActor=health;point.encounterPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Dragons/Wild/Wild_Dragon01.prefab");point.actorScale=wildGo.transform.localScale;point.settings.Read(brain);
            point.smokePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/DemoSceneOne/DeathSmoke.prefab");pointGo.SetActive(true);
            var bag=Object.FindFirstObjectByType<MonsterInventoryAdapter>();var ball=CaptureAssets.LoadBall();var coordinator=Object.FindFirstObjectByType<PetCaptureCoordinator>();coordinator.RandomSource=new FixedRandom();
            string id=wildGo.GetComponent<PetCollectionMetadata>().PetId;int balls=bag.GetQuantity(ball);
            Check(coordinator.TryCaptureTarget(ball,bag,wildGo.GetComponent<WildPetCaptureTarget>(),out var result,out var error)&&result.Success,"returning enemy remains capturable: "+error);
            Check(bag.GetQuantity(ball)==balls-1&&box.Contains(wildGo.GetComponent<PetController>())&&!brain.enabled,"capture commits one ball and disables enemy brain");
            await Until(()=>point.RespawnPending,2,"capture detaches individual and schedules respawn");await Until(()=>point.CurrentActor!=null,7,"fresh encounter spawns after five seconds");
            var next=point.CurrentActor;Check(next.GetComponent<PetCollectionMetadata>().PetId!=id&&next.GetComponent<BasicCreatureEnemy>().useSkills==point.settings.useSkills,"respawn has fresh individual ID and copied AI settings");
            next.ApplyCombatHit(new CombatHit(pet.gameObject,"qa-down",2,100000,CreatureDamageKind.Physical,false,false,next.TargetPosition));Check(next.IsDowned,"zero HP opens downed capture window");
            menu.OpenInventoryDirect();await Until(()=>GameplayPause.IsPaused,1,"menu pauses downed window");float remaining=next.DownedSecondsRemaining;await Gap(700);Check(Mathf.Abs(next.DownedSecondsRemaining-remaining)<.02f,"downed timer freezes while menu is open");menu.HandleShortcut(KeyCode.Escape);await Until(()=>!GameplayPause.IsPaused,2,"close menu resumes downed window");
            await Gap(150);var animator=next.GetComponent<CreatureAnimationDriver>().BoundAnimator;Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle")||animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Idle"),"downed uses Idle when no Stun animation exists");
            int terminal=0;next.Defeated+=_=>terminal++;await Until(()=>terminal==1,17,"downed expiry commits one terminal defeat");await Until(()=>point.LifecycleState=="Smoke",5,"Die finishes before smoke presentation");await Until(()=>point.RespawnPending,2,"smoke hides old actor before scheduling replacement");
            point.enabled=false;Object.Destroy(pointGo);if(next!=null)Object.Destroy(next.gameObject);await Gap(150);
            var owned=await Owned(3,Vector3.zero);owned.enabled=true;input.transform.position=origin-Vector3.forward;var target=Enemy(Vector3.forward*2);target.enabled=false;await Gap(100);target.GetComponent<DummyEnemy>().InitializeCaptureHealth(10000,10000);
            var stats=owned.GetComponent<PetHudRuntimeStats>();
            for(int slot=0;slot<4;slot++){
                string skill=stats.GetSkills()[slot].skillId;stats.RequestSkill(slot);var driver=owned.GetComponent<PetCombatActionDriver>();await Until(()=>driver!=null&&driver.StartedCount>slot,3,"owned form 03 starts equipped skill "+skill);
                if(slot==1)await Until(()=>owned.GetComponent<CreatureCombatant>().OffenseBuffRemaining>0,4,"owned roar still buffs correctly");
                else await Until(()=>damage.Any(d=>d.Hit.Source==owned.gameObject&&d.Hit.SkillId==skill),5,"owned skill still deals actual HP damage: "+skill);
                await Until(()=>!driver.IsSkillBusy,5,"owned cast finishes: "+skill);await Gap(200);
            }
            target.enabled=true;await Gap(100);Provoke(target,owned);await Until(()=>target.Target!=null,2,"enemy is engaged before a real summon switch");
            var movement=input.GetComponent<BasicPlayerMovement>();movement.enabled=true;await Gap(250);
            input.RequestSummonSlot(1);var director=input.GetComponent<PetSummon.PetSummonDirector>();
            await Until(()=>director.PendingSummonPet!=null,3,"real director begins replacement throw");await Gap(120);
            Check(target.ThreatOwner==input.transform,"enemy keeps provoked team during recall and throw gap");
            await Until(()=>input.activePet==input.petSlots[1]&&input.activePet.CanReceiveCommands,10,"real replacement summon finishes");
            await Until(()=>target.Target==input.activePet.GetComponent<CreatureCombatant>(),2,"enemy follows team after actual director-confirmed switch");
            await Resolution(1920,1080,"lifecycle");await Resolution(1366,768,"lifecycle");
        }
        public static Task ResolutionForElements(int width,int height,string label)=>Resolution(width,height,label);
        static async Task Resolution(int width,int height,string label){
            var assembly=typeof(UnityEditor.Editor).Assembly;const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            var viewType=assembly.GetType("UnityEditor.GameView");gameView=EditorWindow.GetWindow(viewType,false,"Game",false);sizeProperty=viewType.GetProperty("selectedSizeIndex",flags);if(originalSize<0)originalSize=(int)sizeProperty.GetValue(gameView);
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
            var method=sizesType.GetMethod("GetGroup",flags);var group=method.Invoke(singleton,new[]{Enum.Parse(method.GetParameters()[0].ParameterType,"Standalone")});int count=(int)group.GetType().GetMethod("GetTotalCount",flags).Invoke(group,null),index=-1;
            for(int i=0;i<count;i++){var size=group.GetType().GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});var type=size.GetType();if((int)type.GetProperty("width",flags).GetValue(size)==width&&(int)type.GetProperty("height",flags).GetValue(size)==height){index=i;break;}}
            if(index<0)throw new InvalidOperationException("Missing Game view preset "+width+"x"+height);sizeProperty.SetValue(gameView,index);gameView.Show();gameView.Focus();await Gap(200);Check(Screen.width==width&&Screen.height==height,"native Game view "+width+"x"+height);
            if(SessionState.GetBool(Session+".capture",true)){string file=Root+label+"-"+width+"x"+height+".png";ScreenCapture.CaptureScreenshot(file);File.AppendAllText(Root+"created-paths.txt",file+"\n");await Gap(150);}
        }
    }
}
#endif

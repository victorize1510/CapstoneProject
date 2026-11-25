#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.WorldFeedback;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CaptureSystem
{
    // Kiem co gioi han, chi tu menu; khong sua scene/clip/controller hay save that.
    public static class CapturePresentationVerification
    {
        sealed class FixedRandom : ICaptureRandom { public double NextFloat01() => 0; }
        public static string Root => QaArtifactPaths.Root + "capture-presentation-repair-2026-11-17/";
        public static void Write(string name, string value)
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Root + name, value);
            File.AppendAllText(Root + "created-paths.txt", Root + name + "\n");
        }
        static void Shot(string name)
        {
            string path = Root + name + ".png";
            ScreenCapture.CaptureScreenshot(path);
            File.AppendAllText(Root + "created-paths.txt", path + "\n");
        }
        [MenuItem("Tools/Capstone/Advanced/Capture/1. Audit player throw bindings")]
        public static void Audit()
        {
            var text = new StringBuilder();
            foreach (var switcher in Object.FindObjectsByType<PlayerVisualSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                for (int i = 0; switcher.TryGetVisualProfile(i, out var profile); i++)
                {
                    var animator = profile.animator;
                    var controller = profile.controller != null ? profile.controller : animator != null ? animator.runtimeAnimatorController : null;
                    text.AppendLine($"visual={profile.displayName}; scale={profile.localScale}; root={profile.root?.name}; animator={animator?.name}; avatar={AssetDatabase.GetAssetPath(animator?.avatar)}; human={animator?.isHuman}; controller={AssetDatabase.GetAssetPath(controller)}; releaseFrame={profile.throwReleaseFrame}");
                    if (controller == null) continue;
                    foreach (var clip in controller.animationClips.Distinct().Where(c => c.name.IndexOf("throw", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        text.AppendLine($" clip={clip.name}; asset={AssetDatabase.GetAssetPath(clip)}; human={clip.humanMotion}; duration={clip.length}; fps={clip.frameRate}");
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.IndexOf("Scale", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            var curve = AnimationUtility.GetEditorCurve(clip, binding);
                            text.AppendLine($" SCALE {binding.path} {binding.propertyName}: " + string.Join(",", curve.keys.Select(k => k.time + "=" + k.value)));
                        }
                    }
                }
            }
            Write("throw-bindings.txt", text.ToString());
        }
        [MenuItem("Tools/Capstone/Advanced/Capture/2. Baseline capture motion (isolated)")]
        public static void Baseline()
        {
            SessionState.SetBool("Capstone.CaptureFeel.Manual",false);
            SessionState.SetBool("Capstone.CaptureFeel.After", false);
            EnemyAiVerification.BeginCaptureFeelRun(Root);
        }
        [MenuItem("Tools/Capstone/Advanced/Capture/3. Verify repaired capture motion (isolated)")]
        public static void After()
        {
            SessionState.SetBool("Capstone.CaptureFeel.Manual",false);
            SessionState.SetBool("Capstone.CaptureFeel.After", true);
            EnemyAiVerification.BeginCaptureFeelRun(Root);
        }
        [MenuItem("Tools/Capstone/Advanced/Capture/4. Verify real G input and summon (isolated)")]
        public static void Manual()
        {
            BeginManual(0);
        }
        [MenuItem("Tools/Capstone/Advanced/Capture/5. Verify real G with male player (isolated)")]
        public static void ManualBoy()
        {
            BeginManual(1);
        }
        static void BeginManual(int visual)
        {
            SessionState.SetBool("Capstone.CaptureFeel.Manual",true);
            SessionState.SetInt("Capstone.CaptureFeel.Visual",visual);
            EnemyAiVerification.BeginCaptureFeelRun(Root);
        }
        public static async Task RunRuntime(PetCommandInput input, Scene arena, Vector3 origin, Action<bool, string> check, Func<int, Task> gap)
        {
            if(SessionState.GetBool("Capstone.CaptureFeel.Manual",false))
            {
                await PublicInput(input,arena,origin,check,gap);
                return;
            }
            Audit();
            bool after = SessionState.GetBool("Capstone.CaptureFeel.After", false);
            string prefix = after ? "after-" : "before-";
            var movement = input.GetComponent<BasicPlayerMovement>();
            var switcher = input.GetComponent<PlayerVisualSwitcher>();
            var rows = new StringBuilder("visual\ttime\tstate\tnormalized\trootScale\tboneScaleMax\tskinSize\n");
            movement.enabled = true;
            await gap(500);
            for (int i = 0; switcher.TryGetVisualProfile(i, out var profile); i++)
            {
                switcher.SwitchTo(i, true);
                await gap(200);
                check(movement.TryPlayScriptedThrow(), "real player throw starts " + profile.displayName);
                float end = Time.realtimeSinceStartup + 4;
                var mesh = new Mesh();
                bool photographed = false;
                while (movement.IsScriptedActionPlaying && Time.realtimeSinceStartup < end)
                {
                    var skin = profile.root.GetComponentInChildren<SkinnedMeshRenderer>();
                    Vector3 size = Vector3.zero;
                    float boneScale = 0;
                    if (skin != null)
                    {
                        skin.BakeMesh(mesh);
                        mesh.RecalculateBounds();
                        size = mesh.bounds.size;
                        foreach (var bone in skin.bones) if (bone != null) boneScale = Mathf.Max(boneScale, Mathf.Max(Mathf.Abs(bone.localScale.x), Mathf.Abs(bone.localScale.y), Mathf.Abs(bone.localScale.z)));
                    }
                    rows.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0}\t{1:F3}\t{2}\t{3:F3}\t{4}\t{5}\t{6}",profile.displayName,Time.time,movement.ActiveScriptedAction,movement.ScriptedActionNormalizedTime,profile.root.transform.localScale,boneScale,size));
                    if(after&&!photographed&&movement.ScriptedActionNormalizedTime>=.45f){Shot("throw-"+profile.displayName);photographed=true;}
                    await gap(50);
                }
                Object.Destroy(mesh);
                check(!movement.IsScriptedActionPlaying, "player throw returns to locomotion " + profile.displayName);
            }
            Write(prefix + "player-motion.tsv", rows.ToString());
            switcher.SwitchTo(0, true);
            await gap(250);
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Bats/Wild/Wild_Bat01.prefab"), origin + Vector3.forward * 4, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(actor, arena);
            var brain = actor.GetComponent<BasicCreatureEnemy>();
            if (brain != null) brain.enabled = false;
            var target = actor.GetComponent<WildPetCaptureTarget>();
            check(target != null && target.IsWild, "presentation uses exact wild encounter instance");
            var renderer = actor.GetComponentsInChildren<Renderer>().First(r => r is SkinnedMeshRenderer || r is MeshRenderer);
            var presentation = input.gameObject.AddComponent<CapturePresentation>();
            presentation.Play(new CapturePresentationResult(new CaptureRoll(false, false, 1), target), input.transform, null);
            await gap(after?250:100);
            Write(prefix + "capture-observation.txt", $"targetVisibleAtStart={renderer.enabled}; playerAction={movement.ActiveScriptedAction}; capturePlaying={presentation.IsPlaying}; targetId={target.Metadata.PersistentId}");
            float deadline = Time.realtimeSinceStartup + 12;
            var phases = new HashSet<CapturePresentation.MotionPhase>();
            Vector3 scaleBefore=input.transform.localScale;
            while (presentation.IsPlaying && Time.realtimeSinceStartup < deadline)
            {
                if (phases.Add(presentation.Phase) && after)
                {
                    if(presentation.Phase==CapturePresentation.MotionPhase.Flight||presentation.Phase==CapturePresentation.MotionPhase.Absorb||presentation.Phase==CapturePresentation.MotionPhase.Shake)Shot("capture-"+presentation.Phase);
                }
                await gap(20);
            }
            check(!presentation.IsPlaying && renderer.enabled && !target.Enemy.CaptureLocked, "failed capture restores exact target visibility and damage gate");
            if(after)
            {
                check(phases.Contains(CapturePresentation.MotionPhase.Flight)&&phases.Contains(CapturePresentation.MotionPhase.Absorb)&&phases.Contains(CapturePresentation.MotionPhase.Drop)&&phases.Contains(CapturePresentation.MotionPhase.Shake)&&phases.Contains(CapturePresentation.MotionPhase.Release),"actual capture passes throw flight absorb gravity settle shake and escape");
                check(presentation.CompletedShakes==1,"failed presentation maps to one shake without extra RNG");
                check(input.transform.localScale==scaleBefore,"capture never changes player root scale");
                check(renderer.GetComponentInParent<PetController>()==target.Pet,"escape returns original individual renderer ownership");
                check(presentation.ReleaseNormalized>0&&presentation.ReleaseNormalized<1,"capture reads actual model release frame");
                var originalId=target.Metadata.PersistentId;
                var targetScale=target.transform.localScale;
                presentation.Play(new CapturePresentationResult(new CaptureRoll(false,false,3),target),input.transform,null);
                deadline=Time.realtimeSinceStartup+12;
                while(presentation.IsPlaying&&Time.realtimeSinceStartup<deadline)await gap(30);
                check(presentation.CompletedShakes==2&&target.Metadata.PersistentId==originalId&&target.transform.localScale==targetScale,"three failed math checks present as two shakes; same individual and model scale restored");
                foreach(var phase in new[]{CapturePresentation.MotionPhase.Windup,CapturePresentation.MotionPhase.Absorb,CapturePresentation.MotionPhase.Shake})
                {
                    while(movement.IsScriptedActionPlaying)await gap(30);
                    presentation.Play(new CapturePresentationResult(new CaptureRoll(false,false,1),target),input.transform,null);
                    deadline=Time.realtimeSinceStartup+8;
                    while(presentation.IsPlaying&&presentation.Phase!=phase&&Time.realtimeSinceStartup<deadline)await gap(20);
                    check(presentation.Phase==phase,"cancel fixture reached "+phase);
                    presentation.Cancel();
                    await gap(30);
                    check(!presentation.IsPlaying&&renderer.enabled&&!target.Enemy.CaptureLocked&&!InventoryInputController.GameplayInputBlocked,"cancel restores target and input at "+phase);
                }
                while(movement.IsScriptedActionPlaying)await gap(30);
                var inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();
                var ball=CaptureAssets.LoadBall();
                inventory.TryAddItemAtomic(ball,5);
                var roster=Object.FindFirstObjectByType<PetBoxRuntimeProvider>();roster.Bind(input);
                var save=Object.FindFirstObjectByType<PlayerSaveController>();
                var coordinator=Object.FindFirstObjectByType<PetCaptureCoordinator>();
                coordinator.RandomSource=new FixedRandom();
                int balls=inventory.GetQuantity(ball),events=0;
                Action<PetController> receipt=pet=>events++;
                coordinator.CaptureCommitted+=receipt;
                try
                {
                    check(coordinator.TryCaptureTarget(ball,inventory,target,out var caught,out var error)&&caught.Success,"actual capture transaction commits: "+error);
                    check(inventory.GetQuantity(ball)==balls-1&&roster.Contains(target.Pet)&&events==1,"one ball consumed and exact target enters roster once");
                    var persisted=PlayerSaveMigration.Read(File.ReadAllText(save.SavePath),out _);
                    check(persisted.pets.petStates.Count(p=>p.petId==originalId)==1,"captured individual persisted before presentation toast");
                    int toastBefore=AcquisitionToastView.Ensure().DisplayedReceipts;
                    coordinator.Present(caught,null);
                    var actual=coordinator.GetComponent<CapturePresentation>();
                    deadline=Time.realtimeSinceStartup+12;
                    while(actual.IsPlaying&&Time.realtimeSinceStartup<deadline)await gap(30);
                    await gap(150);
                    check(actual.CompletedShakes==3&&AcquisitionToastView.Ensure().DisplayedReceipts==toastBefore+1,"success always shakes three times then shows one real capture receipt");
                    Shot("capture-saved-toast");
                    actual.Cancel();actual.Cancel();await gap(80);
                    check(inventory.GetQuantity(ball)==balls-1&&events==1&&AcquisitionToastView.Ensure().DisplayedReceipts==toastBefore+1,"repeated cancel cannot repeat item save event or toast");
                    await EnemyAiVerification.ResolutionForElements(1366,768,"capture-final");
                    Shot("capture-1366");
                }
                finally{coordinator.CaptureCommitted-=receipt;}
            }
            check(!Inventory.InventoryInputController.GameplayInputBlocked, "capture releases input without a stuck owner");
            Object.Destroy(presentation);
            Write(prefix + "status.txt", "COMPLETE bounded presentation and player baseline");
        }
        static async Task PublicInput(PetCommandInput input,Scene arena,Vector3 origin,Action<bool,string> check,Func<int,Task> gap)
        {
            input.GetComponent<BasicPlayerMovement>().enabled=true;
            await gap(500);
            var switcher=input.GetComponent<PlayerVisualSwitcher>();
            switcher.SwitchTo(SessionState.GetInt("Capstone.CaptureFeel.Visual",0),true);
            await gap(200);
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Bats/Wild/Wild_Bat01.prefab"),input.transform.position+input.transform.forward*5,Quaternion.identity);
            SceneManager.MoveGameObjectToScene(actor,arena);
            var brain=actor.GetComponent<BasicCreatureEnemy>();if(brain!=null)brain.enabled=false;
            var target=actor.GetComponent<WildPetCaptureTarget>();
            var inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();
            var ball=CaptureAssets.LoadBall();inventory.TryAddItemAtomic(ball,5);
            var coordinator=Object.FindFirstObjectByType<PetCaptureCoordinator>();coordinator.RandomSource=new FixedRandom();
            var roster=Object.FindFirstObjectByType<PetBoxRuntimeProvider>();roster.Bind(input);
            var hud=Object.FindFirstObjectByType<HubCaptureHud>();
            check(hud!=null,"actual G HUD exists");
            check(coordinator.CanCaptureFromBag(ball,inventory,out string reason),"public preflight accepts grounded idle player: "+reason);
            int before=hud.ThrowIntents,quantity=inventory.GetQuantity(ball);
            var skin=switcher.ActiveVisualRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            var scales=skin.bones.Where(b=>b!=null).ToDictionary(b=>b,b=>b.localScale);
            Vector3 playerScale=input.transform.localScale;
            await EnemyAiVerification.ResolutionForElements(1920,1080,"manual-g");
            Write("manual-g-status.txt","READY: focus Game view and tap G once; bounded 40 seconds");
            float end=Time.realtimeSinceStartup+40;
            while(hud.ThrowIntents==before&&Time.realtimeSinceStartup<end)await gap(40);
            if(hud.ThrowIntents==before){Write("manual-g-status.txt","NOT RUN: no native G key arrived within 40 seconds");return;}
            check(hud.ThrowIntents==before+1&&coordinator.IsCapturing,"one native G tap starts actual capture once");
            check(!hud.TryThrow(),"repeated throw callback is blocked while capturing");
            var visual=coordinator.GetComponent<CapturePresentation>();
            bool photographed=false,scaleStable=true;
            end=Time.realtimeSinceStartup+12;
            while(coordinator.IsCapturing&&Time.realtimeSinceStartup<end)
            {
                scaleStable&=input.transform.localScale==playerScale&&scales.All(p=>p.Key!=null&&Vector3.Distance(p.Key.localScale,p.Value)<.0001f);
                if(!photographed&&visual.Phase==CapturePresentation.MotionPhase.Absorb)
                {
                    await gap(260);Shot("native-G-"+switcher.ActiveVisualName+"-absorb");photographed=true;
                }
                await gap(20);
            }
            check(scaleStable,"native capture does not squash player root or any bound bone scale");
            check(!coordinator.IsCapturing&&inventory.GetQuantity(ball)==quantity-1&&roster.Contains(target.Pet),"native G consumes one ball and saves exact individual");
            check(visual.CompletedShakes==3&&!InventoryInputController.GameplayInputBlocked,"native G finishes three shakes and restores controls");
            Shot("native-G-"+switcher.ActiveVisualName+"-saved");
            var pet=input.petSlots[0];
            check(pet!=null,"existing owned pet slot available for summon regression");
            input.RequestSummonSlot(0);
            end=Time.realtimeSinceStartup+16;
            while((!pet.CanReceiveCommands||input.GetComponent<PetSummon.PetSummonDirector>().IsSequenceRunning)&&Time.realtimeSinceStartup<end)await gap(40);
            check(pet.CanReceiveCommands,"summon still releases from shared actual Throw timing");
            check(input.transform.localScale==playerScale&&scales.All(p=>p.Key!=null&&Vector3.Distance(p.Key.localScale,p.Value)<.0001f),"summon does not scale player or skeleton");
            Write("manual-g-status.txt","COMPLETE native G key and summon regression");
        }
    }
}
#endif

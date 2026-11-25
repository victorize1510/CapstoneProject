#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.CaptureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using System.Reflection;
using Capstone.Game.PetSummon;
using Capstone.Game.UISystem;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem
{
    // Chi menu chu dong, save/arena tam tren E, khong sua scene dang mo.
    public static class GirlAimVerification
    {
        public static string Root => QaArtifactPaths.Root + "girl-throw-m3-fix-2026-11-17/";
        public static void Write(string name,string text)
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Root+name,text);
            File.AppendAllText(Root+"created-paths.txt",Root+name+"\n");
        }
        static void Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Root+name+".png");
            File.AppendAllText(Root+"created-paths.txt",Root+name+".png\n");
        }
        [MenuItem("Tools/Capstone/Advanced/Capture/6. Inspect first girl throw poses (isolated)")]
        public static void Before(){SessionState.SetBool("Capstone.GirlAim.CameraOnly",false);SessionState.SetBool("Capstone.GirlAim.After",false);EnemyAiVerification.BeginGirlAimRun(Root);}
        [MenuItem("Tools/Capstone/Advanced/Capture/7. Verify first girl throw and camera selection (isolated)")]
        public static void After(){SessionState.SetBool("Capstone.GirlAim.CameraOnly",false);SessionState.SetBool("Capstone.GirlAim.After",true);EnemyAiVerification.BeginGirlAimRun(Root);}
        [MenuItem("Tools/Capstone/Advanced/Capture/8. Verify M3 and LMB only (isolated)")]
        public static void CameraOnly(){SessionState.SetBool("Capstone.GirlAim.CameraOnly",true);EnemyAiVerification.BeginGirlAimRun(Root);}
        public static async Task Run(PetCommandInput input,Scene arena,Vector3 origin,Action<bool,string> check,Func<int,Task> gap)
        {
            if(SessionState.GetBool("Capstone.GirlAim.CameraOnly",false)){await CameraChecks(input,arena,check,gap);return;}
            bool after=SessionState.GetBool("Capstone.GirlAim.After",false);
            string prefix=after?"after":"before";
            var movement=input.GetComponent<BasicPlayerMovement>();
            var switcher=input.GetComponent<PlayerVisualSwitcher>();
            switcher.SwitchTo(0,true);
            var animator=switcher.ActiveVisualAnimator;
            var camera=Camera.main;
            var follow=camera.GetComponent<BasicCameraFollow>();
            var brain=camera.GetComponent<CinemachineBrain>();
            bool followEnabled=follow.enabled,brainEnabled=brain!=null&&brain.enabled;
            float fov=camera.fieldOfView;
            var text=new StringBuilder();
            text.AppendLine("visual="+switcher.ActiveVisualName+"; animator="+animator.name+"; root="+switcher.ActiveVisualRoot.name);
            text.AppendLine("quality="+QualitySettings.names[QualitySettings.GetQualityLevel()]+" skinWeights="+QualitySettings.skinWeights);
            foreach(var skin in switcher.ActiveVisualRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
                text.AppendLine("skin="+skin.name+" quality="+skin.quality+" bones="+skin.bones.Length);
            try
            {
                follow.enabled=false;if(brain!=null)brain.enabled=false;
                camera.fieldOfView=45;
                camera.transform.position=input.transform.position+new Vector3(.15f,1.05f,2.6f);
                camera.transform.LookAt(input.transform.position+Vector3.up*.85f);
                foreach(var phase in new[]{0f,.025f,.065f,.13f})
                {
                    animator.enabled=true;animator.Play("IdleNeutral",0,.5f);animator.Update(0);
                    animator.Play("Throw",0,phase);animator.Update(0);animator.enabled=false;
                    text.AppendLine("source phase="+phase.ToString(CultureInfo.InvariantCulture));
                    foreach(var name in new[]{"Hips","Spine","Spine1","Spine2","LeftShoulder","RightShoulder"})
                    {
                        var bone=animator.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==name);
                        if(bone!=null)text.AppendLine(name+" pos="+bone.localPosition+" rot="+bone.localRotation);
                    }
                    await gap(60);if(phase==0)Shot(prefix+"-source-0");await gap(60);
                }
                animator.enabled=true;animator.Play("IdleNeutral",0,.5f);animator.Update(0);
                movement.enabled=true;movement.SetPlayerAnimator(animator,true);await gap(180);
                check(movement.TryPlayScriptedThrow(),"girl live throw starts from neutral");
                animator.Update(0);
                foreach(var phase in new[]{0f,.025f,.065f,.13f})
                {
                    float end=Time.realtimeSinceStartup+4;
                    while(movement.IsScriptedActionPlaying&&(!PlayerThrowPlayback.TryProgress(movement,switcher,out float progress)||progress<phase)&&Time.realtimeSinceStartup<end)await gap(8);
                    animator.enabled=false;movement.enabled=false;
                    text.AppendLine("live desired="+phase+" current="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime+" transition="+animator.IsInTransition(0));
                    await gap(40);if(phase==0)Shot(prefix+"-live-0");await gap(60);
                    animator.enabled=true;movement.enabled=true;
                }
                Write(prefix+"-pose-audit.txt",text.ToString());
                float until=Time.realtimeSinceStartup+4;while(movement.IsScriptedActionPlaying&&Time.realtimeSinceStartup<until)await gap(30);
                check(!movement.IsScriptedActionPlaying,"girl live throw finishes");
                var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Bats/Wild/Wild_Bat01.prefab"),input.transform.position+Vector3.forward*5,Quaternion.identity);
                SceneManager.MoveGameObjectToScene(actor,arena);
                var actorBrain=actor.GetComponent<BasicCreatureEnemy>();if(actorBrain!=null)actorBrain.enabled=false;
                await gap(700);
                var presentation=input.gameObject.AddComponent<CapturePresentation>();
                var target=actor.GetComponent<WildPetCaptureTarget>();
                text.AppendLine("before capture state="+animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                var baseline=animator.GetComponentsInChildren<Transform>().ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
                presentation.Play(new CapturePresentationResult(new CaptureRoll(false,false,1),target),input.transform,null);
                float began=Time.time;int frame=0;int photo=0;
                while(presentation.IsPlaying&&Time.time-began<2)
                {
                    text.AppendLine("capture frame="+(frame++)+" elapsed="+(Time.time-began).ToString("F4",CultureInfo.InvariantCulture)+" phase="+presentation.Phase+" current="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime+" next="+animator.GetNextAnimatorStateInfo(0).normalizedTime+" blend="+animator.IsInTransition(0));
                    foreach(var pair in baseline)
                    {
                        var t=pair.Key;var b=pair.Value;
                        if(t==animator.transform||t.name=="Hips"||Vector3.Distance(b.Item1,t.localPosition)>.0001f||Vector3.Distance(b.Item3,t.localScale)>.0001f)
                            text.AppendLine(t.name+" p="+t.localPosition.ToString("F6")+" s="+t.localScale.ToString("F6")+" q="+t.localRotation.ToString("F6"));
                    }
                    if(photo<4&&Time.time-began>=new[]{.08f,.18f,.3f,.5f}[photo]){Shot(prefix+"-capture-"+(photo++));}
                    await gap(8);
                }
                presentation.Cancel();Object.Destroy(presentation);Object.Destroy(actor);
                Write(prefix+"-pose-audit.txt",text.ToString());
            }
            finally
            {
                animator.enabled=true;movement.enabled=true;
                camera.fieldOfView=fov;if(brain!=null)brain.enabled=brainEnabled;follow.enabled=followEnabled;
            }
            Write(prefix+"-status.txt","COMPLETE early poses captured");
            if(after)await CameraChecks(input,arena,check,gap);
        }

        static async Task CameraChecks(PetCommandInput input,Scene arena,Action<bool,string> check,Func<int,Task> gap)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var camera=Camera.main;var follow=camera.GetComponent<BasicCameraFollow>();
            var brain=camera.GetComponent<CinemachineBrain>();follow.enabled=false;if(brain!=null)brain.enabled=false;
            camera.transform.position=input.transform.position+new Vector3(0,2,-5);
            camera.transform.LookAt(input.transform.position+new Vector3(0,.8f,5));
            DummyEnemy Spawn(Vector3 offset)
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Bats/Wild/Wild_Bat01.prefab"),input.transform.position+offset,Quaternion.identity);
                SceneManager.MoveGameObjectToScene(go,arena);var ai=go.GetComponent<BasicCreatureEnemy>();if(ai!=null)ai.enabled=false;
                return go.GetComponent<DummyEnemy>();
            }
            var visible=Spawn(Vector3.forward*6);var behind=Spawn(Vector3.back*7);var offscreen=Spawn(Vector3.right*25);
            await gap(120);Physics.SyncTransforms();
            check(CameraEnemyVisibility.CanSee(camera,input.transform,visible),"camera sees front enemy");
            check(!CameraEnemyVisibility.CanSee(camera,input.transform,behind)&&!CameraEnemyVisibility.CanSee(camera,input.transform,offscreen),"camera rejects behind and offscreen enemies despite proximity/minimap");
            var find=typeof(BasicCameraFollow).GetMethod("FindPreferredLockCandidate",flags);
            check((DummyEnemy)find.Invoke(follow,null)==visible,"actual FreeLook candidate picks visible enemy");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="QA line-of-sight wall";SceneManager.MoveGameObjectToScene(wall,arena);
            wall.transform.position=input.transform.position+new Vector3(0,1,3);wall.transform.localScale=new Vector3(5,4,.5f);Physics.SyncTransforms();
            check(!CameraEnemyVisibility.CanSee(camera,input.transform,visible)&&(DummyEnemy)find.Invoke(follow,null)==null,"opaque wall rejects hidden enemy in actual lock selector");
            Object.Destroy(wall);await gap(100);Physics.SyncTransforms();
            typeof(BasicCameraFollow).GetField("lockedEnemy",flags).SetValue(follow,visible);
            typeof(BasicCameraFollow).GetField("enemyLockActive",flags).SetValue(follow,true);
            typeof(PetCommandInput).GetMethod("SelectPinnedEnemy",flags).Invoke(input,new object[]{visible});
            check(input.SelectedEnemy==visible&&follow.TryGetLockedEnemy(out var locked)&&locked==visible,"pin keeps exact selection without issuing attack");
            visible.TakeDamage(100000);await gap(50);
            check(visible.IsDowned,"wild target enters real downed state");
            typeof(BasicCameraFollow).GetMethod("UpdateLockedEnemyTracking",flags).Invoke(follow,new object[]{input.transform.position,.016f});
            check(!follow.HasLockedEnemy&&input.SelectedEnemy==visible,"downed clears aim and retains individual for capture");
            check(WildCaptureTargeting.Find(input,follow)==visible.GetComponent<WildPetCaptureTarget>(),"G resolves same downed individual after unlock");
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(wall,arena);wall.transform.position=input.transform.position+new Vector3(0,1,3);wall.transform.localScale=new Vector3(5,4,.5f);Physics.SyncTransforms();
            check(WildCaptureTargeting.Find(input,follow)==null,"temporarily hidden selection cannot silently fall back to a different creature");
            Object.Destroy(wall);Object.Destroy(behind.gameObject);Object.Destroy(offscreen.gameObject);Object.Destroy(visible.gameObject);await gap(120);
            var target=Spawn(Vector3.forward*6);
            target.InitializeCaptureHealth(10000,10000);
            var owned=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon01.prefab"),input.transform.position+Vector3.right,Quaternion.identity);
            SceneManager.MoveGameObjectToScene(owned,arena);
            var pet=owned.GetComponent<PetController>();pet.roamAroundOwner=false;pet.AssignOwner(input.transform);
            pet.GetComponent<PetHudRuntimeStats>().SetStatus(10000,10000,0,0);
            check(pet.TrySummonAt(input.transform.position+Vector3.right,Vector3.forward),"native click fixture summons real owned pet");
            float end=Time.realtimeSinceStartup+10;while(!pet.CanReceiveCommands&&Time.realtimeSinceStartup<end)await gap(30);
            check(pet.CanReceiveCommands,"native click fixture pet ready");input.petSlots[0]=pet;input.SetActivePet(pet);
            check(input.CanUsePetSkills,"native click fixture enables actual command component");
            input.cameraLock=follow;typeof(BasicCameraFollow).GetMethod("ClearLockedEnemy",flags).Invoke(follow,null);
            var movement=input.GetComponent<BasicPlayerMovement>();movement.enabled=true;
            typeof(BasicCameraFollow).GetField("yaw",flags).SetValue(follow,0f);
            typeof(BasicCameraFollow).GetField("pitch",flags).SetValue(follow,15f);
            typeof(BasicCameraFollow).GetField("focusInitialized",flags).SetValue(follow,false);
            follow.enabled=true;if(brain!=null)brain.enabled=true;
            await gap(400);
            pet.ReturnToOwner();
            check(!pet.HasCombatTarget,"native fixture starts without an attack command");
            Write("native-m3-status.txt","READY: middle click Game view, then left click empty ground; bounded 90 seconds");
            end=Time.realtimeSinceStartup+90;while(!follow.HasLockedEnemy&&Time.realtimeSinceStartup<end)await gap(8);
            check(follow.TryGetLockedEnemy(out locked)&&locked==target&&input.SelectedEnemy==target,"native M3 pins visible target and selection event");
            check(!pet.HasCombatTarget,"native M3 alone does not start pet attack");
            end=Time.realtimeSinceStartup+45;
            while(pet.CombatTarget!=target&&Time.realtimeSinceStartup<end)await gap(8);
            check(pet.CombatTarget==target,"native LMB away from enemy commands locked enemy");
            float health=target.CurrentHealth;end=Time.realtimeSinceStartup+8;
            while(target.CurrentHealth>=health&&Time.realtimeSinceStartup<end)await gap(30);
            check(target.CurrentHealth<health,"locked click delivers actual HP damage");
            target.TakeDamage(100000);await gap(80);
            check(!follow.HasLockedEnemy&&input.SelectedEnemy==target,"native attacked target downed unlocks without replacement");
            var alternative=Spawn(Vector3.forward*4+Vector3.right*2);await gap(100);
            check(!follow.HasLockedEnemy&&WildCaptureTargeting.Find(input,follow)==target.GetComponent<WildPetCaptureTarget>(),"new closer enemy does not replace downed capture selection");
            var inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();var ball=CaptureAssets.LoadBall();inventory.TryAddItemAtomic(ball,5);
            var roster=Object.FindFirstObjectByType<PetBoxRuntimeProvider>();roster.Bind(input);
            var coordinator=Object.FindFirstObjectByType<PetCaptureCoordinator>();coordinator.RandomSource=new CaptureSuccessRandom();
            var hud=Object.FindFirstObjectByType<HubCaptureHud>();int balls=inventory.GetQuantity(ball),intents=hud.ThrowIntents;
            check(coordinator.CanCaptureFromBag(ball,inventory,out string reason),"downed capture public preflight: "+reason);
            bool threw=hud.TryThrow();
            var feedback=(UnityEngine.UI.Text)typeof(HubCaptureHud).GetField("notice",flags).GetValue(hud);
            Write("g-callback-observation.txt","callback="+threw+"; intents="+intents+" -> "+hud.ThrowIntents+"; balls="+balls+" -> "+inventory.GetQuantity(ball)+"; remainingDowned="+target.DownedSecondsRemaining+"; savedIndividual="+roster.Contains(target.GetComponent<PetController>())+"; feedback="+feedback?.text);
            check(threw&&hud.ThrowIntents==intents+1&&inventory.GetQuantity(ball)==balls-1&&roster.Contains(target.GetComponent<PetController>()),"actual G HUD callback captures exact old target and spends one ball");
            check(alternative.GetComponent<WildPetCaptureTarget>().IsWild,"closer alternative remains wild");
            end=Time.realtimeSinceStartup+13;while(coordinator.IsCapturing&&Time.realtimeSinceStartup<end)await gap(30);
            check(!coordinator.IsCapturing&&!InventoryInputController.GameplayInputBlocked,"capture completes and restores gameplay input");
            var saved=Object.FindFirstObjectByType<PlayerSaveController>();check(saved.SavePath.StartsWith(Root,StringComparison.OrdinalIgnoreCase),"native G only saves isolated E profile");
            Write("native-m3-status.txt","COMPLETE native middle click, LMB, actual damage, downed unlock and actual G HUD callback capture");
        }
        sealed class CaptureSuccessRandom:ICaptureRandom { public double NextFloat01()=>0; }
    }
}
#endif

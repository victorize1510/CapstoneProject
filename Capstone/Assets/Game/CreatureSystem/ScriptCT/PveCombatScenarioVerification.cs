#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.PetSummon;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using Capstone.Game.Inventory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Capstone.Game.CreatureSystem {
    internal static class PveCombatScenarioVerification {
        static string Qa => QaArtifactPaths.Root+"pve-combat-hub-2026-11-11/";
        const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        static async Task Gap(int ms=100){await Task.Delay(ms);if(!EditorApplication.isPlaying)throw new OperationCanceledException("Play ended");}
        internal static async Task Run(PetCommandInput input,Action<bool,string> check,bool capture=true,string output=null){
            var originalSlots=input.petSlots;var fixtures=new List<GameObject>();var samples=new List<string>{"form,count,hits,rawBudget,rawHits,hpLost,crit,releaseNormalized,observedFrameMs"};
            var director=input.GetComponent<PetSummonDirector>();PetSkillCombatExecutor executor=null;
            input.petSlots=new PetController[6];
            try{
                for(int form=1;form<=3;form++){
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon"+form.ToString("00")+".prefab");
                    var actor=Object.Instantiate(prefab);fixtures.Add(actor);var pet=actor.GetComponent<PetController>();pet.AssignOwner(input.transform);pet.roamAroundOwner=false;
                    var stats=actor.GetComponent<PetHudRuntimeStats>();stats.SetIdentity(stats.DisplayName,20);input.petSlots[form-1]=pet;
                    check(director.RequestSummonSlot(form-1),"form "+form+" real summon request");for(int i=0;i<180&&!pet.CanReceiveCommands;i++)await Gap();
                    check(pet.CanReceiveCommands,"form "+form+" actual runtime animator ready: "+director.LastFailureReason);
                    var skills=new List<SkillHudData>();foreach(string name in new[]{"bite","blast","projectile","wing_strike"})skills.Add(AssetDatabase.LoadAssetAtPath<PetSkillDefinition>("Assets/Game/Skills/Data/Definitions/Skill_"+name+".asset").CreateHudData(1,true));stats.SetSkills(skills);
                    var targetPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab");
                    var target=Object.Instantiate(targetPrefab,pet.transform.position+pet.transform.forward*5,Quaternion.identity);fixtures.Add(target);
                    var brain=target.GetComponent<BasicCreatureEnemy>();brain.enabled=false;var agent=target.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent!=null)agent.enabled=false;
                    var dummy=target.GetComponent<DummyEnemy>();dummy.trainingDummy=true;dummy.autoRevive=false;dummy.InitializeCaptureHealth(50000,50000);target.GetComponent<CreatureCombatant>().SyncEnemyHealth(50000,50000);
                    var capsule=target.GetComponent<CapsuleCollider>();capsule.radius=1.2f;capsule.height=7;capsule.center=new Vector3(0,2,0);
                    await Gap();var metadata=actor.GetComponent<PetCollectionMetadata>();var action=actor.GetComponent<PetCombatActionDriver>();if(action==null)action=actor.AddComponent<PetCombatActionDriver>();
                    for(int count=2;count<=5;count++){
                        for(int focus=0;!Application.isFocused&&focus<150;focus++)await Gap();
                        check(Application.isFocused,"native Game window focused before projectile fixture");
                        PlaceTargetInClearLane(pet,target);
                        pet.ReturnToOwner();input.TryCommandAttack(dummy);pet.ReturnToOwner();
                        PetSkillCooldowns.Set(metadata.PetId,skills[3].skillId,0);stats.SetStatus(stats.MaxHealth,stats.MaxHealth,100,100);await Gap();
                        executor=actor.GetComponent<PetSkillCombatExecutor>();if(executor==null)executor=actor.AddComponent<PetSkillCombatExecutor>();
                        float draw=count==2?.05f:count==3?.3f:count==4?.7f:.95f;executor.VolleyRandomSource=()=>draw;
                        var results=new List<CombatDamageResult>();Action<CombatDamageResult> listener=r=>{if(r.Hit.Source==actor&&r.Hit.SkillId==skills[3].skillId)results.Add(r);};CombatDamageEvents.Applied+=listener;
                        int started=action.StartedCount;stats.RequestSkill(3);await Gap();int token=action.CastToken;
                        for(int i=0;i<60&&(action.IsSkillBusy||executor.ActiveProjectileCount>0);i++)await Gap();CombatDamageEvents.Applied-=listener;
                        check(action.StartedCount==started+1&&executor.LastVolleyCount==count,"form "+form+" starts one "+count+"-shot cast");
                        float raw=results.Sum(r=>r.Hit.RawDamage),lost=results.Sum(r=>r.HpLost);
                        samples.Add(FormattableString.Invariant($"{form},{count},{results.Count},{executor.LastCastRawBudget},{raw},{lost},{(results.Count>0?results[0].Hit.Critical:false)},{action.LastReleaseNormalized},{Time.unscaledDeltaTime*1000}"));File.WriteAllText((output??Qa)+"projectile-budget.csv",string.Join("\n",samples));
                        check(results.Count==count&&results.Select(r=>r.Hit.EmissionIndex).Distinct().Count()==count&&results.All(r=>r.Hit.CastId==token),"form "+form+" all "+count+" real projectile collisions apply once; hits="+results.Count+" focus="+Application.isFocused+" released="+action.LastReleaseNormalized+" cancel="+action.CancelledCount+" driver="+action.LastRejection+" execution="+executor.LastRejection);
                        check(Mathf.Abs(raw-executor.LastCastRawBudget)<.01f&&results.All(r=>r.Hit.Critical==results[0].Hit.Critical)&&Mathf.Abs(stats.Energy-82)<.01f,"form "+form+" total budget one crit snapshot and one MP charge n="+count);
                    }
                    executor.VolleyRandomSource=null;
                    PlaceTargetInClearLane(pet,target);
                    PetSkillCooldowns.Set(metadata.PetId,skills[2].skillId,0);stats.SetStatus(stats.MaxHealth,stats.MaxHealth,100,100);await Gap();int before=executor.HitCount;
                    stats.RequestSkill(2);for(int i=0;i<60&&(i==0||action.IsSkillBusy||executor.ActiveProjectileCount>0);i++)await Gap();
                    check(executor.LastVolleyCount==1&&executor.HitCount==before+1&&Math.Abs(stats.Energy-88)<.01f,"form "+form+" energy skill remains one real hit and costs 12 MP");
                    // Bite and roar use the same committed animation driver as projectiles.
                    for(int slot=0;slot<2;slot++){
                        pet.ReturnToOwner();PetSkillCooldowns.Set(metadata.PetId,skills[slot].skillId,0);stats.SetStatus(stats.MaxHealth,stats.MaxHealth,100,100);await Gap();
                        var presentation=actor.GetComponent<PetSkillPresentationPlayer>();int released=presentation.ReleasedCount;float releaseTime=-1;
                        Action<int,string> observed=(_,id)=>{if(id==skills[slot].skillId)releaseTime=stats.VisualAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;};
                        presentation.SkillReleased+=observed;stats.RequestSkill(slot);
                        for(int wait=0;wait<70&&(wait==0||action.IsSkillBusy);wait++)await Gap();
                        presentation.SkillReleased-=observed;
                        check(presentation.ReleasedCount==released+1&&releaseTime>=.2f,"form "+form+" "+skills[slot].skillId+" releases once from actual animation normalized="+releaseTime);
                        check(Mathf.Abs(stats.Energy-(slot==0?92:85))<.01f,"form "+form+" bite/roar exact MP charge slot="+slot);
                    }
                    if(form==3&&capture)await Capture(input,pet,dummy,check,output);
                    target.SetActive(false);
                }
                director.RequestRecall();for(int i=0;i<120&&director.IsSequenceRunning;i++)await Gap();
            }finally{if(executor!=null)executor.VolleyRandomSource=null;foreach(var go in fixtures)if(go!=null)Object.Destroy(go);input.petSlots=originalSlots;input.activePet=originalSlots.FirstOrDefault(p=>p!=null);}
        }
        static void PlaceTargetInClearLane(PetController pet,GameObject target){
            var origin=pet.GetComponent<PetSkillPresentationPlayer>().SocketPosition("Mouth");
            for(int angle=0;angle<360;angle+=45){
                var direction=Quaternion.Euler(0,angle,0)*Vector3.forward;bool clear=true;
                foreach(var hit in Physics.SphereCastAll(origin,.3f,direction,5,~0,QueryTriggerInteraction.Ignore)){
                    var c=hit.collider;if(c==null||c.transform.IsChildOf(pet.transform)||pet.owner!=null&&c.transform.IsChildOf(pet.owner)||c.GetComponentInParent<DummyEnemy>()!=null)continue;
                    clear=false;break;
                }
                if(!clear)continue;
                target.transform.position=pet.transform.position+direction*5;
                pet.transform.rotation=Quaternion.LookRotation(direction);Physics.SyncTransforms();return;
            }
            throw new InvalidOperationException("QA fixture has no unobstructed lane; do not count misses as damage bugs.");
        }
        static async Task Capture(PetCommandInput input,PetController pet,DummyEnemy enemy,Action<bool,string> check,string output=null){
            string destination=output??Qa;
            var rig=input.cameraLock;var currentZoom=typeof(BasicCameraFollow).GetField("currentZoomDistance",Any);var targetZoom=typeof(BasicCameraFollow).GetField("targetZoomDistance",Any);
            float oldCurrent=(float)currentZoom.GetValue(rig),oldTarget=(float)targetZoom.GetValue(rig);
            currentZoom.SetValue(rig,rig.maxZoomDistance);targetZoom.SetValue(rig,rig.maxZoomDistance);
            // A damage-label screenshot must frame both real recipients, not put a large pet's head above the viewport.
            var camera=input.commandCamera;Vector3 forward=camera.transform.forward;forward.y=0;forward.Normalize();
            Vector3 right=Vector3.Cross(Vector3.up,forward);Vector3 pose=input.transform.position+right*1.4f;
            pet.ReturnToOwner();
            if(pet.agent!=null&&pet.agent.enabled&&pet.agent.isOnNavMesh)pet.agent.Warp(pose);else pet.transform.position=pose;
            pet.HoldCoordinatedSkill();
            typeof(PetController).GetField("skillAnimationUntil",Any).SetValue(pet,Time.time+5);
            enemy.transform.position=pose+forward*1.2f;Physics.SyncTransforms();
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);var size=type.GetProperty("selectedSizeIndex",Any);int original=(int)size.GetValue(view);
            var helper=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture")).First(t=>t!=null).GetMethod("SelectSize",Any);
            try{foreach(var resolution in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                helper.Invoke(null,new object[]{view,size,resolution});view.Focus();await Gap(400);
                check(Screen.width==resolution.x&&Screen.height==resolution.y,"actual Game view "+resolution);
                var picker=Object.FindFirstObjectByType<HubCaptureHud>();picker.OpenPicker();await Gap(200);
                string file=destination+"hud-ball-picker-"+resolution.x+".png";File.AppendAllText((output??Qa)+"created-paths.txt",file+"\n");ScreenCapture.CaptureScreenshot(file);await Gap(250);picker.ClosePicker();
                // Picker deliberately retains its gameplay gate for one release frame. Do not inject hits into that gate.
                for(int wait=0;wait<20&&InventoryInputController.GameplayInputBlocked;wait++)await Gap(25);
                check(!InventoryInputController.GameplayInputBlocked,"picker release gate cleared before damage fixture "+resolution);
                enemy.ApplyCombatHit(new CombatHit(pet.gameObject,"qa-feedback",991,12,CreatureDamageKind.Physical,true,false,enemy.TargetPosition));
                pet.GetComponent<CreatureCombatant>().ReceiveDamage(8,CreatureDamageKind.Physical,enemy.gameObject);await Gap(120);
                var numbers=Object.FindFirstObjectByType<FloatingCombatText>();
                check(numbers!=null&&numbers.ActiveCount>=2,"both actual HP-loss labels on screen "+resolution);
                file=destination+"hud-two-factions-"+resolution.x+".png";File.AppendAllText((output??Qa)+"created-paths.txt",file+"\n");ScreenCapture.CaptureScreenshot(file);await Gap(250);
            }}finally{pet.ReleaseCoordinatedSkill();currentZoom.SetValue(rig,oldCurrent);targetZoom.SetValue(rig,oldTarget);size.SetValue(view,original);}
        }
    }
}
#endif

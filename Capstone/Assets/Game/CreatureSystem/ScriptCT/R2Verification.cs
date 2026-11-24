#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using Capstone.Game.WorldFeedback;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace Capstone.Game.CreatureSystem {
    public static class R2Verification {
        public const string Root=QaArtifactPaths.Root+"pve-hud-r2-2026-11-17/";
        [MenuItem("Tools/Capstone/PvE/12. Verify R2 isolated")]
        public static void Begin(){SessionState.SetBool("Capstone.R2",true);EnemyAiVerification.BeginAtRoot(Root,capture:false);}
        public static async Task Run(PetCommandInput input,Scene arena,Vector3 origin,Action<bool,string> check,Func<int,Task> gap){
            GameObject Spawn(string path,Vector3 offset){var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),origin+offset,Quaternion.identity);SceneManager.MoveGameObjectToScene(go,arena);return go;}
            async Task<bool> Attack(DummyEnemy enemy){float until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until){if(input.TryCommandAttack(enemy))return true;await gap(100);}return false;}
            var go=Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon01.prefab",Vector3.zero);
            var pet=go.GetComponent<PetController>();pet.AssignOwner(input.transform);pet.roamAroundOwner=false;
            var stats=go.GetComponent<PetHudRuntimeStats>();stats.SetStatus(10000,10000,0,0);input.petSlots[0]=pet;input.SetActivePet(pet);
            check(pet.TrySummonAt(origin,Vector3.forward),"owned summon begins");
            float end=Time.time+6;while(!pet.CanReceiveCommands&&Time.time<end)await gap(30);
            check(pet.CanReceiveCommands,"owned pet ready");
            DummyEnemy Wild(Vector3 offset){var actor=Spawn("Assets/Game/CreatureSystem/Runtime/Bats/Wild/Wild_Bat01.prefab",offset);var ai=actor.GetComponent<BasicCreatureEnemy>()??actor.AddComponent<BasicCreatureEnemy>();ai.patrolRadius=0;ai.useSkills=false;ai.attackInterval=100;var hp=actor.GetComponent<DummyEnemy>();hp.InitializeCaptureHealth(10000,10000);return hp;}
            var a=Wild(Vector3.forward*2);var b=Wild(Vector3.forward*5+Vector3.right*2);await gap(150);
            check(await Attack(a),"attack intent A accepted");
            check(input.GetComponent<CombatEngagement>().HudTarget==a,"combat HUD follows accepted attack");
            a.ApplyCombatHit(new CombatHit(go,"r2",1,10,CreatureDamageKind.Physical,false,false,a.TargetPosition));await gap(40);
            check(a.GetComponent<BasicCreatureEnemy>().ThreatOwner==input.transform,"A retaliates after actual HP damage");
            await gap(300);check(await Attack(b),"attack intent B accepted");
            check(a.GetComponent<BasicCreatureEnemy>().State=="Return","A returns when attack changes to B");
            a.ApplyCombatHit(new CombatHit(go,"old-bleed",2,10,CreatureDamageKind.Physical,false,true,a.TargetPosition));
            check(a.GetComponent<BasicCreatureEnemy>().Target==null,"old damage does not reacquire disengaged A");
            var boss=Wild(Vector3.left*3);boss.isBoss=true;
            var initialized=typeof(BasicCreatureEnemy).GetField("initialized",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            end=Time.realtimeSinceStartup+4;while(!(bool)initialized.GetValue(boss.GetComponent<BasicCreatureEnemy>())&&Time.realtimeSinceStartup<end)await gap(100);
            check((bool)initialized.GetValue(boss.GetComponent<BasicCreatureEnemy>()),"boss fixture Start completed");boss.InitializeCaptureHealth(10000,10000);CombatEngagement.Select(input,boss);
            var bossHit=boss.ApplyCombatHit(new CombatHit(go,"boss",3,500,CreatureDamageKind.Physical,false,false,boss.TargetPosition));
            check(bossHit.HpLost>0,"boss fixture receives real damage");await gap(80);CombatEngagement.Select(input,b);
            check(boss.GetComponent<BasicCreatureEnemy>().Target!=null,"boss stays engaged when player attacks B: "+boss.GetComponent<BasicCreatureEnemy>().LastReason);
            check(CreatureCombatContext.Get(boss.gameObject).CanAct,"configured boss may use shared skills");
            check(a.GetComponent<CameraActorOccluder>()!=null,"dynamic enemy registers camera fade");
            a.GetComponent<BasicCreatureEnemy>().enabled=false;b.GetComponent<BasicCreatureEnemy>().enabled=false;pet.ReturnToOwner();
            var rewards=Object.FindFirstObjectByType<PveRewardService>();var wallet=Object.FindFirstObjectByType<PlayerCurrencyWallet>();
            int goldBefore=wallet.Gold,completedBefore=rewards.CompletedCount;
            boss.ApplyCombatHit(new CombatHit(go,"finish",30,100000,CreatureDamageKind.Physical,false,false,boss.TargetPosition));
            end=Time.time+7;while(rewards.CompletedCount==completedBefore&&Time.time<end)await gap(60);
            check(boss.DeathVisualComplete&&!boss.gameObject.activeSelf,"boss model hidden before automatic loot commit");
            check(boss.GetComponent<EnemyDeathVisual>().CompletionReason=="Animation","configured boss completes actual Dead animation rather than watchdog fallback: "+boss.GetComponent<EnemyDeathVisual>().CompletionReason);
            check(rewards.CompletedCount==completedBefore+1&&wallet.Gold>goldBefore,"actual reward journal automatically credits wallet without proximity pickup");
            PveRewardService.HandleDefeat(boss,go);await gap(200);
            check(rewards.CompletedCount==completedBefore+1,"duplicate terminal reward does not pay twice");
            // Tien vao tam truoc khi mat cooldown/cast; khong gia lap projectile.
            var agent=pet.GetComponent<UnityEngine.AI.NavMeshAgent>();
            void PlacePet(){if(agent!=null&&agent.enabled&&agent.isOnNavMesh)agent.Warp(origin);else pet.transform.position=origin;Physics.SyncTransforms();}
            PlacePet();
            a.transform.position=origin+Vector3.left*12;
            b.transform.position=origin+Vector3.forward*7;Physics.SyncTransforms();
            await gap(300);await Attack(b);pet.ReturnToOwner();
            var actions=pet.GetComponent<PetCombatActionDriver>();int starts=actions!=null?actions.StartedCount:0;
            stats.RequestSkill(0);actions=pet.GetComponent<PetCombatActionDriver>();
            check(actions!=null&&actions.Phase==PetCombatPhase.Positioning&&actions.StartedCount==starts,"melee request stages movement before cast/cooldown");
            end=Time.time+4;while(actions.StartedCount==starts&&actions.IsSkillBusy&&Time.time<end)await gap(30);
            check(actions.StartedCount==starts+1,"pet approaches into melee range then starts one actual cast: "+actions.LastRejection+" phase="+actions.Phase+" pet="+pet.transform.position+" target="+b.transform.position);
            end=Time.time+5;while(actions.IsSkillBusy&&Time.time<end)await gap(30);
            check(!actions.IsSkillBusy,"positioned skill returns to Ready");
            // Lenh C giu dich da nhan du lua chon M3 thay doi trong luc lui.
            pet.ReturnToOwner();PlacePet();
            b.transform.position=origin+Vector3.forward*1.5f;Physics.SyncTransforms();await gap(150);
            starts=actions.StartedCount;Vector3 retreatFrom=pet.transform.position;stats.RequestSkill(2);
            check(actions.Phase==PetCombatPhase.Positioning&&actions.StartedCount==starts,"ranged request stages retreat without spending cast early");
            typeof(PetCommandInput).GetMethod("SelectPinnedEnemy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(input,new object[]{a});
            end=Time.time+4;while(actions.StartedCount==starts&&actions.IsSkillBusy&&Time.time<end)await gap(30);
            float retreat=Vector3.Distance(retreatFrom,pet.transform.position);
            check(actions.StartedCount==starts+1&&retreat>1&&retreat<2.9f&&pet.GetComponent<PetSkillCombatExecutor>().LastCastTarget==b,"ranged cast retreats within budget and retains accepted target after pin callback: "+retreat);
            end=Time.time+5;while(actions.IsSkillBusy&&Time.time<end)await gap(30);
            // Vat can that: watchdog huy y dinh, cooldown van nguyen.
            pet.ReturnToOwner();PlacePet();
            b.transform.position=origin+Vector3.forward*7;await Attack(b);pet.ReturnToOwner();
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(wall,arena);
            wall.transform.position=origin+Vector3.forward*3+Vector3.up;wall.transform.localScale=new Vector3(8,2,.25f);Physics.SyncTransforms();
            end=Time.time+3;while(stats.GetSkills()[0].IsCoolingDown&&Time.time<end)await gap(50);
            starts=actions.StartedCount;stats.RequestSkill(0);end=Time.time+4;
            check(actions.Phase==PetCombatPhase.Positioning&&!stats.GetSkills()[0].IsCoolingDown,"blocked-path trial starts from a ready skill with zero cooldown");
            while(actions.IsSkillBusy&&Time.time<end)await gap(30);
            check(actions.StartedCount==starts&&!actions.IsSkillBusy&&!stats.GetSkills()[0].IsCoolingDown,"blocked approach cancels without casting or consuming cooldown: starts="+actions.StartedCount+" expected="+starts+" phase="+actions.Phase+" cooldown="+stats.GetSkills()[0].cooldownRemainingSeconds+" at="+pet.transform.position+" reason="+actions.LastRejection);Object.Destroy(wall);await gap(50);
            // Boss co mapping that dung chung pha lui; khong gia lap release/damage.
            PlacePet();pet.ReturnToOwner();stats.SetStatus(10000,10000,0,0);
            a.isBoss=true;a.InitializeCaptureHealth(10000,10000);a.transform.position=origin+Vector3.forward*1.5f;
            var hostileNav=a.GetComponent<UnityEngine.AI.NavMeshAgent>();if(hostileNav!=null&&hostileNav.enabled&&hostileNav.isOnNavMesh)hostileNav.Warp(a.transform.position);
            var hostile=a.GetComponent<BasicCreatureEnemy>();hostile.attackInterval=100;hostile.useSkills=false;hostile.enabled=true;Physics.SyncTransforms();
            CombatEngagement.Select(input,a);
            a.ApplyCombatHit(new CombatHit(go,"r2-enemy-skill",35,50,CreatureDamageKind.Physical,false,false,a.TargetPosition));
            var hostileStats=a.GetComponent<PetHudRuntimeStats>();var hostileActions=a.GetComponent<PetCombatActionDriver>();
            int hostileStarts=hostileActions.StartedCount;Vector3 hostileFrom=a.transform.position;
            bool hostileAccepted=hostileActions.Submit(1);
            check(hostileAccepted&&hostileActions.Phase==PetCombatPhase.Positioning,"configured boss ranged request uses shared positioning phase: "+hostileActions.LastRejection+" "+hostileStats.LastSkillRejection+" target="+hostile.Target);
            end=Time.time+4;while(hostileActions.StartedCount==hostileStarts&&hostileActions.IsSkillBusy&&Time.time<end)await gap(30);
            check(hostileActions.StartedCount==hostileStarts+1&&Vector3.Distance(hostileFrom,a.transform.position)>1,"actual boss retreats then starts one configured ranged cast: "+hostileActions.LastRejection);
            end=Time.time+5;while(hostileActions.IsSkillBusy&&Time.time<end)await gap(30);
            hostile.enabled=false;a.isBoss=false;a.transform.position=origin+Vector3.left*12;Physics.SyncTransforms();
            await RewardFailures(go,Wild(Vector3.right*12),check,gap);
            // Camera helper su dung renderer va material that, khoi phuc reference goc.
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(blocker,arena);
            var camera=Camera.main;blocker.transform.position=camera.transform.position+camera.transform.forward;
            var original=blocker.GetComponent<Renderer>().sharedMaterial;
            var fade=new CameraNearPlayerVisibility(Resources.Load<Shader>("WorldFeedback/CameraDitherLit"),Resources.Load<Shader>("WorldFeedback/CameraDitherSimpleLit"),Resources.Load<Shader>("WorldFeedback/CameraDitherUnlit"),true);
            fade.Update(blocker.transform,camera,.2f,camera.transform.position+camera.transform.forward*3);
            check(fade.Fade>=.69f,"actor between lens and player fades toward thirty percent visibility");
            fade.Apply();fade.Restore();
            check(blocker.GetComponent<Renderer>().sharedMaterial==original,"camera fade restores authored material reference");
            fade.Dispose();Object.Destroy(blocker);
            var ringGo=new GameObject("QA countdown",typeof(RectTransform),typeof(DownedRing));SceneManager.MoveGameObjectToScene(ringGo,arena);
            ((RectTransform)ringGo.transform).sizeDelta=new Vector2(40,40);var ring=ringGo.GetComponent<DownedRing>();ring.SetRemaining(.75f);
            using(var mesh=new UnityEngine.UI.VertexHelper()){
                typeof(DownedRing).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(UnityEngine.UI.VertexHelper)},null).Invoke(ring,new object[]{mesh});
                var vertex=new UIVertex();mesh.PopulateUIVertex(ref vertex,0);
                check(vertex.position.x>19&&Mathf.Abs(vertex.position.y)<.01f,"quarter elapsed clears clockwise from twelve to three oclock");
            }
            Object.Destroy(ringGo);
            var combat=pet.GetComponent<CreatureCombatant>();combat.ApplyCombatHit(new CombatHit(a.gameObject,"defeat",4,100000,CreatureDamageKind.Physical,false,false,combat.Center));
            check(stats.Health==0&&!input.CanUsePetSkills,"zero HP immediately disables HUB skills");await gap(450);
            check(!pet.IsSummoned&&stats.Health==0,"emergency recall preserves zero HP and does not summon replacement");
            bool visible=false;foreach(var renderer in pet.GetComponentsInChildren<SkinnedMeshRenderer>())visible|=renderer.enabled;
            check(!visible,"emergency recall hides pet without waiting for Dead");
            var down=Wild(Vector3.left*4);await gap(100);down.TakeDamage(100000);check(down.IsDowned&&down.DownedDuration==15,"capturable zero HP keeps existing downed window");
            var dead=Wild(Vector3.right*6);dead.isBoss=true;dead.TakeDamage(100000);await gap(3300);
            check(!dead.gameObject.activeSelf&&dead.DeathVisualComplete,"unowned boss visual disappears after death without ground loot");
            check(Object.FindObjectsByType<PveLootView>(FindObjectsSortMode.None).Length==0,"reward service creates no ground-loot view");
            // Camera chi doi trong encounter QA tam de chup ro vong dem, khong luu scene.
            var follow=Object.FindFirstObjectByType<BasicCameraFollow>();if(follow!=null)follow.enabled=false;
            camera.transform.position=origin+new Vector3(0,5,-9);camera.transform.rotation=Quaternion.LookRotation(origin+Vector3.forward*2-camera.transform.position);await gap(200);
            check(Object.FindObjectsByType<DownedRing>(FindObjectsSortMode.None).Any(r=>r.isActiveAndEnabled&&r.GetComponent<CanvasRenderer>()!=null&&r.canvasRenderer.materialCount>0),"visible countdown ring has a live CanvasRenderer and submitted material");
            await EnemyAiVerification.ResolutionForElements(1920,1080,"r2");
            await Capture("r2-1920x1080",gap);
            await EnemyAiVerification.ResolutionForElements(1366,768,"r2");
            await Capture("r2-1366x768",gap);
            File.WriteAllText(Root+"r2-status.txt","COMPLETE R2 focused engine verification");
        }
        static async Task Capture(string name,Func<int,Task> gap){string path=Root+name+".png";File.AppendAllText(Root+"created-paths.txt",path+"\n");ScreenCapture.CaptureScreenshot(path);await gap(200);}
        sealed class FullTestBag:GDS.Core.ListBag { public override GDS.Core.Events.Result CanAdd(GDS.Core.Item item)=>GDS.Core.Events.Fail.ItemCannotFit; }
        static async Task RewardFailures(GameObject source,DummyEnemy enemy,Action<bool,string> check,Func<int,Task> gap) {
            var rewards=Object.FindFirstObjectByType<PveRewardService>();var save=Object.FindFirstObjectByType<PlayerSaveController>();
            var inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();var wallet=Object.FindFirstObjectByType<PlayerCurrencyWallet>();
            var authored=Resources.Load<PveLootCatalog>("Pve/LootCatalog");var fixture=Object.Instantiate(authored);
            fixture.itemChance=1;fixture.minimumGold=fixture.maximumGold=3;
            var item=authored.entries.FirstOrDefault(e=>e!=null&&e.item!=null)?.item??authored.potion??authored.healingApple;
            check(item!=null,"reward fixture reuses an item from the real authored catalog");
            fixture.entries=new System.Collections.Generic.List<PveLootCatalog.Entry>{new PveLootCatalog.Entry{item=item,weight=1,quantity=2}};
            var bag=inventory.Inventory;int receipts=0;Action<ItemAcquisitionReceipt> listener=_=>receipts++;
            rewards.enabled=false;ItemAcquisitionFeedback.Received+=listener;
            try {
                rewards.ConfigureLoot(fixture);await gap(150);enemy.isBoss=true;enemy.InitializeCaptureHealth(10000,10000);
                enemy.ApplyCombatHit(new CombatHit(source,"r2-reward-fault",40,100000,CreatureDamageKind.Physical,false,false,enemy.TargetPosition));
                var record=rewards.CreateSaveData().pending.Last();int gold=wallet.Gold,quantity=inventory.GetQuantity(item),count=rewards.CompletedCount;
                check(record.experienceCommitted&&record.items.SingleOrDefault(entry=>entry.itemId==item.StableId)?.quantity==2,"real terminal journal freezes item roll and commits EXP once");
                check(!rewards.TryCollect(record.transactionId)&&receipts==0,"delivery API waits for death visual completion");
                float until=Time.time+5;while(!enemy.DeathVisualComplete&&Time.time<until)await gap(40);
                check(enemy.DeathVisualComplete&&!enemy.gameObject.activeSelf,"reward fault fixture finishes death before delivery tests");
                inventory.SetInventory(new FullTestBag{Size=0});
                check(!rewards.TryCollect(record.transactionId)&&wallet.Gold==gold&&rewards.CompletedCount==count&&receipts==0,"capacity-rejecting bag keeps reward pending and emits no success toast");
                inventory.SetInventory(bag);string before=JsonUtility.ToJson(inventory.CreateSaveData());
                using(var locked=new FileStream(save.SavePath,FileMode.Open,FileAccess.Read,FileShare.None))
                    check(!rewards.TryCollect(record.transactionId)&&wallet.Gold==gold&&before==JsonUtility.ToJson(inventory.CreateSaveData())&&receipts==0,"actual save IO failure rolls back wallet and inventory without success receipt");
                check(rewards.TryCollect(record.transactionId)&&wallet.Gold==gold+3&&inventory.GetQuantity(item)==quantity+2&&receipts==record.items.Count+1,"retry commits frozen rewards and publishes exact gold/item receipts");
                check(!rewards.TryCollect(record.transactionId)&&rewards.CompletedCount==count+1,"repeated collection cannot duplicate successful reward");
                var disk=PlayerSaveMigration.Read(File.ReadAllText(save.SavePath),out _);rewards.RestoreFromSaveData(disk.pveRewards);
                check(!rewards.CreateSaveData().pending.Any(r=>r.transactionId==record.transactionId)&&!rewards.TryCollect(record.transactionId),"completed reward stays absent after journal reload");
            } finally { inventory.SetInventory(bag);ItemAcquisitionFeedback.Received-=listener;rewards.ConfigureLoot(authored);rewards.enabled=true;Object.Destroy(fixture); }
        }
    }
}
#endif

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
using Capstone.Game.ShopSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Explicit engine verification with temporary files and no real profile writes.</summary>
    public static class DemoSceneOneGameplayVerification {
        const string Qa=UISystem.QaArtifactPaths.Root+"demoscene1-starter-shop-respawn-2026-11-16/";
        const string Session="Capstone.DemoScene1.Gameplay20261116";
        static readonly List<string> checks=new List<string>();
        static bool running;
        static double deadline;
        static EditorWindow gameView;
        static PropertyInfo sizeProperty;
        static int originalSize=-1;
        static void Write(string file,string text){Directory.CreateDirectory(Qa);File.WriteAllText(Qa+file,text);File.AppendAllText(Qa+"created-paths.txt",Qa+file+"\n");}
        static void Check(bool pass,string name){checks.Add((pass?"PASS ":"FAIL ")+name);Write(SessionState.GetBool(Session+".edges",false)?"edge-checks.txt":"engine-checks.txt",string.Join("\n",checks));if(!pass)throw new InvalidOperationException(name);}
        [MenuItem("Tools/Capstone/PvE/4. Verify starter Shop and respawn (isolated saves)")]
        public static void Begin() => BeginRun(false);
        [MenuItem("Tools/Capstone/PvE/5. Verify full BOX rollback and scene unload")]
        public static void BeginEdges() => BeginRun(true);
        static void BeginRun(bool edges) {
            if(EditorApplication.isPlayingOrWillChangePlaymode||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/DemoScene 1.unity")throw new InvalidOperationException("Mở DemoScene 1 ngoài Play Mode.");
            if(Object.FindFirstObjectByType<DemoSceneOneStarterGrant>()==null)throw new InvalidOperationException("Chạy bước 3 Apply starter Shop and respawn trước.");
            checks.Clear();running=false;SessionState.SetBool(Session,true);SessionState.SetBool(Session+".edges",edges);
            SessionState.SetBool(Session+".reload",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+".options",(int)EditorSettings.enterPlayModeOptions);
            SessionState.SetString(Session+".save",Qa+"test-saves/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Watchdog;
            Write(edges?"edge-status.txt":"engine-status.txt","ENTERING_PLAY; bounded 150 seconds");EditorApplication.EnterPlaymode();
        }
        static void Watchdog(){if(EditorApplication.timeSinceStartup<deadline)return;Write(SessionState.GetBool(Session+".edges",false)?"edge-status.txt":"engine-status.txt","TIMEOUT; stop and do not restart Unity");Finish();}
        static void Finish(){
            SessionState.SetBool(Session,false);running=false;EditorApplication.update-=Watchdog;
            if(originalSize>=0&&gameView!=null){sizeProperty.SetValue(gameView,originalSize);originalSize=-1;}
            EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+".reload",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+".options",0);
            if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){
            if(!SessionState.GetBool(Session,false)||running)return;
            string root=SessionState.GetString(Session+".save","");
            foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new PlayerJsonFileSaveStore(root));
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(root));
            if(SessionState.GetBool(Session+".edges",false))RunEdges();else Run();
        }
        static async Task Gap(int ms=100){await Task.Delay(ms);if(!EditorApplication.isPlaying||!SessionState.GetBool(Session,false))throw new OperationCanceledException("Verification stopped");}
        static async Task Until(Func<bool> condition,float seconds,string label){float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)await Gap(50);Check(condition(),label);}
        sealed class FixedRandom:ICaptureRandom{public double NextFloat01()=>0;}
        static PlayerSaveData Read(PlayerSaveController save)=>PlayerSaveMigration.Read(File.ReadAllText(save.SavePath),out _);
        static void RegisterFile(string path)=>File.AppendAllText(Qa+"created-paths.txt",path+"\n");
        static async void RunEdges(){
            running=true;bool background=Application.runInBackground;Application.runInBackground=true;
            UnityEngine.SceneManagement.Scene temporary=default;
            try{
                await Gap(800);var input=Object.FindFirstObjectByType<PetCommandInput>();var save=Object.FindFirstObjectByType<PlayerSaveController>();
                var grant=save.GetComponent<DemoSceneOneStarterGrant>();var box=Object.FindFirstObjectByType<PetBoxRuntimeProvider>();var bag=Object.FindFirstObjectByType<MonsterInventoryAdapter>();
                foreach(var brain in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None))brain.enabled=false;
                foreach(var owner in Object.FindObjectsByType<EncounterSpawnPoint>(FindObjectsSortMode.None))owner.enabled=false;
                // A real full 6-slot party + one-slot BOX, with form 03 marked captured rather than a repairable starter.
                var protectedPet=input.petSlots[2];var protectedMeta=protectedPet.GetComponent<PetCollectionMetadata>();protectedMeta.SetCaptureBall(CaptureAssets.LoadBall().StableId);
                for(int i=3;i<6;i++){grant.catalog.TryInstantiate("Dragon Dusk","01",null,out var pet);pet.AssignOwner(input.transform);pet.Withdraw();pet.GetComponent<PetCollectionMetadata>().AssignPersistentId("qa-full-party-"+i);box.TryStageOwnedPet(pet,out _);}
                grant.catalog.TryInstantiate("Dragon Dusk","01",null,out var filler);filler.AssignOwner(input.transform);filler.Withdraw();filler.GetComponent<PetCollectionMetadata>().AssignPersistentId("qa-full-box");
                box.RestoreState(new[]{filler},1,string.Empty,0);
                Check(save.SaveNow(),"edge fixture full party and BOX saved only on E");
                var fixture=Read(save);fixture.appliedStarterGrants.Clear();
                var store=new PlayerJsonFileSaveStore(SessionState.GetString(Session+".save","")+"/edge");store.TrySave(save.SlotId,fixture,out _);RegisterFile(store.GetPath(save.SlotId));save.SetSaveStore(store);save.LoadNow();
                var party=(PetController[])input.petSlots.Clone();int quantity=bag.GetQuantity(CaptureAssets.LoadBall());string fileBefore=File.ReadAllText(save.SavePath);
                Check(!grant.TryApply(save,out _),"missing starter form with full party and BOX fails safely");await Gap();
                Check(party.SequenceEqual(input.petSlots)&&box.GetStoredPet(0)==filler&&bag.GetQuantity(CaptureAssets.LoadBall())==quantity&&File.ReadAllText(save.SavePath)==fileBefore&&!save.HasStarterGrant(DemoSceneOneStarterGrant.GrantKey),"full-capacity failure leaves roster inventory file and receipt unchanged");
                box.RestoreState(Array.Empty<PetController>(),1,string.Empty,0);Object.Destroy(filler.gameObject);await Gap();
                int ownedBefore=box.OwnedCount;string protectedBefore=JsonUtility.ToJson(protectedMeta.CreateSaveData());
                using(var locked=new FileStream(save.SavePath,FileMode.Open,FileAccess.Read,FileShare.None))Check(!grant.TryApply(save,out _),"file-lock failure after creating missing form is rejected");await Gap();
                Check(box.OwnedCount==ownedBefore&&box.GetStoredPet(0)==null&&party.SequenceEqual(input.petSlots)&&bag.GetQuantity(CaptureAssets.LoadBall())==quantity,"created form and BOX placement rolled back after failed save");
                Check(grant.TryApply(save,out _)&&box.GetStoredPet(0)?.GetComponent<PetHudRuntimeStats>().Level==36,"missing form 03 goes to BOX when party is full");
                Check(party.SequenceEqual(input.petSlots)&&protectedBefore==JsonUtility.ToJson(protectedMeta.CreateSaveData()),"BOX fallback preserves six party pets and existing captured form 03");
                Check(save.LoadNow()&&box.GetStoredPet(0)?.GetComponent<PetHudRuntimeStats>().Level==36&&grant.TryApply(save,out _)&&bag.GetQuantity(CaptureAssets.LoadBall())==quantity+5,"BOX fallback and one-time receipt survive load");

                temporary=UnityEngine.SceneManagement.SceneManager.CreateScene("QA - encounter unload (temporary)");
                var smokePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/DemoSceneOne/DeathSmoke.prefab");
                var starsPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/DemoSceneOne/DownedStars_CapstoneFallback.prefab");
                var owners=new List<EncounterSpawnPoint>();var actors=new List<DummyEnemy>();
                Vector3 forward=Camera.main!=null?Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized:input.transform.forward;
                Vector3 right=Vector3.Cross(Vector3.up,forward);
                for(int form=1;form<=3;form++){
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Dragons/Wild/Wild_Dragon"+form.ToString("00")+".prefab");
                    var go=Object.Instantiate(prefab,input.transform.position+forward*4+right*(form*2.3f),Quaternion.LookRotation(-forward));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,temporary);
                    var brain=go.AddComponent<BasicCreatureEnemy>();brain.patrolRadius=0;brain.aggroRadius=0;
                    var level=go.AddComponent<EncounterLevel>();level.openingEncounter=false;level.Initialize();
                    var health=go.GetComponent<DummyEnemy>();
                    var ownerGo=new GameObject("QA form "+form+" encounter home");ownerGo.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ownerGo,temporary);
                    ownerGo.transform.SetPositionAndRotation(go.transform.position,go.transform.rotation);var point=ownerGo.AddComponent<EncounterSpawnPoint>();point.initialActor=health;point.encounterPrefab=prefab;point.actorScale=go.transform.localScale;point.smokePrefab=smokePrefab;point.downedStarsPrefab=starsPrefab;
                    var stars=go.AddComponent<DownedStatusVisual>();stars.effectPrefab=starsPrefab;ownerGo.SetActive(true);owners.Add(point);actors.Add(health);
                }
                await Gap(120);
                for(int i=0;i<3;i++){
                    var health=actors[i];health.ApplyCombatHit(new CombatHit(input.activePet.gameObject,"qa-form-stun",i,100000,CreatureDamageKind.Physical,false,false,health.TargetPosition));
                    Check(health.IsDowned&&health.GetComponent<EncounterLevel>().RolledLevel>=3&&health.GetComponent<EncounterLevel>().RolledLevel<=7,"wild form "+(i+1)+" enters capture window at weighted level 3-7");
                }
                await Gap(160);
                for(int i=0;i<3;i++){var animator=actors[i].GetComponent<CreatureAnimationDriver>().BoundAnimator;Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle")||animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Idle"),"dragon form "+(i+1)+" Stun fallback is actual Idle");}
                await Resolution(1920,1080,"dragon-stun");
                await Until(()=>actors.All(a=>!a.IsDowned),17,"all three dragon stun windows expire into terminal death");
                await Gap(100);
                for(int i=0;i<3;i++){var animator=actors[i].GetComponent<CreatureAnimationDriver>().BoundAnimator;Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Die")||animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Die"),"dragon form "+(i+1)+" terminal animation is Die");}
                await Until(()=>owners.Any(p=>p.LifecycleState=="Smoke"),4,"dragon death creates pooled smoke");
                await Resolution(1366,768,"dragon-smoke");
                var pendingGo=new GameObject("QA pending respawn owner");pendingGo.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pendingGo,temporary);
                var pending=pendingGo.AddComponent<EncounterSpawnPoint>();pending.encounterPrefab=owners[0].encounterPrefab;pendingGo.SetActive(true);
                Check(pending.RespawnPending,"temporary scene has a pending respawn timer");
                var unload=UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(temporary);await Until(()=>unload.isDone,3,"temporary encounter scene unload completes");
                Check(owners.All(o=>o==null)&&actors.All(a=>a==null)&&pending==null,"unload destroys encounter owners and wild actors");
                Check(Skills.CombatEffectPool.ActiveCount==0,"scene unload returns active stun and smoke effects to pool");
                int count=DummyEnemy.ActiveEnemies.Count;await Gap(5500);Check(DummyEnemy.ActiveEnemies.Count==count,"no late respawn after owner/scene unload");
                foreach(string file in Directory.GetFiles(SessionState.GetString(Session+".save",""),"*",SearchOption.AllDirectories))RegisterFile(file);
                Write("edge-status.txt","COMPLETE "+checks.Count+" boundary/three-form/unload checks.");
            }catch(Exception exception){Write("edge-status.txt","FAILED\n"+exception);Debug.LogException(exception);}
            finally{if(temporary.IsValid()&&temporary.isLoaded)UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(temporary);Application.runInBackground=background;Finish();}
        }
        static async void Run(){
            running=true;bool background=Application.runInBackground;Application.runInBackground=true;
            var created=new List<GameObject>();var enabledBrains=new List<BasicCreatureEnemy>();
            PlayerSaveController save=null;
            try{
                await Gap(1000);
                var input=Object.FindFirstObjectByType<PetCommandInput>();save=Object.FindFirstObjectByType<PlayerSaveController>();
                var grant=save.GetComponent<DemoSceneOneStarterGrant>();var bag=Object.FindFirstObjectByType<MonsterInventoryAdapter>();
                var box=Object.FindFirstObjectByType<PetBoxRuntimeProvider>();var ball=CaptureAssets.LoadBall();
                Check(save.SavePath.StartsWith(Qa,StringComparison.OrdinalIgnoreCase),"test profile on E, real profile untouched");
                Check(save.HasStarterGrant(DemoSceneOneStarterGrant.GrantKey),"fresh starter grant receipt committed");
                Check(input.petSlots.Take(3).Select(p=>p.GetComponent<PetHudRuntimeStats>().Level).SequenceEqual(new[]{5,18,36}),"fresh starter levels 5 / 18 / 36");
                Check(bag.GetQuantity(ball)==5&&box.OwnedCount==3,"fresh starter owns exactly 3 forms and 5 basic balls");
                Check(Read(save).appliedStarterGrants.SequenceEqual(new[]{DemoSceneOneStarterGrant.GrantKey}),"receipt persisted inside same player file");
                foreach(var existingBrain in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None))if(existingBrain.enabled){enabledBrains.Add(existingBrain);existingBrain.enabled=false;}
                bag.TryRemoveItemAtomic(ball,2);Check(save.SaveNow()&&save.LoadNow(),"save and reload consumed starter inventory");
                Check(grant.TryApply(save,out _)&&bag.GetQuantity(ball)==3&&box.OwnedCount==3,"reload does not replenish balls or duplicate pets");

                // Simulate the exact old 1/2/3 preview IDs using only these temporary actors/files.
                input.petSlots[1].GetComponent<PetCollectionMetadata>().AssignPersistentId("preview-dragon-2-qa-legacy");
                input.petSlots[2].GetComponent<PetCollectionMetadata>().AssignPersistentId("preview-dragon-3-qa-legacy");
                grant.catalog.TryInstantiate("Dragon Dusk","02",null,out var protectedPet);created.Add(protectedPet.gameObject);
                protectedPet.AssignOwner(input.transform);protectedPet.Withdraw();
                var protectedMeta=protectedPet.GetComponent<PetCollectionMetadata>();protectedMeta.AssignPersistentId("qa-captured-pet");protectedMeta.SetCaptureBall(ball.StableId);protectedMeta.RecordCaptureInfo();
                protectedMeta.TrySetNickname("Giữ nguyên cá thể",out _);protectedMeta.SetProgress(41,999);
                var protectedStats=protectedPet.GetComponent<PetHudRuntimeStats>();protectedStats.SetIdentity(protectedStats.DisplayName,27,protectedStats.Icon);protectedStats.SetStatus(12,protectedStats.MaxHealth,0,0);
                Check(box.TryStageOwnedPet(protectedPet,out _),"temporary captured individual placed in party");
                Check(save.SaveNow(),"stage temporary legacy migration fixture");
                var fixture=Read(save);fixture.appliedStarterGrants.Clear();
                foreach(var state in fixture.pets.petStates){int slot=fixture.pets.partyPetIds.IndexOf(state.petId);if(slot>=0&&slot<3){state.runtimeStats.level=slot+1;state.runtimeStats.health=1;}}
                string root=SessionState.GetString(Session+".save","");
                var legacyStore=new PlayerJsonFileSaveStore(root+"/legacy");Check(legacyStore.TrySave(save.SlotId,fixture,out _),"write isolated old 1/2/3 fixture");RegisterFile(legacyStore.GetPath(save.SlotId));
                save.SetSaveStore(legacyStore);Check(save.LoadNow(),"load v14 fixture without optional grant field");
                string protectedBefore=JsonUtility.ToJson(protectedStats.CreateSaveData())+JsonUtility.ToJson(protectedMeta.CreateSaveData());
                string[] genetics=input.petSlots.Take(3).Select(p=>JsonUtility.ToJson(p.GetComponent<PetCollectionMetadata>().CreateSaveData())).ToArray();
                int ballsBefore=bag.GetQuantity(ball);
                Check(grant.TryApply(save,out var migrationError),"legacy starter migration commits: "+migrationError);
                Check(input.petSlots.Take(3).Select(p=>p.GetComponent<PetHudRuntimeStats>().Level).SequenceEqual(new[]{5,18,36}),"legacy starter repaired to 5 / 18 / 36");
                Check(bag.GetQuantity(ball)==ballsBefore+5&&box.OwnedCount==4,"legacy migration grants five balls without replacing roster");
                Check(protectedBefore==JsonUtility.ToJson(protectedStats.CreateSaveData())+JsonUtility.ToJson(protectedMeta.CreateSaveData()),"captured pet keeps level HP genetics skills nickname and progress");
                for(int i=0;i<3;i++){
                    var before=JsonUtility.FromJson<PetCustomizationSaveData>(genetics[i]);var after=input.petSlots[i].GetComponent<PetCollectionMetadata>().CreateSaveData();
                    Check(JsonUtility.ToJson(before.individualValues)==JsonUtility.ToJson(after.individualValues)&&JsonUtility.ToJson(before.effortValues)==JsonUtility.ToJson(after.effortValues)&&before.nature==after.nature&&before.nickname==after.nickname&&before.experience==after.experience,"starter "+(i+1)+" genetics nickname and earned EXP preserved");
                }
                Check(save.LoadNow()&&grant.TryApply(save,out _)&&bag.GetQuantity(ball)==ballsBefore+5,"legacy receipt survives load and prevents repeat grant");

                fixture=Read(save);fixture.appliedStarterGrants.Clear();
                var trained=fixture.pets.petStates.Single(p=>p.petId==fixture.pets.partyPetIds[1]);trained.runtimeStats.level=50;
                var failureStore=new PlayerJsonFileSaveStore(root+"/rollback");failureStore.TrySave(save.SlotId,fixture,out _);RegisterFile(failureStore.GetPath(save.SlotId));
                save.SetSaveStore(failureStore);Check(save.LoadNow(),"load rollback fixture with trained starter level 50");
                string inventoryBefore=JsonUtility.ToJson(bag.CreateSaveData());string[] statesBefore=input.petSlots.Where(p=>p!=null).Select(p=>JsonUtility.ToJson(p.GetComponent<PetHudRuntimeStats>().CreateSaveData())+JsonUtility.ToJson(p.GetComponent<PetCollectionMetadata>().CreateSaveData())).ToArray();
                using(var locked=new FileStream(save.SavePath,FileMode.Open,FileAccess.Read,FileShare.None))Check(!grant.TryApply(save,out _),"actual file lock rejects starter commit");
                Check(!save.HasStarterGrant(DemoSceneOneStarterGrant.GrantKey)&&inventoryBefore==JsonUtility.ToJson(bag.CreateSaveData())&&statesBefore.SequenceEqual(input.petSlots.Where(p=>p!=null).Select(p=>JsonUtility.ToJson(p.GetComponent<PetHudRuntimeStats>().CreateSaveData())+JsonUtility.ToJson(p.GetComponent<PetCollectionMetadata>().CreateSaveData()))),"failed save rolls back receipt inventory HP levels and metadata");
                Check(grant.TryApply(save,out _)&&input.petSlots[1].GetComponent<PetHudRuntimeStats>().Level==50,"retry succeeds and never lowers a trained starter");
                Check(input.petSlots.Take(3).All(p=>p.GetComponent<PetHudRuntimeStats>().Health==p.GetComponent<PetHudRuntimeStats>().MaxHealth),"recognized starters healed once on successful receipt");

                var expectedOffers = new Dictionary<string, int> {
                    ["capture-ball-basic-01"] = 5, ["hp-potion-small"] = 7, ["mana-potion"] = 15, ["revive-small"] = 35
                };
                var shop = Object.FindObjectsByType<ShopSource>(FindObjectsSortMode.None)
                    .Single(s => expectedOffers.Keys.All(id => s.Find(id) != null));
                Check(expectedOffers.All(pair => shop.Find(pair.Key) is { } offer && offer.GoldPrice == pair.Value
                    && offer.Stock == -1 && offer.MaxPerPurchase == 99), "scene merchant stocks the expanded priced catalog");
                var wallet=Object.FindFirstObjectByType<PlayerCurrencyWallet>();wallet.RestoreFromSaveData(new CurrencySaveData{captured=true,gold=1000});
                var profile=Object.FindFirstObjectByType<Capstone.Game.ProfileSystem.PlayerProfileRuntimeProvider>();
                profile.SetLevelProgress(1,0,100);
                double clock=0;var purchase=new ShopPurchaseService(bag,wallet,()=>clock,profile);
                var visible=shop.Offers.Where(purchase.IsVisible).ToArray();
                Check(visible.Length==expectedOffers.Count&&visible.All(offer=>expectedOffers.ContainsKey(offer.Id)),
                    "scene merchant shows only first-tier items at player level one");
                foreach(var offer in visible){clock+=1;int owned=purchase.Owned(offer),gold=wallet.Gold;Check(purchase.TryBuy(shop,offer.Id,2,offer.GoldPrice,out _)&&purchase.Owned(offer)==owned+2&&wallet.Gold==gold-2*offer.GoldPrice,"real bulk purchase "+offer.Id);Check(!purchase.TryBuy(shop,offer.Id,1,offer.GoldPrice,out _),"rapid repeat purchase gated "+offer.Id);}
                wallet.RestoreFromSaveData(new CurrencySaveData{captured=true,gold=0});clock+=1;
                Check(!purchase.TryBuy(shop,shop.Offers[0].Id,1,shop.Offers[0].GoldPrice,out _)&&wallet.Gold==0,"insufficient gold keeps inventory/gold safe");
                var evolution=Object.FindFirstObjectByType<PetEvolutionService>()??save.gameObject.AddComponent<PetEvolutionService>();evolution.Bind(bag);
                Check(shop.Find("dusk-stone")==null&&bag.TryAddItemAtomic(InventoryDefinitionCatalog.Find("dusk-stone"),1),
                    "stone fixture uses the canonical item outside the shop");
                for(int form=1;form<=2;form++){
                    grant.catalog.TryInstantiate("Dragon Dusk",form.ToString("00"),null,out var pet);created.Add(pet.gameObject);pet.AssignOwner(input.transform);pet.Withdraw();
                    var stats=pet.GetComponent<PetHudRuntimeStats>();int level=form==1?18:36;
                    stats.SetIdentity(stats.DisplayName,level-1,stats.Icon);Check(!evolution.CreatePreview(pet).RequirementMet,"level evolution rejects "+(level-1));
                    Check(evolution.CreatePreview(pet,PetEvolutionMethod.Item,"dusk-stone").RequirementMet,"stone may evolve below level "+level);
                    stats.SetIdentity(stats.DisplayName,level,stats.Icon);Check(evolution.CreatePreview(pet).RequirementMet,"level evolution accepts "+level);
                }
                Check(save.SaveNow(),"shop inventory and roster saved to temporary profile");
                await Resolution(1920,1080,"starter");await Resolution(1366,768,"starter");

                var point=Object.FindObjectsByType<EncounterSpawnPoint>(FindObjectsSortMode.None).First(p=>p.CurrentActor?.GetComponent<WildPetCaptureTarget>()!=null);
                var actor=point.CurrentActor;var wild=actor.GetComponent<WildPetCaptureTarget>();var oldId=wild.Metadata.PetId;string encounter=actor.EncounterId;
                Check(!actor.autoRevive&&actor.GetComponent<EncounterLevel>().RolledLevel==5,"opening encounter fixed level 5 and same-actor revive disabled");
                var coordinator=Object.FindFirstObjectByType<PetCaptureCoordinator>();coordinator.RandomSource=new FixedRandom();bag.TryAddItemAtomic(ball,10);
                int generation=point.Generation;var ownedActor=wild.Pet;
                Check(coordinator.TryCaptureTarget(ball,bag,wild,out var result,out var captureError)&&result.Success,"actual encounter capture commits: "+captureError);
                await Until(()=>point.RespawnPending,1,"capture detaches old actor and schedules one respawn");
                Check(ownedActor!=null&&box.Contains(ownedActor)&&ownedActor.GetComponent<PetCollectionMetadata>().PetId==oldId,"captured individual retained in roster");
                await Gap(300);Check(point.CurrentActor==null,"capture does not respawn immediately");
                await Until(()=>point.CurrentActor!=null,7,"capture respawns after real five-second wait");
                var replacement=point.CurrentActor;var replacementWild=replacement.GetComponent<WildPetCaptureTarget>();
                Check(replacement!=actor&&replacement.EncounterId!=encounter&&replacementWild.Metadata.PetId!=oldId&&replacement.CurrentHealth==replacement.maxHealth,"replacement is a fresh full-HP individual with new encounter and pet IDs");
                Check(ownedActor!=null&&box.Contains(ownedActor)&&point.Generation==generation+2,"captured pet survives respawn and no duplicate schedule");
                var brain=replacement.GetComponent<BasicCreatureEnemy>();brain.enabled=true;brain.patrolRadius=0;
                int deaths=0;replacement.Defeated+=_=>deaths++;
                replacement.ApplyCombatHit(new CombatHit(input.activePet.gameObject,"qa-terminal",1,100000,CreatureDamageKind.Physical,false,false,replacement.TargetPosition));
                Check(replacement.IsDowned&&deaths==0,"zero HP starts 15-second capture window, no terminal reward yet");
                await Gap(160);var animator=replacement.GetComponent<CreatureAnimationDriver>().BoundAnimator;
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle")||animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Idle"),"missing real Stun clip falls back to Idle, never Die");
                await Resolution(1920,1080,"stunned");
                await Until(()=>deaths==1,17,"downed timeout commits exactly one death");
                await Gap(140);Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Die")||animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Die"),"terminal death plays Die before despawn");
                await Until(()=>point.LifecycleState=="Smoke",4,"Die completes before pooled smoke starts");
                await Resolution(1366,768,"smoke");
                await Until(()=>point.RespawnPending,2,"body disposed under smoke and one replacement scheduled");
                Check(replacement==null&&deaths==1,"dead actor destroyed after death/smoke, reward callback remains once");
                await Until(()=>point.CurrentActor!=null,7,"terminal death respawns a fresh actor after five seconds");
                Check(point.CurrentActor.EncounterId!=encounter&&point.CurrentActor.IsAlive&&point.CurrentActor.GetComponent<EncounterLevel>().RolledLevel==5,"opening encounter remains level 5 on respawn");
                point.enabled=false;var retained=point.CurrentActor;await Gap(300);Check(point.CurrentActor==retained&&!point.RespawnPending,"disabled owner cancels scheduling without destroying live actor");
                Write("engine-status.txt","COMPLETE "+checks.Count+" checks. Callback/engine checks executed; physical mouse keyboard, scene-unload and visual review are reported separately.");
                foreach(string file in Directory.GetFiles(root,"*",SearchOption.AllDirectories))RegisterFile(file);
            }catch(Exception exception){Write("engine-status.txt","FAILED\n"+exception);Debug.LogException(exception);}
            finally{foreach(var go in created)if(go!=null)Object.Destroy(go);Application.runInBackground=background;Finish();}
        }
        static async Task Resolution(int width,int height,string label){
            var assembly=typeof(UnityEditor.Editor).Assembly;const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            var viewType=assembly.GetType("UnityEditor.GameView");gameView=EditorWindow.GetWindow(viewType,false,"Game",false);
            sizeProperty=viewType.GetProperty("selectedSizeIndex",flags);if(originalSize<0)originalSize=(int)sizeProperty.GetValue(gameView);
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
            var method=sizesType.GetMethod("GetGroup",flags);var group=method.Invoke(singleton,new[]{Enum.Parse(method.GetParameters()[0].ParameterType,"Standalone")});
            int count=(int)group.GetType().GetMethod("GetTotalCount",flags).Invoke(group,null),index=-1;
            for(int i=0;i<count;i++){var size=group.GetType().GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});var type=size.GetType();if((int)type.GetProperty("width",flags).GetValue(size)==width&&(int)type.GetProperty("height",flags).GetValue(size)==height){index=i;break;}}
            if(index<0)throw new InvalidOperationException("Thiếu preset Game view "+width+"x"+height);
            sizeProperty.SetValue(gameView,index);gameView.Show();gameView.Focus();await Gap(180);
            Check(Screen.width==width&&Screen.height==height,"native Game view "+label+" "+width+"x"+height);
            string path=Qa+label+"-"+width+"x"+height+".png";ScreenCapture.CaptureScreenshot(path);RegisterFile(path);await Gap(100);
        }
    }
    /// <summary>Exercises normal Start using a copy of the real profile, then explicitly applies the approved starter.</summary>
    public static class CombatStarterStartupVerification {
        const string Qa=UISystem.QaArtifactPaths.Root+"demoscene1-combat-slots-fix-2026-11-16/";
        const string Session="Capstone.CombatStarter.NormalStartup";
        static void Write(string file,string value){Directory.CreateDirectory(Qa);File.WriteAllText(Qa+file,value);File.AppendAllText(Qa+"created-paths.txt",Qa+file+"\n");}
        [MenuItem("Tools/Capstone/PvE/6. Apply combat starter and show HUD (DemoScene 1)")]
        public static void ApplyReal()=>Begin(true);
        [MenuItem("Tools/Capstone/PvE/7. Verify normal startup from profile copy")]
        public static void VerifyCopy()=>Begin(false);
        static void Begin(bool real){
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/Scenes/DemoScene 1.unity")throw new InvalidOperationException("Dừng Play và mở DemoScene 1.");
            var isolation=Object.FindFirstObjectByType<DemoSceneOneSaveIsolation>();var save=Object.FindFirstObjectByType<PlayerSaveController>();
            if(isolation==null||save?.GetComponent<DemoSceneOneStarterGrant>()==null)throw new InvalidOperationException("Thiếu binding starter/isolation.");
            string source=new PlayerJsonFileSaveStore(isolation.developmentProfile).GetPath(save.SlotId);
            string target=real?isolation.developmentProfile:Qa+"profile-copy-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(Qa+"backups");
            if(File.Exists(source)){
                string backup=Qa+"backups/pre-apply-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json";
                File.Copy(source,backup,false);File.AppendAllText(Qa+"created-paths.txt",backup+"\n");
                if(!real){Directory.CreateDirectory(target);string copy=new PlayerJsonFileSaveStore(target).GetPath(save.SlotId);File.Copy(source,copy,false);File.AppendAllText(Qa+"created-paths.txt",copy+"\n");}
            }
            if(real){
                string backup=Qa+"backups/before-load-binding.unity";UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,backup,true);File.AppendAllText(Qa+"created-paths.txt",backup+"\n");
                var data=new SerializedObject(save);data.FindProperty("loadOnStart").boolValue=true;data.ApplyModifiedProperties();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
            SessionState.SetBool(Session,true);SessionState.SetBool(Session+".real",real);SessionState.SetString(Session+".root",target);
            SessionState.SetBool(Session+".reload",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+".options",(int)EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            Write(real?"apply-status.txt":"normal-startup-status.txt","ENTERING_PLAY; normal PlayerSaveController.Start, no manual LoadNow or grant call");EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){
            if(!SessionState.GetBool(Session,false))return;
            string root=SessionState.GetString(Session+".root","");
            foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsSortMode.None)){
                save.SetSaveStore(new PlayerJsonFileSaveStore(root));
                if(!SessionState.GetBool(Session+".real",false))typeof(PlayerSaveController).GetField("loadOnStart",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(save,false);
            }
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(root));
            Run();
        }
        static async void Run(){
            bool real=SessionState.GetBool(Session+".real",false);string name=real?"apply":"normal-startup";
            try{
                await Task.Delay(1400);if(!EditorApplication.isPlaying)throw new OperationCanceledException("Play stopped");
                var save=Object.FindFirstObjectByType<PlayerSaveController>();var input=Object.FindFirstObjectByType<PetCommandInput>();
                var provider=Object.FindFirstObjectByType<PetCommandHudProvider>();var bag=Object.FindFirstObjectByType<MonsterInventoryAdapter>();
                if(!save.IsCurrentSlotLoaded||save.IsSaveBlocked)throw new InvalidOperationException("Normal startup did not restore save: "+save.SaveBlockedReason);
                if(!input.petSlots.Take(3).Select(p=>p!=null?p.GetComponent<PetHudRuntimeStats>().Level:0).SequenceEqual(new[]{5,18,36}))throw new InvalidOperationException("Combat slots are not 5/18/36");
                provider.NotifyHudDataChanged();GameplayHudController.Live.Radial.SetOpen(true);
                var capture=Object.FindFirstObjectByType<HubCaptureHud>();capture.Refresh();
                await Task.Delay(200);
                var slots=provider.GetPetSlots();int balls=bag.GetQuantity(CaptureAssets.LoadBall());
                if(slots.Take(3).Any(s=>!s.occupied||s.icon==null)||slots.Skip(3).Any(s=>s.occupied))throw new InvalidOperationException("Expected three combat slot portraits and three empty slots");
                var text=(UnityEngine.UI.Text)typeof(HubCaptureHud).GetField("quantity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(capture);
                if(balls!=5||text.text!="5"||!save.HasStarterGrant(DemoSceneOneStarterGrant.GrantKey))throw new InvalidOperationException("Five starter balls are not bound to G");
                string screenshot=Qa+name+"-combat-hud.png";ScreenCapture.CaptureScreenshot(screenshot);File.AppendAllText(Qa+"created-paths.txt",screenshot+"\n");
                Write(name+"-status.txt","PASS normal startup restored current profile\nPASS combat slots 1/2/3 levels 5/18/36 with portraits\nPASS combat slots 4/5/6 empty\nPASS G quantity = 5 from inventory\nPASS starter receipt persisted once\n"+(real?"Actual development profile updated through existing save transaction; Play left open.":"Copied profile only; loadOnStart deliberately false to verify required-load guard."));
                await Task.Delay(250);
            }catch(Exception exception){Write(name+"-status.txt","FAILED\n"+exception);Debug.LogException(exception);}
            finally{
                SessionState.SetBool(Session,false);EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+".reload",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+".options",0);
                if(!real&&EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
            }
        }
    }
}
#endif

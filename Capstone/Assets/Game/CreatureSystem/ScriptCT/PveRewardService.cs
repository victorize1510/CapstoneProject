using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.WorldFeedback;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Durable rolled rewards. Pickup is a saved inventory/wallet transaction, never a visual callback.</summary>
    [DisallowMultipleComponent,RequireComponent(typeof(PlayerSaveController))]
    public sealed class PveRewardService:MonoBehaviour {
        readonly List<PendingPveRewardSaveData> pending=new List<PendingPveRewardSaveData>();
        readonly HashSet<string> journaled=new HashSet<string>(StringComparer.Ordinal),completed=new HashSet<string>(StringComparer.Ordinal);
        readonly Queue<string> completedOrder=new Queue<string>();
        PlayerSaveController save;MonsterInventoryAdapter inventory;PlayerCurrencyWallet wallet;PetCommandInput player;
        PetLevelUpService levels;
        PlayerProfileRuntimeProvider profile;
        string announcedFailure;
        [SerializeField] PveLootCatalog catalog;
        readonly Dictionary<string,DummyEnemy> awaitingDeath=new Dictionary<string,DummyEnemy>();
        bool busy;float nextLookup,nextPoll,retryAt;int writeFailures;
        public string LastFailure{get;private set;}
        public int PendingCount=>pending.Count;
        public int CompletedCount{get;private set;}
        public string LastStagedId{get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install(){foreach(var controller in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(controller.GetComponent<PveRewardService>()==null)controller.gameObject.AddComponent<PveRewardService>();}
        void Awake(){save=GetComponent<PlayerSaveController>();Resolve();}
        void OnEnable(){SceneManager.activeSceneChanged+=SceneChanged;}
        void OnDisable(){SceneManager.activeSceneChanged-=SceneChanged;}
        void SceneChanged(Scene old,Scene current){player=null;nextLookup=0;}
        void Resolve(){
            if(save==null)save=GetComponent<PlayerSaveController>();
            if(inventory==null)inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();
            if(wallet==null)wallet=Object.FindFirstObjectByType<PlayerCurrencyWallet>();
            if(player==null)player=Object.FindFirstObjectByType<PetCommandInput>();
            if(levels==null)levels=Object.FindFirstObjectByType<PetLevelUpService>();
            if(profile==null)profile=Object.FindFirstObjectByType<PlayerProfileRuntimeProvider>();
            if(catalog==null)catalog=Resources.Load<PveLootCatalog>("Pve/LootCatalog");
        }
        public void ConfigureLoot(PveLootCatalog value) { catalog=value; }
        public PveRewardsSaveData CreateSaveData()=>new PveRewardsSaveData{captured=true,pending=new List<PendingPveRewardSaveData>(pending)};
        public void RestoreFromSaveData(PveRewardsSaveData data){
            pending.Clear();journaled.Clear();completed.Clear();completedOrder.Clear();awaitingDeath.Clear();announcedFailure=null;
            if(data?.captured!=true||data.pending==null)return;
            foreach(var record in data.pending){pending.Add(record);journaled.Add(record.transactionId);}
        }
        public static bool HandleDefeat(DummyEnemy enemy,GameObject source){
            if(enemy==null||enemy.IsTrainingDummy||enemy.GetComponent<CreatureCombatant>()==null)return false;
            var memory=enemy.GetComponent<PveCombatState>();if(memory==null||memory.Contributions.Count==0)return false;
            var controller=Object.FindFirstObjectByType<PlayerSaveController>();if(controller==null)return false;
            var service=controller.GetComponent<PveRewardService>();if(service==null)service=controller.gameObject.AddComponent<PveRewardService>();
            return service.Stage(enemy,source,memory.Contributions);
        }
        bool Stage(DummyEnemy enemy,GameObject source,IReadOnlyList<PetDamageContribution> contributions){
            Resolve();if(catalog==null||inventory==null||wallet==null)return false;
            var loot = catalog.EffectiveCatalog;
            string id="pve:"+enemy.EncounterId;
            if(completed.Contains(id)||pending.Any(r=>r.transactionId==id))return true;
            float total=0;foreach(var part in contributions)total+=part.healthLost;if(total<=0)return false;
            var record=new PendingPveRewardSaveData{transactionId=id,scene=enemy.gameObject.scene.name,x=enemy.transform.position.x,y=enemy.transform.position.y,z=enemy.transform.position.z,createdUtcTicks=DateTime.UtcNow.Ticks};
            record.gold=UnityEngine.Random.Range(Mathf.Max(0,loot.minimumGold),Mathf.Max(loot.minimumGold,loot.maximumGold)+1);
            void AddLoot(MonsterItemDefinition item, int quantity = 1) {
                if (item != null) record.items.Add(new PveItemRewardSaveData { itemId = item.StableId, quantity = quantity });
            }
            if (UnityEngine.Random.value < loot.itemChance && loot.TrySelect(UnityEngine.Random.value, out var drop, out int quantity))
                AddLoot(drop, quantity);
            if (loot.smallHpPotion != null || loot.smallMpPotion != null) {
                loot.SelectSmallPotions(UnityEngine.Random.value, UnityEngine.Random.value, out var hp, out var mp);
                AddLoot(hp);
                AddLoot(mp);
            }
            var enemyStats=enemy.GetComponent<PetHudRuntimeStats>();
            int enemyLevel=enemyStats!=null?enemyStats.Level:1;
            var defeated=enemy.GetComponent<PetCollectionMetadata>();
            var material=defeated!=null?PveLootCatalog.SelectFamilyMaterial(defeated.DefinitionId,defeated.CurrentFormId,UnityEngine.Random.value):null;
            AddLoot(material);
            if (defeated != null && loot.evolutionStoneChance > 0f)
                AddLoot(loot.SelectEvolutionStone(defeated.Element, UnityEngine.Random.value));
            record.trainerExperience=10+2*enemyLevel;
            string finalPet=source!=null?source.GetComponent<PetCollectionMetadata>()?.PetId:null;
            var helpers=new HashSet<string>(StringComparer.Ordinal);
            foreach(var part in contributions)if(part.healthLost>0&&!string.IsNullOrWhiteSpace(part.petId))helpers.Add(part.petId);
            var awarded=new HashSet<string>(StringComparer.Ordinal);
            // Freeze eligibility at terminal defeat, not when a delayed journal is retried.
            if(player!=null&&player.petSlots!=null)foreach(var member in player.petSlots){
                if(member==null||member.IsWildEncounter)continue;
                var identity=member.GetComponent<PetCollectionMetadata>();var stats=member.GetComponent<PetHudRuntimeStats>();
                if(identity==null||stats==null||!awarded.Add(identity.PetId))continue;
                bool finisher=identity.PetId==finalPet;
                double share=PveBalanceRules.PartyExperienceShare(stats.Health>0,finisher,helpers.Contains(identity.PetId));
                if(share<=0)continue;
                record.pets.Add(new PvePetRewardSaveData{petId=identity.PetId,experience=PveBalanceRules.ExperienceAward(enemy.ExperienceReward,stats.Level,enemyLevel,share),effort=finisher?enemy.EffortYield:default});
            }
            pending.Add(record);awaitingDeath[id]=enemy;LastStagedId=id;
            // Roll once, persist the journal, then show loot. A failed journal stays in memory for retry.
            if(!CaptureEventBatch.IsActive&&SavePending()){journaled.Add(id);TryExperience(record);}
            return true;
        }
        bool SavePending(){
            bool saved=save!=null&&save.RetryPendingRewardSave();
            if(saved){foreach(var record in pending)journaled.Add(record.transactionId);writeFailures=0;retryAt=0;LastFailure="";return true;}
            LastFailure=save!=null?save.SaveBlockedReason:"Chưa kết nối hệ thống lưu.";
            ReportPending("Chưa lưu được thưởng. Hệ thống sẽ tự thử lại.");
            retryAt=Time.unscaledTime+Mathf.Min(30,Mathf.Pow(2,++writeFailures));return false;
        }
        sealed class PetSnapshot {public PetCollectionMetadata metadata;public PetHudRuntimeStats stats;public PetCustomizationSaveData customization;public PetRuntimeStatsSaveData runtime;}
        bool TryExperience(PendingPveRewardSaveData record){
            if(record.experienceCommitted)return true;if(busy||CaptureEventBatch.IsActive)return false;
            var snapshots=new List<PetSnapshot>();busy=true;
            var profileSnapshot=record.trainerExperience>0&&profile!=null?profile.CreateSaveData():null;
            using(var batch=new CaptureEventBatch()){
                bool committed=false;
                try{
                    if(levels==null)levels=gameObject.AddComponent<PetLevelUpService>();
                    var metadata=Object.FindObjectsByType<PetCollectionMetadata>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                    foreach(var award in record.pets){
                        var individual=metadata.FirstOrDefault(m=>m.PetId==award.petId&&m.GetComponent<PetController>()!=null&&!m.GetComponent<PetController>().IsWildEncounter);
                        if(individual==null)continue;var stats=individual.GetComponent<PetHudRuntimeStats>();if(stats==null)continue;
                        var action=individual.GetComponent<Capstone.Game.Skills.PetCombatActionDriver>();if(action!=null&&action.IsSkillBusy)return false;
                        snapshots.Add(new PetSnapshot{metadata=individual,stats=stats,customization=individual.CreateSaveData(),runtime=stats.CreateSaveData()});
                        individual.GrantEffortValues(award.effort);if(award.experience>0)levels.GrantBattleExperience(individual.GetComponent<PetController>(),award.experience,out _);
                    }
                    if(profileSnapshot!=null)profile.AddExperience(record.trainerExperience);
                    record.experienceCommitted=true;if(!SavePending())return false;
                    committed=true;batch.Commit();return true;
                }finally{
                    if(!committed){record.experienceCommitted=false;foreach(var old in snapshots){old.metadata.RestoreFromSaveData(old.customization);old.stats.RestoreFromSaveData(old.runtime);}
                        if(profileSnapshot!=null)profile.RestoreFromSaveData(profileSnapshot);}
                    busy=false;
                }
            }
        }
        public bool TryCollect(string transactionId){
            if(busy||CaptureEventBatch.IsActive||completed.Contains(transactionId))return false;
            if(awaitingDeath.TryGetValue(transactionId,out var dying)&&dying!=null&&dying.gameObject.activeInHierarchy&&!dying.DeathVisualComplete)return false;
            var record=pending.FirstOrDefault(r=>r.transactionId==transactionId);if(record==null||!journaled.Contains(transactionId)||!record.experienceCommitted)return false;
            Resolve();if(inventory==null||wallet==null||save==null||wallet.Gold>int.MaxValue-record.gold)return false;
            var originalSlots=inventory.CaptureSlots();var originalGold=wallet.CreateSaveData();int index=pending.IndexOf(record);bool committed=false;busy=true;
            using(var batch=new CaptureEventBatch()){
                try{
                    foreach(var reward in record.items){var definition=inventory.FindItemBase(reward.itemId);if(definition==null||!inventory.TryAddItemAtomic(definition,reward.quantity,false)){LastFailure="Túi chưa thể nhận phần thưởng. Vật phẩm vẫn đang chờ.";ReportPending("Túi chưa nhận được thưởng. Vật phẩm vẫn đang chờ.");retryAt=Time.unscaledTime+2;return false;}}
                    wallet.AddGold(record.gold);pending.RemoveAt(index);
                    if(!SavePending())return false;
                    committed=true;completed.Add(transactionId);completedOrder.Enqueue(transactionId);while(completedOrder.Count>1024)completed.Remove(completedOrder.Dequeue());
                    journaled.Remove(transactionId);awaitingDeath.Remove(transactionId);CompletedCount++;
                    if(pending.Count==0)announcedFailure=null;
                    inventory.NotifyAtomicAddition();batch.Commit();
                }finally{
                    if(!committed){inventory.RestoreCaptureSlots(originalSlots);wallet.RestoreFromSaveData(originalGold);if(!pending.Contains(record))pending.Insert(Mathf.Min(index,pending.Count),record);}
                    busy=false;
                }
            }
            // Only successful save receipts reach the screen. Never inferred from inventory differences.
            if(record.gold>0)ItemAcquisitionFeedback.Publish(new ItemAcquisitionReceipt(transactionId+":gold",catalog?.EffectiveCatalog.goldIcon,record.gold));
            for(int i=0;i<record.items.Count;i++){var item=record.items[i];ItemAcquisitionFeedback.Publish(new ItemAcquisitionReceipt(transactionId+":item:"+i,inventory.FindItemBase(item.itemId),item.quantity,AcquisitionSource.Pickup));}
            return true;
        }
        void Update(){
            if(UISystem.GameplayPause.IsPaused)return;
            if(Time.unscaledTime>=nextLookup){nextLookup=Time.unscaledTime+1;Resolve();}
            if(player==null||save==null||save.IsApplyingSave||PetEvolutionPresentationController.AnyPlaying)return;
            bool poll=Time.unscaledTime>=nextPoll&&Time.unscaledTime>=retryAt;
            if(!poll)return;
            nextPoll=Time.unscaledTime+.25f;
            for(int i=pending.Count-1;i>=0;i--){
                if(i>=pending.Count)continue;var record=pending[i];
                if(!journaled.Contains(record.transactionId)&&!SavePending())continue;
                if(!record.experienceCommitted&&!TryExperience(record))continue;
                if(awaitingDeath.TryGetValue(record.transactionId,out var actor) && actor!=null && actor.gameObject.activeInHierarchy && !actor.DeathVisualComplete)continue;
                awaitingDeath.Remove(record.transactionId);
                TryCollect(record.transactionId);
            }
        }
        void ReportPending(string message){if(announcedFailure==message)return;announcedFailure=message;AcquisitionToastView.ShowPending(message);}
    }
}

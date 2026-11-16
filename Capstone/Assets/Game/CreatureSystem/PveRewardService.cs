using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
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
        readonly Dictionary<string,float> flights=new Dictionary<string,float>();
        readonly Dictionary<string,InventoryItemRarity> rarityCache=new Dictionary<string,InventoryItemRarity>();
        PlayerSaveController save;MonsterInventoryAdapter inventory;PlayerCurrencyWallet wallet;PetCommandInput player;
        PetLevelUpService levels;PveLootView view;
        [SerializeField] PveLootCatalog catalog;
        bool busy;float nextLookup,nextPoll,retryAt;int writeFailures;
        public string LastFailure{get;private set;}
        public int PendingCount=>pending.Count;
        public int CompletedCount{get;private set;}
        public string LastStagedId{get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install(){foreach(var controller in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(controller.GetComponent<PveRewardService>()==null)controller.gameObject.AddComponent<PveRewardService>();}
        void Awake(){save=GetComponent<PlayerSaveController>();Resolve();}
        void OnEnable(){SceneManager.activeSceneChanged+=SceneChanged;}
        void OnDisable(){SceneManager.activeSceneChanged-=SceneChanged;if(view!=null)view.Clear();flights.Clear();}
        void SceneChanged(Scene old,Scene current){if(view!=null)view.Clear();flights.Clear();player=null;nextLookup=0;}
        void Resolve(){
            if(save==null)save=GetComponent<PlayerSaveController>();
            if(inventory==null)inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();
            if(wallet==null)wallet=Object.FindFirstObjectByType<PlayerCurrencyWallet>();
            if(player==null)player=Object.FindFirstObjectByType<PetCommandInput>();
            if(levels==null)levels=Object.FindFirstObjectByType<PetLevelUpService>();
            if(catalog==null)catalog=Resources.Load<PveLootCatalog>("Pve/LootCatalog");
        }
        public void ConfigureLoot(PveLootCatalog value) { catalog=value; }
        public PveRewardsSaveData CreateSaveData()=>new PveRewardsSaveData{captured=true,pending=new List<PendingPveRewardSaveData>(pending)};
        public void RestoreFromSaveData(PveRewardsSaveData data){
            if(view!=null)view.Clear();pending.Clear();journaled.Clear();completed.Clear();completedOrder.Clear();flights.Clear();rarityCache.Clear();
            if(data?.captured!=true||data.pending==null)return;
            foreach(var record in data.pending){pending.Add(record);journaled.Add(record.transactionId);}
        }
        public static bool HandleDefeat(DummyEnemy enemy,GameObject source){
            if(enemy==null||enemy.IsTrainingDummy||enemy.IsBoss||enemy.GetComponent<CreatureCombatant>()==null)return false;
            var memory=enemy.GetComponent<PveCombatState>();if(memory==null||memory.Contributions.Count==0)return false;
            var controller=Object.FindFirstObjectByType<PlayerSaveController>();if(controller==null)return false;
            var service=controller.GetComponent<PveRewardService>();if(service==null)service=controller.gameObject.AddComponent<PveRewardService>();
            return service.Stage(enemy,source,memory.Contributions);
        }
        bool Stage(DummyEnemy enemy,GameObject source,IReadOnlyList<PetDamageContribution> contributions){
            Resolve();if(catalog==null||inventory==null||wallet==null)return false;
            string id="pve:"+enemy.EncounterId;
            if(completed.Contains(id)||pending.Any(r=>r.transactionId==id))return true;
            float total=0;foreach(var part in contributions)total+=part.healthLost;if(total<=0)return false;
            var record=new PendingPveRewardSaveData{transactionId=id,scene=enemy.gameObject.scene.name,x=enemy.transform.position.x,y=enemy.transform.position.y,z=enemy.transform.position.z,createdUtcTicks=DateTime.UtcNow.Ticks};
            record.gold=UnityEngine.Random.Range(Mathf.Max(0,catalog.minimumGold),Mathf.Max(catalog.minimumGold,catalog.maximumGold)+1);
            if(UnityEngine.Random.value<catalog.itemChance){
                if(catalog.TrySelect(UnityEngine.Random.value,out var item,out int quantity))record.items.Add(new PveItemRewardSaveData{itemId=item.StableId,quantity=quantity});
            }
            int enemyLevel=enemy.GetComponent<PetHudRuntimeStats>().Level;
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
            pending.Add(record);LastStagedId=id;
            // Roll once, persist the journal, then show loot. A failed journal stays in memory for retry.
            if(!CaptureEventBatch.IsActive&&SavePending()){journaled.Add(id);TryExperience(record);}
            return true;
        }
        bool SavePending(){
            bool saved=save!=null&&save.RetryPendingRewardSave();
            if(saved){foreach(var record in pending)journaled.Add(record.transactionId);writeFailures=0;retryAt=0;LastFailure="";return true;}
            LastFailure=save!=null?save.SaveBlockedReason:"Chưa kết nối hệ thống lưu.";
            retryAt=Time.unscaledTime+Mathf.Min(30,Mathf.Pow(2,++writeFailures));return false;
        }
        sealed class PetSnapshot {public PetCollectionMetadata metadata;public PetHudRuntimeStats stats;public PetCustomizationSaveData customization;public PetRuntimeStatsSaveData runtime;}
        bool TryExperience(PendingPveRewardSaveData record){
            if(record.experienceCommitted)return true;if(busy||CaptureEventBatch.IsActive)return false;
            var snapshots=new List<PetSnapshot>();busy=true;
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
                    record.experienceCommitted=true;if(!SavePending())return false;
                    committed=true;batch.Commit();return true;
                }finally{
                    if(!committed){record.experienceCommitted=false;foreach(var old in snapshots){old.metadata.RestoreFromSaveData(old.customization);old.stats.RestoreFromSaveData(old.runtime);}}
                    busy=false;
                }
            }
        }
        public bool TryCollect(string transactionId){
            if(busy||CaptureEventBatch.IsActive||completed.Contains(transactionId))return false;
            var record=pending.FirstOrDefault(r=>r.transactionId==transactionId);if(record==null||!journaled.Contains(transactionId)||!record.experienceCommitted)return false;
            Resolve();if(inventory==null||wallet==null||save==null||wallet.Gold>int.MaxValue-record.gold)return false;
            var originalSlots=inventory.CaptureSlots();var originalGold=wallet.CreateSaveData();int index=pending.IndexOf(record);bool committed=false;busy=true;
            using(var batch=new CaptureEventBatch()){
                try{
                    foreach(var reward in record.items){var definition=inventory.FindItemBase(reward.itemId);if(definition==null||!inventory.TryAddItemAtomic(definition,reward.quantity,false)){LastFailure="Túi chưa thể nhận phần thưởng. Vật phẩm vẫn đang chờ.";retryAt=Time.unscaledTime+2;return false;}}
                    wallet.AddGold(record.gold);pending.RemoveAt(index);
                    if(!SavePending())return false;
                    committed=true;completed.Add(transactionId);completedOrder.Enqueue(transactionId);while(completedOrder.Count>1024)completed.Remove(completedOrder.Dequeue());
                    journaled.Remove(transactionId);flights.Remove(transactionId);rarityCache.Remove(transactionId);view?.Remove(transactionId);CompletedCount++;
                    inventory.NotifyAtomicAddition();batch.Commit();
                }finally{
                    if(!committed){inventory.RestoreCaptureSlots(originalSlots);wallet.RestoreFromSaveData(originalGold);if(!pending.Contains(record))pending.Insert(Mathf.Min(index,pending.Count),record);}
                    busy=false;
                }
            }
            // Only successful save receipts reach the screen. Never inferred from inventory differences.
            if(record.gold>0)ItemAcquisitionFeedback.Publish(new ItemAcquisitionReceipt(transactionId+":gold",catalog!=null?catalog.goldIcon:null,record.gold));
            for(int i=0;i<record.items.Count;i++){var item=record.items[i];ItemAcquisitionFeedback.Publish(new ItemAcquisitionReceipt(transactionId+":item:"+i,inventory.FindItemBase(item.itemId),item.quantity,AcquisitionSource.Pickup));}
            return true;
        }
        void Update(){
            if(Time.unscaledTime>=nextLookup){nextLookup=Time.unscaledTime+1;Resolve();}
            if(player==null||save==null||save.IsApplyingSave||PetEvolutionPresentationController.AnyPlaying)return;
            if(view==null&&pending.Count>0&&catalog!=null&&catalog.glowMaterial!=null){view=gameObject.AddComponent<PveLootView>();view.Build(catalog.glowMaterial);}
            Vector3 receiver=player.transform.position+Vector3.up;
            long nowTicks=DateTime.UtcNow.Ticks;
            bool poll=Time.unscaledTime>=nextPoll&&Time.unscaledTime>=retryAt;if(poll)nextPoll=Time.unscaledTime+.25f;
            // Snapshot IDs allows successful collections to remove records without invalidating iteration.
            for(int i=pending.Count-1;i>=0;i--){
                if(i>=pending.Count)continue;var record=pending[i];
                if(!journaled.Contains(record.transactionId)){if(poll&&SavePending())journaled.Add(record.transactionId);else continue;}
                if(!record.experienceCommitted){if(poll)TryExperience(record);if(!record.experienceCommitted)continue;}
                Vector3 position=new Vector3(record.x,record.y,record.z)+Vector3.up*.25f;
                bool sameScene=record.scene==SceneManager.GetActiveScene().name;
                bool close=(position-player.transform.position).sqrMagnitude<=2.5f*2.5f;
                double age=Math.Max(0,(nowTicks-record.createdUtcTicks)/(double)TimeSpan.TicksPerSecond);
                if(flights.TryGetValue(record.transactionId,out float start)){
                    float t=Mathf.Clamp01((Time.unscaledTime-start)/.35f);Vector3 control=(position+receiver)*.5f+Vector3.up*.8f;
                    view?.Place(record.transactionId,(1-t)*(1-t)*position+2*(1-t)*t*control+t*t*receiver,Rarity(record));
                    if(t>=1&&poll){if(!TryCollect(record.transactionId))flights.Remove(record.transactionId);}continue;
                }
                if(poll&&(close||age>=20||!sameScene)){
                    if(!sameScene||(position-player.transform.position).sqrMagnitude>25*25){var rarity=Rarity(record);if(TryCollect(record.transactionId))view?.Receive(receiver,rarity);continue;}
                    flights[record.transactionId]=Time.unscaledTime;
                }
                if(sameScene)view?.Place(record.transactionId,position,Rarity(record));
            }
        }
        InventoryItemRarity Rarity(PendingPveRewardSaveData record){if(rarityCache.TryGetValue(record.transactionId,out var cached))return cached;InventoryItemRarity rarity=InventoryItemRarity.Common;foreach(var reward in record.items)if(inventory.FindItemBase(reward.itemId) is MonsterItemDefinition item&&item.Rarity>rarity)rarity=item.Rarity;rarityCache[record.transactionId]=rarity;return rarity;}
    }
}

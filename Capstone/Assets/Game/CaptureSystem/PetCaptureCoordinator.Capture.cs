using System;
using Capstone.Game.CaptureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.QuestSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    public sealed partial class PetCaptureCoordinator {
        bool processing;
        internal ICaptureRandom RandomSource;
        CapturePresentation presentation;
        public bool IsCapturing => processing || presentation != null && presentation.IsPlaying;
        public bool CanCaptureFromBag(MonsterItemDefinition ball, MonsterInventoryAdapter inventory, out string reason) {
            var target=LockedTarget();bool valid=ValidateAttempt(ball,inventory,target,out _,out var failure);reason=CaptureMessages.For(failure);return valid;
        }
        WildPetCaptureTarget LockedTarget(){var camera=FindFirstObjectByType<BasicCameraFollow>();return camera!=null&&camera.TryGetLockedEnemy(out var enemy)?enemy.GetComponent<WildPetCaptureTarget>():null;}
        bool ValidateAttempt(MonsterItemDefinition ball,MonsterInventoryAdapter inventory,WildPetCaptureTarget target,out CaptureContext context,out CaptureFailure failure){
            context=default;ResolveReferences();failure=CaptureFailure.None;
            if(IsCapturing||CaptureEventBatch.IsActive){failure=CaptureFailure.Busy;return false;}
            if(roster==null||profileProvider==null||questManager==null||saveController==null||inventory==null){failure=CaptureFailure.MissingServices;return false;}
            if(saveController.IsSaveBlocked){failure=CaptureFailure.SaveFailed;return false;}
            if(target==null){failure=CaptureFailure.NoTarget;return false;}
            if(!target.IsWild){failure=roster.Contains(target.Pet)?CaptureFailure.AlreadyOwned:CaptureFailure.NotWild;return false;}
            if(!target.Allowed){failure=CaptureFailure.ProtectedTarget;return false;}
            if(target.Enemy.CaptureLocked){failure=CaptureFailure.Busy;return false;}
            if(!target.Enemy.IsAlive||target.Enemy.CurrentHealth<=0){failure=CaptureFailure.Defeated;return false;}
            if(roster.Contains(target.Pet)){failure=CaptureFailure.AlreadyOwned;return false;}
            if(ball==null||!ball.IsCaptureBall){CaptureDiagnostics.Missing(ball);failure=CaptureFailure.MissingConfiguration;return false;}
            if(inventory.GetQuantity(ball)<1){failure=CaptureFailure.NoBall;return false;}
            if(!roster.HasCaptureRoom){failure=CaptureFailure.NoRoom;return false;}
            if(target.Metadata==null||target.Stats==null||!PetStatCalculator.TryGetProfile(target.Metadata,out var profile)
                ||profile.BaseCatchRate<1||profile.BaseCatchRate>255){CaptureDiagnostics.Missing(target.Metadata);failure=CaptureFailure.MissingConfiguration;return false;}
            if(!profile.IsCatchable){failure=CaptureFailure.ProtectedTarget;return false;}
            // No live major-status or turn service exists. Do not infer either from unrelated buffs/time.
            if(CaptureAssets.Settings?.enableCriticalCapture==true){failure=CaptureFailure.DexUnavailable;return false;}
            context=new CaptureContext(target.Enemy.CurrentHealth,target.Enemy.maxHealth,profile.BaseCatchRate,ball.CaptureRules);
            bool valid=CaptureCalculator.TryOdds(context,out _,out failure);if(!valid&&failure==CaptureFailure.InvalidData)CaptureDiagnostics.Missing(ball);return valid;
        }
        public bool TryCaptureFromBag(MonsterItemDefinition ball,MonsterInventoryAdapter inventory,out CapturePresentationResult result,out string error)
        {
            result=default;
            if(!Capstone.Game.UISystem.PlayerActionGate.TryBegin(this,"capture-throw",out var lease)){error=CaptureMessages.For(CaptureFailure.Busy);return false;}
            using(lease)return TryCaptureTarget(ball,inventory,LockedTarget(),out result,out error);
        }
        internal bool TryCaptureTarget(MonsterItemDefinition ball,MonsterInventoryAdapter inventory,WildPetCaptureTarget target,out CapturePresentationResult result,out string error){
            result=default;
            if(!ValidateAttempt(ball,inventory,target,out var context,out var failure)){error=CaptureMessages.For(failure);return false;}
            processing=true;target.Lock(true);
            try{
                RandomSource??=new SeededCaptureRandom(Guid.NewGuid().GetHashCode());
                if(!CaptureCalculator.TryRoll(context,RandomSource,out var roll,out failure)){error=CaptureMessages.For(failure);return false;}
                result=new CapturePresentationResult(roll,target);
                if(!CommitAttempt(target.Pet,target.Metadata.PersistentId,target.Metadata.DefinitionId,roll.Success,ball,inventory,target,out error)){result=default;return false;}
                error=result.Message;return true;
            }finally{processing=false;if(target!=null)target.Lock(false);}
        }
        public void Present(CapturePresentationResult result,UIDocument document){
            if(this==null||!isActiveAndEnabled)return;
            if(presentation==null)presentation=gameObject.AddComponent<CapturePresentation>();
            presentation.Play(result,roster?.PartyInput?.transform,document);
        }
        bool CommitAttempt(PetController pet,string id,string speciesId,bool success,MonsterItemDefinition ball,MonsterInventoryAdapter inventory,WildPetCaptureTarget target,out string error){
            ResolveReferences();error=string.Empty;
            if(roster==null||profileProvider==null||questManager==null||saveController==null||pet==null){error=CaptureMessages.For(CaptureFailure.MissingServices);return false;}
            var metadata=pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if(metadata==null||string.IsNullOrWhiteSpace(speciesId)){error=CaptureMessages.For(CaptureFailure.MissingConfiguration);return false;}
            if(success&&(!roster.HasCaptureRoom||roster.Contains(pet))){error=CaptureMessages.For(roster.Contains(pet)?CaptureFailure.AlreadyOwned:CaptureFailure.NoRoom);return false;}
            var profileBefore=profileProvider.CreateSaveData();var questBefore=questManager.CreateSaveData();var achievementsBefore=achievementManager?.CreateSaveData();
            var metadataBefore=metadata.CreateSaveData();string oldId=metadata.PersistentId;
            var slotsBefore=inventory?.CaptureSlots();PetBoxRuntimeProvider.CapturePlacement placement=null;
            bool saved=false;
            using(var batch=new CaptureEventBatch()){
                try{
                    if(ball!=null&&!inventory.TryRemoveItemAtomic(ball,1)){error=CaptureMessages.For(CaptureFailure.NoBall);return false;}
                    if(success){
                        if(!roster.TryStageCapture(pet,out placement)){error=CaptureMessages.For(CaptureFailure.NoRoom);return false;}
                        if(target!=null)target.StagedForSave=true;
                        if(!string.IsNullOrWhiteSpace(id))metadata.AssignPersistentId(id);
                        if(ball!=null)metadata.SetCaptureBall(ball.StableId);
                        metadata.RecordCaptureInfo();
                        profileProvider.RecordCreatureSeen();profileProvider.RecordSpeciesCaptured(speciesId);
                        questManager.ReportProgress(QuestProgressEvent.CreatureCaptured(metadata.PersistentId,speciesId));
                    }
                    if(!saveController.SaveNow()){error=CaptureMessages.For(CaptureFailure.SaveFailed);return false;}
                    saved=true;
                    if(success){
                        if(target!=null)target.Complete(roster.PartyInput?.transform);else pet.Withdraw();
                        roster.PublishCapture();
                        CaptureEventBatch.Publish(this,"capture",()=>CaptureCommitted?.Invoke(pet));
                    }
                    if(inventory!=null)inventory.NotifyAtomicAddition();
                    batch.Commit();return true;
                }catch(Exception exception){
                    if(saved){Debug.LogException(exception);batch.Commit();return true;}
                    error=CaptureMessages.For(CaptureFailure.SaveFailed);return false;
                }finally{
                    if(!saved){
                        if(target!=null)target.StagedForSave=false;
                        roster.RollbackCapture(placement);metadata.RestoreFromSaveData(metadataBefore);metadata.AssignPersistentId(oldId);
                        if(inventory!=null&&slotsBefore!=null)inventory.RestoreCaptureSlots(slotsBefore);
                        profileProvider.RestoreFromSaveData(profileBefore);questManager.RestoreFromSaveData(questBefore);
                        if(achievementsBefore!=null)achievementManager.RestoreFromSaveData(achievementsBefore);
                    }
                }
            }
        }
        void OnDisable(){presentation?.Cancel();}
        void OnEnable(){if(Application.isPlaying&&GetComponent<CaptureTargetHud>()==null)gameObject.AddComponent<CaptureTargetHud>();}
    }
}

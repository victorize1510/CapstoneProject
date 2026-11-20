using UnityEngine;

namespace Capstone.Game.CaptureSystem {
    /// <summary>Selection/lock/nearest visible wild target. Dither never changes physics visibility.</summary>
    public static class WildCaptureTargeting {
        static readonly RaycastHit[] hits=new RaycastHit[32];
        public static WildPetCaptureTarget Find(PetCommandInput player,BasicCameraFollow camera){
            if(player==null)return null;
            // Chon ro rang se giu nguyen ca the, ke ca khi tam thoi ngoai tam/bi che.
            if(player.SelectedEnemy!=null)return Valid(player.SelectedEnemy,player);
            if(camera!=null&&camera.TryGetLockedEnemy(out var locked)){var target=Valid(locked,player);if(target!=null)return target;}
            float nearest=float.PositiveInfinity;WildPetCaptureTarget best=null;
            foreach(var enemy in DummyEnemy.ActiveEnemies){var target=Valid(enemy,player);if(target==null)continue;float d=(enemy.transform.position-player.transform.position).sqrMagnitude;if(d<nearest){nearest=d;best=target;}}
            return best;
        }
        static WildPetCaptureTarget Valid(DummyEnemy enemy,PetCommandInput player){
            if(enemy==null||!enemy.isActiveAndEnabled||!enemy.IsSelectable)return null;
            var target=enemy.GetComponent<WildPetCaptureTarget>();if(target==null||!target.IsWild||!target.Allowed)return null;
            var view=player.commandCamera!=null?player.commandCamera:Camera.main;
            if(!CreatureSystem.CameraEnemyVisibility.CanSee(view,player.transform,enemy))return null;
            float range=CaptureAssets.Settings!=null?Mathf.Max(1,CaptureAssets.Settings.targetRange):15;
            Vector3 origin=player.transform.position+Vector3.up,delta=enemy.TargetPosition-origin;
            if(delta.sqrMagnitude>range*range)return null;
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,hits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return null;
            for(int i=0;i<count;i++)if(!hits[i].transform.IsChildOf(player.transform)&&!hits[i].transform.IsChildOf(enemy.transform))return null;
            return target;
        }
    }
}

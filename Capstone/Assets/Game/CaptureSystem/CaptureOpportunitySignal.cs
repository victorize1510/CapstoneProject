using UnityEngine;

namespace Capstone.Game.CaptureSystem {
    /// <summary>Explicit combat/presentation signal. No HP/odds rule and no save state.</summary>
    [DisallowMultipleComponent,RequireComponent(typeof(WildPetCaptureTarget))]
    public sealed class CaptureOpportunitySignal : MonoBehaviour {
        bool open;
        public bool IsOpen=>open&&isActiveAndEnabled&&TryGetComponent<WildPetCaptureTarget>(out var target)&&target.IsWild&&target.Enemy!=null&&target.Enemy.IsAlive;
        // Connect only to an actual authored combat event/window. Defaults off.
        public void SetOpportunity(bool value)=>open=value;
        public void BeginOpportunity()=>open=true;
        public void EndOpportunity()=>open=false;
        void OnDisable()=>open=false;
    }
    [DisallowMultipleComponent]
    public sealed class LockedCaptureOpportunitySource : MonoBehaviour,ICaptureOpportunitySource {
        BasicCameraFollow cameraSource;
        public bool HasCaptureOpportunity {
            get {
                if(cameraSource==null)cameraSource=FindFirstObjectByType<BasicCameraFollow>();
                return cameraSource!=null&&cameraSource.TryGetLockedEnemy(out var enemy)&&enemy!=null
                    &&enemy.TryGetComponent<CaptureOpportunitySignal>(out var signal)&&signal.IsOpen;
            }
        }
    }
}

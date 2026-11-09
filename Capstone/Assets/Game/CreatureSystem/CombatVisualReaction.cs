using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Small cosmetic recoil and local visual pause. Never moves the actor/collider.</summary>
    [DefaultExecutionOrder(80), DisallowMultipleComponent]
    public sealed class CombatVisualReaction : MonoBehaviour {
        PetHudRuntimeStats stats;
        PetController pet;
        bool resolved;
        Animator animator, pausedAnimator;
        Transform visual;
        Quaternion baseRotation;
        float recoilAt = -1, pauseUntil, previousSpeed, nextPause;
        bool rotated;
        void Awake() { stats = GetComponent<PetHudRuntimeStats>(); pet = GetComponent<PetController>(); Bind(); }
        void Bind() {
            if (stats == null && resolved) return;
            var next = stats != null ? stats.VisualAnimator : GetComponentInChildren<Animator>(); resolved = true;
            if (animator == next) return;
            Restore(); animator = next; visual = next != null && next.transform != transform ? next.transform : null;
        }
        public void Recoil() { Bind(); recoilAt = Time.unscaledTime; }
        public void Pause(float seconds) {
            Bind(); if (seconds <= 0 || animator == null || !animator.enabled || Time.unscaledTime < nextPause) return;
            RestorePause(); nextPause = Time.unscaledTime + .2f;
            previousSpeed = animator.speed; pausedAnimator = animator; pausedAnimator.speed = 0;
            pauseUntil = Time.unscaledTime + Mathf.Clamp(seconds, 0, .05f);
        }
        void Update() {
            Bind(); RestoreRotation();
            if (pausedAnimator != null && Time.unscaledTime >= pauseUntil) RestorePause();
            if (InventoryInputController.GameplayInputBlocked || PetEvolutionPresentationController.AnyPlaying || (pet != null && !pet.CanReceiveCommands)) Restore();
        }
        void LateUpdate() {
            if (visual == null || recoilAt < 0) return;
            float t = (Time.unscaledTime - recoilAt) / .12f;
            if (t >= 1) { recoilAt = -1; return; }
            baseRotation = visual.localRotation;
            visual.localRotation = baseRotation * Quaternion.Euler(-3 * Mathf.Sin(t * Mathf.PI), 0, 0); rotated = true;
        }
        void RestorePause() { if (pausedAnimator != null && Mathf.Approximately(pausedAnimator.speed, 0)) pausedAnimator.speed = previousSpeed; pausedAnimator = null; }
        void RestoreRotation() { if (rotated && visual != null) visual.localRotation = baseRotation; rotated = false; }
        void Restore() { RestorePause(); RestoreRotation(); recoilAt = -1; }
        void OnDisable() { Restore(); }
        void OnApplicationFocus(bool value) { if (!value) Restore(); }
    }
}

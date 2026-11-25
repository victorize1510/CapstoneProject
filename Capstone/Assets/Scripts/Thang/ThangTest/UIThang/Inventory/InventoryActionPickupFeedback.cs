using System.Reflection;
using UnityEngine;

namespace Capstone.Game.Inventory {
    [DisallowMultipleComponent]
    public sealed class InventoryActionPickupFeedback : MonoBehaviour {
        [SerializeField] MonsterInventoryController inventory = null;
        [SerializeField] Animator animator = null;
        [SerializeField] string pickupStateName = "PickingUp";
        [SerializeField] float fadeTime = 0.06f;
        [SerializeField] bool autoFindPlayerAnimator = true;

        public void Bind(MonsterInventoryController controller) {
            if (inventory == controller) return;

            if (inventory != null) inventory.ActionCompleted -= HandleActionCompleted;
            inventory = controller;
            if (isActiveAndEnabled && inventory != null) inventory.ActionCompleted += HandleActionCompleted;
        }

        void OnEnable() {
            ResolveReferences();
            if (inventory != null) inventory.ActionCompleted += HandleActionCompleted;
        }

        void OnDisable() {
            if (inventory != null) inventory.ActionCompleted -= HandleActionCompleted;
        }

        void ResolveReferences() {
            inventory = inventory != null ? inventory : GetComponent<MonsterInventoryController>();
            if (inventory == null) inventory = GetComponentInParent<MonsterInventoryController>();
            if (animator == null && autoFindPlayerAnimator) animator = FindPlayerAnimator();
        }

        void HandleActionCompleted(InventoryActionRequest request) {
            if (request == null || !request.Success) return;

            ResolveReferences();
            if (animator == null || string.IsNullOrWhiteSpace(pickupStateName)) return;

            // Temporary feedback: all successful inventory actions reuse the current pickup clip.
            animator.CrossFadeInFixedTime(pickupStateName, Mathf.Max(0f, fadeTime), 0, 0f);
        }

        static Animator FindPlayerAnimator() {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (behaviour == null || behaviour.GetType().Name != "BasicPlayerMovement") continue;

                var animatorFromField = ReadAnimatorField(behaviour);
                if (animatorFromField != null) return animatorFromField;

                var animatorInChildren = behaviour.GetComponentInChildren<Animator>(true);
                if (animatorInChildren != null) return animatorInChildren;
            }

            return null;
        }

        static Animator ReadAnimatorField(MonoBehaviour behaviour) {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = behaviour.GetType().GetField("animator", flags);
            return field != null ? field.GetValue(behaviour) as Animator : null;
        }
    }
}

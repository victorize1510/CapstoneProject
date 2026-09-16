using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.UISystem {
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class NpcPanelInteractable : MonoBehaviour {
        public enum PanelTarget {
            Box,
            Shop
        }

        [SerializeField] PanelTarget targetPanel = PanelTarget.Box;
        [SerializeField] KeyCode interactKey = KeyCode.E;
        [SerializeField, Min(0.1f)] float interactDistance = 2.75f;
        [SerializeField] Transform player = null;
        [SerializeField] GameMenuController menuController = null;

        [Header("Prompt")]
        [SerializeField] Vector3 promptOffset = new Vector3(0f, 1.65f, 0f);
        [SerializeField] Color promptColor = Color.white;

        TextMesh promptLabel;
        Camera mainCamera;

        void Awake() {
            ResolveReferences();
            EnsurePrompt();
            EnsureNavigationObstacle();
            SetPromptVisible(false);
        }

        void OnDisable() {
            SetPromptVisible(false);
        }

        void Update() {
            ResolveReferences();

            bool canInteract = !InventoryInputController.GameplayInputBlocked && IsPlayerNear();
            SetPromptVisible(canInteract);
            FacePromptToCamera();

            if (canInteract && Input.GetKeyDown(interactKey)) {
                OpenTargetPanel();
            }
        }

        void OpenTargetPanel() {
            if (menuController == null) return;

            SetPromptVisible(false);
            if (targetPanel == PanelTarget.Box) menuController.OpenBoxFromWorld();
            else menuController.OpenShopFromWorld();
        }

        void ResolveReferences() {
            if (menuController == null) menuController = FindFirstObjectByType<GameMenuController>();
            if (player != null) return;

            var movement = FindFirstObjectByType<BasicPlayerMovement>();
            if (movement != null) {
                player = movement.transform;
                return;
            }

            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        bool IsPlayerNear() {
            if (player == null) return false;

            Vector3 delta = player.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= interactDistance * interactDistance;
        }

        void EnsurePrompt() {
            if (promptLabel != null) return;

            var promptObject = new GameObject("Interaction Prompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = promptOffset;

            promptLabel = promptObject.AddComponent<TextMesh>();
            promptLabel.text = targetPanel == PanelTarget.Box ? "[E] Mở Box" : "[E] Mở Shop";
            promptLabel.anchor = TextAnchor.MiddleCenter;
            promptLabel.alignment = TextAlignment.Center;
            promptLabel.characterSize = 0.075f;
            promptLabel.fontSize = 64;
            promptLabel.color = promptColor;

            MeshRenderer renderer = promptObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 25;
        }

        void EnsureNavigationObstacle() {
            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = gameObject.AddComponent<NavMeshObstacle>();

            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            if (TryGetComponent(out BoxCollider box)) {
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = box.center;
                obstacle.size = box.size;
            }
            else if (TryGetComponent(out CapsuleCollider capsule)) {
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.center = capsule.center;
                obstacle.radius = Mathf.Max(0.1f, capsule.radius);
                obstacle.height = Mathf.Max(capsule.height, obstacle.radius * 2f);
            }
        }

        void SetPromptVisible(bool visible) {
            if (promptLabel != null && promptLabel.gameObject.activeSelf != visible) {
                promptLabel.gameObject.SetActive(visible);
            }
        }

        void FacePromptToCamera() {
            if (promptLabel == null || !promptLabel.gameObject.activeSelf) return;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            Transform cameraTransform = mainCamera.transform;
            promptLabel.transform.rotation = Quaternion.LookRotation(
                promptLabel.transform.position - cameraTransform.position,
                cameraTransform.up);
        }
    }
}

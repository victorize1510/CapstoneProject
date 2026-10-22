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
        [SerializeField, Range(1f, 180f)] float viewAngle = WorldInteractionView.DefaultViewAngle;
        [SerializeField] LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
        [SerializeField] Transform player = null;
        [SerializeField] GameMenuController menuController = null;

        [Header("Prompt")]
        [SerializeField] Vector3 promptOffset = new Vector3(0f, 1.65f, 0f);
        [SerializeField] Color promptColor = Color.white;

        TextMesh promptLabel;
        Camera mainCamera;
        Capstone.Game.ShopSystem.ShopSource shopSource;
        public KeyCode InteractKey => interactKey;

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

            bool canInteract = CanInteract();
            SetPromptVisible(canInteract);
            FacePromptToCamera();

            if (canInteract && Input.GetKeyDown(interactKey)) {
                OpenTargetPanel();
            }
        }

        void OpenTargetPanel() {
            if (menuController == null || !CanInteract() || !WorldInteractionView.TryConsumePress()) return;

            SetPromptVisible(false);
            if (targetPanel == PanelTarget.Box) menuController.OpenBoxFromWorld();
            else {
                if (shopSource == null) {
                    shopSource = GetComponent<Capstone.Game.ShopSystem.ShopSource>();
                    if (shopSource == null) shopSource = gameObject.AddComponent<Capstone.Game.ShopSystem.ShopSource>();
                }
                menuController.OpenShopFromWorld(shopSource);
            }
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

        bool CanInteract() => !InventoryInputController.GameplayInputBlocked && menuController != null
            && WorldInteractionView.CanInteract(player, transform, interactDistance, viewAngle, obstructionLayers);

        void EnsurePrompt() {
            if (promptLabel != null) return;

            var promptObject = new GameObject("Interaction Prompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = promptOffset;

            promptLabel = promptObject.AddComponent<TextMesh>();
            promptLabel.text = targetPanel == PanelTarget.Box ? "[E] Mở kho thú" : "[E] Mở cửa hàng";
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
            visible &= Capstone.Game.SettingsSystem.GameSettingsService.InteractionPromptsVisible;
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

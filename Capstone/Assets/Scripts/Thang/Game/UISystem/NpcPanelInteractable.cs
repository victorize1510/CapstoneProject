
using PhysicalKeys = Capstone.Game.UISystem.GameplayKeyboard;
using Capstone.Game.Inventory;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Capstone.Game.UISystem
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class NpcPanelInteractable : MonoBehaviour
    {
        public enum PanelTarget
        {
            Box,
            Shop
        }

        [Header("Tuong tac")]
        [SerializeField] PanelTarget targetPanel = PanelTarget.Box;
        [SerializeField] KeyCode interactKey = KeyCode.E;
        [SerializeField, Min(0.1f)] float interactDistance = 2.75f;

        [Header("Tham chieu")]
        [SerializeField] Transform player = null;
        [SerializeField] GameMenuController menuController = null;
        [Header("Hội thoại")]
        [SerializeField] DialogueBoxUI dialogue;
        [SerializeField] string speaker;
        [SerializeField] Sprite portrait;
        [SerializeField] string greeting;
        [SerializeField] string openChoice;
        [SerializeField] string healChoice = "Hồi phục cho pet";
        bool talking;
        GameObject flash;

        [Header("Hien thi nut tuong tac")]
        [SerializeField] Vector3 promptOffset = new Vector3(0f, 1.65f, 0f);
        [SerializeField] Color promptColor = Color.white;

        TextMesh promptLabel;
        Camera mainCamera;

        Capstone.Game.ShopSystem.ShopSource shopSource;

        public KeyCode InteractKey => interactKey;

        // Khoi tao
        void Awake()
        {
            ResolveReferences();
            EnsurePrompt();
            EnsureNavigationObstacle();
            SetPromptVisible(false);
        }

        void OnEnable()
        {
            Capstone.Game.WorldFeedback.CameraActorOccluder.Ensure(gameObject);
        }

        void OnDisable()
        {
            SetPromptVisible(false);
            StopAllCoroutines();
            if (talking && dialogue != null) dialogue.HideImmediate();
            if (flash != null) Destroy(flash);
            talking = false;
            InventoryInputController.SetExternalGameplayInputBlocked(this, false);
        }

        // Cap nhat tuong tac
        void Update()
        {
            ResolveReferences();

            bool canInteract = CanInteract();

            SetPromptVisible(canInteract);
            FacePromptToCamera();

            if (canInteract && PhysicalKeys.GetKeyDown(interactKey))
            {
                OpenTargetPanel();
            }
        }

        // Kiem tra khoang cach giua Player va NPC
        bool CanInteract()
        {
            if (player == null || menuController == null)
                return false;

            if (talking || InventoryInputController.GameplayInputBlocked)
                return false;

            Vector3 offset = player.position - transform.position;

            return offset.sqrMagnitude
                <= interactDistance * interactDistance;
        }

        // Mo panel tuong ung
        void OpenTargetPanel()
        {
            if (menuController == null)
                return;

            if (!CanInteract())
                return;

            if (!WorldInteractionView.TryConsumePress())
                return;

            SetPromptVisible(false);
            if (dialogue != null && (dialogue.IsBuilt || dialogue.GetComponentInParent<Canvas>() != null)) {
                var canvas = dialogue.GetComponentInParent<Canvas>();
                dialogue.Build(canvas != null ? canvas.transform : dialogue.transform);
                StartCoroutine(Talk());
                return;
            }
            OpenPanel();
        }

        void OpenPanel()
        {
            switch (targetPanel)
            {
                case PanelTarget.Box:

                    menuController.OpenBoxFromWorld();
                    break;

                case PanelTarget.Shop:

                    if (shopSource == null)
                    {
                        shopSource =
                            GetComponent<Capstone.Game.ShopSystem.ShopSource>();

                        if (shopSource == null)
                        {
                            shopSource =
                                gameObject.AddComponent<
                                    Capstone.Game.ShopSystem.ShopSource>();
                        }
                    }

                    menuController.OpenShopFromWorld(shopSource);
                    break;
            }
        }

        IEnumerator Talk() {
            talking = true;
            InventoryInputController.SetExternalGameplayInputBlocked(this, true);
            bool shop = targetPanel == PanelTarget.Shop;
            int choice = -1;
            try {
                yield return dialogue.Show(string.IsNullOrWhiteSpace(speaker) ? (shop ? "Chủ cửa hàng" : "Quản lý kho thú") : speaker, portrait);
                yield return dialogue.TypeLine(string.IsNullOrWhiteSpace(greeting) ? (shop
                    ? "Chào bạn! Bạn muốn xem hàng hay hồi phục cho đội thú?"
                    : "Bạn muốn xem những thú đang gửi trong kho không?") : greeting, 52f, false);
                string open = string.IsNullOrWhiteSpace(openChoice) ? (shop ? "Xem shop" : "Mở kho thú") : openChoice;
                yield return dialogue.Choose(shop ? new[] { open, healChoice } : new[] { open, "Rời đi" }, value => choice = value);
                yield return dialogue.Hide();
                if (shop && choice == 1) {
                    yield return FlashRecovery();
                    bool restored = RestoreParty();
                    yield return dialogue.Show(string.IsNullOrWhiteSpace(speaker) ? "Chủ cửa hàng" : speaker, portrait);
                    yield return dialogue.TypeLine(restored ? "Cả đội thú đã hồi phục đầy đủ HP."
                        : "Chưa thể hồi phục đội thú. Hãy kiểm tra đội hình và bản lưu.", 52f);
                    yield return dialogue.Hide();
                }
            } finally {
                talking = false;
                dialogue.HideImmediate();
                InventoryInputController.SetExternalGameplayInputBlocked(this, false);
            }
            if (choice == 0 && menuController != null) OpenPanel();
        }

        IEnumerator FlashRecovery() {
            flash = new GameObject("Party Recovery Flash", typeof(Canvas));
            var canvas = flash.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var cover = new GameObject("Flash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            cover.transform.SetParent(flash.transform, false);
            cover.rectTransform.anchorMin = Vector2.zero;
            cover.rectTransform.anchorMax = Vector2.one;
            cover.rectTransform.offsetMin = cover.rectTransform.offsetMax = Vector2.zero;
            cover.raycastTarget = false;
            var color = new Color(.76f, .94f, 1f, 0f);
            for (int pulse = 0; pulse < 3; pulse++) {
                for (float time = 0f; time < .48f; time += Time.unscaledDeltaTime) {
                    color.a = .34f * Mathf.Sin(Mathf.PI * time / .48f);
                    cover.color = color;
                    yield return null;
                }
                cover.color = Color.clear;
                yield return new WaitForSecondsRealtime(.24f);
            }
            Destroy(flash);
            flash = null;
        }

        bool RestoreParty() {
            var command = player != null ? player.GetComponent<PetCommandInput>() : null;
            if (command == null) command = FindFirstObjectByType<PetCommandInput>();
            var save = FindFirstObjectByType<PlayerSaveController>();
            if (command == null || save == null || command.petSlots == null) return false;
            var party = command.petSlots.Where(pet => pet != null).Distinct()
                .Select(pet => (pet, stats: pet.GetComponent<PetHudRuntimeStats>()))
                .Where(slot => slot.stats != null).ToArray();
            if (party.Length == 0) return false;
            var before = party.Select(slot => slot.stats.CreateSaveData()).ToArray();
            foreach (var slot in party)
                slot.stats.SetStatus(slot.stats.MaxHealth, slot.stats.MaxHealth, slot.stats.Energy, slot.stats.MaxEnergy);
            if (!save.SaveNow()) {
                for (int i = 0; i < party.Length; i++) party[i].stats.RestoreFromSaveData(before[i]);
                return false;
            }
            foreach (var slot in party)
                if (slot.pet.IsDefeatPresentation) slot.pet.FinishDefeatRecall();
            return true;
        }

        // Tu dong tim Player va Menu Controller
        void ResolveReferences()
        {
            if (menuController == null)
            {
                menuController =
                    FindFirstObjectByType<GameMenuController>();
            }

            if (player != null)
                return;

            var movement =
                FindFirstObjectByType<BasicPlayerMovement>();

            if (movement != null)
            {
                player = movement.transform;
                return;
            }

            GameObject playerObject = GameObject.Find("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        // Tao chu hien thi tren dau NPC
        void EnsurePrompt()
        {
            if (promptLabel != null)
                return;

            GameObject promptObject =
                new GameObject("Interaction Prompt");

            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = promptOffset;

            promptLabel = promptObject.AddComponent<TextMesh>();

            promptLabel.text = targetPanel == PanelTarget.Box
                ? $"[{interactKey}] Mở kho thú"
                : $"[{interactKey}] Mở cửa hàng";

            promptLabel.anchor = TextAnchor.MiddleCenter;
            promptLabel.alignment = TextAlignment.Center;
            promptLabel.characterSize = 0.075f;
            promptLabel.fontSize = 64;
            promptLabel.color = promptColor;

            MeshRenderer meshRenderer =
                promptObject.GetComponent<MeshRenderer>();

            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = 25;
            }
        }

        // Hien hoac an nut tuong tac
        void SetPromptVisible(bool visible)
        {
            visible &=
                Capstone.Game.SettingsSystem.GameSettingsService
                    .InteractionPromptsVisible;

            if (promptLabel == null)
                return;

            if (promptLabel.gameObject.activeSelf != visible)
            {
                promptLabel.gameObject.SetActive(visible);
            }
        }

        // Xoay chu ve phia Camera
        void FacePromptToCamera()
        {
            if (promptLabel == null)
                return;

            if (!promptLabel.gameObject.activeSelf)
                return;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
                return;

            Transform cameraTransform = mainCamera.transform;

            Vector3 direction =
                promptLabel.transform.position
                - cameraTransform.position;

            if (direction.sqrMagnitude < 0.0001f)
                return;

            promptLabel.transform.rotation =
                Quaternion.LookRotation(
                    direction,
                    cameraTransform.up
                );
        }

        // Tao vat can cho he thong NavMesh
        void EnsureNavigationObstacle()
        {
            NavMeshObstacle obstacle =
                GetComponent<NavMeshObstacle>();

            if (obstacle == null)
            {
                obstacle =
                    gameObject.AddComponent<NavMeshObstacle>();
            }

            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            if (TryGetComponent(out BoxCollider box))
            {
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = box.center;
                obstacle.size = box.size;
            }
            else if (TryGetComponent(out CapsuleCollider capsule))
            {
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.center = capsule.center;

                obstacle.radius =
                    Mathf.Max(0.1f, capsule.radius);

                obstacle.height =
                    Mathf.Max(
                        capsule.height,
                        obstacle.radius * 2f
                    );
            }
        }
    }
}

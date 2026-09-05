using UnityEngine;

namespace Capstone.Game.QuestSystem {
    [DisallowMultipleComponent]
    public sealed class QuestGiverInteractable : MonoBehaviour {
        [SerializeField] QuestDefinition questDefinition;
        [SerializeField] QuestManager questManager;
        [SerializeField] Transform player;
        [SerializeField] KeyCode interactKey = KeyCode.E;
        [SerializeField] float interactDistance = 2.5f;
        [SerializeField] string displayName = "TestQuest";
        [SerializeField] string promptText = "[E] Nhan quest";
        [SerializeField] bool trackAfterAccept = true;
        [SerializeField] bool hidePromptAfterAccepted = true;

        [Header("Runtime Labels")]
        [SerializeField] Vector3 titleOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] Vector3 promptOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField] Color titleColor = new Color(1f, 0.92f, 0.35f, 1f);
        [SerializeField] Color promptColor = Color.white;

        TextMesh titleLabel;
        TextMesh promptLabel;
        Camera mainCamera;
        QuestManager registeredManager;
        QuestDefinition registeredDefinition;
        bool accepted;

        void Awake() {
            EnsureLabels();
            ResolveReferences();
            accepted = IsQuestAlreadyKnown();
            UpdatePrompt(false);
        }

        void Update() {
            ResolveReferences();

            var near = IsPlayerNear();
            UpdatePrompt(near && (!accepted || !hidePromptAfterAccepted));
            FaceLabelsToCamera();

            if (near
                && !accepted
                && !Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked
                && Input.GetKeyDown(interactKey)) {
                AcceptQuest();
            }
        }

        void AcceptQuest() {
            if (questManager == null || questDefinition == null) {
                Debug.LogWarning($"{nameof(QuestGiverInteractable)} on {name} is missing QuestManager or QuestDefinition.");
                return;
            }

            accepted = questManager.AcceptQuest(questDefinition) || IsQuestAlreadyKnown();
            if (accepted && trackAfterAccept) {
                questManager.TrackQuest(questDefinition.QuestId);
            }

            UpdatePrompt(false);
        }

        void ResolveReferences() {
            if (questManager == null) {
                questManager = FindFirstObjectByType<QuestManager>();
            }

            RegisterDefinitionIfNeeded();

            if (player != null) return;

            var playerObject = GameObject.Find("Player");
            if (playerObject != null) {
                player = playerObject.transform;
                return;
            }

            try {
                playerObject = GameObject.FindGameObjectWithTag("Player");
            } catch (UnityException) {
                playerObject = null;
            }

            if (playerObject != null) {
                player = playerObject.transform;
                return;
            }

            var controller = FindFirstObjectByType<CharacterController>();
            if (controller != null) {
                player = controller.transform;
            }
        }

        void RegisterDefinitionIfNeeded() {
            if (questManager == null || questDefinition == null) return;
            if (registeredManager == questManager && registeredDefinition == questDefinition) return;

            questManager.RegisterQuestDefinition(questDefinition);
            registeredManager = questManager;
            registeredDefinition = questDefinition;
        }

        bool IsPlayerNear() {
            if (player == null) return false;

            var distance = Vector3.Distance(player.position, transform.position);
            return distance <= Mathf.Max(0.1f, interactDistance);
        }

        bool IsQuestAlreadyKnown() {
            if (questManager == null || questDefinition == null) return false;

            var quests = questManager.GetAllQuests();
            foreach (var quest in quests) {
                if (quest != null && quest.QuestId == questDefinition.QuestId) {
                    return true;
                }
            }

            return false;
        }

        void EnsureLabels() {
            if (titleLabel == null) {
                titleLabel = CreateLabel("Quest Name Label", displayName, titleOffset, titleColor, 0.08f, 72);
            }

            if (promptLabel == null) {
                promptLabel = CreateLabel("Quest Prompt Label", promptText, promptOffset, promptColor, 0.075f, 64);
            }
        }

        TextMesh CreateLabel(string objectName, string text, Vector3 offset, Color color, float characterSize, int fontSize) {
            var labelObject = new GameObject(objectName);
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = offset;

            var textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = characterSize;
            textMesh.fontSize = fontSize;
            textMesh.color = color;

            var renderer = labelObject.GetComponent<MeshRenderer>();
            renderer.sortingOrder = 20;
            return textMesh;
        }

        void UpdatePrompt(bool visible) {
            if (promptLabel != null) {
                promptLabel.gameObject.SetActive(visible);
            }
        }

        void FaceLabelsToCamera() {
            if (mainCamera == null) {
                mainCamera = Camera.main;
            }

            if (mainCamera == null) return;

            FaceLabel(titleLabel);
            FaceLabel(promptLabel);
        }

        void FaceLabel(TextMesh label) {
            if (label == null || !label.gameObject.activeSelf) return;

            var cameraTransform = mainCamera.transform;
            label.transform.rotation = Quaternion.LookRotation(
                label.transform.position - cameraTransform.position,
                cameraTransform.up);
        }
    }
}

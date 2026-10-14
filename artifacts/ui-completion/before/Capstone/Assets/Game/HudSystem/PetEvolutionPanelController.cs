using System;
using System.Collections;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetEvolutionPanelController : MonoBehaviour {
        bool confirmationRunning;
        [SerializeField] UIDocument document = null;
        [SerializeField] MonoBehaviour petHudProvider = null;
        [SerializeField] PetEvolutionService evolutionService = null;
        [SerializeField] bool autoFindReferences = true;

        MonsterInventoryAdapter inventory;
        VisualTreeAsset standaloneTree;
        UIDocument screenDocument;
        readonly System.Collections.Generic.HashSet<PetController> pendingPrompts = new();
        VisualElement overlay;
        VisualElement content;
        VisualElement currentPortrait;
        Label currentFallback;
        VisualElement targetPortrait;
        Label targetFallback;
        Label currentName;
        Label targetName;
        Label formPath;
        Label requirement;
        Label itemCount;
        VisualElement itemRow;
        Label dialogue;
        Label feedback;
        Button laterButton;
        Button confirmButton;

        PetController targetPet;
        PetEvolutionPreview preview;
        bool controlsRegistered;
        bool resultShown;
        bool openedAutomatically;

        PetCommandHudProvider CommandProvider => petHudProvider as PetCommandHudProvider;
        public bool IsOpen => screenDocument != null
            && screenDocument.rootVisualElement.style.display.value != DisplayStyle.None;

        void OnEnable() {
            ResolveReferences();
            CacheElements();
            RegisterControls();
            Subscribe();
            Close();
        }

        void OnDisable() {
            Unsubscribe();
            UnregisterControls();
            Close();
        }

        void OnDestroy() {
            if (screenDocument != null) Destroy(screenDocument.gameObject);
        }

        public void Bind(UIDocument targetDocument, MonsterInventoryAdapter targetInventory,
            VisualTreeAsset targetStandaloneTree) {
            Unsubscribe();
            UnregisterControls();
            document = targetDocument != null ? targetDocument : document;
            inventory = targetInventory != null ? targetInventory : inventory;
            standaloneTree = targetStandaloneTree != null ? targetStandaloneTree : standaloneTree;
            ResolveReferences();
            EnsureStandaloneScreen();
            CacheElements();
            RegisterControls();
            Subscribe();
            Close();
        }

        public void Open(PetController pet = null, bool automaticPrompt = false) {
            EnsureStandaloneScreen();
            if (overlay == null) CacheElements();
            if (overlay == null) return;

            ResolveReferences();
            targetPet = pet != null ? pet : CommandProvider?.GetSelectedPetController();
            preview = evolutionService?.CreatePreview(targetPet);
            resultShown = false;
            openedAutomatically = automaticPrompt;
            screenDocument.rootVisualElement.style.display = DisplayStyle.Flex;
            Refresh(automaticPrompt);
            content?.Focus();
        }

        public bool TryOpenFromInventory(InventoryItemSnapshot item) {
            if (!IsEvolutionItem(item)) return false;
            ResolveReferences();
            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            controller?.OpenPetPartyPanel();
            PetPartyPanelController party = GetComponent<PetPartyPanelController>();
            if (party == null) party = FindFirstObjectByType<PetPartyPanelController>();
            party?.BeginEvolutionItemSelection(item.ItemId, this);
            return true;
        }

        public bool IsEvolutionItem(InventoryItemSnapshot item) {
            if (item == null) return false;
            ResolveReferences();
            for (int i = 0; i < 6; i++) {
                PetController pet = CommandProvider?.GetPetControllerAt(i);
                PetCollectionMetadata metadata = pet != null ? pet.GetComponentInChildren<PetCollectionMetadata>(true) : null;
                if (metadata == null || !metadata.TryGetNextEvolutionRule(out PetEvolutionRule rule)
                    || rule.Trigger == PetEvolutionTrigger.Level) continue;
                if (string.Equals(item.ItemId, rule.RequiredItemId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Name, rule.RequiredItemId, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public bool CanUseForSelectedPet(InventoryItemSnapshot item) {
            if (item == null) return false;
            ResolveReferences();
            PetController selectedPet = CommandProvider?.GetSelectedPetController();
            PetEvolutionPreview itemPreview = evolutionService?.CreatePreview(selectedPet,
                PetEvolutionMethod.Item, item.ItemId);
            if (itemPreview?.Rule == null || itemPreview.Rule.Trigger == PetEvolutionTrigger.Level) return false;

            return string.Equals(item.ItemId, itemPreview.RequiredItemId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Name, itemPreview.RequiredItemId, StringComparison.OrdinalIgnoreCase);
        }

        public void Close() {
            targetPet = null;
            preview = null;
            resultShown = false;
            openedAutomatically = false;
            if (screenDocument != null)
                screenDocument.rootVisualElement.style.display = DisplayStyle.None;
        }

        public void DismissPanel() {
            Dismiss();
        }

        void Dismiss() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "Dismiss")) return;
            bool wasAutomatic = openedAutomatically;
            Close();
            if (wasAutomatic) {
                MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
                if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
                controller?.EvolutionScreenClosed();
            }
        }

        void Refresh(bool automaticPrompt = false) {
            if (evolutionService == null) return;
            preview = evolutionService.CreatePreview(targetPet);

            string current = preview?.CurrentName ?? "Chọn một pet";
            string target = preview != null && preview.IsConfigured ? preview.TargetName : "Dạng cuối";
            SetPortrait(currentPortrait, currentFallback, preview?.CurrentIcon, current);
            SetPortrait(targetPortrait, targetFallback, preview?.TargetIcon, target);
            if (currentName != null) currentName.text = current;
            if (targetName != null) targetName.text = target;
            if (formPath != null) {
                formPath.text = preview?.Metadata != null
                    ? $"FORM {preview.Metadata.FormCode}  →  FORM {preview.Rule?.TargetFormId ?? "--"}"
                    : "FORM -";
            }

            if (itemRow != null) itemRow.style.display = DisplayStyle.None;
            if (requirement != null) requirement.text = BuildRequirement(preview);
            if (dialogue != null) {
                dialogue.text = preview == null || !preview.IsConfigured
                    ? "Pet này không còn dạng tiến hóa tiếp theo."
                    : automaticPrompt
                        ? $"{current} đã đủ điều kiện tiến hóa thành {target}. Bạn có muốn tiến hóa ngay không?"
                        : $"Bạn có muốn cho {current} tiến hóa thành {target} không?";
            }
            if (feedback != null) feedback.text = preview?.CanEvolve == true ? string.Empty : preview?.DisabledReason ?? string.Empty;
            if (laterButton != null) laterButton.style.display = DisplayStyle.Flex;
            if (confirmButton != null) {
                confirmButton.text = "TIẾN HÓA";
                confirmButton.SetEnabled(!confirmationRunning && (preview?.CanEvolve == true));
            }
        }

        void ConfirmEvolution() {
            if (confirmationRunning || !Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "confirm")) return;
            confirmationRunning = true;
            confirmButton?.SetEnabled(false);
            try { ConfirmEvolutionCore(); }
            finally { confirmationRunning = false; if (IsOpen) confirmButton?.SetEnabled(resultShown || preview?.CanEvolve == true); }
        }

        void ConfirmEvolutionCore() {
            if (resultShown) {
                Dismiss();
                return;
            }

            if (evolutionService == null || preview == null) return;
            Sprite evolvedIcon = preview.TargetIcon ?? preview.CurrentIcon;
            string evolvedName = preview.TargetName;
            bool success = evolutionService.TryEvolve(targetPet, out string message,
                PetEvolutionMethod.Level);
            if (!success) {
                Refresh();
                if (feedback != null) feedback.text = message;
                return;
            }

            CommandProvider?.NotifyHudDataChanged();
            resultShown = true;
            SetPortrait(targetPortrait, targetFallback, evolvedIcon, evolvedName);
            if (targetName != null) targetName.text = evolvedName;
            if (dialogue != null) dialogue.text = message;
            if (requirement != null) requirement.text = "Tiến hóa hoàn tất";
            if (feedback != null) feedback.text = "Dữ liệu, chỉ số và vị trí Party/Box đã được giữ nguyên.";
            if (itemRow != null) itemRow.style.display = DisplayStyle.None;
            if (laterButton != null) laterButton.style.display = DisplayStyle.None;
            if (confirmButton != null) {
                confirmButton.text = "TIẾP TỤC";
                confirmButton.SetEnabled(!confirmationRunning);
            }
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            if (petHudProvider == null && autoFindReferences) {
                petHudProvider = FindFirstObjectByType<PetCommandHudProvider>();
            }
            if (inventory == null) inventory = GetComponent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = GetComponentInParent<MonsterInventoryAdapter>();
            if (inventory == null && autoFindReferences) inventory = FindFirstObjectByType<MonsterInventoryAdapter>();
            if (evolutionService == null) evolutionService = GetComponent<PetEvolutionService>();
            if (evolutionService == null && Application.isPlaying) evolutionService = gameObject.AddComponent<PetEvolutionService>();
            evolutionService?.Bind(inventory);
        }

        void EnsureStandaloneScreen() {
            if (screenDocument != null || document == null || standaloneTree == null) return;
            GameObject screen = new GameObject("Pet Evolution Screen");
            // A child UIDocument is nested under the Inventory UIDocument and would be
            // hidden when Inventory closes for an automatic level-up prompt.
            screen.transform.SetParent(transform.parent, false);
            screenDocument = screen.AddComponent<UIDocument>();
            screenDocument.panelSettings = document.panelSettings;
            screenDocument.visualTreeAsset = standaloneTree;
            screenDocument.sortingOrder = document.sortingOrder + 100;
            screenDocument.rootVisualElement.style.display = DisplayStyle.None;
        }

        void CacheElements() {
            if (screenDocument == null || screenDocument.rootVisualElement == null) return;
            VisualElement root = screenDocument.rootVisualElement;
            overlay = root.Q<VisualElement>("evolution-modal-overlay");
            content = root.Q<VisualElement>("evolution-modal-content");
            currentPortrait = root.Q<VisualElement>("evolution-current-portrait");
            currentFallback = root.Q<Label>("evolution-current-fallback");
            targetPortrait = root.Q<VisualElement>("evolution-target-portrait");
            targetFallback = root.Q<Label>("evolution-target-fallback");
            currentName = root.Q<Label>("evolution-current-name");
            targetName = root.Q<Label>("evolution-target-name");
            formPath = root.Q<Label>("evolution-form-path");
            requirement = root.Q<Label>("evolution-requirement");
            itemRow = root.Q<VisualElement>("evolution-item-row");
            itemCount = root.Q<Label>("evolution-item-count");
            dialogue = root.Q<Label>("evolution-dialogue");
            feedback = root.Q<Label>("evolution-feedback");
            laterButton = root.Q<Button>("evolution-later-button");
            confirmButton = root.Q<Button>("evolution-confirm-button");
            if (content != null) content.focusable = true;
        }

        void RegisterControls() {
            if (controlsRegistered || overlay == null) return;
            if (laterButton != null) laterButton.clicked += Dismiss;
            if (confirmButton != null) confirmButton.clicked += ConfirmEvolution;
            if (content != null) content.RegisterCallback<KeyDownEvent>(HandleKeyDown);
            controlsRegistered = true;
        }

        void UnregisterControls() {
            if (!controlsRegistered) return;
            if (laterButton != null) laterButton.clicked -= Dismiss;
            if (confirmButton != null) confirmButton.clicked -= ConfirmEvolution;
            if (content != null) content.UnregisterCallback<KeyDownEvent>(HandleKeyDown);
            controlsRegistered = false;
        }

        void Subscribe() {
            if (inventory != null) {
                inventory.ItemsChanged -= HandleInventoryChanged;
                inventory.ItemsChanged += HandleInventoryChanged;
            }
            PetLevelUpService.AnyPetLeveledUp -= HandlePetLeveledUp;
            PetLevelUpService.AnyPetLeveledUp += HandlePetLeveledUp;
        }

        void Unsubscribe() {
            if (inventory != null) inventory.ItemsChanged -= HandleInventoryChanged;
            PetLevelUpService.AnyPetLeveledUp -= HandlePetLeveledUp;
        }

        void HandleInventoryChanged(System.Collections.Generic.IReadOnlyList<InventoryItemSnapshot> _) {
            if (IsOpen && !resultShown) Refresh();
        }

        void HandlePetLeveledUp(PetController pet) {
            if (pet == null || !pendingPrompts.Add(pet)) return;
            StartCoroutine(OpenLevelPromptWhenReady(pet));
        }

        IEnumerator OpenLevelPromptWhenReady(PetController pet) {
            yield return null;
            var director = FindFirstObjectByType<Capstone.Game.PetSummon.PetSummonDirector>();
            while (pet != null && ((director != null && director.IsSequenceRunning)
                || pet.IsSummonPresentationPending || IsOpen)) yield return null;
            pendingPrompts.Remove(pet);
            if (pet == null) yield break;
            PetEvolutionPreview next = evolutionService?.CreatePreview(pet);
            if (next == null || !next.ShouldAutoPrompt) yield break;
            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            if (controller != null) controller.OpenAutomaticPetEvolutionPrompt(pet);
            else Open(pet, true);
            if (IsOpen) evolutionService.MarkAutoPromptShown(next);
        }

        void HandleKeyDown(KeyDownEvent evt) {
            if (evt.keyCode != KeyCode.Escape) return;
            Dismiss();
            evt.StopImmediatePropagation();
        }

        static string BuildRequirement(PetEvolutionPreview value) {
            if (value == null || !value.IsConfigured || value.Rule == null) return "Dạng tiến hóa cuối";
            return value.Rule.Trigger == PetEvolutionTrigger.Item
                ? "Dạng này chỉ tiến hóa bằng vật phẩm trong Bag"
                : $"Lv. {value.CurrentLevel}/{value.RequiredLevel}";
        }

        static void SetPortrait(VisualElement portrait, Label fallback, Sprite sprite, string label) {
            if (portrait == null) return;
            portrait.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;
            if (fallback == null) return;
            fallback.style.display = sprite != null ? DisplayStyle.None : DisplayStyle.Flex;
            fallback.text = string.IsNullOrWhiteSpace(label) ? "?" : label.Substring(0, 1).ToUpperInvariant();
        }
    }
}

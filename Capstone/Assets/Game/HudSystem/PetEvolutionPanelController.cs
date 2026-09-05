using System;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetEvolutionPanelController : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] MonoBehaviour petHudProvider = null;
        [SerializeField] PetEvolutionService evolutionService = null;
        [SerializeField] bool autoFindReferences = true;

        MonsterInventoryAdapter inventory;
        PetLevelUpService levelUpService;
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

        PetCommandHudProvider CommandProvider => petHudProvider as PetCommandHudProvider;
        public bool IsOpen => overlay != null && overlay.resolvedStyle.display != DisplayStyle.None;

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
        }

        public void Bind(UIDocument targetDocument, MonsterInventoryAdapter targetInventory) {
            document = targetDocument != null ? targetDocument : document;
            inventory = targetInventory != null ? targetInventory : inventory;
            ResolveReferences();
            CacheElements();
            RegisterControls();
            Subscribe();
            Close();
        }

        public void Open(PetController pet = null, bool automaticPrompt = false) {
            if (overlay == null) CacheElements();
            if (overlay == null) return;

            ResolveReferences();
            targetPet = pet != null ? pet : CommandProvider?.GetSelectedPetController();
            preview = evolutionService?.CreatePreview(targetPet);
            resultShown = false;
            overlay.style.display = DisplayStyle.Flex;
            overlay.BringToFront();
            Refresh(automaticPrompt);
            content?.Focus();
        }

        public bool TryOpenFromInventory(InventoryItemSnapshot item) {
            if (!CanUseForSelectedPet(item)) return false;
            ResolveReferences();
            PetController selectedPet = CommandProvider?.GetSelectedPetController();
            document?.rootVisualElement?.schedule.Execute(() => Open(selectedPet));
            return true;
        }

        public bool CanUseForSelectedPet(InventoryItemSnapshot item) {
            if (item == null) return false;
            ResolveReferences();
            PetController selectedPet = CommandProvider?.GetSelectedPetController();
            PetEvolutionPreview itemPreview = evolutionService?.CreatePreview(selectedPet);
            if (itemPreview?.Rule == null || itemPreview.Rule.Trigger != PetEvolutionTrigger.Item) return false;

            return string.Equals(item.ItemId, itemPreview.RequiredItemId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Name, itemPreview.RequiredItemId, StringComparison.OrdinalIgnoreCase);
        }

        public void Close() {
            targetPet = null;
            preview = null;
            resultShown = false;
            if (overlay != null) overlay.style.display = DisplayStyle.None;
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
                    ? $"FORM {preview.Metadata.EvolutionStage + 1}  →  FORM {preview.Metadata.EvolutionStage + 2}"
                    : "FORM -";
            }

            bool usesItem = preview?.Rule?.Trigger == PetEvolutionTrigger.Item;
            if (itemRow != null) itemRow.style.display = usesItem ? DisplayStyle.Flex : DisplayStyle.None;
            if (itemCount != null && preview != null) {
                itemCount.text = $"{preview.RequiredItemId}: {preview.OwnedItemQuantity} / {preview.RequiredItemQuantity}";
            }
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
                confirmButton.SetEnabled(preview?.CanEvolve == true);
            }
        }

        void ConfirmEvolution() {
            if (resultShown) {
                Close();
                return;
            }

            if (evolutionService == null || preview == null) return;
            Sprite evolvedIcon = preview.TargetIcon ?? preview.CurrentIcon;
            string evolvedName = preview.TargetName;
            bool success = evolutionService.TryEvolve(targetPet, out string message);
            if (!success) {
                if (feedback != null) feedback.text = message;
                Refresh();
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
                confirmButton.SetEnabled(true);
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
            levelUpService = GetComponent<PetLevelUpService>();
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;
            VisualElement root = document.rootVisualElement;
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
            if (laterButton != null) laterButton.clicked += Close;
            if (confirmButton != null) confirmButton.clicked += ConfirmEvolution;
            if (content != null) content.RegisterCallback<KeyDownEvent>(HandleKeyDown);
            controlsRegistered = true;
        }

        void UnregisterControls() {
            if (!controlsRegistered) return;
            if (laterButton != null) laterButton.clicked -= Close;
            if (confirmButton != null) confirmButton.clicked -= ConfirmEvolution;
            if (content != null) content.UnregisterCallback<KeyDownEvent>(HandleKeyDown);
            controlsRegistered = false;
        }

        void Subscribe() {
            if (inventory != null) {
                inventory.ItemsChanged -= HandleInventoryChanged;
                inventory.ItemsChanged += HandleInventoryChanged;
            }
            if (levelUpService != null) {
                levelUpService.PetLeveledUp -= HandlePetLeveledUp;
                levelUpService.PetLeveledUp += HandlePetLeveledUp;
            }
        }

        void Unsubscribe() {
            if (inventory != null) inventory.ItemsChanged -= HandleInventoryChanged;
            if (levelUpService != null) levelUpService.PetLeveledUp -= HandlePetLeveledUp;
        }

        void HandleInventoryChanged(System.Collections.Generic.IReadOnlyList<InventoryItemSnapshot> _) {
            if (IsOpen && !resultShown) Refresh();
        }

        void HandlePetLeveledUp(PetController pet) {
            PetEvolutionPreview next = evolutionService?.CreatePreview(pet);
            if (next == null || !next.ShouldAutoPrompt) return;
            document?.rootVisualElement?.schedule.Execute(() => {
                Open(pet, true);
                if (IsOpen) evolutionService.MarkAutoPromptShown(next);
            });
        }

        void HandleKeyDown(KeyDownEvent evt) {
            if (evt.keyCode != KeyCode.Escape) return;
            Close();
            evt.StopImmediatePropagation();
        }

        static string BuildRequirement(PetEvolutionPreview value) {
            if (value == null || !value.IsConfigured || value.Rule == null) return "Dạng tiến hóa cuối";
            if (value.Rule.Trigger == PetEvolutionTrigger.Level) {
                return value.RequirementMet
                    ? $"Đã đạt yêu cầu Lv. {value.RequiredLevel}"
                    : $"Yêu cầu Lv. {value.RequiredLevel} • Hiện tại Lv. {value.CurrentLevel}";
            }

            return value.RequirementMet
                ? $"Đã đủ {value.RequiredItemQuantity} {value.RequiredItemId}"
                : $"Cần {value.RequiredItemQuantity} {value.RequiredItemId}";
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

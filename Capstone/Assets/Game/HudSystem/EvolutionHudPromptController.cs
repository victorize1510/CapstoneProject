using System.Collections.Generic;
using Capstone.Game.CreatureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class EvolutionHudPromptController : MonoBehaviour {
        static int playSession;
        int observedSession = -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void BeginSession() => playSession++;
        readonly Queue<PetController> queue = new();
        readonly HashSet<PetController> queued = new();
        readonly HashSet<string> shown = new(System.StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> failures = new(System.StringComparer.OrdinalIgnoreCase);
        EvolutionHudPromptView view;
        PetEvolutionService service;
        PetCommandInput party;
        PlayerSaveController save;
        PetController current;
        PetController manualRequest;
        bool manual;
        string currentKey;
        PetEvolutionPreview offeredPreview;
        GameObject previousSelection;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible, ownsInput, restoreCursorLater, presenting;
        int openedFrame;
        int dismissedFrame = -1;
        float lookupAt, tickAt;
        public bool IsOpen => current != null && view != null && view.gameObject.activeSelf;
        public bool IsShowing(PetController pet) => IsOpen && current == pet;
        public bool TryDismissForBack() {
            if (dismissedFrame == Time.frameCount) return true;
            if (!IsOpen) return false;
            Later();
            return true;
        }

        public void RequestManual(PetController pet) {
            if (pet == null || pet.IsWildEncounter) return;
            if (current != null) { var previous = current; Hide(true); Enqueue(previous); }
            manualRequest = pet;
            tickAt = 0;
        }
        public void CloseFor(PetController pet) {
            if (manualRequest == pet) manualRequest = null;
            if (current == pet) Hide(true);
        }
        internal void ConfirmCurrent() => Confirm();
        static string ManualKey(PetController pet) {
            var metadata = pet != null ? pet.GetComponent<PetCollectionMetadata>() : null;
            var stats = pet != null ? pet.GetComponent<PetHudRuntimeStats>() : null;
            return metadata == null || stats == null || metadata.IsReleased ? null
                : metadata.PetId + "|" + metadata.CurrentFormId + "|manual|" + stats.Level;
        }

        public void Build(RectTransform hudRoot) {
            Hide(false);
            if (view != null) Destroy(view.gameObject);
            var prefab = Resources.Load<EvolutionHudPromptView>("UI/EvolutionHudPrompt");
            if (prefab == null) { Debug.LogWarning("Missing UI/EvolutionHudPrompt prefab; evolution remains available in Pet panel.", this); return; }
            view = Instantiate(prefab, hudRoot, false);
            view.Build();
            view.Confirmed += Confirm;
            view.Deferred += Later;
            view.gameObject.SetActive(false);
            Resolve(); ScanRoster();
        }
        void OnEnable() {
            if (observedSession != playSession) { observedSession = playSession; shown.Clear(); failures.Clear(); }
            PetLevelUpService.AnyPetLeveledUp += Enqueue;
        }
        void OnDisable() {
            PetLevelUpService.AnyPetLeveledUp -= Enqueue;
            if (save != null) save.LoadCompleted -= Loaded;
            save = null;
            Hide(false); queue.Clear(); queued.Clear(); manualRequest = null;
        }
        void OnDestroy() { if (view != null) Destroy(view.gameObject); }
        void Resolve() {
            if (service == null) service = GetComponent<PetEvolutionService>() ?? gameObject.AddComponent<PetEvolutionService>();
            if (party == null) party = FindFirstObjectByType<PetCommandInput>();
            var nextSave = FindFirstObjectByType<PlayerSaveController>();
            if (nextSave != save) {
                if (save != null) save.LoadCompleted -= Loaded;
                save = nextSave;
                if (save != null) save.LoadCompleted += Loaded;
            }
        }
        void Loaded(PlayerSaveData _) { ScanRoster(); }
        void ScanRoster() {
            if (party?.petSlots != null) foreach (var pet in party.petSlots) Enqueue(pet);
            var box = FindFirstObjectByType<PetBoxRuntimeProvider>();
            if (box != null) foreach (var pet in box.StoredPets) Enqueue(pet);
        }
        internal static string PromptKey(PetController pet) {
            var metadata = pet != null ? pet.GetComponent<PetCollectionMetadata>() : null;
            var stats = pet != null ? pet.GetComponent<PetHudRuntimeStats>() : null;
            if (metadata == null || metadata.IsReleased || stats == null || !metadata.TryGetNextEvolutionRule(out var rule)) return null;
            string promptId = PetEvolutionService.PendingLevelPromptId(metadata, rule, stats.Level);
            return promptId == null ? null : metadata.PetId + "|" + metadata.CurrentFormId + "|" + promptId;
        }
        public void Enqueue(PetController pet) {
            if (pet == null || pet.IsWildEncounter) return;
            string key = PromptKey(pet);
            if (key == null || shown.Contains(key) || pet == current || !queued.Add(pet)) return;
            queue.Enqueue(pet);
        }
        bool GloballyBlocked() {
            if (InventoryInputController.IsBlockedByOtherThan(this) || PetEvolutionPresentationController.AnyPlaying
                || UISystem.GameplayPause.IsPaused || presenting) return true;
            if (save != null && (save.IsApplyingSave || (save.HasSave && !save.IsCurrentSlotLoaded))) return true;
            var activePet = party != null ? party.activePet : null;
            if (activePet != null && activePet.IsSummoned && InCombat(activePet)) return true;
            var summon = party != null ? party.GetComponent<PetSummon.PetSummonDirector>() : null;
            return summon != null && summon.IsSequenceRunning;
        }
        static bool PetBlocked(PetController pet) {
            if (pet == null) return true;
            var stats = pet.GetComponent<PetHudRuntimeStats>();
            if (stats == null || stats.Health <= 0 || pet.IsSummonPresentationPending
                || pet.HasCombatTarget || (pet.IsSummoned && !pet.CanReceiveCommands)) return true;
            var action = pet.GetComponent<Skills.PetCombatActionDriver>();
            if (action != null && action.IsSkillBusy) return true;
            if (InCombat(pet)) return true;
            var state = pet.GetComponent<PveCombatState>();
            return state != null && pet.IsSummoned && state.SafeSeconds < 3f;
        }
        static bool InCombat(PetController pet) {
            if (pet.HasCombatTarget) return true;
            var combat = pet.GetComponent<CreatureCombatant>();
            if (combat == null) return false;
            foreach (var enemy in DummyEnemy.ActiveEnemies) {
                var brain = enemy != null && enemy.IsAlive ? enemy.GetComponent<BasicCreatureEnemy>() : null;
                if (brain != null && brain.isActiveAndEnabled && brain.Target == combat) return true;
            }
            return false;
        }
        void Update() {
            if (restoreCursorLater && !InventoryInputController.GameplayInputBlocked) RestoreCursor();
            if (view == null) return;
            if (Time.unscaledTime >= lookupAt) { lookupAt = Time.unscaledTime + 1f; Resolve(); ScanRoster(); }
            if (IsOpen && Time.frameCount > openedFrame && !GloballyBlocked()) {
                if (Input.GetKeyDown(KeyCode.N)) { Later(); return; }
                if (Input.GetKeyDown(KeyCode.Y)) { Confirm(); return; }
                // Some scenes share/disable their UI action map with player input.
                // Keep the focused popup usable; Confirm/Later guards prevent a
                // second Submit from the UI module committing the same action twice.
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) {
                    if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == view.Confirm.gameObject) Confirm();
                    else Later();
                    return;
                }
            }
            if (Time.unscaledTime < tickAt) return;
            tickAt = Time.unscaledTime + .2f;
            if (current != null) {
                if ((manual ? ManualKey(current) : PromptKey(current)) != currentKey) { Hide(true); return; }
                if (GloballyBlocked() || PetBlocked(current)) {
                    var pet = current; bool wasManual = manual;
                    Hide(true); if (wasManual) manualRequest = pet; else Enqueue(pet); return;
                }
                var preview = service.CreatePreview(current);
                Display(preview);
                return;
            }
            if (GloballyBlocked()) return;
            if (manualRequest != null) {
                if (PetBlocked(manualRequest)) return;
                var pet = manualRequest; manualRequest = null;
                string key = ManualKey(pet);
                if (key != null) { manual = true; Show(pet, key); return; }
            }
            int attempts = queue.Count;
            while (attempts-- > 0) {
                var pet = queue.Dequeue(); queued.Remove(pet);
                string key = PromptKey(pet);
                if (key == null || shown.Contains(key)) continue;
                if (PetBlocked(pet)) { Enqueue(pet); continue; }
                var preview = service.CreatePreview(pet);
                if (!preview.ShouldAutoPrompt || !string.IsNullOrEmpty(PresentationBlockReason(pet))) continue;
                manual = false; Show(pet, key, preview); break;
            }
        }
        void Show(PetController pet, string key, PetEvolutionPreview preview = null) {
            current = pet; currentKey = key; openedFrame = Time.frameCount;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible;
            ownsInput = true;
            InventoryInputController.SetExternalGameplayInputBlocked(this, true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            offeredPreview = preview ?? service.CreatePreview(pet);
            Display(offeredPreview);
            view.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(view.Later.gameObject);
        }
        void Later() {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            dismissedFrame = Time.frameCount;
            shown.Add(currentKey);
            var offer = offeredPreview;
            Hide(true);
            service.DeferLevelPrompt(offer);
        }
        void Confirm() {
            if (!IsOpen || Time.frameCount == openedFrame || GloballyBlocked() || PetBlocked(current)) return;
            if (failures.ContainsKey(currentKey)) return;
            var preview = service.CreatePreview(current);
            if (!preview.CanEvolve) { view.Display(preview.CurrentName, preview.TargetName, false, preview.DisabledReason); return; }
            string presentationReason = PresentationBlockReason(current);
            if (!string.IsNullOrEmpty(presentationReason)) { view.Display(preview.CurrentName, preview.TargetName, false, presentationReason); return; }
            var pet = current; string key = currentKey;
            shown.Add(key); Hide(true); presenting = true;
            var scene = pet.owner != null ? pet.owner.gameObject.scene : gameObject.scene;
            PetEvolutionPresentationController.Present(scene, service, pet, PetEvolutionMethod.Level, null, (success, message) => {
                if (this == null) return;
                presenting = false;
                if (!success) { shown.Remove(key); failures[key] = message; Enqueue(pet); Debug.LogWarning("Evolution: " + message, this); }
                else Enqueue(pet);
            });
        }
        void Display(PetEvolutionPreview preview) {
            bool failed = currentKey != null && failures.TryGetValue(currentKey, out _);
            string reason = failed ? failures[currentKey] : !preview.CanEvolve ? preview.DisabledReason : PresentationBlockReason(current);
            view.Display(preview.CurrentName, preview.TargetName, preview.CanEvolve && !failed && string.IsNullOrEmpty(reason), reason);
        }
        string PresentationBlockReason(PetController pet) {
            var scene = pet != null && pet.owner != null ? pet.owner.gameObject.scene : gameObject.scene;
            var presenter = PetEvolutionPresentationController.FindFor(scene);
            if (presenter == null || presenter.whiteOverlayMaterial == null
                || !Application.CanStreamedLevelBeLoaded(presenter.evolutionSceneName))
                return "Chưa cấu hình trình diễn tiến hóa cho scene này.";
            if (UnityEngine.SceneManagement.SceneManager.GetSceneByName(presenter.evolutionSceneName).isLoaded)
                return "Cảnh trình diễn tiến hóa đang bận.";
            return string.Empty;
        }
        void Hide(bool restoreFocus) {
            current = null; currentKey = null; offeredPreview = null; manual = false;
            if (view != null) view.gameObject.SetActive(false);
            if (!ownsInput) return;
            ownsInput = false;
            InventoryInputController.SetExternalGameplayInputBlocked(this, false);
            if (!InventoryInputController.GameplayInputBlocked) {
                RestoreCursor();
                if (restoreFocus && EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
                    EventSystem.current.SetSelectedGameObject(previousSelection);
                else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            } else restoreCursorLater = true;
            previousSelection = null;
        }
        void RestoreCursor() {
            restoreCursorLater = false;
            Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
        }
    }
}

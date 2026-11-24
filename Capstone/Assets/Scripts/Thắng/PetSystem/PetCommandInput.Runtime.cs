using System.Collections;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class PetCommandInput
{
    // Party/BOX hien giu PetController lam handle du lieu. Khong destroy handle nay.
    // Day la kho instance theo ca the, KHONG phai pool tai su dung cho PetId khac.
    Transform ownedRuntimeRoot;
    Transform activeOwnedPets;
    Transform retainedOwnedPets;
    PetBoxRuntimeProvider runtimeRoster;
    PetCaptureCoordinator runtimeCapture;
    readonly HashSet<PetController> pendingOwnedSleep = new HashSet<PetController>();
    readonly List<PetController> ownedSleepBatch = new List<PetController>();
    Coroutine ownedSleepRoutine;

    [Header("Hồi về điểm xuất phát khi cả đội gục")]
    [SerializeField] Transform partyDefeatSpawnPoint;
    Vector3 initialPlayerPosition;
    Quaternion initialPlayerRotation;
    Coroutine partyDefeatRecovery;
    GameObject partyDefeatOverlay;
    bool defeatInputLocked;

    void Start()
    {
        initialPlayerPosition = transform.position;
        initialPlayerRotation = transform.rotation;
        runtimeRoster = FindFirstObjectByType<PetBoxRuntimeProvider>(FindObjectsInactive.Include);
        if (runtimeRoster != null && runtimeRoster.PartyInput == this)
            runtimeRoster.Changed += OrganizeOwnedPetRoster;
        else runtimeRoster = null;
        SceneManager.activeSceneChanged += MoveOwnedRuntimeToOwnerScene;
        OrganizeOwnedPetRoster();
    }

    internal void HandleActivePetDefeat(PetController defeated)
    {
        if (isActiveAndEnabled && activePet == defeated && partyDefeatRecovery == null)
            partyDefeatRecovery = StartCoroutine(RecoverAfterPetDefeat(defeated));
    }

    int NextHealthySlot(PetController defeated)
    {
        if (petSlots == null || petSlots.Length == 0) return -1;
        int current = System.Array.IndexOf(petSlots, defeated);
        for (int offset = 1; offset <= petSlots.Length; offset++)
        {
            int index = (current + offset) % petSlots.Length;
            var pet = petSlots[index];
            if (pet != null && pet != defeated && pet.GetComponentInChildren<PetHudRuntimeStats>(true)?.Health > 0f)
                return index;
        }
        return -1;
    }

    IEnumerator RecoverAfterPetDefeat(PetController defeated)
    {
        // Recall has already restored the body and released the defeated actor.
        yield return null;
        try
        {
            var movement = GetComponent<BasicPlayerMovement>();
            if (movement == null) yield break;
            while (activePet == defeated && (InventoryInputController.GameplayInputBlocked ||
                Capstone.Game.UISystem.GameplayPause.IsPaused || summonDirector != null && summonDirector.IsSequenceRunning))
                yield return null;
            if (activePet != defeated) yield break;

            int next = NextHealthySlot(defeated);
            if (next >= 0)
            {
                while (activePet == defeated && next >= 0)
                {
                    if (!InventoryInputController.GameplayInputBlocked && !Capstone.Game.UISystem.GameplayPause.IsPaused)
                    {
                        if (summonDirector != null && !summonDirector.IsSequenceRunning) RequestSummonSlot(next);
                        else if (summonDirector == null && TryGetPetInSlot(next, out var replacement))
                        {
                            replacement.AssignOwner(transform);
                            if (replacement.TryRestoreSummonedNearOwner(true)) SetActivePet(replacement);
                        }
                        if (summonDirector != null && summonDirector.IsSequenceRunning)
                            while (summonDirector.IsSequenceRunning && activePet == defeated) yield return null;
                    }
                    if (activePet != defeated) yield break;
                    yield return new WaitForSeconds(.5f);
                    next = NextHealthySlot(defeated);
                }
                if (activePet != defeated) yield break;
            }

            bool hasEquipped = false;
            foreach (var pet in petSlots)
            {
                if (pet == null) continue;
                var stats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
                if (stats == null || stats.Health > 0f) yield break;
                hasEquipped = true;
            }
            if (!hasEquipped) yield break;

            InventoryInputController.SetExternalGameplayInputBlocked(this, true);
            defeatInputLocked = true;
            partyDefeatOverlay = new GameObject("Party defeat fade", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(partyDefeatOverlay, gameObject.scene);
            var canvas = partyDefeatOverlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var curtain = Capstone.Game.HudSystem.GameplayHudVisuals.Image(canvas.transform, "Blackout");
            Capstone.Game.HudSystem.GameplayHudVisuals.Stretch(curtain.rectTransform);
            yield return FadeDefeatCurtain(curtain, false);

            var point = partyDefeatSpawnPoint;
            movement.WarpTo(point != null ? point.position : initialPlayerPosition,
                point != null ? point.rotation : initialPlayerRotation);
            selectedEnemy = null;
            cameraLock?.ClearPartyDefeatLock();
            foreach (var pet in petSlots)
            {
                if (pet == null) continue;
                pet.Withdraw();
                if (pet.GetComponentInChildren<PetHudRuntimeStats>(true) is PetHudRuntimeStats stats)
                    stats.SetStatus(stats.MaxHealth, stats.MaxHealth, stats.MaxEnergy, stats.MaxEnergy);
            }
            FindFirstObjectByType<Capstone.Game.SaveSystem.PlayerSaveController>()?.RequestSave();
            yield return FadeDefeatCurtain(curtain, true);
        }
        finally { ClearPartyDefeatPresentation(); partyDefeatRecovery = null; }
    }

    static IEnumerator FadeDefeatCurtain(Image curtain, bool reveal)
    {
        const float duration = .45f;
        float started = Time.unscaledTime;
        while (curtain != null)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - started) / duration);
            curtain.color = new Color(0f, 0f, 0f, reveal ? 1f - t : t);
            if (t >= 1f) yield break;
            yield return null;
        }
    }

    void CancelPartyDefeatRecovery()
    {
        if (partyDefeatRecovery != null) StopCoroutine(partyDefeatRecovery);
        partyDefeatRecovery = null;
        ClearPartyDefeatPresentation();
    }

    void ClearPartyDefeatPresentation()
    {
        if (partyDefeatOverlay != null) Destroy(partyDefeatOverlay);
        partyDefeatOverlay = null;
        if (!defeatInputLocked) return;
        defeatInputLocked = false;
        InventoryInputController.SetExternalGameplayInputBlocked(this, false);
    }

    void OnDestroy()
    {
        if (runtimeRoster != null) runtimeRoster.Changed -= OrganizeOwnedPetRoster;
        SceneManager.activeSceneChanged -= MoveOwnedRuntimeToOwnerScene;
        // Root co cung vong doi voi owner; khong tao singleton/DDOL rieng.
        if (ownedRuntimeRoot != null) Destroy(ownedRuntimeRoot.gameObject);
    }

    internal void OrganizeOwnedPetRoster()
    {
        if (!Application.isPlaying) return;
        if (petSlots != null)
            foreach (var pet in petSlots) TrackOwnedPet(pet);
        if (runtimeRoster != null)
            foreach (var pet in runtimeRoster.StoredPets) TrackOwnedPet(pet);
    }

    internal void TrackOwnedPet(PetController pet)
    {
        if (!Application.isPlaying || pet == null || pet.IsWildEncounter) return;
        if (pet.owner == null) pet.AssignOwner(transform);
        if (pet.owner != transform || pet.transform == transform || transform.IsChildOf(pet.transform)) return;
        if (pet.IsSummoned) PrepareOwnedPetRuntime(pet);
        else QueueOwnedPetSleep(pet);
    }

    internal void PrepareOwnedPetRuntime(PetController pet)
    {
        if (!Application.isPlaying || pet == null || pet.IsWildEncounter || pet.owner != transform) return;
        EnsureOwnedRuntimeRoot();
        pendingOwnedSleep.Remove(pet);
        // Container identity: reparent khong lam doi world pose/scale hoac keo pet theo player.
        if (pet.transform.parent != activeOwnedPets) pet.transform.SetParent(activeOwnedPets, true);
        if (!pet.gameObject.activeSelf) pet.gameObject.SetActive(true);
    }

    internal void QueueOwnedPetSleep(PetController pet)
    {
        if (!Application.isPlaying || pet == null || pet.IsWildEncounter || pet.owner != transform) return;
        pendingOwnedSleep.Add(pet);
        ResumeOwnedPetStorage();
    }

    void ResumeOwnedPetStorage()
    {
        if (Application.isPlaying && isActiveAndEnabled && pendingOwnedSleep.Count > 0 && ownedSleepRoutine == null)
            ownedSleepRoutine = StartCoroutine(StoreDormantOwnedPets());
    }

    void PauseOwnedPetStorage()
    {
        if (ownedSleepRoutine != null) StopCoroutine(ownedSleepRoutine);
        ownedSleepRoutine = null;
    }

    IEnumerator StoreDormantOwnedPets()
    {
        // Cho Awake/Start, giao dich capture va callback VFX hoan tat frame hien tai.
        yield return null;
        if (runtimeCapture == null)
            runtimeCapture = FindFirstObjectByType<PetCaptureCoordinator>(FindObjectsInactive.Include);
        while (pendingOwnedSleep.Count > 0)
        {
            if (runtimeCapture != null && runtimeCapture.IsCapturing)
            {
                yield return null;
                continue;
            }
            ownedSleepBatch.Clear();
            ownedSleepBatch.AddRange(pendingOwnedSleep);
            foreach (var pet in ownedSleepBatch)
            {
                if (pet == null || pet.owner != transform || pet.IsWildEncounter || pet.IsSummoned)
                {
                    pendingOwnedSleep.Remove(pet);
                    continue;
                }
                // Recall/defeat dang trinh dien phai tiep tuc cho den callback hide.
                if (!pet.CanSuspendOwnedRuntime) continue;
                EnsureOwnedRuntimeRoot();
                if (pet.transform.parent != retainedOwnedPets) pet.transform.SetParent(retainedOwnedPets, true);
                if (pet.gameObject.activeSelf) pet.gameObject.SetActive(false);
                pendingOwnedSleep.Remove(pet);
            }
            yield return null;
        }
        ownedSleepBatch.Clear();
        ownedSleepRoutine = null;
    }

    void EnsureOwnedRuntimeRoot()
    {
        if (ownedRuntimeRoot == null)
        {
            ownedRuntimeRoot = new GameObject("CreatureRuntimeRoot").transform;
            activeOwnedPets = new GameObject("ActiveOwnedPets").transform;
            activeOwnedPets.SetParent(ownedRuntimeRoot, false);
            retainedOwnedPets = new GameObject("RetainedOwnedPetInstances").transform;
            retainedOwnedPets.SetParent(ownedRuntimeRoot, false);
        }
        MoveOwnedRuntimeToOwnerScene(default, default);
    }

    void MoveOwnedRuntimeToOwnerScene(Scene previous, Scene next)
    {
        if (ownedRuntimeRoot == null) return;
        var ownerScene = gameObject.scene;
        if (ownerScene.IsValid() && ownerScene.isLoaded && ownedRuntimeRoot.gameObject.scene != ownerScene)
            SceneManager.MoveGameObjectToScene(ownedRuntimeRoot.gameObject, ownerScene);
    }
}

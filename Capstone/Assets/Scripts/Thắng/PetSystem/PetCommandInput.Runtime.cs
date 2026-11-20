using System.Collections;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
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

    void Start()
    {
        runtimeRoster = FindFirstObjectByType<PetBoxRuntimeProvider>(FindObjectsInactive.Include);
        if (runtimeRoster != null && runtimeRoster.PartyInput == this)
            runtimeRoster.Changed += OrganizeOwnedPetRoster;
        else runtimeRoster = null;
        SceneManager.activeSceneChanged += MoveOwnedRuntimeToOwnerScene;
        OrganizeOwnedPetRoster();
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

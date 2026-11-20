using UnityEngine;

public partial class PetController
{
    internal bool CanSuspendOwnedRuntime => !wildEncounter && !IsSummoned
        && state == PetState.Dormant && !defeatPresentation && !recallLocked && !summonRevealPending;

    void QueueOwnedRuntimeStorage()
    {
        if (Application.isPlaying && !wildEncounter && owner != null)
            owner.GetComponent<PetCommandInput>()?.QueueOwnedPetSleep(this);
    }

    void PrepareOwnedRuntime()
    {
        if (Application.isPlaying && !wildEncounter && owner != null)
            owner.GetComponent<PetCommandInput>()?.PrepareOwnedPetRuntime(this);
    }
}

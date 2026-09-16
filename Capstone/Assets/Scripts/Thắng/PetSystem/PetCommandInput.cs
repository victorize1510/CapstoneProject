using UnityEngine;
using UnityEngine.EventSystems;
using Capstone.Game.PetSummon;

[DisallowMultipleComponent]
public class PetCommandInput : MonoBehaviour
{
    private static readonly KeyCode[] NumberKeys =
    {
        KeyCode.Alpha1,
        KeyCode.Alpha2,
        KeyCode.Alpha3,
        KeyCode.Alpha4,
        KeyCode.Alpha5,
        KeyCode.Alpha6
    };

    private static readonly KeyCode[] KeypadKeys =
    {
        KeyCode.Keypad1,
        KeyCode.Keypad2,
        KeyCode.Keypad3,
        KeyCode.Keypad4,
        KeyCode.Keypad5,
        KeyCode.Keypad6
    };

    [Header("References")]
    public PetController activePet;
    public PetController[] petSlots = new PetController[6];
    public Camera commandCamera;
    public BasicCameraFollow cameraLock;
    [SerializeField] private PetSummonDirector summonDirector;

    [Header("Input")]
    public int commandMouseButton = 0;
    public KeyCode withdrawKey = KeyCode.Backspace;
    public bool ignoreWhileRightMouseHeld = false;
    public bool allowCommandsWhileRightMouseHeld = true;
    public int aimMouseButton = 1;
    public bool ignoreWhenPointerOverUI = true;

    [Header("Raycast")]
    public float rayDistance = 250f;
    public LayerMask commandMask = ~0;
    public bool useScreenCenterWhenCursorLocked = true;
    public bool useScreenCenterWhileAiming = false;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private void Awake()
    {
        if (commandCamera == null)
        {
            commandCamera = Camera.main;
        }

        ResolveCameraLock(commandCamera);

        if (summonDirector == null)
        {
            summonDirector = GetComponent<PetSummonDirector>();
        }

        EnsureSlots();

        if (activePet == null)
        {
            PetController[] candidates = FindObjectsByType<PetController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null && candidates[i].owner == transform)
                {
                    activePet = candidates[i];
                    break;
                }
            }

            EnsureSlots();
        }
    }

    private void Update()
    {
        if (Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked)
        {
            return;
        }

        HandlePetSlotInput();

        if (summonDirector != null && summonDirector.IsSequenceRunning)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(commandMouseButton) || activePet == null)
        {
            return;
        }

        if (!allowCommandsWhileRightMouseHeld && ignoreWhileRightMouseHeld && Input.GetMouseButton(aimMouseButton))
        {
            return;
        }

        if (ignoreWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Camera cameraToUse = commandCamera != null ? commandCamera : Camera.main;
        if (cameraToUse == null)
        {
            return;
        }

        Ray ray = cameraToUse.ScreenPointToRay(GetCommandScreenPoint());
        int hitCount = Physics.RaycastNonAlloc(ray, hits, rayDistance, commandMask, QueryTriggerInteraction.Collide);
        if (hitCount <= 0)
        {
            return;
        }

        if (TryFindClickedEnemy(hitCount, out DummyEnemy clickedEnemy))
        {
            activePet.CommandAttack(clickedEnemy);
        }
    }

    public void SetActivePet(PetController pet)
    {
        activePet = pet;
        if (activePet != null && activePet.owner == null)
        {
            activePet.AssignOwner(transform);
        }
    }

    private void HandlePetSlotInput()
    {
        EnsureSlots();

        for (int i = 0; i < petSlots.Length && i < NumberKeys.Length; i++)
        {
            if (Input.GetKeyDown(NumberKeys[i]) || Input.GetKeyDown(KeypadKeys[i]))
            {
                SummonSlot(i);
                return;
            }
        }

        if (withdrawKey != KeyCode.None && Input.GetKeyDown(withdrawKey))
        {
            WithdrawActivePet();
        }
    }

    private void SummonSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= petSlots.Length)
        {
            return;
        }

        if (summonDirector != null)
        {
            summonDirector.RequestSummonSlot(slotIndex);
            return;
        }

        if (!TryGetPetInSlot(slotIndex, out PetController selectedPet))
        {
            return;
        }

        if (activePet != null && activePet != selectedPet)
        {
            activePet.Withdraw();
        }

        activePet = selectedPet;
        activePet.AssignOwner(transform);
        activePet.Summon();
    }

    public bool TryGetPetInSlot(int slotIndex, out PetController pet)
    {
        EnsureSlots();
        pet = null;
        if (slotIndex < 0 || slotIndex >= petSlots.Length)
        {
            return false;
        }

        pet = petSlots[slotIndex];
        return pet != null;
    }

    private void WithdrawActivePet()
    {
        if (summonDirector != null)
        {
            summonDirector.RequestRecall();
            return;
        }

        if (activePet == null)
        {
            return;
        }

        activePet.Withdraw();
    }

    private void EnsureSlots()
    {
        if (petSlots == null || petSlots.Length != 6)
        {
            PetController[] oldSlots = petSlots;
            petSlots = new PetController[6];
            if (oldSlots != null)
            {
                int length = Mathf.Min(oldSlots.Length, petSlots.Length);
                for (int i = 0; i < length; i++)
                {
                    petSlots[i] = oldSlots[i];
                }
            }
        }

        if (activePet != null && petSlots[0] == null && System.Array.IndexOf(petSlots, activePet) < 0)
        {
            petSlots[0] = activePet;
        }

        for (int i = 0; i < petSlots.Length; i++)
        {
            if (activePet == null && petSlots[i] != null)
            {
                activePet = petSlots[i];
            }

            if (petSlots[i] != null && petSlots[i].owner == null)
            {
                petSlots[i].AssignOwner(transform);
            }
        }
    }

    private Vector3 GetCommandScreenPoint()
    {
        if (useScreenCenterWhileAiming && Input.GetMouseButton(aimMouseButton))
        {
            return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

        if (useScreenCenterWhenCursorLocked && Cursor.lockState == CursorLockMode.Locked)
        {
            return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

        return Input.mousePosition;
    }

    private void ResolveCameraLock(Camera cameraToUse)
    {
        if (cameraLock != null && cameraLock.isActiveAndEnabled)
        {
            return;
        }

        cameraLock = null;

        if (cameraToUse != null)
        {
            cameraLock = cameraToUse.GetComponent<BasicCameraFollow>();
            if (cameraLock != null)
            {
                return;
            }
        }

        cameraLock = FindFirstObjectByType<BasicCameraFollow>();
    }

    private bool TryFindClickedEnemy(int hitCount, out DummyEnemy clickedEnemy)
    {
        clickedEnemy = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = hits[i].collider;
            if (candidate == null || hits[i].distance >= bestDistance)
            {
                continue;
            }

            DummyEnemy enemy = candidate.GetComponentInParent<DummyEnemy>();
            if (candidate.transform.IsChildOf(transform)
                || candidate.GetComponentInParent<PetController>() != null
                || (candidate.isTrigger && enemy == null))
            {
                continue;
            }

            bestDistance = hits[i].distance;
            clickedEnemy = enemy != null && enemy.IsAlive ? enemy : null;
        }

        return clickedEnemy != null;
    }

}

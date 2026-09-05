using UnityEngine;
using UnityEngine.EventSystems;

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

    [Header("Input")]
    public int commandMouseButton = 0;
    public KeyCode withdrawKey = KeyCode.Backspace;
    public bool ignoreWhileRightMouseHeld = false;
    public bool allowCommandsWhileRightMouseHeld = true;
    public bool commandLockedEnemyFirst = true;
    public int aimMouseButton = 1;
    public bool ignoreWhenPointerOverUI = true;

    [Header("Raycast")]
    public float rayDistance = 250f;
    public LayerMask commandMask = ~0;
    public bool moveToGroundWhenNoEnemy = true;
    public float enemySearchRadius = 1.25f;
    public bool useScreenCenterWhenCursorLocked = true;
    public bool useScreenCenterWhileAiming = false;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private readonly Collider[] nearbyColliders = new Collider[24];
    private void Awake()
    {
        if (commandCamera == null)
        {
            commandCamera = Camera.main;
        }

        ResolveCameraLock(commandCamera);

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

            if (activePet == null && candidates.Length > 0)
            {
                activePet = candidates[0];
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

        ResolveCameraLock(cameraToUse);
        if (commandLockedEnemyFirst && cameraLock != null && cameraLock.TryGetLockedEnemy(out DummyEnemy lockedEnemy))
        {
            activePet.CommandAttack(lockedEnemy);
            return;
        }

        Ray ray = cameraToUse.ScreenPointToRay(GetCommandScreenPoint());
        int hitCount = Physics.RaycastNonAlloc(ray, hits, rayDistance, commandMask, QueryTriggerInteraction.Collide);
        if (hitCount <= 0)
        {
            return;
        }

        DummyEnemy enemy = FindNearestEnemy(hitCount);
        if (enemy != null)
        {
            activePet.CommandAttack(enemy);
            return;
        }

        if (TryFindNearestHit(hitCount, out RaycastHit nearestHit))
        {
            DummyEnemy nearbyEnemy = FindEnemyNearPoint(nearestHit.point);
            if (nearbyEnemy != null)
            {
                activePet.CommandAttack(nearbyEnemy);
                return;
            }

            if (moveToGroundWhenNoEnemy)
            {
                activePet.CommandMove(nearestHit.point);
            }
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

        PetController selectedPet = petSlots[slotIndex];
        if (selectedPet == null)
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

    private void WithdrawActivePet()
    {
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

        if (activePet != null && petSlots[0] == null)
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

    private DummyEnemy FindNearestEnemy(int hitCount)
    {
        DummyEnemy bestEnemy = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            DummyEnemy enemy = hits[i].collider.GetComponentInParent<DummyEnemy>();
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    private bool TryFindNearestHit(int hitCount, out RaycastHit nearestHit)
    {
        int bestIndex = -1;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            if (hits[i].distance < bestDistance)
            {
                bestIndex = i;
                bestDistance = hits[i].distance;
            }
        }

        nearestHit = bestIndex >= 0 ? hits[bestIndex] : default;
        return bestIndex >= 0;
    }

    private DummyEnemy FindEnemyNearPoint(Vector3 point)
    {
        if (enemySearchRadius <= 0f)
        {
            return null;
        }

        int count = Physics.OverlapSphereNonAlloc(point, enemySearchRadius, nearbyColliders, commandMask, QueryTriggerInteraction.Collide);
        DummyEnemy bestEnemy = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider targetCollider = nearbyColliders[i];
            nearbyColliders[i] = null;
            if (targetCollider == null)
            {
                continue;
            }

            DummyEnemy enemy = targetCollider.GetComponentInParent<DummyEnemy>();
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(enemy.TargetPosition - point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }
}

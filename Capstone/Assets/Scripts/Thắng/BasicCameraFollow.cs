using UnityEngine;

public class BasicCameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Mode")]
    public bool useFreeLookMouseMode = true;

    [Header("Orbit Camera")]
    public Vector3 offset = new Vector3(0f, 2.2f, -4.5f);
    public float lookHeight = 1.2f;
    public float followSpeed = 10f;
    public float positionSmoothTime = 0.12f;
    public float focusSmoothTime = 0.18f;
    public float maxFocusLag = 2f;
    public float verticalFollowSmoothTime = 0.08f;
    public float maxVerticalLag = 0.7f;
    public float mouseSensitivity = 2.2f;
    public float minPitch = -10f;
    public float maxPitch = 45f;
    public bool lockCursorOnPlay = true;

    [Header("Mouse Control")]
    public bool showCursorInPlay = false;
    public bool rotateOnlyWhileRightMouseHeld = false;
    public bool lockCursorWhileRotating = true;
    public int rotateMouseButton = 1;

    [Header("Scroll Zoom")]
    public bool enableScrollZoom = true;
    public float minZoomDistance = 3f;
    public float maxZoomDistance = 7f;
    public float zoomSpeed = 1.25f;
    public float zoomSmoothTime = 0.08f;

    [Header("Aim Camera")]
    public bool enableAimCamera = true;
    public Vector3 aimOffset = new Vector3(0.85f, 1.75f, -2.55f);
    public float aimLookHeight = 1.35f;
    public float aimLookAhead = 8f;
    public int aimMouseButton = 1;
    public float aimBlendSpeed = 10f;
    public float aimBlendSmoothTime = 0.08f;
    public float aimFocusSmoothTime = 0.1f;
    public float aimPositionSmoothTime = 0.08f;
    public bool showAimReticle = false;
    public Color reticleColor = new Color(1f, 1f, 1f, 0.85f);

    [Header("Enemy Lock")]
    public bool enableEnemyLock = true;
    public int lockMouseButton = 2;
    public LayerMask lockMask = ~0;
    public float lockRayDistance = 250f;
    public float lockSphereRadius = 1.25f;
    public float lockSearchRadius = 8f;
    public float lockScreenRadius = 120f;
    public float lockBreakDistance = 35f;
    public float lockLookHeight = 0f;
    public float lockYawSpeed = 720f;
    public Vector3 lockCameraOffset = new Vector3(0.8f, 1.9f, -3.15f);
    public float lockBlendSpeed = 10f;
    public bool useScreenCenterWhenCursorLocked = true;
    public bool showLockReticle = true;
    public bool showLockMarker = true;
    public Color lockMarkerColor = new Color(1f, 0.85f, 0.2f, 0.95f);

    private float yaw;
    private float pitch = 18f;
    private float aimBlend;
    private float aimBlendVelocity;
    private float lockBlend;
    private float currentZoomDistance;
    private float targetZoomDistance;
    private float zoomVelocity;
    private float smoothedTargetY;
    private float targetYVelocity;
    private Vector3 positionVelocity;
    private Vector3 focusVelocity;
    private Vector3 focusPoint;
    private DummyEnemy lockedEnemy;
    private readonly RaycastHit[] lockHits = new RaycastHit[16];
    private bool focusInitialized;
    private bool targetYInitialized;

    public bool IsAiming
    {
        get { return aimBlend > 0.5f; }
    }

    public Vector3 PlanarForward
    {
        get
        {
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }
    }

    public bool HasLockedEnemy
    {
        get { return lockedEnemy != null && lockedEnemy.IsAlive; }
    }

    public bool TryGetLockedTargetPosition(out Vector3 position)
    {
        if (HasLockedEnemy)
        {
            position = lockedEnemy.TargetPosition;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    private void OnValidate()
    {
        minZoomDistance = Mathf.Max(0.5f, minZoomDistance);
        maxZoomDistance = Mathf.Max(minZoomDistance, maxZoomDistance);
        zoomSpeed = Mathf.Max(0.05f, zoomSpeed);
        zoomSmoothTime = Mathf.Max(0.01f, zoomSmoothTime);
        verticalFollowSmoothTime = Mathf.Max(0.01f, verticalFollowSmoothTime);
        maxVerticalLag = Mathf.Max(0.05f, maxVerticalLag);
        lockRayDistance = Mathf.Max(1f, lockRayDistance);
        lockSphereRadius = Mathf.Max(0.05f, lockSphereRadius);
        lockSearchRadius = Mathf.Max(0.5f, lockSearchRadius);
        lockScreenRadius = Mathf.Max(8f, lockScreenRadius);
        lockBreakDistance = Mathf.Max(1f, lockBreakDistance);
        lockYawSpeed = Mathf.Max(1f, lockYawSpeed);
        lockBlendSpeed = Mathf.Max(0.1f, lockBlendSpeed);
        aimBlendSpeed = Mathf.Max(0.1f, aimBlendSpeed);
        aimBlendSmoothTime = Mathf.Max(0.01f, aimBlendSmoothTime);
        aimFocusSmoothTime = Mathf.Max(0.01f, aimFocusSmoothTime);
        aimPositionSmoothTime = Mathf.Max(0.01f, aimPositionSmoothTime);
    }

    private void Start()
    {
        ApplyCameraModeDefaults();
        TryFindTarget();
        InitializeAnglesFromCurrentCamera();
        InitializeFocusPoint();
        InitializeZoomDistance();
        InitializeTargetHeight();

        ApplyCursorState();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        ApplyCameraModeDefaults();
        if (hasFocus)
        {
            ApplyCursorState();
        }
    }

    private void LateUpdate()
    {
        ApplyCameraModeDefaults();
        TryFindTarget();

        if (target == null)
        {
            return;
        }

        UpdateCursorLock();
        UpdateEnemyLockInput();
        UpdateMouseLook();
        UpdateScrollZoom();
        UpdateAimBlend();
        FollowTarget();
    }

    private void TryFindTarget()
    {
        if (target != null)
        {
            return;
        }

        BasicPlayerMovement player = Object.FindFirstObjectByType<BasicPlayerMovement>();
        if (player != null)
        {
            target = player.transform;
            focusInitialized = false;
            targetYInitialized = false;
        }
    }

    private void ApplyCameraModeDefaults()
    {
        if (!useFreeLookMouseMode)
        {
            return;
        }

        lockCursorOnPlay = true;
        showCursorInPlay = false;
        rotateOnlyWhileRightMouseHeld = false;
        lockCursorWhileRotating = true;
        enableAimCamera = true;
        showAimReticle = false;
        enableEnemyLock = true;
        lockLookHeight = 0f;
        useScreenCenterWhenCursorLocked = true;
        showLockReticle = true;
    }

    private void InitializeAnglesFromCurrentCamera()
    {
        if (target != null)
        {
            Vector3 targetToCamera = transform.position - target.position;
            targetToCamera.y = 0f;
            if (targetToCamera.sqrMagnitude > 0.001f)
            {
                Vector3 cameraForward = -targetToCamera.normalized;
                yaw = Quaternion.LookRotation(cameraForward, Vector3.up).eulerAngles.y;
            }
            else
            {
                yaw = target.eulerAngles.y;
            }
        }
        else
        {
            yaw = transform.eulerAngles.y;
        }

        pitch = Mathf.Clamp(NormalizePitch(transform.eulerAngles.x), minPitch, maxPitch);
    }

    private void InitializeFocusPoint()
    {
        if (target == null)
        {
            return;
        }

        focusPoint = target.position + Vector3.up * lookHeight;
        focusInitialized = true;
        positionVelocity = Vector3.zero;
        focusVelocity = Vector3.zero;
    }

    private void InitializeTargetHeight()
    {
        if (target == null)
        {
            return;
        }

        smoothedTargetY = target.position.y;
        targetYVelocity = 0f;
        targetYInitialized = true;
    }

    private void InitializeZoomDistance()
    {
        float distance = Mathf.Clamp(GetBaseOrbitDistance(), minZoomDistance, maxZoomDistance);
        currentZoomDistance = distance;
        targetZoomDistance = distance;
        zoomVelocity = 0f;
    }

    private void UpdateCursorLock()
    {
        if (IsGameplayInputBlocked())
        {
            UnlockCursor();
            return;
        }

        if (showCursorInPlay)
        {
            UnlockCursor();
            return;
        }

        if (rotateOnlyWhileRightMouseHeld)
        {
            if (Input.GetMouseButton(rotateMouseButton))
            {
                if (lockCursorWhileRotating)
                {
                    LockCursor();
                }

                return;
            }

            UnlockCursor();
            return;
        }

        if (lockCursorOnPlay)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                UnlockCursor();
            }

            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(aimMouseButton) || Input.GetMouseButtonDown(lockMouseButton))
            {
                LockCursor();
            }
        }
    }

    private void UpdateMouseLook()
    {
        if (IsGameplayInputBlocked())
        {
            return;
        }

        if (rotateOnlyWhileRightMouseHeld && !Input.GetMouseButton(rotateMouseButton))
        {
            return;
        }

        if (!showCursorInPlay && !rotateOnlyWhileRightMouseHeld && lockCursorOnPlay && Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        // Keep vertical orbit readable; wide pitch makes third-person control feel unstable.
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateAimBlend()
    {
        float targetBlend = enableAimCamera && Input.GetMouseButton(aimMouseButton) ? 1f : 0f;
        aimBlend = Mathf.SmoothDamp(aimBlend, targetBlend, ref aimBlendVelocity, aimBlendSmoothTime, aimBlendSpeed, Time.deltaTime);
        if (Mathf.Abs(aimBlend - targetBlend) < 0.001f)
        {
            aimBlend = targetBlend;
            aimBlendVelocity = 0f;
        }
    }

    private void UpdateEnemyLockInput()
    {
        if (IsGameplayInputBlocked())
        {
            return;
        }

        if (!enableEnemyLock)
        {
            lockedEnemy = null;
            return;
        }

        if (Input.GetMouseButtonDown(lockMouseButton))
        {
            if (lockedEnemy != null)
            {
                lockedEnemy = null;
                focusInitialized = false;
                return;
            }

            DummyEnemy candidate = FindLockCandidate();
            if (candidate != null)
            {
                lockedEnemy = candidate;
                focusInitialized = false;
            }
            else
            {
                lockedEnemy = null;
            }
        }

        if (lockedEnemy != null && !lockedEnemy.IsAlive)
        {
            Vector3 searchPoint = lockedEnemy.TargetPosition;
            lockedEnemy = FindNearestAliveEnemy(searchPoint, lockedEnemy);
            focusInitialized = false;
        }
    }

    private DummyEnemy FindLockCandidate()
    {
        Camera cameraToUse = GetComponent<Camera>();
        if (cameraToUse == null)
        {
            cameraToUse = Camera.main;
        }

        if (cameraToUse == null)
        {
            return null;
        }

        Ray ray = cameraToUse.ScreenPointToRay(GetLockScreenPoint());
        DummyEnemy enemy = FindEnemyFromRaycast(Physics.RaycastNonAlloc(ray, lockHits, lockRayDistance, lockMask, QueryTriggerInteraction.Collide));
        if (enemy != null)
        {
            return enemy;
        }

        enemy = FindEnemyFromRaycast(Physics.SphereCastNonAlloc(ray, lockSphereRadius, lockHits, lockRayDistance, lockMask, QueryTriggerInteraction.Collide));
        if (enemy != null)
        {
            return enemy;
        }

        return FindEnemyNearMouse(cameraToUse);
    }

    private DummyEnemy FindEnemyFromRaycast(int hitCount)
    {
        DummyEnemy bestEnemy = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            DummyEnemy enemy = lockHits[i].collider != null ? lockHits[i].collider.GetComponentInParent<DummyEnemy>() : null;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            if (lockHits[i].distance < bestDistance)
            {
                bestDistance = lockHits[i].distance;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    private DummyEnemy FindEnemyNearMouse(Camera cameraToUse)
    {
        DummyEnemy[] enemies = Object.FindObjectsByType<DummyEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        DummyEnemy bestEnemy = null;
        float bestScore = float.PositiveInfinity;
        Vector2 mouse = GetLockScreenPoint();

        foreach (DummyEnemy enemy in enemies)
        {
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            Vector3 screen = cameraToUse.WorldToScreenPoint(enemy.TargetPosition);
            if (screen.z <= 0f)
            {
                continue;
            }

            float screenDistance = Vector2.Distance(mouse, new Vector2(screen.x, screen.y));
            if (screenDistance > lockScreenRadius)
            {
                continue;
            }

            float worldDistance = target != null ? Vector3.Distance(target.position, enemy.TargetPosition) : screen.z;
            if (target != null && worldDistance > lockSearchRadius)
            {
                continue;
            }

            float score = screenDistance + worldDistance * 2f;
            if (score < bestScore)
            {
                bestScore = score;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    private DummyEnemy FindNearestAliveEnemy(Vector3 searchPoint, DummyEnemy ignoredEnemy)
    {
        DummyEnemy bestEnemy = null;
        float bestDistance = float.PositiveInfinity;
        DummyEnemy[] enemies = Object.FindObjectsByType<DummyEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (DummyEnemy enemy in enemies)
        {
            if (enemy == null || enemy == ignoredEnemy || !enemy.IsAlive)
            {
                continue;
            }

            float distance = FlatDistance(searchPoint, enemy.TargetPosition);
            if (distance > lockSearchRadius)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    private Vector3 GetLockScreenPoint()
    {
        if (useScreenCenterWhenCursorLocked)
        {
            return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

        return Input.mousePosition;
    }

    private void UpdateScrollZoom()
    {
        if (IsGameplayInputBlocked())
        {
            return;
        }

        if (!enableScrollZoom)
        {
            return;
        }

        EnsureZoomInitialized();
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f)
        {
            return;
        }

        targetZoomDistance = Mathf.Clamp(targetZoomDistance - scroll * zoomSpeed, minZoomDistance, maxZoomDistance);
    }

    private void FollowTarget()
    {
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        EnsureZoomInitialized();
        Vector3 smoothedTargetPosition = GetSmoothedTargetPosition(deltaTime);
        UpdateLockedEnemyTracking(smoothedTargetPosition, deltaTime);
        bool hasLockTarget = HasValidLockTarget(smoothedTargetPosition);
        float targetLockBlend = hasLockTarget ? 1f : 0f;
        lockBlend = Mathf.MoveTowards(lockBlend, targetLockBlend, lockBlendSpeed * deltaTime);
        targetZoomDistance = Mathf.Clamp(targetZoomDistance, minZoomDistance, maxZoomDistance);
        currentZoomDistance = Mathf.SmoothDamp(currentZoomDistance, targetZoomDistance, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, deltaTime);

        Quaternion cameraYaw = Quaternion.Euler(0f, yaw, 0f);
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 normalOffset = cameraRotation * GetZoomedOrbitOffset();
        Vector3 shoulderOffset = cameraYaw * aimOffset;
        Vector3 actionOffset = Vector3.Lerp(normalOffset, shoulderOffset, aimBlend);
        Vector3 lockOffset = cameraYaw * lockCameraOffset;
        Vector3 blendedOffset = Vector3.Lerp(actionOffset, lockOffset, lockBlend);

        Vector3 normalFocus = smoothedTargetPosition + Vector3.up * lookHeight;
        Vector3 aimDirection = cameraRotation * Vector3.forward;
        Vector3 aimFocus = smoothedTargetPosition + Vector3.up * aimLookHeight + aimDirection * aimLookAhead;
        Vector3 desiredFocus = Vector3.Lerp(normalFocus, aimFocus, aimBlend);
        if (hasLockTarget)
        {
            desiredFocus = GetLockFocusPoint();
        }

        if (!focusInitialized)
        {
            focusPoint = desiredFocus;
            focusInitialized = true;
        }

        float blendedFocusSmoothTime = Mathf.Lerp(focusSmoothTime, aimFocusSmoothTime, aimBlend);
        focusPoint = Vector3.SmoothDamp(focusPoint, desiredFocus, ref focusVelocity, blendedFocusSmoothTime, Mathf.Infinity, deltaTime);
        Vector3 focusOffset = focusPoint - desiredFocus;
        if (focusOffset.sqrMagnitude > maxFocusLag * maxFocusLag)
        {
            focusPoint = desiredFocus + focusOffset.normalized * maxFocusLag;
            focusVelocity = Vector3.zero;
        }

        // Aim looks toward a point ahead of the player, but the camera position
        // must stay anchored to the player. Using focusPoint here makes the
        // camera drift forward while aiming and breaks the over-shoulder view.
        Vector3 desiredPosition = smoothedTargetPosition + blendedOffset;
        float blendedFollowSpeed = Mathf.Lerp(followSpeed, followSpeed * 1.35f, aimBlend);
        float speedRatio = followSpeed > 0.001f ? blendedFollowSpeed / followSpeed : 1f;
        float normalSmoothTime = Mathf.Max(0.01f, positionSmoothTime / Mathf.Max(0.1f, speedRatio));
        float smoothTime = Mathf.Lerp(normalSmoothTime, aimPositionSmoothTime, aimBlend);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, smoothTime, Mathf.Infinity, deltaTime);
        transform.rotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
    }

    private void UpdateLockedEnemyTracking(Vector3 smoothedTargetPosition, float deltaTime)
    {
        if (!HasValidLockTarget(smoothedTargetPosition))
        {
            lockedEnemy = null;
            return;
        }

        Vector3 direction = GetLockFocusPoint() - smoothedTargetPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float targetYaw = Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles.y;
        yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, lockYawSpeed * deltaTime);
    }

    private bool HasValidLockTarget(Vector3 fromPosition)
    {
        if (lockedEnemy == null || !lockedEnemy.IsAlive)
        {
            return false;
        }

        return FlatDistance(fromPosition, lockedEnemy.TargetPosition) <= lockBreakDistance;
    }

    private Vector3 GetLockFocusPoint()
    {
        return lockedEnemy != null ? lockedEnemy.TargetPosition + Vector3.up * lockLookHeight : focusPoint;
    }

    private Vector3 GetSmoothedTargetPosition(float deltaTime)
    {
        if (!targetYInitialized)
        {
            InitializeTargetHeight();
        }

        float targetY = target.position.y;
        smoothedTargetY = Mathf.SmoothDamp(smoothedTargetY, targetY, ref targetYVelocity, verticalFollowSmoothTime, Mathf.Infinity, deltaTime);
        float yOffset = smoothedTargetY - targetY;
        if (Mathf.Abs(yOffset) > maxVerticalLag)
        {
            smoothedTargetY = targetY + Mathf.Sign(yOffset) * maxVerticalLag;
            targetYVelocity = 0f;
        }

        return new Vector3(target.position.x, smoothedTargetY, target.position.z);
    }

    private void EnsureZoomInitialized()
    {
        if (currentZoomDistance > 0f && targetZoomDistance > 0f)
        {
            return;
        }

        InitializeZoomDistance();
    }

    private Vector3 GetZoomedOrbitOffset()
    {
        Vector3 baseOffset = offset.sqrMagnitude > 0.001f ? offset : new Vector3(0f, 2.2f, -4.5f);
        return baseOffset.normalized * currentZoomDistance;
    }

    private float GetBaseOrbitDistance()
    {
        return Mathf.Max(0.1f, offset.magnitude);
    }

    private void OnGUI()
    {
        if (showAimReticle && !IsGameplayInputBlocked() && IsAiming)
        {
            DrawCenterReticle();
        }

        if (showLockReticle && !IsGameplayInputBlocked() && HasLockedEnemy)
        {
            DrawCenterReticle();
        }

        if (showLockMarker && lockedEnemy != null && lockedEnemy.IsAlive)
        {
            DrawLockMarker();
        }
    }

    private void DrawCenterReticle()
    {
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        Color oldColor = GUI.color;
        GUI.color = reticleColor;
        GUI.DrawTexture(new Rect(centerX - 1f, centerY - 1f, 2f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - 9f, centerY, 5f, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX + 4f, centerY, 5f, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX, centerY - 9f, 1f, 5f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX, centerY + 4f, 1f, 5f), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void DrawLockMarker()
    {
        Camera cameraToUse = GetComponent<Camera>();
        if (cameraToUse == null)
        {
            cameraToUse = Camera.main;
        }

        if (cameraToUse == null)
        {
            return;
        }

        Vector3 screen = cameraToUse.WorldToScreenPoint(GetLockFocusPoint());
        if (screen.z <= 0f)
        {
            return;
        }

        float x = screen.x;
        float y = Screen.height - screen.y;
        float size = 34f;
        float corner = 9f;
        Color oldColor = GUI.color;
        GUI.color = lockMarkerColor;
        GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, corner, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, 2f, corner), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + size * 0.5f - corner, y - size * 0.5f, corner, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + size * 0.5f - 2f, y - size * 0.5f, 2f, corner), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - size * 0.5f, y + size * 0.5f - 2f, corner, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - size * 0.5f, y + size * 0.5f - corner, 2f, corner), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + size * 0.5f - corner, y + size * 0.5f - 2f, corner, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + size * 0.5f - 2f, y + size * 0.5f - corner, 2f, corner), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ApplyCursorState()
    {
        if (showCursorInPlay || rotateOnlyWhileRightMouseHeld || !lockCursorOnPlay)
        {
            UnlockCursor();
            return;
        }

        LockCursor();
    }

    private static bool IsGameplayInputBlocked()
    {
        return Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked;
    }

    private float NormalizePitch(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}

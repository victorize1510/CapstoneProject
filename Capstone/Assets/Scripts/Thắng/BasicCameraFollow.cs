using UnityEngine;

public class BasicCameraFollow : MonoBehaviour
{
    public Transform target;

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

    [Header("Scroll Zoom")]
    public bool enableScrollZoom = true;
    public float minZoomDistance = 3f;
    public float maxZoomDistance = 7f;
    public float zoomSpeed = 1.25f;
    public float zoomSmoothTime = 0.08f;

    [Header("Aim Camera")]
    public Vector3 aimOffset = new Vector3(0.75f, 1.8f, -2.75f);
    public float aimLookHeight = 1.45f;
    public float aimLookAhead = 10f;
    public int aimMouseButton = 1;
    public float aimBlendSpeed = 10f;
    public bool showAimReticle = true;
    public Color reticleColor = new Color(1f, 1f, 1f, 0.85f);

    private float yaw;
    private float pitch = 18f;
    private float aimBlend;
    private float currentZoomDistance;
    private float targetZoomDistance;
    private float zoomVelocity;
    private float smoothedTargetY;
    private float targetYVelocity;
    private Vector3 positionVelocity;
    private Vector3 focusVelocity;
    private Vector3 focusPoint;
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

    private void OnValidate()
    {
        minZoomDistance = Mathf.Max(0.5f, minZoomDistance);
        maxZoomDistance = Mathf.Max(minZoomDistance, maxZoomDistance);
        zoomSpeed = Mathf.Max(0.05f, zoomSpeed);
        zoomSmoothTime = Mathf.Max(0.01f, zoomSmoothTime);
        verticalFollowSmoothTime = Mathf.Max(0.01f, verticalFollowSmoothTime);
        maxVerticalLag = Mathf.Max(0.05f, maxVerticalLag);
    }

    private void Start()
    {
        TryFindTarget();
        InitializeAnglesFromCurrentCamera();
        InitializeFocusPoint();
        InitializeZoomDistance();
        InitializeTargetHeight();

        if (lockCursorOnPlay)
        {
            LockCursor();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && lockCursorOnPlay)
        {
            LockCursor();
        }
    }

    private void LateUpdate()
    {
        TryFindTarget();

        if (target == null)
        {
            return;
        }

        UpdateCursorLock();
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
        if (!lockCursorOnPlay)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(aimMouseButton))
        {
            LockCursor();
        }
    }

    private void UpdateMouseLook()
    {
        if (lockCursorOnPlay && Cursor.lockState != CursorLockMode.Locked)
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
        float targetBlend = Input.GetMouseButton(aimMouseButton) ? 1f : 0f;
        aimBlend = Mathf.MoveTowards(aimBlend, targetBlend, aimBlendSpeed * Time.deltaTime);
    }

    private void UpdateScrollZoom()
    {
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
        targetZoomDistance = Mathf.Clamp(targetZoomDistance, minZoomDistance, maxZoomDistance);
        currentZoomDistance = Mathf.SmoothDamp(currentZoomDistance, targetZoomDistance, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, deltaTime);

        Quaternion cameraYaw = Quaternion.Euler(0f, yaw, 0f);
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 normalOffset = cameraRotation * GetZoomedOrbitOffset();
        Vector3 shoulderOffset = cameraYaw * aimOffset;
        Vector3 blendedOffset = Vector3.Lerp(normalOffset, shoulderOffset, aimBlend);

        Vector3 normalFocus = smoothedTargetPosition + Vector3.up * lookHeight;
        Vector3 aimDirection = cameraRotation * Vector3.forward;
        Vector3 aimFocus = smoothedTargetPosition + Vector3.up * aimLookHeight + aimDirection * aimLookAhead;
        Vector3 desiredFocus = Vector3.Lerp(normalFocus, aimFocus, aimBlend);
        if (!focusInitialized)
        {
            focusPoint = desiredFocus;
            focusInitialized = true;
        }

        focusPoint = Vector3.SmoothDamp(focusPoint, desiredFocus, ref focusVelocity, focusSmoothTime, Mathf.Infinity, deltaTime);
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
        float smoothTime = Mathf.Max(0.01f, positionSmoothTime / Mathf.Max(0.1f, speedRatio));

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, smoothTime, Mathf.Infinity, deltaTime);
        transform.rotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
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
        if (!showAimReticle || aimBlend < 0.55f)
        {
            return;
        }

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

    private float NormalizePitch(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}

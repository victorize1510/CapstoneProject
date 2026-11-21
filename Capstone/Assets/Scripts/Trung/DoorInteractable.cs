using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.UI;

// Gắn lên cánh cửa.
// Player tới gần + nhìn về phía cửa -> hiện "[F] Mở cửa" / "[F] Đóng cửa", bấm phím để đổi.
// Hai cách chạy:
//   RotateByCode: không cần Animator, code tự xoay cửa quanh bản lề (dùng cho cửa 1 cánh).
//   UseAnimator : dùng Animator có Bool DoorOpen / DoorClose (vd. cửa 2 cánh của asset).
[DisallowMultipleComponent]
public class DoorInteractable : MonoBehaviour
{
    public enum MotionMode { RotateByCode, UseAnimator }
    public enum HingeSide { Left, Right }

    [SerializeField] MotionMode motionMode = MotionMode.RotateByCode;
    [Tooltip("Bật nếu cửa này lúc vào game đang mở sẵn")]
    [SerializeField] bool startOpen;

    [Header("RotateByCode (cửa 1 cánh)")]
    [Tooltip("Bản lề nằm ở mép nào của cánh cửa. Xem chấm vàng trong Scene khi chọn cửa.")]
    [SerializeField] HingeSide hingeSide = HingeSide.Left;
    [SerializeField, Range(10f, 170f)] float openAngle = 95f;
    [Tooltip("Tốc độ xoay (độ / giây)")]
    [SerializeField, Min(10f)] float rotateSpeed = 160f;
    [Tooltip("Mở về phía ngược với Player để cửa không đập vào người")]
    [SerializeField] bool openAwayFromPlayer = true;

    [Header("UseAnimator (cửa có sẵn animation)")]
    [SerializeField] Animator animator;
    [SerializeField] string openParameter = "DoorOpen";
    [SerializeField] string closeParameter = "DoorClose";

    [Header("Tương tác")]
    [Tooltip("Không dùng E vì E đang là phím Nhặt đồ")]
    [SerializeField] KeyCode interactKey = KeyCode.F;
    [SerializeField, Min(0.5f)] float interactDistance = 2.5f;
    [Tooltip("Góc nhìn (độ) phía trước Player để được tương tác")]
    [SerializeField, Range(10f, 360f)] float viewAngle = 140f;
    [Tooltip("Chặn bấm liên tục")]
    [SerializeField, Min(0f)] float toggleCooldown = 0.4f;

    [Header("Tự đóng khi đi xa (tùy chọn)")]
    [SerializeField] bool autoCloseWhenLeave;
    [SerializeField, Min(0f)] float autoCloseDelay = 2f;

    [Header("Tooltip")]
    [SerializeField] string openText = "Mở cửa";
    [SerializeField] string closeText = "Đóng cửa";
    [Tooltip("Độ cao tooltip so với tâm cửa")]
    [SerializeField] float promptHeight = 0.6f;

    [Header("Âm thanh (tùy chọn)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip openSound;
    [SerializeField] AudioClip closeSound;

    [Header("Không đẩy Player khi cửa đang chuyển động")]
    [Tooltip("Lúc cửa đang xoay: xuyên qua Player. Lúc đứng yên: chặn Player như tường.")]
    [SerializeField] bool passThroughPlayerWhileMoving = true;
    [Tooltip("Chỉ dùng cho UseAnimator: thời gian animation mở / đóng (giây)")]
    [SerializeField, Min(0.1f)] float animatorMotionDuration = 1f;

    [Header("Viền sáng khi tới gần")]
    [Tooltip("Để trống sẽ tự thêm InteractionHighlight lên cửa")]
    [SerializeField] InteractionHighlight highlight;
    [SerializeField] bool useHighlight = true;

    static DoorInteractable focused;   // Chỉ cửa gần nhất được hiện tooltip / nhận phím.
    static Transform player;
    static float nextPlayerLookup;

    Collider doorCollider;
    bool isOpen;
    bool playerWasNear;
    float lastDistance;
    float nextToggleTime;
    float leftTime = -1f;

    // Xuyên qua Player khi cửa chuyển động
    static CharacterController playerController;
    static readonly Collider[] overlapHits = new Collider[16];
    bool ignoringPlayer;
    float motionEndTime;

    // RotateByCode
    bool closedPoseCaptured;
    Vector3 closedLocalPosition;
    Quaternion closedLocalRotation;
    Vector3 hingeInParent;
    float currentAngle;
    float targetAngle;

    public bool IsOpen => isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { focused = null; player = null; playerController = null; nextPlayerLookup = 0f; }

    void Awake()
    {
        doorCollider = GetComponent<Collider>();
        if (animator == null) animator = GetComponent<Animator>();
        if (useHighlight && highlight == null)
        {
            highlight = GetComponent<InteractionHighlight>();
            if (highlight == null) highlight = gameObject.AddComponent<InteractionHighlight>();
        }

        if (motionMode == MotionMode.RotateByCode)
        {
            // Animator của asset sẽ ghi đè rotation mỗi frame -> phải tắt.
            if (animator != null && animator.enabled)
            {
                Debug.LogWarning($"{name}: đang dùng RotateByCode nên tắt Animator trên cửa. Nên xóa component Animator.", this);
                animator.enabled = false;
            }
            CaptureClosedPose();
        }
    }

    void Start()
    {
        if (startOpen) SetOpen(true, false, true);
    }

    void OnDisable()
    {
        if (focused == this) focused = null;
        DoorPrompt.Hide(this);
        if (highlight != null) highlight.SetHighlighted(false);
        SetPlayerCollisionIgnored(false);
    }

    void Update()
    {
        ResolvePlayer();
        bool near = player != null && IsPlayerInRange(out lastDistance);

        if (near)
        {
            playerWasNear = true;
            leftTime = -1f;
            if (focused == null || focused == this || !focused.isActiveAndEnabled
                || lastDistance < focused.lastDistance)
                focused = this;
        }
        else
        {
            if (focused == this) focused = null;
            UpdateAutoClose();
        }

        if (focused == this && !InventoryInputController.GameplayInputBlocked
            && Input.GetKeyDown(interactKey) && Time.time >= nextToggleTime)
            SetOpen(!isOpen, true, false);

        if (motionMode == MotionMode.RotateByCode && !Mathf.Approximately(currentAngle, targetAngle))
        {
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
            ApplyAngle(currentAngle);
        }

        UpdatePlayerPassThrough();
    }

    // ---------- Xuyên qua Player khi chuyển động ----------

    bool IsMoving => motionMode == MotionMode.RotateByCode
        ? !Mathf.Approximately(currentAngle, targetAngle)
        : Time.time < motionEndTime;

    void BeginPassThrough()
    {
        if (!passThroughPlayerWhileMoving) return;
        motionEndTime = Time.time + animatorMotionDuration;
        SetPlayerCollisionIgnored(true);
    }

    void UpdatePlayerPassThrough()
    {
        if (!ignoringPlayer || IsMoving) return;
        // Cửa đã đứng yên. Chỉ bật va chạm lại khi Player không còn kẹt trong cửa,
        // nếu không CharacterController sẽ bị bật văng ra.
        if (!IsPlayerOverlappingDoor()) SetPlayerCollisionIgnored(false);
    }

    void SetPlayerCollisionIgnored(bool ignore)
    {
        if (ignoringPlayer == ignore) return;
        if (doorCollider == null || !ResolvePlayerController())
        {
            ignoringPlayer = false;
            return;
        }
        Physics.IgnoreCollision(doorCollider, playerController, ignore);
        ignoringPlayer = ignore;
    }

    bool IsPlayerOverlappingDoor()
    {
        if (doorCollider == null || !ResolvePlayerController()) return false;

        CharacterController cc = playerController;
        Transform t = cc.transform;
        float scale = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.z));
        float radius = cc.radius * scale + cc.skinWidth;
        float halfHeight = Mathf.Max(0f, cc.height * Mathf.Abs(t.lossyScale.y) * 0.5f - radius);
        Vector3 center = t.TransformPoint(cc.center);
        Vector3 top = center + Vector3.up * halfHeight;
        Vector3 bottom = center - Vector3.up * halfHeight;

        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlapHits,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (overlapHits[i] == doorCollider) return true;
        return false;
    }

    static bool ResolvePlayerController()
    {
        if (playerController != null) return true;
        ResolvePlayer();
        if (player != null) playerController = player.GetComponent<CharacterController>();
        return playerController != null;
    }

    void LateUpdate()
    {
        bool showHint = focused == this
            && !InventoryInputController.GameplayInputBlocked
            && Capstone.Game.SettingsSystem.GameSettingsService.InteractionPromptsVisible;
        if (useHighlight && highlight != null) highlight.SetHighlighted(showHint);

        if (focused != this || !showHint) { DoorPrompt.Hide(this); return; }

        Vector3 anchor = doorCollider != null ? doorCollider.bounds.center : transform.position;
        string action = isOpen ? closeText : openText;
        DoorPrompt.Show(this, anchor + Vector3.up * promptHeight, $"<color=#FFD54A><b>[{interactKey}]</b></color> {action}");
    }

    public void SetOpen(bool open, bool playSound, bool instant)
    {
        isOpen = open;
        nextToggleTime = Time.time + toggleCooldown;
        if (!instant) BeginPassThrough();

        if (motionMode == MotionMode.UseAnimator)
        {
            SetBool(openParameter, open);
            SetBool(closeParameter, !open);
        }
        else
        {
            targetAngle = open ? ChooseOpenAngle() : 0f;
            if (instant) { currentAngle = targetAngle; ApplyAngle(currentAngle); }
        }

        if (playSound && audioSource != null)
        {
            AudioClip clip = open ? openSound : closeSound;
            if (clip != null) audioSource.PlayOneShot(clip);
        }
    }

    // ---------- RotateByCode ----------

    void CaptureClosedPose()
    {
        if (closedPoseCaptured) return;
        closedLocalPosition = transform.localPosition;
        closedLocalRotation = transform.localRotation;
        hingeInParent = closedLocalPosition
            + closedLocalRotation * Vector3.Scale(GetHingeLocalPoint(), transform.localScale);
        closedPoseCaptured = true;
    }

    // Tìm mép bản lề từ kích thước mesh: trục rộng nhất trong X/Z là chiều ngang cánh cửa.
    Vector3 GetHingeLocalPoint()
    {
        Bounds bounds = GetLocalBounds();
        bool widthOnX = bounds.size.x >= bounds.size.z;
        Vector3 hinge = bounds.center;
        if (widthOnX) hinge.x = hingeSide == HingeSide.Left ? bounds.min.x : bounds.max.x;
        else hinge.z = hingeSide == HingeSide.Left ? bounds.min.z : bounds.max.z;
        return hinge;
    }

    Bounds GetLocalBounds()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null) return meshFilter.sharedMesh.bounds;
        var box = GetComponent<BoxCollider>();
        if (box != null) return new Bounds(box.center, box.size);
        return new Bounds(Vector3.zero, Vector3.one);
    }

    void ApplyAngle(float angle)
    {
        // Xoay quanh trục dọc của cửa, tâm là bản lề (trong không gian của object cha).
        Quaternion swing = closedLocalRotation * Quaternion.AngleAxis(angle, Vector3.up)
            * Quaternion.Inverse(closedLocalRotation);
        transform.localPosition = hingeInParent + swing * (closedLocalPosition - hingeInParent);
        transform.localRotation = swing * closedLocalRotation;
    }

    float ChooseOpenAngle()
    {
        if (!openAwayFromPlayer || player == null) return openAngle;
        // Thử 2 hướng, chọn hướng mà tâm cửa sau khi mở xa Player hơn.
        float plus = DistanceToPlayerAt(openAngle);
        float minus = DistanceToPlayerAt(-openAngle);
        return plus >= minus ? openAngle : -openAngle;
    }

    float DistanceToPlayerAt(float angle)
    {
        Quaternion swing = closedLocalRotation * Quaternion.AngleAxis(angle, Vector3.up)
            * Quaternion.Inverse(closedLocalRotation);
        Vector3 centerClosed = closedLocalPosition
            + closedLocalRotation * Vector3.Scale(GetLocalBounds().center, transform.localScale);
        Vector3 centerOpen = hingeInParent + swing * (centerClosed - hingeInParent);
        Vector3 world = transform.parent != null ? transform.parent.TransformPoint(centerOpen) : centerOpen;
        world.y = player.position.y;
        return Vector3.Distance(world, player.position);
    }

    // ---------- Chung ----------

    bool IsPlayerInRange(out float distance)
    {
        Vector3 playerPos = player.position;
        Vector3 target = doorCollider != null && doorCollider.enabled
            ? doorCollider.ClosestPoint(playerPos + Vector3.up)
            : transform.position;

        Vector3 toDoor = target - playerPos;
        toDoor.y = 0f;
        distance = toDoor.magnitude;
        if (distance > interactDistance) return false;
        if (distance < 0.3f || viewAngle >= 360f) return true;

        Vector3 forward = player.forward;
        forward.y = 0f;
        return Vector3.Angle(forward, toDoor) <= viewAngle * 0.5f;
    }

    void UpdateAutoClose()
    {
        if (!autoCloseWhenLeave || !isOpen || !playerWasNear) return;
        if (leftTime < 0f) leftTime = Time.time;
        if (Time.time - leftTime < autoCloseDelay) return;
        playerWasNear = false;
        SetOpen(false, true, false);
    }

    static void ResolvePlayer()
    {
        if (player != null || Time.unscaledTime < nextPlayerLookup) return;
        nextPlayerLookup = Time.unscaledTime + 1f;
        var movement = FindFirstObjectByType<BasicPlayerMovement>();
        if (movement != null) player = movement.transform;
    }

    void SetBool(string parameter, bool value)
    {
        if (animator == null || string.IsNullOrEmpty(parameter)) return;
        foreach (var p in animator.parameters)
        {
            if (p.name == parameter && p.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(parameter, value);
                return;
            }
        }
        Debug.LogWarning($"{name}: Animator không có Bool '{parameter}'.", this);
    }

    // Chấm vàng = vị trí bản lề. Nếu nằm sai mép, đổi Hinge Side.
    void OnDrawGizmosSelected()
    {
        if (motionMode != MotionMode.RotateByCode) return;
        Vector3 hingeWorld = Application.isPlaying && closedPoseCaptured
            ? (transform.parent != null ? transform.parent.TransformPoint(hingeInParent) : hingeInParent)
            : transform.TransformPoint(GetHingeLocalPoint());
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(hingeWorld, 0.08f);
        Gizmos.DrawLine(hingeWorld - transform.up, hingeWorld + transform.up);
    }
}

// Một tooltip dùng chung cho mọi vật tương tác (cửa, bàn bóng...).
// Có "owner" để vật này không tắt nhầm tooltip của vật khác.
static class DoorPrompt
{
    static Canvas canvas;
    static RectTransform panel;
    static Text label;
    static object owner;

    public static void Show(object who, Vector3 worldPosition, string text)
    {
        Camera cam = Camera.main;
        if (cam == null) { Hide(who); return; }

        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f) { Hide(who); return; }   // Ở sau lưng camera.
        ShowScreen(who, screen, text);
    }

    public static void ShowScreen(object who, Vector2 screenPosition, string text)
    {
        Ensure();
        owner = who;
        if (label.text != text) label.text = text;
        panel.position = new Vector3(screenPosition.x, screenPosition.y, 0f);
        if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
    }

    // Chỉ tắt nếu tooltip đang thuộc về "who".
    public static void Hide(object who)
    {
        if (!ReferenceEquals(owner, who)) return;
        owner = null;
        if (canvas != null && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
    }

    static void Ensure()
    {
        if (canvas != null) return;

        var root = new GameObject("InteractionPromptCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panelObject = new GameObject("Prompt", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(root.transform, false);
        panel = (RectTransform)panelObject.transform;
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchorMin = panel.anchorMax = Vector2.zero;

        var background = panelObject.GetComponent<Image>();
        background.color = new Color(0.05f, 0.08f, 0.12f, 0.78f);
        background.raycastTarget = false;

        var layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 8, 8);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var fitter = panelObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(panel, false);
        label = labelObject.GetComponent<Text>();
        // Font của menu (NotoSerif) có đủ dấu tiếng Việt.
        label.font = Capstone.Game.UISystem.MenuTypography.Font
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.supportRichText = true;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;

        root.SetActive(false);
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.Events;

// Bàn chọn bóng khởi đầu.
// Tới gần -> "[F] Chọn bóng". Bấm F -> camera lia vào bàn, ẩn Player.
// A/D/W/S (hoặc phím mũi tên, hoặc rê chuột) đổi bóng, bóng đang chọn sáng viền.
// Enter / Space / click -> xác nhận (gọi onBallConfirmed). Esc / Backspace -> thoát.
[DisallowMultipleComponent]
public class StarterBallTable : MonoBehaviour
{
    [Serializable]
    public class BallSlot
    {
        public Transform ball;
        public string displayName = "Bóng";
    }

    [Header("Bóng trên bàn")]
    [SerializeField] List<BallSlot> balls = new List<BallSlot>();

    [Header("Camera")]
    [Tooltip("Vị trí + góc camera khi chọn bóng. Chuột phải component -> 'Đặt Camera Point theo Scene View'")]
    [SerializeField] Transform cameraPoint;
    [SerializeField, Min(0.05f)] float cameraMoveTime = 0.6f;
    [SerializeField] bool hidePlayerWhileSelecting = true;

    [Header("Tương tác")]
    [SerializeField] KeyCode interactKey = KeyCode.F;
    [Tooltip("Tâm vùng tương tác. Để trống = tự lấy tâm của các quả bóng")]
    [SerializeField] Transform interactCenter;
    [SerializeField, Min(0.5f)] float interactDistance = 2.5f;
    [SerializeField] string enterText = "Chọn bóng";

    [Header("Xác nhận (gắn hành động sau)")]
    public UnityEvent<int> onBallConfirmed = new UnityEvent<int>();

    enum State { Idle, Entering, Selecting, Exiting }

    State state = State.Idle;
    int selected = -1;
    Transform player;
    float nextPlayerLookup;
    Camera cam;
    Vector3 savedCameraPosition;
    Quaternion savedCameraRotation;
    readonly List<Behaviour> disabledCameraDrivers = new List<Behaviour>();
    readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    GameMenuController menu;
    bool blockedWithMenu;
    Vector3 lastMousePosition;

    public bool IsSelecting => state != State.Idle;
    public int SelectedIndex => selected;

    void Reset() => FindChildBalls();

    void OnValidate()
    {
        // Camera Point phải là object riêng (CameraPoint), không phải chính bàn này.
        if (cameraPoint == transform)
        {
            Debug.LogWarning($"{name}: Camera Point đang là chính StarterBallTable. "
                + "Xóa ô này rồi chuột phải component -> 'Đặt Camera Point theo Scene View'.", this);
            cameraPoint = null;
        }
    }

    void Update()
    {
        if (state == State.Idle) UpdateIdle();
        else if (state == State.Selecting) UpdateSelecting();
    }

    void OnDisable()
    {
        DoorPrompt.Hide(this);
        if (state == State.Idle) return;
        StopAllCoroutines();
        SetSelected(-1);
        if (cam != null) cam.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
        RestoreCameraDrivers();
        SetPlayerHidden(false);
        BlockGameplay(false);
        state = State.Idle;
    }

    // ---------- Ngoài chế độ chọn ----------

    void UpdateIdle()
    {
        ResolvePlayer();
        bool near = player != null && balls.Count > 0
            && !InventoryInputController.GameplayInputBlocked && IsPlayerNear();
        if (!near) { DoorPrompt.Hide(this); return; }

        if (Capstone.Game.SettingsSystem.GameSettingsService.InteractionPromptsVisible)
            DoorPrompt.Show(this, TableAnchor(), $"<color=#FFD54A><b>[{interactKey}]</b></color> {enterText}");
        else DoorPrompt.Hide(this);

        if (Input.GetKeyDown(interactKey)) StartCoroutine(EnterRoutine());
    }

    IEnumerator EnterRoutine()
    {
        state = State.Entering;
        DoorPrompt.Hide(this);
        BlockGameplay(true);

        cam = Camera.main;
        if (cam != null)
        {
            savedCameraPosition = cam.transform.position;
            savedCameraRotation = cam.transform.rotation;
            DisableCameraDrivers();
        }
        if (hidePlayerWhileSelecting) SetPlayerHidden(true);

        if (cam != null && cameraPoint != null)
            yield return MoveCamera(cameraPoint.position, cameraPoint.rotation);
        else if (cameraPoint == null)
            Debug.LogWarning($"{name}: chưa gán Camera Point, camera sẽ đứng yên.", this);

        state = State.Selecting;
        lastMousePosition = Input.mousePosition;
        SetSelected(FindBallNearestScreenCenter());
    }

    IEnumerator ExitRoutine()
    {
        state = State.Exiting;
        SetSelected(-1);
        DoorPrompt.Hide(this);
        if (menu != null) menu.MarkShortcutHandled(KeyCode.Escape);

        if (cam != null) yield return MoveCamera(savedCameraPosition, savedCameraRotation);
        RestoreCameraDrivers();
        SetPlayerHidden(false);

        // Đợi 1 frame để phím Esc không mở luôn màn Cài đặt.
        yield return null;
        BlockGameplay(false);
        state = State.Idle;
    }

    // ---------- Trong chế độ chọn ----------

    void UpdateSelecting()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            StartCoroutine(ExitRoutine());
            return;
        }

        Vector2 direction = Vector2.zero;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) direction = Vector2.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) direction = Vector2.right;
        else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) direction = Vector2.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) direction = Vector2.down;
        if (direction != Vector2.zero) Navigate(direction);

        int hovered = UpdateMouseHover();

        bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space) || (Input.GetMouseButtonDown(0) && hovered >= 0);
        if (confirm && selected >= 0) Confirm();

        UpdateSelectionPrompt();
    }

    void Navigate(Vector2 direction)
    {
        if (cam == null || balls.Count == 0) return;
        if (selected < 0) { SetSelected(FindBallNearestScreenCenter()); return; }

        Vector2 from = ScreenPosition(selected);
        int best = -1;
        float bestScore = float.MaxValue;
        for (int i = 0; i < balls.Count; i++)
        {
            if (i == selected || !IsValid(i)) continue;
            Vector2 delta = ScreenPosition(i) - from;
            if (delta.sqrMagnitude < 1f) continue;
            float alignment = Vector2.Dot(delta.normalized, direction);
            if (alignment < 0.3f) continue;          // Không nằm theo hướng bấm.
            float score = delta.magnitude / alignment;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        if (best >= 0) SetSelected(best);
    }

    int UpdateMouseHover()
    {
        if (cam == null) return -1;
        Vector3 mouse = Input.mousePosition;
        bool moved = (mouse - lastMousePosition).sqrMagnitude > 4f;
        lastMousePosition = mouse;

        int hovered = -1;
        if (Physics.Raycast(cam.ScreenPointToRay(mouse), out RaycastHit hit, 50f, Physics.AllLayers, QueryTriggerInteraction.Collide))
        {
            for (int i = 0; i < balls.Count; i++)
                if (IsValid(i) && hit.transform.IsChildOf(balls[i].ball)) { hovered = i; break; }
        }
        // Chỉ đổi theo chuột khi chuột thật sự di chuyển, để không đè lên phím A/D.
        if (hovered >= 0 && moved) SetSelected(hovered);
        return hovered;
    }

    void Confirm()
    {
        Debug.Log($"[StarterBall] Đã chọn bóng {selected}: {balls[selected].displayName}");
        onBallConfirmed?.Invoke(selected);
    }

    void UpdateSelectionPrompt()
    {
        if (selected < 0) { DoorPrompt.Hide(this); return; }
        string ballName = balls[selected].displayName;
        string hint = "<size=18><color=#FFD54A>[Enter]</color> Chọn    "
            + "<color=#FFD54A>[Esc]</color> Thoát</size>";
        DoorPrompt.Show(this, BallTop(selected) + Vector3.up * 0.15f, $"<b>{ballName}</b>\n{hint}");
    }

    void SetSelected(int index)
    {
        if (selected == index) return;
        if (IsValid(selected)) GetHighlight(selected)?.SetHighlighted(false);
        selected = IsValid(index) ? index : -1;
        if (selected >= 0) GetHighlight(selected)?.SetHighlighted(true);
    }

    // ---------- Camera / Player / Input ----------

    IEnumerator MoveCamera(Vector3 targetPosition, Quaternion targetRotation)
    {
        Vector3 startPosition = cam.transform.position;
        Quaternion startRotation = cam.transform.rotation;
        float time = 0f;
        while (time < cameraMoveTime)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, time / cameraMoveTime);
            cam.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, targetPosition, t),
                Quaternion.Slerp(startRotation, targetRotation, t));
            yield return null;
        }
        cam.transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    void DisableCameraDrivers()
    {
        disabledCameraDrivers.Clear();
        // Camera follow của game (và Cinemachine nếu có) sẽ kéo camera về Player -> tắt tạm.
        TryDisable(cam.GetComponent<BasicCameraFollow>());
        TryDisable(cam.GetComponent("CinemachineBrain") as Behaviour);
    }

    void TryDisable(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        behaviour.enabled = false;
        disabledCameraDrivers.Add(behaviour);
    }

    void RestoreCameraDrivers()
    {
        foreach (Behaviour behaviour in disabledCameraDrivers)
            if (behaviour != null) behaviour.enabled = true;
        disabledCameraDrivers.Clear();
    }

    void SetPlayerHidden(bool hidden)
    {
        if (hidden)
        {
            if (player == null) return;
            hiddenRenderers.Clear();
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                renderer.enabled = false;
                hiddenRenderers.Add(renderer);
            }
            return;
        }

        foreach (Renderer renderer in hiddenRenderers)
            if (renderer != null) renderer.enabled = true;
        hiddenRenderers.Clear();
    }

    // Dùng chế độ Dialogue của GameMenuController: khóa di chuyển, ẩn HUD,
    // và Esc không mở màn Cài đặt trong lúc chọn bóng.
    void BlockGameplay(bool block)
    {
        if (block)
        {
            menu = FindFirstObjectByType<GameMenuController>();
            blockedWithMenu = menu != null;
            if (blockedWithMenu) menu.SetDialogueOpen(this, true);
            else InventoryInputController.SetExternalGameplayInputBlocked(this, true);
            return;
        }

        if (blockedWithMenu && menu != null) menu.SetDialogueOpen(this, false);
        else InventoryInputController.SetExternalGameplayInputBlocked(this, false);
        blockedWithMenu = false;
    }

    // ---------- Tiện ích ----------

    void ResolvePlayer()
    {
        if (player != null || Time.unscaledTime < nextPlayerLookup) return;
        nextPlayerLookup = Time.unscaledTime + 1f;
        var movement = FindFirstObjectByType<BasicPlayerMovement>();
        if (movement != null) player = movement.transform;
    }

    Vector3 InteractionCenter()
    {
        if (interactCenter != null) return interactCenter.position;
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < balls.Count; i++)
        {
            if (!IsValid(i)) continue;
            sum += balls[i].ball.position;
            count++;
        }
        return count > 0 ? sum / count : transform.position;
    }

    bool IsPlayerNear()
    {
        Vector3 center = InteractionCenter();
        Vector3 delta = center - player.position;
        delta.y = 0f;
        return delta.magnitude <= interactDistance;
    }

    bool IsValid(int index) => index >= 0 && index < balls.Count && balls[index] != null && balls[index].ball != null;

    InteractionHighlight GetHighlight(int index)
    {
        Transform ball = balls[index].ball;
        var highlight = ball.GetComponentInChildren<InteractionHighlight>(true);
        if (highlight == null) highlight = ball.gameObject.AddComponent<InteractionHighlight>();
        return highlight;
    }

    Vector2 ScreenPosition(int index) => cam.WorldToScreenPoint(balls[index].ball.position);

    Vector3 BallTop(int index)
    {
        Transform ball = balls[index].ball;
        var renderer = ball.GetComponentInChildren<Renderer>();
        return renderer != null
            ? new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z)
            : ball.position + Vector3.up * 0.3f;
    }

    Vector3 TableAnchor()
    {
        Vector3 sum = Vector3.zero;
        float top = float.MinValue;
        int count = 0;
        for (int i = 0; i < balls.Count; i++)
        {
            if (!IsValid(i)) continue;
            Vector3 ballTop = BallTop(i);
            sum += ballTop;
            top = Mathf.Max(top, ballTop.y);
            count++;
        }
        if (count == 0) return transform.position + Vector3.up;
        Vector3 center = sum / count;
        center.y = top + 0.3f;
        return center;
    }

    int FindBallNearestScreenCenter()
    {
        if (cam == null) return IsValid(0) ? 0 : -1;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < balls.Count; i++)
        {
            if (!IsValid(i)) continue;
            float distance = (ScreenPosition(i) - center).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; best = i; }
        }
        return best;
    }

    [ContextMenu("Tự tìm bóng con")]
    void FindChildBalls()
    {
        balls.Clear();
        int number = 1;
        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<Collider>() == null) continue;
            if (cameraPoint != null && child == cameraPoint) continue;
            balls.Add(new BallSlot { ball = child, displayName = "Bóng " + number++ });
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Đặt Camera Point theo Scene View")]
    void SetCameraPointFromSceneView()
    {
        var sceneView = UnityEditor.SceneView.lastActiveSceneView;
        if (sceneView == null) { Debug.LogWarning("Không tìm thấy cửa sổ Scene."); return; }

        if (cameraPoint == null)
        {
            var point = new GameObject("CameraPoint");
            UnityEditor.Undo.RegisterCreatedObjectUndo(point, "Create CameraPoint");
            point.transform.SetParent(transform, false);
            cameraPoint = point.transform;
        }
        UnityEditor.Undo.RecordObject(cameraPoint, "Move CameraPoint");
        Transform view = sceneView.camera.transform;
        cameraPoint.SetPositionAndRotation(view.position, view.rotation);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(InteractionCenter(), interactDistance);
        if (cameraPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(cameraPoint.position, 0.08f);
            Gizmos.DrawRay(cameraPoint.position, cameraPoint.forward * 1.5f);
        }
    }
}
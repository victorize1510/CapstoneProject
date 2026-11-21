using System;
using System.Collections;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Cutscene trưởng làng (kiểu Pokémon). Gắn lên một object có Box Collider (Is Trigger) đặt trước cửa nhà Player.
//   1. Player bước ra cửa (đi vào vùng trigger) -> khóa điều khiển, camera lia mượt lên góc cutscene.
//   2. Trưởng làng đi theo đường đã đặt tới trước mặt Player.
//   3. Hai người quay mặt vào nhau (camera đổi sang góc nói chuyện nếu có), hội thoại trên DialogueBox.
//   4. Nói xong: trưởng làng đi về tới nhà, camera đợi tới khi ông về hẳn.
//   5. Camera lia mượt về lại camera của game, người chơi điều khiển tiếp.
// Không cần Timeline: hội thoại phải chờ người chơi bấm Space, script làm việc này gọn hơn.
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class VillageChiefCutscene : MonoBehaviour
{
    public enum Speaker { Npc, Player }

    [Serializable]
    public class Line
    {
        public Speaker speaker = Speaker.Npc;
        [TextArea(2, 3)] public string text;
    }

    [Header("Trưởng làng")]
    [SerializeField] Transform npc;
    [SerializeField] string npcName = "Trưởng làng";
    [SerializeField] Sprite npcPortrait;
    [Tooltip("Để trống = tự tìm Animator trong NPC. Không có animation thì NPC vẫn trượt đi được")]
    [SerializeField] Animator npcAnimator;
    [Tooltip("Tham số Float tốc độ trong Animator của NPC (để chuyển Idle / Walk). Để trống = không dùng")]
    [SerializeField] string npcSpeedParameter = "Speed";
    [SerializeField, Min(0.2f)] float npcWalkSpeed = 1.4f;
    [Tooltip("Các điểm NPC đi qua, từ nhà trưởng làng tới chỗ đứng nói chuyện. Điểm CUỐI = chỗ NPC dừng lại")]
    [SerializeField] List<Transform> approachPath = new List<Transform>();
    [Tooltip("Đi về: đi ngược lại đường cũ rồi về đúng chỗ đứng ban đầu")]
    [SerializeField] bool returnHomeAfterTalk = true;
    [Tooltip("Dính chân NPC xuống mặt đất khi đi qua dốc / bậc thềm")]
    [SerializeField] bool snapNpcToGround = true;

    [Header("Player")]
    [Tooltip("Chỗ Player đứng khi cutscene bắt đầu (tùy chọn). Để trống = giữ nguyên chỗ Player đang đứng")]
    [SerializeField] Transform playerStandPoint;
    [SerializeField] string playerIdleState = "IdleNeutral";
    [Tooltip("Ảnh chân dung Player (tùy chọn)")]
    [SerializeField] Sprite playerPortrait;

    [Header("Camera")]
    [Tooltip("Góc camera lúc NPC đi tới. Camera đứng yên và xoay theo NPC")]
    [SerializeField] Transform approachCamera;
    [Tooltip("Góc camera lúc nói chuyện, thấy cả 2 người")]
    [SerializeField] Transform talkCamera;
    [Tooltip("Thời gian camera lia từ camera game sang góc cutscene (và lia về)")]
    [SerializeField, Min(0.1f)] float cameraBlendTime = 1.2f;
    [Tooltip("Camera xoay nhẹ theo NPC khi NPC đang đi")]
    [SerializeField] bool cameraLooksAtNpc = true;
    [Tooltip("Tắt (kiểu Pokémon): camera lia mượt vào / ra. Bật: tối màn hình lúc bắt đầu và kết thúc")]
    [SerializeField] bool useFadeTransitions;

    [Header("Lúc NPC về nhà")]
    [Tooltip("Đợi NPC đi về tới nhà rồi mới trả camera cho Player")]
    [SerializeField] bool waitUntilNpcHome = true;
    [Tooltip("Đợi tối đa bao lâu (giây), phòng khi đường về quá dài")]
    [SerializeField, Min(1f)] float maxWaitNpcHome = 15f;
    [Tooltip("Chỉ dùng khi tắt Wait Until Npc Home: nhìn theo NPC bao lâu rồi vào chơi")]
    [SerializeField, Min(0f)] float watchNpcLeaveTime = 2.5f;

    [Header("Hội thoại  ({name} = tên Player)")]
    [SerializeField] List<Line> lines = new List<Line>
    {
        new Line { speaker = Speaker.Npc, text = "A, {name}! Cháu dậy rồi đấy à? Ông đang định qua gọi cháu đây." },
        new Line { speaker = Speaker.Player, text = "Chào ông trưởng làng! Hôm nay ông có chuyện gì vậy ạ?" },
        new Line { speaker = Speaker.Npc, text = "Hôm nay là ngày cháu đủ tuổi trở thành Nhà Huấn Luyện rồi." },
        new Line { speaker = Speaker.Npc, text = "Ông đã chuẩn bị sẵn mấy quả bóng thú cho cháu chọn người bạn đồng hành đầu tiên." },
        new Line { speaker = Speaker.Player, text = "Thật ạ?! Cháu chờ ngày này lâu lắm rồi!" },
        new Line { speaker = Speaker.Npc, text = "Ha ha, ông biết mà. Ghé qua nhà ông nhé, bóng thú để trên bàn đấy." },
        new Line { speaker = Speaker.Npc, text = "Ông về trước chuẩn bị. Đừng để ông chờ lâu nhé!" }
    };
    [SerializeField, Min(5f)] float typingSpeed = 40f;
    [Tooltip("Để trống = dùng khung hội thoại tự chỉnh trong DialogueCanvas (nếu có), không thì tạo khung mặc định")]
    [SerializeField] DialogueBoxUI dialogueBox;

    [Header("Khác")]
    [SerializeField] bool playOnlyOnce = true;
    [Tooltip("Gọi khi cutscene xong. Ví dụ: bật bàn chọn bóng ở nhà trưởng làng, thêm nhiệm vụ...")]
    public UnityEvent onFinished = new UnityEvent();

    bool played;
    bool running;
    Transform player;
    Camera cam;
    GameMenuController menu;
    bool blockedWithMenu;
    readonly List<Behaviour> disabledCameraDrivers = new List<Behaviour>();
    Behaviour frozenMovement;
    Animator playerAnimator;
    int playerIdleHash;
    Vector3 npcHomePosition;
    Quaternion npcHomeRotation;
    Behaviour npcAgent;
    Transform cameraTarget;     // Camera xoay theo object này (null = đứng yên).
    Canvas fadeCanvas;
    Image fadeImage;
    int npcSpeedHash;

    public bool IsPlaying => running;

    void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3f, 2f, 2f);
        box.center = new Vector3(0f, 1f, 0f);
    }

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (npc != null)
        {
            npcHomePosition = npc.position;
            npcHomeRotation = npc.rotation;
            if (npcAnimator == null) npcAnimator = npc.GetComponentInChildren<Animator>();
        }
        npcSpeedHash = string.IsNullOrEmpty(npcSpeedParameter) ? 0 : Animator.StringToHash(npcSpeedParameter);
    }

    void OnTriggerEnter(Collider other)
    {
        if (running || (playOnlyOnce && played) || npc == null) return;
        var movement = other.GetComponentInParent<BasicPlayerMovement>();
        if (movement == null) return;

        // Không chạy chồng lên đoạn mở đầu hoặc khi đang mở menu.
        var intro = FindFirstObjectByType<NewGameIntroSequence>();
        if (intro != null && intro.IsPlaying) return;
        if (InventoryInputController.GameplayInputBlocked) return;

        player = movement.transform;
        StartCoroutine(Run());
    }

    [ContextMenu("Xem thử (chỉ dùng khi đang Play)")]
    void PlayNow()
    {
        if (!Application.isPlaying || running) return;
        var movement = FindFirstObjectByType<BasicPlayerMovement>();
        if (movement == null) return;
        player = movement.transform;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        running = true;
        played = true;
        EnsureDialogueBox();
        BuildFade();

        // 1. Khóa điều khiển, nhớ góc camera game, đưa camera lên góc cutscene.
        BlockGameplay(true);
        Cursor.visible = false;
        cam = Camera.main;
        Vector3 gameCameraPosition = cam != null ? cam.transform.position : Vector3.zero;
        Quaternion gameCameraRotation = cam != null ? cam.transform.rotation : Quaternion.identity;

        if (useFadeTransitions) yield return Fade(1f, 0.35f);
        DisableCameraDrivers();
        HoldPlayerIdle();
        DisableNpcAgent();
        if (playerStandPoint != null)
        {
            if (useFadeTransitions) MovePlayer(playerStandPoint.position, playerStandPoint.rotation);
            else StartCoroutine(SlidePlayer(playerStandPoint.position, playerStandPoint.rotation, 0.4f));
        }

        Transform cameraPoint = approachCamera != null ? approachCamera : talkCamera;
        if (cam != null && cameraPoint != null)
        {
            if (useFadeTransitions) cam.transform.SetPositionAndRotation(cameraPoint.position, cameraPoint.rotation);
            else yield return BlendCamera(cameraPoint.position, cameraPoint.rotation, cameraBlendTime);
        }
        if (useFadeTransitions) yield return Fade(0f, 0.5f);
        cameraTarget = cameraLooksAtNpc ? npc : null;

        // 2. Trưởng làng đi tới.
        yield return WalkNpc(BuildApproachPoints());

        // 3. Quay mặt vào nhau, camera sang góc nói chuyện.
        cameraTarget = null;
        SetNpcSpeed(0f);
        StartCoroutine(TurnToFace(npc, player.position, 0.4f));
        StartCoroutine(TurnToFace(player, npc.position, 0.4f));
        if (cam != null && talkCamera != null) yield return BlendCamera(talkCamera.position, talkCamera.rotation, cameraBlendTime);
        else yield return WaitRealtime(0.4f);

        // 4. Hội thoại.
        yield return PlayDialogue();

        // 5. Trưởng làng về nhà. Camera đợi tới khi ông về hẳn.
        if (returnHomeAfterTalk)
        {
            bool npcHome = false;
            StartCoroutine(WalkNpcThenRestore(BuildReturnPoints(), () => npcHome = true));
            cameraTarget = cameraLooksAtNpc ? npc : null;

            if (waitUntilNpcHome)
            {
                float start = Time.unscaledTime;
                while (!npcHome && Time.unscaledTime - start < maxWaitNpcHome) yield return null;
                yield return WaitRealtime(0.4f);
            }
            else yield return WaitRealtime(watchNpcLeaveTime);
        }

        // 6. Trả camera về cho Player.
        cameraTarget = null;
        if (useFadeTransitions)
        {
            yield return Fade(1f, 0.4f);
            RestoreCameraDrivers();
            ReleasePlayerIdle();
            BlockGameplay(false);
            yield return WaitRealtime(0.25f);   // Cho camera game kịp về sau lưng Player dưới màn đen.
            yield return Fade(0f, 0.5f);
        }
        else
        {
            // Lia mượt về đúng góc camera game lúc bắt đầu rồi mới trả quyền điều khiển.
            if (cam != null) yield return BlendCamera(gameCameraPosition, gameCameraRotation, cameraBlendTime);
            RestoreCameraDrivers();
            ReleasePlayerIdle();
            BlockGameplay(false);
        }

        fadeCanvas.gameObject.SetActive(false);
        running = false;
        onFinished?.Invoke();
    }

    void LateUpdate()
    {
        if (!running) return;

        // Camera đứng yên, xoay mượt theo NPC.
        if (cam != null && cameraTarget != null)
        {
            Vector3 look = cameraTarget.position + Vector3.up * 1.3f - cam.transform.position;
            if (look.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(look, Vector3.up);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, target, 1f - Mathf.Exp(-4f * Time.deltaTime));
            }
        }

        // Giữ Player ở tư thế đứng yên.
        if (frozenMovement != null && playerAnimator != null && playerAnimator.isActiveAndEnabled
            && !playerAnimator.IsInTransition(0)
            && playerAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash != playerIdleHash)
            playerAnimator.CrossFadeInFixedTime(playerIdleHash, 0.2f, 0);
    }

    // ---------- Hội thoại ----------

    IEnumerator PlayDialogue()
    {
        if (lines.Count == 0 || dialogueBox == null) yield break;

        bool opened = false;
        foreach (Line line in lines)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.text)) continue;
            string speakerName = line.speaker == Speaker.Npc ? npcName : PlayerName();
            Sprite portrait = line.speaker == Speaker.Npc ? npcPortrait : playerPortrait;

            if (!opened) { yield return dialogueBox.Show(speakerName, portrait); opened = true; }
            else dialogueBox.SetSpeaker(speakerName, portrait);

            yield return dialogueBox.TypeLine(FormatName(line.text), typingSpeed);
        }
        if (opened) yield return dialogueBox.Hide();
    }

    void EnsureDialogueBox()
    {
        if (dialogueBox == null)
        {
            // Ưu tiên khung đã chỉnh tay trong DialogueCanvas để mọi cảnh trông giống nhau.
            foreach (var candidate in FindObjectsByType<DialogueBoxUI>(FindObjectsSortMode.None))
                if (candidate.HasManualLayout) { dialogueBox = candidate; break; }
        }
        if (dialogueBox == null) dialogueBox = gameObject.AddComponent<DialogueBoxUI>();

        if (!dialogueBox.HasManualLayout && !dialogueBox.IsBuilt)
        {
            var canvasObject = new GameObject("CutsceneDialogueCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            SetupCanvas(canvasObject, 1001);
            dialogueBox.Build(canvasObject.transform);
        }
        else dialogueBox.Build(null);
    }

    // ---------- NPC ----------

    List<Vector3> BuildApproachPoints()
    {
        var points = new List<Vector3>();
        foreach (Transform point in approachPath) if (point != null) points.Add(point.position);
        if (points.Count == 0 && player != null)
        {
            // Chưa đặt đường đi: đi thẳng tới trước mặt Player 1.5m.
            Vector3 toNpc = npc.position - player.position;
            toNpc.y = 0f;
            points.Add(player.position + toNpc.normalized * 1.5f);
        }
        return points;
    }

    List<Vector3> BuildReturnPoints()
    {
        var points = new List<Vector3>();
        for (int i = approachPath.Count - 2; i >= 0; i--)
            if (approachPath[i] != null) points.Add(approachPath[i].position);
        points.Add(npcHomePosition);
        return points;
    }

    IEnumerator WalkNpcThenRestore(List<Vector3> points, Action done)
    {
        yield return WalkNpc(points);
        done?.Invoke();
        yield return TurnToRotation(npc, npcHomeRotation, 0.4f);
        RestoreNpcAgent();
    }

    IEnumerator WalkNpc(List<Vector3> points)
    {
        foreach (Vector3 rawPoint in points)
        {
            Vector3 point = rawPoint;
            while (true)
            {
                Vector3 position = npc.position;
                Vector3 flat = point - position;
                flat.y = 0f;
                if (flat.magnitude < 0.05f) break;

                Vector3 step = Vector3.MoveTowards(new Vector3(position.x, 0f, position.z),
                    new Vector3(point.x, 0f, point.z), npcWalkSpeed * Time.deltaTime);
                float y = snapNpcToGround ? GroundHeight(step, position.y) : Mathf.MoveTowards(position.y, point.y, npcWalkSpeed * Time.deltaTime);
                npc.position = new Vector3(step.x, y, step.z);
                npc.rotation = Quaternion.Slerp(npc.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up),
                    1f - Mathf.Exp(-10f * Time.deltaTime));
                SetNpcSpeed(npcWalkSpeed);
                yield return null;
            }
        }
        SetNpcSpeed(0f);
    }

    float GroundHeight(Vector3 flatPosition, float currentY)
    {
        Vector3 origin = new Vector3(flatPosition.x, currentY + 1.5f, flatPosition.z);
        var hits = Physics.RaycastAll(origin, Vector3.down, 4f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(npc)) continue;
            if (player != null && hit.transform.IsChildOf(player)) continue;
            if (hit.point.y > best) best = hit.point.y;
        }
        return float.IsNegativeInfinity(best) ? currentY : best;
    }

    void SetNpcSpeed(float speed)
    {
        if (npcAnimator == null || npcSpeedHash == 0) return;
        foreach (var parameter in npcAnimator.parameters)
        {
            if (parameter.nameHash != npcSpeedHash || parameter.type != AnimatorControllerParameterType.Float) continue;
            npcAnimator.SetFloat(npcSpeedHash, speed);
            return;
        }
    }

    // NavMeshAgent (nếu có) sẽ kéo NPC lại -> tắt trong lúc cutscene.
    void DisableNpcAgent()
    {
        npcAgent = npc.GetComponent("NavMeshAgent") as Behaviour;
        if (npcAgent != null && npcAgent.enabled) npcAgent.enabled = false;
        else npcAgent = null;
    }

    void RestoreNpcAgent()
    {
        if (npcAgent != null) npcAgent.enabled = true;
        npcAgent = null;
    }

    // ---------- Player ----------

    void HoldPlayerIdle()
    {
        var movement = player != null ? player.GetComponent<BasicPlayerMovement>() : null;
        if (movement == null) return;
        if (movement.enabled) { movement.enabled = false; frozenMovement = movement; }

        var switcher = movement.GetComponent<PlayerVisualSwitcher>();
        playerAnimator = switcher != null ? switcher.ActiveVisualAnimator : null;
        if (playerAnimator == null) playerAnimator = movement.GetComponentInChildren<Animator>();
        if (playerAnimator == null || string.IsNullOrEmpty(playerIdleState)) return;

        playerIdleHash = Animator.StringToHash(playerIdleState);
        if (playerAnimator.HasState(0, playerIdleHash)) playerAnimator.CrossFadeInFixedTime(playerIdleHash, 0.15f, 0);
        else playerAnimator = null;
    }

    void ReleasePlayerIdle()
    {
        if (frozenMovement != null) frozenMovement.enabled = true;
        frozenMovement = null;
        playerAnimator = null;
    }

    IEnumerator SlidePlayer(Vector3 position, Quaternion rotation, float time)
    {
        var controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        Vector3 fromPosition = player.position;
        Quaternion fromRotation = player.rotation;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            player.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, k), Quaternion.Slerp(fromRotation, rotation, k));
            yield return null;
        }
        player.SetPositionAndRotation(position, rotation);
        if (controller != null) controller.enabled = wasEnabled;
        Physics.SyncTransforms();
    }

    void MovePlayer(Vector3 position, Quaternion rotation)
    {
        var controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        player.SetPositionAndRotation(position, rotation);
        if (controller != null) controller.enabled = wasEnabled;
        Physics.SyncTransforms();
    }

    string PlayerName()
    {
        string playerName = null;
        var provider = FindFirstObjectByType<PlayerProfileRuntimeProvider>(FindObjectsInactive.Include);
        if (provider != null) playerName = provider.GetSnapshot().DisplayName;
        return string.IsNullOrWhiteSpace(playerName) ? "Bạn" : playerName;
    }

    string FormatName(string text) => text.Replace("{name}", PlayerName());

    // ---------- Camera / khóa điều khiển ----------

    IEnumerator BlendCamera(Vector3 position, Quaternion rotation, float time)
    {
        Vector3 fromPosition = cam.transform.position;
        Quaternion fromRotation = cam.transform.rotation;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, k),
                Quaternion.Slerp(fromRotation, rotation, k));
            yield return null;
        }
        cam.transform.SetPositionAndRotation(position, rotation);
    }

    void DisableCameraDrivers()
    {
        if (cam == null) return;
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
        foreach (Behaviour behaviour in disabledCameraDrivers) if (behaviour != null) behaviour.enabled = true;
        disabledCameraDrivers.Clear();
    }

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

    static IEnumerator TurnToFace(Transform who, Vector3 target, float time)
    {
        Vector3 direction = target - who.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) yield break;
        yield return TurnToRotation(who, Quaternion.LookRotation(direction.normalized, Vector3.up), time);
    }

    static IEnumerator TurnToRotation(Transform who, Quaternion target, float time)
    {
        Quaternion from = who.rotation;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            who.rotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0f, 1f, t / time));
            yield return null;
        }
        who.rotation = target;
    }

    // ---------- Màn đen ----------

    void BuildFade()
    {
        if (fadeCanvas != null) { fadeCanvas.gameObject.SetActive(true); return; }
        var canvasObject = new GameObject("CutsceneFadeCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        fadeCanvas = SetupCanvas(canvasObject, 999);

        var imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        fadeImage = imageObject.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;
    }

    IEnumerator Fade(float to, float time)
    {
        float from = fadeImage.color.a;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / time));
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, to);
    }

    static Canvas SetupCanvas(GameObject canvasObject, int sortingOrder)
    {
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;
    }

    // ---------- Công cụ trong Editor ----------

#if UNITY_EDITOR
    Transform CreatePoint(string pointName, bool useCameraPose)
    {
        var sceneView = UnityEditor.SceneView.lastActiveSceneView;
        if (sceneView == null) { Debug.LogWarning("Không tìm thấy cửa sổ Scene."); return null; }

        Transform container = transform.Find("CutscenePoints");
        if (container == null)
        {
            var containerObject = new GameObject("CutscenePoints");
            UnityEditor.Undo.RegisterCreatedObjectUndo(containerObject, "Create CutscenePoints");
            containerObject.transform.SetParent(transform, false);
            container = containerObject.transform;
        }

        var point = new GameObject(pointName);
        UnityEditor.Undo.RegisterCreatedObjectUndo(point, "Create " + pointName);
        point.transform.SetParent(container, true);
        if (useCameraPose)
            point.transform.SetPositionAndRotation(sceneView.camera.transform.position, sceneView.camera.transform.rotation);
        else
            point.transform.position = sceneView.pivot;
        UnityEditor.Selection.activeTransform = point.transform;
        return point.transform;
    }

    [ContextMenu("1. Thêm điểm đường đi NPC (ở tâm Scene View)")]
    void AddPathPoint()
    {
        UnityEditor.Undo.RecordObject(this, "Add NPC Path Point");
        Transform point = CreatePoint($"NpcPath{approachPath.Count + 1}", false);
        if (point == null) return;
        approachPath.Add(point);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("2. Camera lúc NPC đi tới = Scene View")]
    void SetApproachCamera()
    {
        UnityEditor.Undo.RecordObject(this, "Set Approach Camera");
        if (approachCamera != null) UnityEditor.Undo.DestroyObjectImmediate(approachCamera.gameObject);
        approachCamera = CreatePoint("ApproachCamera", true);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("3. Camera lúc nói chuyện = Scene View")]
    void SetTalkCamera()
    {
        UnityEditor.Undo.RecordObject(this, "Set Talk Camera");
        if (talkCamera != null) UnityEditor.Undo.DestroyObjectImmediate(talkCamera.gameObject);
        talkCamera = CreatePoint("TalkCamera", true);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("4. Chỗ Player đứng (ở tâm Scene View)")]
    void SetPlayerStandPoint()
    {
        UnityEditor.Undo.RecordObject(this, "Set Player Stand Point");
        if (playerStandPoint != null) UnityEditor.Undo.DestroyObjectImmediate(playerStandPoint.gameObject);
        playerStandPoint = CreatePoint("PlayerStandPoint", false);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        Gizmos.color = Color.yellow;
        Vector3? previous = npc != null ? npc.position : (Vector3?)null;
        foreach (Transform point in approachPath)
        {
            if (point == null) continue;
            Gizmos.DrawSphere(point.position, 0.15f);
            if (previous.HasValue) Gizmos.DrawLine(previous.Value, point.position);
            previous = point.position;
        }

        DrawCamera(approachCamera, Color.cyan);
        DrawCamera(talkCamera, Color.magenta);

        if (playerStandPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerStandPoint.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawRay(playerStandPoint.position + Vector3.up * 0.9f, playerStandPoint.forward);
        }
    }

    static void DrawCamera(Transform point, Color color)
    {
        if (point == null) return;
        Gizmos.color = color;
        Gizmos.DrawSphere(point.position, 0.15f);
        Gizmos.DrawRay(point.position, point.forward * 1.5f);
    }
}
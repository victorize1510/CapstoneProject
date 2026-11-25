using PhysicalKeys = Capstone.Game.UISystem.GameplayKeyboard;
using System;
using System.Collections;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Bàn chọn bóng khởi đầu.
// Tới gần -> "[F] Chọn bóng". Bấm F -> camera lia vào bàn, ẩn Player.
// A/D/W/S (hoặc phím mũi tên, hoặc rê chuột) đổi bóng, bóng đang chọn sáng viền.
// F / click -> hiện hình thú + hỏi "Bạn có muốn chọn ... không?" với [Xác nhận] / [Hủy].
// Xác nhận -> "Xin chúc mừng..." -> nhận thú (onPetGranted) -> camera về lại gameplay.
// Esc / Backspace -> thoát khỏi bàn.
[DisallowMultipleComponent]
public class StarterBallTable : MonoBehaviour
{
    [Serializable]
    public class BallSlot
    {
        public Transform ball;
        [Tooltip("Chỉ dùng khi chưa gán Pet Prefab và Pet Name")]
        public string displayName = "Bóng";
        [Tooltip("Prefab thú trong quả bóng này (vd. Ốc Gai)")]
        public GameObject petPrefab;
        [Tooltip("Tên hiện trên bóng. Để trống = lấy tên Prefab")]
        public string petName;
        [Tooltip("Hình con thú hiện ra khi bấm chọn quả bóng này")]
        public Sprite petImage;
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

    [Header("Hội thoại chọn thú  ({pet} = tên thú, {name} = tên Player)")]
    [Tooltip("Tên người nói trên hộp thoại. Để trống = không hiện bảng tên")]
    [SerializeField] string speakerName = "";
    [SerializeField] string askText = "Bạn có muốn chọn {pet} làm thú đồng hành đầu tiên không?";
    [SerializeField] string confirmText = "Xác nhận";
    [SerializeField] string cancelText = "Hủy";
    [SerializeField] string congratsText = "Xin chúc mừng! {name} đã nhận được {pet}!";
    [SerializeField, Min(5f)] float typingSpeed = 40f;
    [Tooltip("Để trống = dùng khung hội thoại tự chỉnh trong DialogueCanvas (nếu có)")]
    [SerializeField] DialogueBoxUI dialogueBox;

    [Header("Âm thanh chọn bóng")]
    [Tooltip("Tiếng khi chuyển sang quả bóng khác (rê chuột / A D / mũi tên). Để trống = tiếng hover chung của game")]
    [SerializeField] AudioClip hoverSound;
    [Tooltip("Tiếng khi bấm chọn quả bóng (F / chuột trái). Để trống = tiếng bấm chung của game")]
    [SerializeField] AudioClip selectSound;
    [SerializeField, Range(0f, 1f)] float hoverVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] float selectVolume = 1f;

    [Header("Sau khi nhận thú")]
    [SerializeField] bool hideChosenBall = true;
    [Tooltip("Chọn xong thì bàn không cho chọn nữa")]
    [SerializeField] bool disableAfterChoosing = true;
    [Tooltip("Mã lưu trong file save để nhớ đã chọn bóng (tắt game mở lại vẫn nhớ). Mỗi bàn bóng một mã riêng")]
    [SerializeField] string saveKey = "starterball.v1";
    [Tooltip("Thú đã tự vào Party (StarterPetGranter). Ô này để gắn thêm việc khác nếu cần, vd. bật quest")]
    public UnityEvent<GameObject> onPetGranted = new UnityEvent<GameObject>();
    [Tooltip("Gọi khi xác nhận chọn. Tham số = số thứ tự quả bóng")]
    public UnityEvent<int> onBallConfirmed = new UnityEvent<int>();

    enum State { Idle, Entering, Selecting, Confirming, Exiting }

    State state = State.Idle;
    int selected = -1;
    AudioSource uiAudio;
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
    bool completed;
    CanvasGroup revealGroup;
    RectTransform revealRoot;
    Image revealGlow;
    Image revealImage;
    Text revealName;
    static Sprite glowSprite;

    PlayerSaveController save;

    public bool IsSelecting => state != State.Idle;

    // ---------- Nhớ đã chọn (file save) ----------

    void Start()
    {
        save = FindFirstObjectByType<PlayerSaveController>(FindObjectsInactive.Include);
        if (save != null) save.LoadCompleted += OnSaveLoaded;
        RefreshFromSave();
    }

    void OnDestroy()
    {
        if (save != null) save.LoadCompleted -= OnSaveLoaded;
    }

    void OnSaveLoaded(PlayerSaveData data) => RefreshFromSave();

    string ChoiceKey(int index) => $"{saveKey}.{index}";

    // Save đã có dấu "đã chọn bóng N" -> khóa bàn, ẩn đúng quả đã chọn.
    void RefreshFromSave()
    {
        if (state != State.Idle) return;
        bool chosenInSave = false;
        if (save != null && !string.IsNullOrEmpty(saveKey))
        {
            for (int i = 0; i < balls.Count; i++)
            {
                if (!save.HasStarterGrant(ChoiceKey(i))) continue;
                chosenInSave = true;
                if (hideChosenBall && IsValid(i)) balls[i].ball.gameObject.SetActive(false);
            }
        }
        // Save cũ (trước khi có mã chọn bóng) nhưng nhiệm vụ ghi đã chọn thú -> cũng khóa bàn.
        var quests = FindFirstObjectByType<Capstone.Game.QuestSystem.QuestManager>(FindObjectsInactive.Include);
        if (quests != null && quests.StarterChosen) chosenInSave = true;
        if (chosenInSave) completed = disableAfterChoosing;
    }
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
        if (completed) { DoorPrompt.Hide(this); return; }
        ResolvePlayer();
        bool near = player != null && balls.Count > 0
            && !InventoryInputController.GameplayInputBlocked && IsPlayerNear();
        if (!near) { DoorPrompt.Hide(this); return; }

        if (Capstone.Game.SettingsSystem.GameSettingsService.InteractionPromptsVisible)
            DoorPrompt.Show(this, TableAnchor(), $"<color=#FFD54A><b>[{interactKey}]</b></color> {enterText}");
        else DoorPrompt.Hide(this);

        if (PhysicalKeys.GetKeyDown(interactKey)) StartCoroutine(EnterRoutine());
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
        if (PhysicalKeys.GetKeyDown(KeyCode.Escape) || PhysicalKeys.GetKeyDown(KeyCode.Backspace))
        {
            StartCoroutine(ExitRoutine());
            return;
        }

        Vector2 direction = Vector2.zero;
        if (PhysicalKeys.GetKeyDown(KeyCode.A) || PhysicalKeys.GetKeyDown(KeyCode.LeftArrow)) direction = Vector2.left;
        else if (PhysicalKeys.GetKeyDown(KeyCode.D) || PhysicalKeys.GetKeyDown(KeyCode.RightArrow)) direction = Vector2.right;
        else if (PhysicalKeys.GetKeyDown(KeyCode.W) || PhysicalKeys.GetKeyDown(KeyCode.UpArrow)) direction = Vector2.up;
        else if (PhysicalKeys.GetKeyDown(KeyCode.S) || PhysicalKeys.GetKeyDown(KeyCode.DownArrow)) direction = Vector2.down;
        if (direction != Vector2.zero) Navigate(direction);

        int hovered = UpdateMouseHover();

        bool confirm = PhysicalKeys.GetKeyDown(interactKey) || (Input.GetMouseButtonDown(0) && hovered >= 0);
        if (confirm && selected >= 0)
        {
            PlayUi(false);
            StartCoroutine(ConfirmRoutine());
            return;   // Không vẽ lại tooltip tên bóng khi đang hỏi xác nhận.
        }

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

    IEnumerator ConfirmRoutine()
    {
        state = State.Confirming;
        DoorPrompt.Hide(this);
        BallSlot slot = balls[selected];
        string petName = PetName(slot);

        // 1. Hiện hình con thú + hỏi xác nhận.
        EnsureDialogueBox();
        yield return ShowReveal(slot.petImage, petName);
        yield return dialogueBox.Show(speakerName, null);
        yield return dialogueBox.TypeLine(FormatText(askText, petName), typingSpeed, false);
        int choice = -1;
        yield return dialogueBox.Choose(new[] { confirmText, cancelText }, result => choice = result);

        if (choice != 0)
        {
            // Hủy (hoặc Esc): quay lại chọn bóng.
            yield return dialogueBox.Hide();
            yield return HideReveal();
            lastMousePosition = Input.mousePosition;
            state = State.Selecting;
            yield break;
        }

        // 2. Thêm thú vào Party (Party đầy thì vào Box).
        var granter = GetComponent<StarterPetGranter>();
        if (granter == null) granter = gameObject.AddComponent<StarterPetGranter>();
        if (!granter.TryGrant(slot.petPrefab, string.IsNullOrEmpty(saveKey) ? null : ChoiceKey(selected), out string grantError))
        {
            Debug.LogWarning($"[StarterBall] Không nhận được thú: {grantError}", this);
            yield return dialogueBox.TypeLine($"Không thể nhận {petName}: {grantError}", typingSpeed);
            yield return dialogueBox.Hide();
            yield return HideReveal();
            lastMousePosition = Input.mousePosition;
            state = State.Selecting;
            yield break;
        }
        int chosen = selected;
        // Báo cho hệ thống nhiệm vụ: đã chọn thú đầu tiên -> trưởng làng mới giao nhiệm vụ chương 1.
        FindFirstObjectByType<Capstone.Game.QuestSystem.QuestManager>(FindObjectsInactive.Include)?.MarkStarterChosen();
        onBallConfirmed?.Invoke(chosen);
        onPetGranted?.Invoke(slot.petPrefab);

        // 3. Chúc mừng.
        yield return dialogueBox.TypeLine(FormatText(congratsText, petName), typingSpeed);
        yield return dialogueBox.Hide();
        yield return HideReveal();

        SetSelected(-1);
        if (hideChosenBall && slot.ball != null) slot.ball.gameObject.SetActive(false);
        completed = disableAfterChoosing;

        // 4. Về lại gameplay.
        yield return ExitRoutine();
    }

    string PetName(BallSlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.petName)) return slot.petName.Trim();
        if (slot.petPrefab != null) return slot.petPrefab.name;
        return slot.displayName;
    }

    string FormatText(string text, string petName)
    {
        string playerName = null;
        var provider = FindFirstObjectByType<PlayerProfileRuntimeProvider>(FindObjectsInactive.Include);
        if (provider != null) playerName = provider.GetSnapshot().DisplayName;
        if (string.IsNullOrWhiteSpace(playerName)) playerName = "Bạn";
        return (text ?? string.Empty).Replace("{pet}", petName).Replace("{name}", playerName);
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
            var canvasObject = new GameObject("BallTableDialogueCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            SetupCanvas(canvasObject, 1001);
            dialogueBox.Build(canvasObject.transform);
        }
        else dialogueBox.Build(null);
    }

    // ---------- Hình thú hiện ra ----------

    IEnumerator ShowReveal(Sprite image, string petName)
    {
        BuildReveal();
        revealImage.sprite = image;
        revealImage.enabled = image != null;
        revealName.text = petName;
        revealRoot.gameObject.SetActive(true);

        float t = 0f;
        const float time = 0.4f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / time);
            float pop = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);   // Phóng quá một chút rồi về 1.
            revealGroup.alpha = k;
            revealRoot.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, k) * pop;
            yield return null;
        }
        revealGroup.alpha = 1f;
        revealRoot.localScale = Vector3.one;
    }

    IEnumerator HideReveal()
    {
        if (revealRoot == null || !revealRoot.gameObject.activeSelf) yield break;
        float t = 0f;
        const float time = 0.25f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            revealGroup.alpha = 1f - t / time;
            yield return null;
        }
        revealGroup.alpha = 0f;
        revealRoot.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        // Vầng sáng sau lưng con thú xoay chậm + nhấp nháy nhẹ.
        if (revealRoot == null || !revealRoot.gameObject.activeSelf) return;
        revealGlow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 12f);
        revealGlow.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 3f));
    }

    void BuildReveal()
    {
        if (revealRoot != null) return;
        var canvasObject = new GameObject("StarterPetRevealCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        SetupCanvas(canvasObject, 1000);

        var rootObject = new GameObject("Reveal", typeof(RectTransform), typeof(CanvasGroup));
        rootObject.transform.SetParent(canvasObject.transform, false);
        revealRoot = (RectTransform)rootObject.transform;
        revealRoot.anchorMin = revealRoot.anchorMax = new Vector2(0.5f, 0.5f);
        revealRoot.anchoredPosition = new Vector2(0f, 110f);
        revealRoot.sizeDelta = new Vector2(600f, 600f);
        revealGroup = rootObject.GetComponent<CanvasGroup>();
        revealGroup.alpha = 0f;
        revealGroup.blocksRaycasts = false;

        revealGlow = NewImage(revealRoot, "Glow", new Color(1f, 0.85f, 0.4f, 0.75f), GlowSprite, new Vector2(620f, 620f), Vector2.zero);
        revealImage = NewImage(revealRoot, "Pet", Color.white, null, new Vector2(420f, 420f), Vector2.zero);
        revealImage.preserveAspect = true;

        var nameObject = new GameObject("Name", typeof(RectTransform), typeof(Text));
        nameObject.transform.SetParent(revealRoot, false);
        revealName = nameObject.GetComponent<Text>();
        revealName.font = MenuTypography.Font != null ? MenuTypography.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        revealName.fontSize = 48;
        revealName.fontStyle = FontStyle.Bold;
        revealName.alignment = TextAnchor.MiddleCenter;
        revealName.color = new Color(1f, 0.92f, 0.6f);
        revealName.horizontalOverflow = HorizontalWrapMode.Overflow;
        revealName.raycastTarget = false;
        var nameRect = revealName.rectTransform;
        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
        nameRect.sizeDelta = new Vector2(800f, 70f);
        nameRect.anchoredPosition = new Vector2(0f, -250f);
        var outline = nameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.12f, 0.02f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);

        revealRoot.gameObject.SetActive(false);
    }

    static Image NewImage(RectTransform parent, string objectName, Color color, Sprite sprite, Vector2 size, Vector2 position)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return image;
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

    // Vầng sáng tròn: sáng ở giữa, mờ dần ra ngoài, có vài tia sáng.
    static Sprite GlowSprite
    {
        get
        {
            if (glowSprite != null) return glowSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "PetRevealGlow", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f) / (size * 0.5f);
                    float r = d.magnitude;
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
                    float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(d.y, d.x) * 6f)), 12f) * Mathf.Clamp01(1f - r) * 0.5f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(glow + rays));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            glowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            glowSprite.hideFlags = HideFlags.HideAndDontSave;
            return glowSprite;
        }
    }

    void UpdateSelectionPrompt()
    {
        if (selected < 0) { DoorPrompt.Hide(this); return; }
        string ballName = PetName(balls[selected]);
        string hint = $"<size=18><color=#FFD54A>[{interactKey}]</color> Chọn    "
            + "<color=#FFD54A>[Esc]</color> Thoát</size>";
        DoorPrompt.Show(this, BallTop(selected) + Vector3.up * 0.15f, $"<b>{ballName}</b>\n{hint}");
    }

    void SetSelected(int index)
    {
        if (selected == index) return;
        if (IsValid(selected)) GetHighlight(selected)?.SetHighlighted(false);
        selected = IsValid(index) ? index : -1;
        if (selected >= 0)
        {
            GetHighlight(selected)?.SetHighlighted(true);
            if (state == State.Selecting) PlayUi(true);   // Đổi sang bóng khác.
        }
    }

    void PlayUi(bool hover)
    {
        if (uiAudio == null)
        {
            uiAudio = gameObject.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            uiAudio.spatialBlend = 0f;
            Capstone.Game.SettingsSystem.GameAudioRouting.Route(uiAudio, Capstone.Game.SettingsSystem.GameAudioBus.UI);
            var assets = Capstone.Game.SettingsSystem.GameAudioRouting.Assets;
            if (assets != null)
            {
                if (hoverSound == null) hoverSound = assets.hover;
                if (selectSound == null) selectSound = assets.click;
            }
        }
        AudioClip clip = hover ? hoverSound : selectSound;
        if (clip != null) uiAudio.PlayOneShot(clip, hover ? hoverVolume : selectVolume);
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
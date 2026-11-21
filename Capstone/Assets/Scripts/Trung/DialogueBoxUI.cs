using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Hộp thoại kiểu RPG ở dưới màn hình:
//   - Khung bo góc, viền vàng 2 lớp, nền tối trong suốt nhẹ, vệt sáng phía trên.
//   - Bảng tên màu vàng nằm vắt trên mép khung, 2 hạt kim cương trang trí.
//   - Ảnh chân dung bên trái (tùy chọn).
//   - Chữ chạy từng ký tự (+ âm thanh gõ chữ tùy chọn), hạt kim cương nhún nhảy chờ bấm Space.
//   - Hiện ra: trượt lên + mờ dần. Ẩn: trượt xuống + mờ dần.
// Dùng lại được cho hội thoại NPC sau này: Build() -> Show() -> TypeLine() ... -> Hide().
//
// 2 cách dùng:
//   A. Tự động: không làm gì, khi chạy game script tự dựng khung theo các ô bên dưới.
//   B. Chỉnh tay trong Canvas: ⋮ -> "Tạo khung hội thoại trong Canvas (để chỉnh tay)".
//      Script tạo sẵn DialogueCanvas trong Scene, bạn kéo / đổi kích thước / đổi ảnh / đổi màu
//      trực tiếp từng phần (Frame, NameTag, Body...). Khi chạy game, script dùng đúng khung đó.
[DisallowMultipleComponent]
public class DialogueBoxUI : MonoBehaviour
{
    [Header("Kích thước (theo màn hình 1920 x 1080)")]
    [SerializeField] Vector2 boxSize = new Vector2(1500f, 230f);
    [SerializeField] float bottomMargin = 40f;
    [SerializeField, Min(2f)] float cornerRadius = 22f;
    [SerializeField, Min(1f)] float borderThickness = 3f;
    [SerializeField, Min(80f)] float portraitSize = 180f;

    [Header("Màu")]
    [SerializeField] Color borderColor = new Color(0.86f, 0.68f, 0.32f, 0.95f);
    [SerializeField] Color fillColor = new Color(0.06f, 0.08f, 0.13f, 0.92f);
    [SerializeField] Color innerLineColor = new Color(0.86f, 0.68f, 0.32f, 0.28f);
    [SerializeField] Color nameTagColor = new Color(0.93f, 0.72f, 0.33f, 1f);
    [SerializeField] Color nameTextColor = new Color(0.22f, 0.12f, 0.04f, 1f);
    [SerializeField] Color bodyTextColor = new Color(0.97f, 0.94f, 0.86f, 1f);
    [SerializeField] Color accentColor = new Color(1f, 0.84f, 0.4f, 1f);

    [Header("Chữ")]
    [SerializeField, Min(10)] int nameFontSize = 30;
    [SerializeField, Min(10)] int bodyFontSize = 30;
    [Tooltip("Để trống = dùng font menu của game (NotoSerif, đủ dấu tiếng Việt)")]
    [SerializeField] Font fontOverride;

    [Header("Ảnh khung riêng (tùy chọn, Sprite dạng 9-slice)")]
    [Tooltip("Có ảnh này thì dùng thay cho khung vẽ bằng code")]
    [SerializeField] Sprite customBoxSprite;
    [SerializeField] Sprite customNameTagSprite;

    [Header("Âm thanh gõ chữ (tùy chọn)")]
    [SerializeField] AudioClip typeSound;
    [SerializeField, Range(0f, 1f)] float typeVolume = 0.35f;
    [SerializeField, Min(1)] int soundEveryChars = 2;

    const float SpriteCorner = 24f;   // Bán kính góc (pixel) của sprite bo góc tạo bằng code.
    static Sprite roundedSprite;
    static Sprite shineSprite;

    [Header("Khung trong Canvas (tự điền khi dùng menu ⋮ -> Tạo khung...)")]
    [SerializeField] RectTransform box;
    [SerializeField] CanvasGroup group;
    [SerializeField] RectTransform nameTag;
    [SerializeField] Text nameText;
    [SerializeField] Text body;
    [SerializeField] RectTransform portraitFrame;
    [SerializeField] Image portraitImage;
    [SerializeField] RectTransform nextMarker;
    [SerializeField] Image nextMarkerImage;

    AudioSource audioSource;
    float textLeft;
    bool prepared;
    bool manualLayout;     // true = khung dựng sẵn trong Canvas, không tự đổi vị trí / lề chữ.
    Vector2 basePosition;

    public bool IsBuilt => box != null;
    public bool IsVisible => group != null && group.alpha > 0.01f;
    // Khung được dựng sẵn trong DialogueCanvas (menu ⋮ -> Tạo khung...) -> dùng chung được ở mọi cảnh.
    public bool HasManualLayout => manualLayout || (!prepared && box != null);

    // ---------- API ----------

    public void Build(Transform canvasRoot)
    {
        if (prepared) return;
        prepared = true;

        if (box != null)
        {
            // Đã có khung chỉnh tay trong Canvas -> dùng nguyên vị trí, kích thước, màu của bạn.
            manualLayout = true;
            basePosition = box.anchoredPosition;
            EnsureAudio();
            HideImmediate();
            return;
        }

        BuildInto(canvasRoot, false);
        basePosition = box.anchoredPosition;
        EnsureAudio();
        HideImmediate();
    }

    void EnsureAudio()
    {
        if (typeSound == null || audioSource != null) return;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    void BuildInto(Transform canvasRoot, bool editPreview)
    {
        Font font = fontOverride != null ? fontOverride : Capstone.Game.UISystem.MenuTypography.Font;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var root = new GameObject("DialogueBox", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvasRoot, false);
        box = (RectTransform)root.transform;
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f);
        box.pivot = new Vector2(0.5f, 0f);
        box.sizeDelta = boxSize;
        box.anchoredPosition = new Vector2(0f, bottomMargin);
        group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        BuildFrame();
        BuildPortrait();

        // Chữ thoại
        body = NewText(box, "Body", font, bodyFontSize, TextAnchor.UpperLeft, bodyTextColor);
        body.lineSpacing = 1.15f;
        var bodyShadow = body.gameObject.AddComponent<Shadow>();
        bodyShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        bodyShadow.effectDistance = new Vector2(1.5f, -1.5f);

        BuildNameTag(font);
        BuildNextMarker();
        SetPortrait(null);

        if (editPreview)
        {
            // Chữ mẫu để nhìn thấy khi chỉnh trong Scene. Lúc chạy game sẽ được thay.
            group.alpha = 1f;
            nameText.text = "Tên nhân vật";
            body.text = "Đây là câu thoại mẫu. Kéo, đổi kích thước, đổi màu từng phần trong Hierarchy để trang trí khung.";
            nextMarker.gameObject.SetActive(true);
        }
    }

    public IEnumerator Show(string speaker, Sprite portrait)
    {
        if (box == null) yield break;
        // Canvas chứa hộp thoại có thể đã bị tắt (vd. Canvas của đoạn mở đầu) -> bật lại.
        Canvas parentCanvas = box.GetComponentInParent<Canvas>(true);
        if (parentCanvas != null && !parentCanvas.gameObject.activeSelf) parentCanvas.gameObject.SetActive(true);
        box.gameObject.SetActive(true);
        SetPortrait(portrait);
        nameText.text = speaker ?? string.Empty;
        nameTag.gameObject.SetActive(!string.IsNullOrWhiteSpace(speaker));
        body.text = string.Empty;
        nextMarker.gameObject.SetActive(false);

        // Trượt lên + hiện dần, bảng tên phóng nhẹ.
        float t = 0f;
        const float time = 0.3f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOut(t / time);
            group.alpha = k;
            box.anchoredPosition = basePosition + new Vector2(0f, Mathf.Lerp(-28f, 0f, k));
            nameTag.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, k);
            yield return null;
        }
        group.alpha = 1f;
        box.anchoredPosition = basePosition;
        nameTag.localScale = Vector3.one;
    }

    // Đổi người nói giữa đoạn hội thoại (bảng tên + ảnh chân dung), không đóng hộp.
    public void SetSpeaker(string speaker, Sprite portrait)
    {
        if (box == null) return;
        SetPortrait(portrait);
        nameText.text = speaker ?? string.Empty;
        nameTag.gameObject.SetActive(!string.IsNullOrWhiteSpace(speaker));
        nameTag.localScale = Vector3.one * 1.12f;   // Nảy nhẹ bảng tên khi đổi người.
        StartCoroutine(SettleNameTag());
    }

    IEnumerator SettleNameTag()
    {
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.unscaledDeltaTime;
            nameTag.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, t / 0.15f);
            yield return null;
        }
        nameTag.localScale = Vector3.one;
    }

    public IEnumerator Hide()
    {
        if (box == null || !box.gameObject.activeSelf) yield break;
        nextMarker.gameObject.SetActive(false);
        float from = group.alpha;
        float t = 0f;
        const float time = 0.25f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = t / time;
            group.alpha = Mathf.Lerp(from, 0f, k);
            box.anchoredPosition = basePosition + new Vector2(0f, Mathf.Lerp(0f, -18f, k));
            yield return null;
        }
        HideImmediate();
    }

    public void HideImmediate()
    {
        if (box == null) return;
        if (choicePanel != null) choicePanel.gameObject.SetActive(false);
        group.alpha = 0f;
        body.text = string.Empty;
        box.anchoredPosition = basePosition;
        box.gameObject.SetActive(false);
    }

    // Chạy chữ 1 câu, rồi đợi người chơi bấm Space / Enter / click để qua câu.
    // waitForInput = false: chạy chữ xong là trả về ngay (dùng trước khi hiện lựa chọn).
    public IEnumerator TypeLine(string line, float charactersPerSecond, bool waitForInput = true)
    {
        if (box == null) yield break;
        line ??= string.Empty;
        nextMarker.gameObject.SetActive(false);
        body.text = string.Empty;

        // Bỏ qua frame hiện tại: phím bấm để qua câu trước không được tính cho câu này.
        yield return null;

        float shown = 0f;
        int lastSoundIndex = 0;
        while (shown < line.Length)
        {
            if (AdvancePressed()) break;
            shown += Mathf.Max(1f, charactersPerSecond) * Time.unscaledDeltaTime;
            int count = Mathf.Min(line.Length, Mathf.FloorToInt(shown));
            body.text = line.Substring(0, count);
            if (count - lastSoundIndex >= soundEveryChars && count > 0 && !char.IsWhiteSpace(line[count - 1]))
            {
                lastSoundIndex = count;
                PlayTypeSound();
            }
            yield return null;
        }
        body.text = line;
        yield return null;
        if (!waitForInput) yield break;

        // Hạt kim cương nhún nhảy chờ bấm.
        nextMarker.gameObject.SetActive(true);
        Vector2 basePosition = nextMarker.anchoredPosition;
        float time = 0f;
        while (!AdvancePressed())
        {
            time += Time.unscaledDeltaTime;
            nextMarker.anchoredPosition = basePosition + new Vector2(0f, Mathf.Sin(time * 6f) * 4f);
            Color c = accentColor;
            c.a = 0.55f + 0.45f * Mathf.Sin(time * 6f);
            nextMarkerImage.color = c;
            yield return null;
        }
        nextMarker.anchoredPosition = basePosition;
        nextMarker.gameObject.SetActive(false);
    }

    // ---------- Lựa chọn (Xác nhận / Hủy ...) ----------

    RectTransform choicePanel;
    readonly System.Collections.Generic.List<Image> choiceBackgrounds = new System.Collections.Generic.List<Image>();
    readonly System.Collections.Generic.List<Text> choiceLabels = new System.Collections.Generic.List<Text>();
    readonly System.Collections.Generic.List<Image> choiceMarkers = new System.Collections.Generic.List<Image>();

    // Hiện các lựa chọn phía trên góc phải hộp thoại.
    // W/S/A/D hoặc phím mũi tên / rê chuột: đổi lựa chọn. F / Enter / Space / click: chọn. Esc: hủy (trả về -1).
    public IEnumerator Choose(string[] options, System.Action<int> onChosen, int defaultIndex = 0)
    {
        if (box == null || options == null || options.Length == 0) { onChosen?.Invoke(-1); yield break; }
        BuildChoices(options);
        int index = Mathf.Clamp(defaultIndex, 0, options.Length - 1);
        RefreshChoices(index, 0f);
        choicePanel.gameObject.SetActive(true);
        yield return null;   // Phím vừa bấm để mở lựa chọn không được tính.

        int result;
        float time = 0f;
        while (true)
        {
            time += Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)
                || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                index = (index - 1 + options.Length) % options.Length;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)
                || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                index = (index + 1) % options.Length;

            int hovered = -1;
            for (int i = 0; i < options.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(choiceBackgrounds[i].rectTransform, Input.mousePosition, null))
                    hovered = i;
            if (hovered >= 0) index = hovered;

            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space) || (Input.GetMouseButtonDown(0) && hovered >= 0)) { result = index; break; }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) { result = -1; break; }

            RefreshChoices(index, time);
            yield return null;
        }

        choicePanel.gameObject.SetActive(false);
        onChosen?.Invoke(result);
    }

    void BuildChoices(string[] options)
    {
        if (choicePanel == null)
        {
            var panelObject = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelObject.transform.SetParent(box, false);
            choicePanel = (RectTransform)panelObject.transform;
            choicePanel.anchorMin = choicePanel.anchorMax = new Vector2(1f, 1f);
            choicePanel.pivot = new Vector2(1f, 0f);
            choicePanel.anchoredPosition = new Vector2(-24f, 16f);
            var layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = panelObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        Font font = nameText != null ? nameText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        while (choiceBackgrounds.Count < options.Length)
        {
            var row = NewImage(choicePanel, "Choice", fillColor, RoundedSprite);
            row.type = Image.Type.Sliced;
            row.pixelsPerUnitMultiplier = SpriteCorner / 12f;
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 300f;
            element.preferredHeight = 58f;
            var outline = row.gameObject.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(2f, -2f);

            var label = NewText(row.rectTransform, "Label", font, bodyFontSize, TextAnchor.MiddleLeft, bodyTextColor);
            Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(52f, 0f);
            label.fontStyle = FontStyle.Bold;

            var marker = AddDiamond(row.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 0f), 14f, accentColor);
            choiceBackgrounds.Add(row);
            choiceLabels.Add(label);
            choiceMarkers.Add(marker);
        }

        for (int i = 0; i < choiceBackgrounds.Count; i++)
        {
            bool used = i < options.Length;
            choiceBackgrounds[i].gameObject.SetActive(used);
            if (used) choiceLabels[i].text = options[i];
        }
        choicePanel.SetAsLastSibling();
    }

    void RefreshChoices(int selectedIndex, float time)
    {
        for (int i = 0; i < choiceBackgrounds.Count; i++)
        {
            if (!choiceBackgrounds[i].gameObject.activeSelf) continue;
            bool selected = i == selectedIndex;
            choiceBackgrounds[i].color = selected ? accentColor : fillColor;
            choiceLabels[i].color = selected ? nameTextColor : bodyTextColor;
            choiceMarkers[i].enabled = selected;
            if (selected)
                choiceMarkers[i].rectTransform.anchoredPosition = new Vector2(28f + Mathf.Sin(time * 8f) * 3f, 0f);
        }
    }

    public static bool AdvancePressed() =>
        Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
        || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0);

    // ---------- Dựng UI ----------

    void BuildFrame()
    {
        if (customBoxSprite != null)
        {
            var custom = NewImage(box, "Frame", Color.white, customBoxSprite);
            Stretch(custom.rectTransform, 0f);
            return;
        }

        // Viền vàng ngoài -> nền tối -> đường chỉ vàng mảnh bên trong -> nền tối.
        float inner = borderThickness;
        Rounded(NewImage(box, "Border", borderColor, RoundedSprite), 0f, cornerRadius);
        Rounded(NewImage(box, "Fill", fillColor, RoundedSprite), inner, cornerRadius - inner);
        Rounded(NewImage(box, "InnerLine", innerLineColor, RoundedSprite), inner + 7f, cornerRadius - inner - 5f);
        Rounded(NewImage(box, "InnerFill", fillColor, RoundedSprite), inner + 8f, cornerRadius - inner - 6f);

        // Vệt sáng mờ ở nửa trên.
        var shine = NewImage(box, "Shine", new Color(1f, 1f, 1f, 0.07f), ShineSprite);
        var rect = shine.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(cornerRadius, 0f);
        rect.offsetMax = new Vector2(-cornerRadius, -(inner + 8f));
    }

    void BuildPortrait()
    {
        var frameObject = new GameObject("Portrait", typeof(RectTransform));
        frameObject.transform.SetParent(box, false);
        portraitFrame = (RectTransform)frameObject.transform;
        portraitFrame.anchorMin = portraitFrame.anchorMax = new Vector2(0f, 0.5f);
        portraitFrame.pivot = new Vector2(0f, 0.5f);
        portraitFrame.anchoredPosition = new Vector2(30f, -6f);
        portraitFrame.sizeDelta = new Vector2(portraitSize, portraitSize);

        Rounded(NewImage(portraitFrame, "Border", borderColor, RoundedSprite), 0f, 16f);
        var mask = NewImage(portraitFrame, "Mask", new Color(0.12f, 0.15f, 0.22f, 1f), RoundedSprite);
        Rounded(mask, 3f, 13f);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        portraitImage = NewImage(mask.rectTransform, "Image", Color.white, null);
        portraitImage.preserveAspect = true;
        Stretch(portraitImage.rectTransform, 0f);
    }

    void BuildNameTag(Font font)
    {
        var tagImage = NewImage(box, "NameTag", nameTagColor, customNameTagSprite != null ? customNameTagSprite : RoundedSprite);
        if (customNameTagSprite == null)
        {
            tagImage.type = Image.Type.Sliced;
            tagImage.pixelsPerUnitMultiplier = SpriteCorner / 14f;
        }
        var outline = tagImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.13f, 0.03f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);

        nameTag = tagImage.rectTransform;
        nameTag.anchorMin = nameTag.anchorMax = new Vector2(0f, 1f);
        nameTag.pivot = new Vector2(0f, 0.5f);

        var layout = tagImage.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(34, 34, 6, 6);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = tagImage.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        nameText = NewText(nameTag, "Name", font, nameFontSize, TextAnchor.MiddleCenter, nameTextColor);
        nameText.fontStyle = FontStyle.Bold;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 2 hạt kim cương nhỏ 2 bên tên.
        AddDiamond(nameTag, new Vector2(0f, 0.5f), new Vector2(15f, 0f), 9f, new Color(nameTextColor.r, nameTextColor.g, nameTextColor.b, 0.7f));
        AddDiamond(nameTag, new Vector2(1f, 0.5f), new Vector2(-15f, 0f), 9f, new Color(nameTextColor.r, nameTextColor.g, nameTextColor.b, 0.7f));
    }

    void BuildNextMarker()
    {
        nextMarkerImage = AddDiamond(box, new Vector2(1f, 0f), new Vector2(-40f, 32f), 16f, accentColor);
        nextMarker = nextMarkerImage.rectTransform;
        var outline = nextMarkerImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
        nextMarker.gameObject.SetActive(false);
    }

    void SetPortrait(Sprite portrait)
    {
        bool hasPortrait = portrait != null;
        if (portraitFrame != null) portraitFrame.gameObject.SetActive(hasPortrait);
        if (portraitImage != null) portraitImage.sprite = portrait;
        if (manualLayout) return;   // Khung chỉnh tay: giữ nguyên lề chữ và vị trí bảng tên.
        textLeft = hasPortrait ? 30f + portraitSize + 30f : 52f;

        body.rectTransform.anchorMin = Vector2.zero;
        body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.offsetMin = new Vector2(textLeft, 28f);
        body.rectTransform.offsetMax = new Vector2(-70f, -46f);

        nameTag.anchoredPosition = new Vector2(textLeft - 14f, 2f);
    }

    void PlayTypeSound()
    {
        if (audioSource == null || typeSound == null) return;
        audioSource.pitch = Random.Range(0.94f, 1.06f);
        audioSource.PlayOneShot(typeSound, typeVolume);
    }

    // ---------- Công cụ trong Editor ----------

#if UNITY_EDITOR
    [ContextMenu("Tạo khung hội thoại trong Canvas (để chỉnh tay)")]
    void CreateEditableLayout()
    {
        if (Application.isPlaying) { Debug.LogWarning("Hãy tắt Play rồi mới tạo khung."); return; }
        if (box != null)
        {
            Debug.LogWarning("Đã có khung hội thoại. Muốn tạo lại thì xóa DialogueCanvas cũ và xóa ô Box trước.", box);
            UnityEditor.Selection.activeObject = box.gameObject;
            return;
        }

        SaveSpriteAssets();

        var canvasObject = new GameObject("DialogueCanvas", typeof(Canvas), typeof(CanvasScaler));
        UnityEditor.Undo.RegisterCreatedObjectUndo(canvasObject, "Create Dialogue Canvas");
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1001;   // Trên màn đen của đoạn mở đầu và trên HUD.
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        UnityEditor.Undo.RecordObject(this, "Create Dialogue Layout");
        BuildInto(canvasObject.transform, true);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.Selection.activeObject = box.gameObject;
        Debug.Log("Đã tạo DialogueCanvas. Chọn từng phần trong Hierarchy (Frame / Border / NameTag / Body / Diamond) để chỉnh.");
    }

    // Sprite bo góc phải được lưu thành file thì Scene mới giữ được khi đóng / mở lại.
    static void SaveSpriteAssets()
    {
        const string folder = "Assets/Generated/DialogueBox";
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Generated"))
            UnityEditor.AssetDatabase.CreateFolder("Assets", "Generated");
        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            UnityEditor.AssetDatabase.CreateFolder("Assets/Generated", "DialogueBox");

        roundedSprite = SaveSprite(folder + "/DialogueRounded.png", CreateRoundedTexture(), SpriteCorner);
        shineSprite = SaveSprite(folder + "/DialogueShine.png", CreateShineTexture(), 0f);
    }

    static Sprite SaveSprite(string path, Texture2D texture, float border)
    {
        var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEditor.AssetDatabase.ImportAsset(path);
        var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
        importer.textureType = UnityEditor.TextureImporterType.Sprite;
        importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spritePixelsPerUnit = 100f;
        var settings = new UnityEditor.TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteBorder = new Vector4(border, border, border, border);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
#endif

    // ---------- Tiện ích ----------

    Image AddDiamond(RectTransform parent, Vector2 anchor, Vector2 position, float size, Color color)
    {
        var image = NewImage(parent, "Diamond", color, null);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        return image;
    }

    static void Rounded(Image image, float inset, float radius)
    {
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SpriteCorner / Mathf.Max(1f, radius);
        Stretch(image.rectTransform, inset);
    }

    static Image NewImage(Transform parent, string objectName, Color color, Sprite sprite)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        return image;
    }

    static Text NewText(Transform parent, string objectName, Font font, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }

    // Sprite bo góc 64x64, góc 24px, dùng 9-slice nên kéo dãn không bị méo góc.
    static Sprite RoundedSprite
    {
        get
        {
            if (roundedSprite != null) return roundedSprite;
            float r = SpriteCorner;
            var texture = CreateRoundedTexture();
            texture.hideFlags = HideFlags.HideAndDontSave;
            roundedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            roundedSprite.name = "DialogueRounded";
            roundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return roundedSprite;
        }
    }

    static Texture2D CreateRoundedTexture()
    {
        const int size = 64;
        float r = SpriteCorner;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "DialogueRounded", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float cx = Mathf.Clamp(px, r, size - r);
                float cy = Mathf.Clamp(py, r, size - r);
                float distance = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - distance + 0.5f));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    static Texture2D CreateShineTexture()
    {
        const int height = 32;
        var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
        {
            name = "DialogueShine", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < height; y++)
            texture.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Pow(y / (float)(height - 1), 2f)));
        texture.Apply();
        return texture;
    }

    // Dải sáng dọc: đậm ở trên, trong suốt ở dưới.
    static Sprite ShineSprite
    {
        get
        {
            if (shineSprite != null) return shineSprite;
            var texture = CreateShineTexture();
            texture.hideFlags = HideFlags.HideAndDontSave;
            shineSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            shineSprite.name = "DialogueShine";
            shineSprite.hideFlags = HideFlags.HideAndDontSave;
            return shineSprite;
        }
    }
}
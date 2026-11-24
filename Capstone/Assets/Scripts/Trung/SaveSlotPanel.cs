using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

// Bảng danh sách ô save ở Main Menu. Tự dựng giao diện, không cần tạo Hierarchy.
// Gắn vào 1 object trong Canvas của MainMenu, rồi kéo vào ô "Save Slot Panel" của MainMenuController.
// Trong DemoScene: gắn thêm 1 cái nữa để nút "Lưu tiến trình" ở Cài đặt cho chọn ô.
public class SaveSlotPanel : MonoBehaviour
{
    public enum Mode { Load, NewGame, Save }

    [Header("Giao diện")]
    [Tooltip("Khung bảng. Để trống = khung tối viền vàng")]
    [SerializeField] Sprite panelSprite;
    [Tooltip("Hình nút. Để trống = nút màu")]
    [SerializeField] Sprite buttonSprite;
    [Tooltip("Font TextMeshPro có dấu tiếng Việt (dùng chung font với Options)")]
    [SerializeField] TMP_FontAsset font;
    [SerializeField] Color accent = new Color(1f, 0.84f, 0.35f);
    [SerializeField] Color textColor = new Color(0.97f, 0.93f, 0.84f);
    [SerializeField] Color mutedColor = new Color(0.75f, 0.68f, 0.55f);
    [SerializeField] Vector2 panelSize = new Vector2(1200f, 820f);

    RectTransform root, window, list, confirmBox;
    TextMeshProUGUI heading, confirmText;
    Action confirmYes;
    Mode mode;
    Action<string> onPicked;

    public bool IsOpen => root != null && root.gameObject.activeSelf;

    // ===== Dùng trong game (Cài đặt -> Lưu tiến trình) =====
    readonly List<(UIDocument doc, DisplayStyle display)> hiddenDocs = new List<(UIDocument, DisplayStyle)>();
    Action<string> inGameDone;

    /// <summary>Mở bảng chọn ô để lưu. done(message) được gọi khi lưu xong, done(null) khi bấm Quay lại.
    /// Trả về false nếu scene không có SaveSlotPanel.</summary>
    public static bool TryOpenInGame(Action<string> done)
    {
        var panel = FindFirstObjectByType<SaveSlotPanel>(FindObjectsInactive.Include);
        if (panel == null) return false;
        if (!panel.gameObject.activeInHierarchy) panel.gameObject.SetActive(true);
        panel.OpenSave(done);
        return true;
    }

    void OpenSave(Action<string> done)
    {
        inGameDone = done;
        // Menu Cài đặt là UI Toolkit, vẽ đè lên Canvas -> ẩn tạm đi khi bảng ô save đang mở.
        hiddenDocs.Clear();
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (doc.rootVisualElement == null) continue;
            hiddenDocs.Add((doc, doc.rootVisualElement.resolvedStyle.display));
            doc.rootVisualElement.style.display = DisplayStyle.None;
        }
        Show(Mode.Save, slot =>
        {
            SaveSlotSession.SaveInto(slot, out string message);
            FinishInGame(message);
        });
    }

    void FinishInGame(string message)
    {
        foreach (var (doc, display) in hiddenDocs)
            if (doc != null && doc.rootVisualElement != null) doc.rootVisualElement.style.display = display;
        hiddenDocs.Clear();
        var done = inGameDone;
        inGameDone = null;
        done?.Invoke(message);
    }

    /// <summary>Mở bảng. onPicked nhận id ô được chọn.</summary>
    public void Show(Mode showMode, Action<string> picked)
    {
        if (root == null) Build();
        mode = showMode;
        onPicked = picked;
        heading.text = mode == Mode.Load ? "TẢI GAME" : mode == Mode.Save ? "LƯU TIẾN TRÌNH" : "CHỌN Ô ĐỂ CHƠI MỚI";
        confirmBox.gameObject.SetActive(false);
        Refresh();
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        Debug.Log($"[SaveSlot] Mở bảng ({mode}) trong Canvas '{root.parent.name}'.");
    }

    public void Hide()
    {
        if (root != null) root.gameObject.SetActive(false);
        if (inGameDone != null) FinishInGame(null);   // Bấm Quay lại khi đang lưu trong game.
    }

    void Update()
    {
        if (!IsOpen || !Capstone.Game.UISystem.GameplayKeyboard.GetKeyDown(KeyCode.Escape)) return;
        if (confirmBox.gameObject.activeSelf) confirmBox.gameObject.SetActive(false);
        else Hide();
    }

    // ================= Danh sách =================
    void Refresh()
    {
        for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
        for (int i = 0; i < SaveSlotSession.Slots.Length; i++) BuildRow(SaveSlotSession.Slots[i], i);
    }

    void BuildRow(string slot, int index)
    {
        var info = SaveSlotSession.ReadInfo(slot);
        const float rowHeight = 170f;

        RectTransform row = NewRect($"Slot{index + 1}", list);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(0f, rowHeight);
        row.anchoredPosition = new Vector2(0f, -index * (rowHeight + 20f));
        var bg = row.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, info.exists ? 0.45f : 0.25f);

        // Cột trái: tên ô + thông tin
        var title = NewText("Title", row, SaveSlotSession.DisplayName(slot), 34, accent, TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        Anchor(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(30f, 15f), new Vector2(0f, -15f));

        string body;
        if (!info.exists) body = "<color=#9a8f7a>Trống</color>";
        else if (!info.readable) body = "<color=#ff8a7a>Save bị lỗi, không đọc được</color>";
        else
        {
            string name = string.IsNullOrEmpty(info.playerName) ? "Nhà thuần hóa" : info.playerName;
            string scene = string.IsNullOrEmpty(info.scene) ? "" : $"   •   {info.scene}";
            body = $"\n<b>{name}</b>   •   Cấp {info.level}{scene}\n" +
                   $"Thời gian chơi: {SaveSlotSession.FormatPlayTime(info.playSeconds)}   •   Vàng: {info.gold}   •   Đội: {info.partyCount} pet\n" +
                   $"<size=80%>Lưu lúc: {info.savedAtLocal:HH:mm  dd/MM/yyyy}</size>";
        }
        var text = NewText("Info", row, body, 28, textColor, TextAlignmentOptions.TopLeft);
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        Anchor(text.rectTransform, new Vector2(0f, 0f), new Vector2(0.68f, 1f), new Vector2(30f, 10f), new Vector2(0f, -20f));

        // Cột phải: nút
        if (mode == Mode.Load)
        {
            if (info.exists && info.readable)
            {
                var play = NewButton(row, "Chơi tiếp", new Vector2(220f, 64f), () => Pick(slot));
                Place((RectTransform)play.transform, new Vector2(1f, 0.5f), new Vector2(-290f, 0f));
            }
            if (info.exists)
            {
                var del = NewButton(row, "Xóa", new Vector2(130f, 64f), () =>
                    Ask($"Xóa {SaveSlotSession.DisplayName(slot)}?\nKhông thể khôi phục lại.", () =>
                    {
                        if (!SaveSlotSession.Delete(slot, out string error)) Debug.LogWarning("Xóa save lỗi: " + error);
                        Refresh();
                    }));
                Place((RectTransform)del.transform, new Vector2(1f, 0.5f), new Vector2(-95f, 0f));
            }
        }
        else if (mode == Mode.Save)
        {
            bool playing = slot == SaveSlotSession.Current && info.exists;
            string label = playing ? "Lưu" : info.exists ? "Ghi đè" : "Lưu vào đây";
            var pick = NewButton(row, label, new Vector2(220f, 64f), () =>
            {
                if (info.exists && !playing)
                    Ask($"Ghi đè {SaveSlotSession.DisplayName(slot)}?\nSave cũ ở ô này sẽ bị thay bằng tiến trình hiện tại.", () => Pick(slot));
                else Pick(slot);
            });
            Place((RectTransform)pick.transform, new Vector2(1f, 0.5f), new Vector2(-150f, 0f));
            if (playing)
            {
                var tag = NewText("Playing", row, "ĐANG CHƠI", 22, accent, TextAlignmentOptions.Right);
                Anchor(tag.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-300f, -40f), new Vector2(-30f, -10f));
            }
        }
        else
        {
            string label = info.exists ? "Ghi đè" : "Chơi mới";
            var pick = NewButton(row, label, new Vector2(220f, 64f), () =>
            {
                if (info.exists)
                    Ask($"Ghi đè {SaveSlotSession.DisplayName(slot)}?\nDữ liệu cũ sẽ bị xóa khi vào game.", () => Pick(slot));
                else Pick(slot);
            });
            Place((RectTransform)pick.transform, new Vector2(1f, 0.5f), new Vector2(-150f, 0f));
        }
    }

    void Pick(string slot)
    {
        if (root != null) root.gameObject.SetActive(false);
        var picked = onPicked;
        onPicked = null;
        picked?.Invoke(slot);
    }

    void Ask(string message, Action yes)
    {
        confirmText.text = message;
        confirmYes = yes;
        confirmBox.gameObject.SetActive(true);
        confirmBox.SetAsLastSibling();
    }

    // ================= Dựng giao diện =================
    void Build()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;
        if (canvas == null)
        {
            var go = new GameObject("SaveSlotCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 500;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            parent = go.transform;
        }

        root = NewRect("SaveSlotPanel", parent);
        Stretch(root);
        root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        window = NewRect("Window", root);
        window.sizeDelta = panelSize;
        var windowImage = window.gameObject.AddComponent<Image>();
        if (panelSprite != null)
        {
            windowImage.sprite = panelSprite;
            windowImage.type = panelSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        else
        {
            windowImage.color = new Color(0.16f, 0.11f, 0.08f, 0.97f);
            var outline = window.gameObject.AddComponent<Outline>();
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(4f, -4f);
        }

        heading = NewText("Title", window, "TẢI GAME", 56, accent, TextAlignmentOptions.Center);
        heading.fontStyle = FontStyles.Bold;
        Anchor(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -120f), new Vector2(0f, -30f));

        list = NewRect("Slots", window);
        Anchor(list, Vector2.zero, Vector2.one, new Vector2(50f, 130f), new Vector2(-50f, -140f));

        var back = NewButton(window, "Quay lại", new Vector2(240f, 70f), Hide);
        Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 65f));

        // Hộp hỏi lại
        confirmBox = NewRect("Confirm", root);
        Stretch(confirmBox);
        confirmBox.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var box = NewRect("Box", confirmBox);
        box.sizeDelta = new Vector2(700f, 320f);
        var boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = new Color(0.2f, 0.14f, 0.09f, 1f);
        box.gameObject.AddComponent<Outline>().effectColor = accent;
        confirmText = NewText("Text", box, "", 34, textColor, TextAlignmentOptions.Center);
        confirmText.textWrappingMode = TextWrappingModes.Normal;
        Anchor(confirmText.rectTransform, Vector2.zero, Vector2.one, new Vector2(30f, 110f), new Vector2(-30f, -30f));
        var yes = NewButton(box, "Đồng ý", new Vector2(200f, 64f), () =>
        {
            confirmBox.gameObject.SetActive(false);
            var action = confirmYes; confirmYes = null;
            action?.Invoke();
        });
        Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(-120f, 60f));
        var no = NewButton(box, "Hủy", new Vector2(200f, 64f), () => confirmBox.gameObject.SetActive(false));
        Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(120f, 60f));
        confirmBox.gameObject.SetActive(false);

        root.gameObject.SetActive(false);
    }

    Button NewButton(RectTransform parent, string text, Vector2 size, Action onClick)
    {
        RectTransform rect = NewRect(text, parent);
        rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>();
        if (buttonSprite != null)
        {
            image.sprite = buttonSprite;
            image.type = buttonSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        else image.color = new Color(0.36f, 0.24f, 0.13f, 1f);

        var button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.9f, 0.65f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(() => onClick());

        var label = NewText("Text", rect, text, 30, textColor, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        Stretch(label.rectTransform);
        return button;
    }

    TextMeshProUGUI NewText(string objectName, RectTransform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect(objectName, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    static RectTransform NewRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
    }
}
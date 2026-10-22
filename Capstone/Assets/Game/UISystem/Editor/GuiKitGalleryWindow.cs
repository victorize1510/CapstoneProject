using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem.Editor {
    public sealed class GuiKitGalleryWindow : EditorWindow {
        public const string GalleryPath = "Assets/ThangTest/UIThang/GUIArt/UI/GuiKitGallery.uxml";
        public static readonly string[] IconIds = {
            "back", "forward", "close", "search", "filter", "grid", "list", "star", "chevron_down", "chevron_up",
            "plus", "minus", "check", "profile", "paw", "box", "bag", "quest", "map", "book", "settings", "coin",
            "heart", "mana", "stats", "rename", "trash", "info", "lock", "sort", "compass", "camera", "leaf",
            "volume", "mouse", "keyboard", "refresh", "exit", "warning"
        };
        [MenuItem("Game Tools/GameToolThang/GUI/Open Shared GUI Gallery")]
        public static void Open() {
            var window = GetWindow<GuiKitGalleryWindow>("Bộ GUI dùng chung");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }
        public void CreateGUI() {
            rootVisualElement.Clear();
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GalleryPath);
            if (template == null) { rootVisualElement.Add(new Label("Không tìm thấy màn mẫu GUI.")); return; }
            template.CloneTree(rootVisualElement);
            Populate(rootVisualElement);
        }
        public static void Populate(VisualElement root) {
            var status = root.Q<Label>("gallery-status");
            void Report(string value) => status.text = value;
            foreach (var id in IconIds) {
                var cell = new VisualElement(); cell.AddToClassList("gui-gallery-icon-cell");
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("gui-icon");
                icon.style.backgroundImage = new StyleBackground(GuiKitAssets.Icon(id));
                cell.Add(icon);
                var caption = new Label(id) { pickingMode = PickingMode.Ignore }; caption.AddToClassList("gui-caption");
                cell.Add(caption); root.Q("gallery-icons").Add(cell);
            }
            var search = root.Q<TextField>("gallery-search");
            root.Q<Button>("gallery-clear").clicked += () => { search.value = string.Empty; search.Focus(); };
            search.RegisterValueChangedCallback(e => Report(string.IsNullOrEmpty(e.newValue) ? "Đã xóa tìm kiếm." : "Đang nhập: " + e.newValue));
            var sort = root.Q<DropdownField>("gallery-sort");
            sort.choices = new List<string> { "Mặc định", "Theo tên", "Theo loại" };
            sort.SetValueWithoutNotify("Mặc định");
            sort.RegisterValueChangedCallback(e => Report("Sắp xếp mẫu: " + e.newValue));
            foreach (var name in new[] { "gallery-back", "gallery-primary", "gallery-secondary", "gallery-danger", "gallery-icon", "gallery-cancel", "gallery-confirm" }) {
                string action = name;
                root.Q<Button>(name).clicked += () => Report("Đã thử nút: " + action + ". Không có thao tác gameplay.");
            }
            var all = root.Q<Button>("gallery-tab-all");
            var favorite = root.Q<Button>("gallery-tab-favorite");
            all.clicked += () => { all.AddToClassList("is-selected"); favorite.RemoveFromClassList("is-selected"); Report("Danh mục: Tất cả"); };
            favorite.clicked += () => { favorite.AddToClassList("is-selected"); all.RemoveFromClassList("is-selected"); Report("Danh mục: Yêu thích"); };
            root.Q<Toggle>("gallery-toggle").RegisterValueChangedCallback(e => Report(e.newValue ? "Gợi ý: Bật" : "Gợi ý: Tắt"));
            root.Q<Slider>("gallery-slider").RegisterValueChangedCallback(e => Report("Âm lượng mẫu: " + Mathf.RoundToInt(e.newValue) + "%"));
            // All callbacks belong to this disposable tree, not EditorApplication or game services.
        }
    }
}

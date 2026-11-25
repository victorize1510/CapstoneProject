using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.UISystem;
using GDS.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.WorldFeedback {
    /// <summary>Three non-interactive rows shared by purchase, quest and future pickup adapters.</summary>
    public sealed class AcquisitionToastView : MonoBehaviour {
        sealed class Row {
            public RectTransform Rect; public CanvasGroup Group; public Image Icon;
            public Text Name, Quantity; public string ItemId; public int Count; public float Born, MergeUntil;
        }
        static AcquisitionToastView live;
        readonly List<Row> rows = new List<Row>(3);
        readonly Queue<Row> unused = new Queue<Row>(3);
        RectTransform root;
        public int VisibleCount => rows.Count;
        public int DisplayedReceipts { get; private set; }
        public string LastItemName { get; private set; }
        public static void ShowPending(string reason) {
            if(string.IsNullOrWhiteSpace(reason))return;
            var view=Ensure();
            foreach(var row in view.rows)if(row.ItemId=="pve:pending")return;
            if(view.rows.Count==3){view.unused.Enqueue(view.rows[0]);view.rows.RemoveAt(0);}
            var notice=view.unused.Dequeue();notice.Rect.gameObject.SetActive(true);
            notice.ItemId="pve:pending";notice.Icon.enabled=false;notice.Quantity.text="";
            notice.Name.text=reason;notice.Name.horizontalOverflow=HorizontalWrapMode.Wrap;
            V.Top(notice.Name.rectTransform,12,5,276,46);
            notice.Born=Time.unscaledTime;notice.MergeUntil=0;notice.Group.alpha=0;view.rows.Add(notice);
        }
        public static AcquisitionToastView Ensure() {
            if (live != null) return live;
            var go = new GameObject("Item Acquisition Feedback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(go); return go.AddComponent<AcquisitionToastView>();
        }
        void Awake() {
            live = this; root = (RectTransform)transform;
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 130;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            // Deliberately no GraphicRaycaster: these decorations cannot steal a pointer.
            SceneManager.activeSceneChanged += SceneChanged;
            for (int i = 0; i < 3; i++) { var row = CreateRow(); row.Rect.gameObject.SetActive(false); unused.Enqueue(row); }
        }
        void OnDestroy() { SceneManager.activeSceneChanged -= SceneChanged; if (live == this) live = null; }
        void SceneChanged(Scene previous, Scene current) { foreach (var row in rows) { row.Rect.gameObject.SetActive(false); unused.Enqueue(row); } rows.Clear(); }
        void Update() {
            bool hidden = PetEvolutionPresentationController.AnyPlaying;
            foreach (var row in rows) row.Group.alpha = hidden ? 0 : row.Group.alpha;
            if (hidden) { foreach (var row in rows) { row.Born += Time.unscaledDeltaTime; row.MergeUntil += Time.unscaledDeltaTime; } return; }
            int read = 0;
            while (read++ < 8 && ItemAcquisitionFeedback.TryRead(out var receipt, rows.Count < 3)) Push(receipt);
            for (int i = rows.Count - 1; i >= 0; i--) {
                float age = Time.unscaledTime - rows[i].Born;
                if (age > 2.85f) { rows[i].Rect.gameObject.SetActive(false); unused.Enqueue(rows[i]); rows.RemoveAt(i); continue; }
                rows[i].Group.alpha = Mathf.Min(Mathf.Clamp01(age / .14f), 1 - Mathf.Clamp01((age - 2.5f) / .35f));
            }
            for (int i = 0; i < rows.Count; i++) {
                float slide = (1 - Mathf.Clamp01((Time.unscaledTime - rows[i].Born) / .14f)) * 14;
                V.Place(rows[i].Rect, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-24 + slide, 62 - i * 64), new Vector2(300, 56));
            }
        }
        void Push(ItemAcquisitionReceipt receipt) {
            if (receipt.Item == null&&receipt.Source!=AcquisitionSource.Gold&&receipt.Source!=AcquisitionSource.Capture && receipt.Source!=AcquisitionSource.Progression) return;
            bool single = receipt.Source == AcquisitionSource.Capture || receipt.Source == AcquisitionSource.Progression;
            string id = single?receipt.TransactionId:receipt.Source==AcquisitionSource.Gold?"currency:gold":receipt.Item is Inventory.MonsterItemDefinition def ? def.StableId : receipt.Item.name;
            foreach (var row in rows) if (row.ItemId == id && Time.unscaledTime <= row.MergeUntil) {
                row.Count = (int)Math.Min(int.MaxValue, (long)row.Count + receipt.Quantity);
                row.Quantity.text = "×" + row.Count; row.Born = Time.unscaledTime;
                LastItemName = row.Name.text; DisplayedReceipts++; return;
            }
            if (rows.Count == 3) { unused.Enqueue(rows[0]); rows.RemoveAt(0); }
            var reused = unused.Dequeue(); reused.Rect.gameObject.SetActive(true);
            reused.Rect.name = "Acquired " + id; reused.Icon.sprite = receipt.Icon; reused.Icon.enabled = receipt.Icon != null;
            reused.Name.text = receipt.DisplayName;
            float maxNameWidth=single?230:180;
            V.Top(reused.Name.rectTransform,58,5,maxNameWidth,46);
            reused.Name.horizontalOverflow = receipt.Source == AcquisitionSource.Progression ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            if (receipt.Source != AcquisitionSource.Progression && reused.Name.preferredWidth > maxNameWidth) {
                string original = receipt.DisplayName; int low = 0, high = original.Length;
                while (low < high) { int mid = (low + high + 1) / 2; reused.Name.text = original.Substring(0, mid) + "…"; if (reused.Name.preferredWidth <= maxNameWidth) low = mid; else high = mid - 1; }
                reused.Name.text = original.Substring(0, low).TrimEnd() + "…";
            }
            reused.Quantity.text = single?"":"×" + receipt.Quantity; reused.ItemId = id; reused.Count = receipt.Quantity;
            reused.Born = Time.unscaledTime; reused.MergeUntil = Time.unscaledTime + .4f;
            reused.Group.alpha = 0; rows.Add(reused);
            LastItemName = receipt.DisplayName; DisplayedReceipts++;
        }
        Row CreateRow() {
            var rect = V.Rect(root, "Acquisition row");
            var frame = V.Frame(rect, "Surface", HudFrameGraphic.Shape.Chamfered,
                new Color(.12f, .16f, .24f, .87f), new Color(.73f, .71f, .66f, .72f), 1, 7);
            V.Stretch(frame.rectTransform); frame.raycastTarget = false;
            var image = V.Image(rect, "Item"); image.preserveAspect = true;
            image.raycastTarget = false; V.Top(image.rectTransform, 8, 8, 40, 40);
            var name = V.Text(rect, "Name", "", 18, TextAnchor.MiddleLeft);
            name.fontStyle = FontStyle.Bold; name.color = new Color(.94f, .94f, .96f); name.raycastTarget = false;
            name.horizontalOverflow = HorizontalWrapMode.Wrap; name.verticalOverflow = VerticalWrapMode.Truncate;
            V.Top(name.rectTransform, 58, 5, 180, 46);
            var quantity = V.Text(rect, "Quantity", "", 18, TextAnchor.MiddleRight);
            quantity.color = new Color(.95f, .89f, .77f); quantity.raycastTarget = false;
            V.Top(quantity.rectTransform, 238, 5, 52, 46);
            var group = rect.gameObject.AddComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            return new Row { Rect = rect, Group = group, Icon = image, Name = name, Quantity = quantity };
        }
    }
}

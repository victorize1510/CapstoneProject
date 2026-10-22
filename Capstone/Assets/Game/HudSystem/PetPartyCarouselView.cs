using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.HudSystem {
    // Focus is local UI state. Only confirmation requests the existing summon sequence.
    public sealed class PetPartyCarouselView : MonoBehaviour, IScrollHandler {
        sealed class Tile {
            public RectTransform rect;
            public Button button;
            public HudFrameGraphic frame;
            public CanvasGroup group;
            public Image icon;
            public Text number;
            public Text empty;
            public HudFrameGraphic emptyIcon;
            public GameObject diamond;
            public int slot;
            public float distance;
        }
        readonly Tile[] tiles = new Tile[7];
        readonly Tile[] drawOrder = new Tile[7];
        readonly PetSlotHudData[] data = new PetSlotHudData[6];
        const float Duration = .22f;
        const float CardWidth = 92f;
        Action<int> confirm;
        int focus, lastActive = -1, lastSummoned = -1;
        float phase, startPhase, targetPhase, elapsed = Duration;
        bool initialized;
        public int FocusIndex => focus;
        public bool IsAnimating => elapsed < Duration;
        public static int Wrap(int index) => (index % 6 + 6) % 6;

        public void Build(Action<int> onConfirm) {
            confirm = onConfirm;
            RectTransform viewport = V.Rect(transform, "LoopViewport");
            V.Bottom(viewport, 56f, 0f, V.PetRowWidth - 112f, 152f);
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(8, 0);
            for (int i = 0; i < tiles.Length; i++) {
                var button = V.Button(viewport, "CarouselTile" + i, HudFrameGraphic.Shape.Chamfered, 12f);
                var tile = new Tile { button = button, rect = (RectTransform)button.transform,
                    frame = button.GetComponent<HudFrameGraphic>(), group = button.gameObject.AddComponent<CanvasGroup>() };
                V.Place(tile.rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(92, 116));
                tile.icon = V.Image(tile.rect, "Portrait"); V.Top(tile.icon.rectTransform, 9, 8, 74, 78);
                tile.empty = V.Text(tile.rect, "Empty", "—", 30, TextAnchor.MiddleCenter); V.Top(tile.empty.rectTransform, 9, 8, 74, 78);
                tile.empty.color = V.Sage;
                tile.emptyIcon = V.Frame(tile.rect, "EmptyPetIcon", HudFrameGraphic.Shape.Pawprint,
                    V.Sage, Color.clear, 0f);
                V.Top(tile.emptyIcon.rectTransform, 21, 19, 50, 54);
                tile.number = V.Text(tile.rect, "PartySlotNumber", "", 23, TextAnchor.MiddleCenter);
                V.Bottom(tile.number.rectTransform, 12, 0, 68, 28);
                V.Diamond(tile.rect, "FocusMarker", 0f, 60f, V.Gold);
                tile.diamond = tile.rect.Find("FocusMarker").gameObject;
                button.onClick.AddListener(() => ClickSlot(tile.slot)); tiles[i] = tile;
            }
            Arrow("Previous", 0f, -1); Arrow("Next", V.PetRowWidth - 56f, 1);
            Layout();
        }
        void Arrow(string name, float x, int direction) {
            Button arrow = V.Button(transform, name, HudFrameGraphic.Shape.Chamfered, 28f);
            V.Bottom((RectTransform)arrow.transform, x, 32f, 56f, 88f);
            arrow.GetComponent<HudFrameGraphic>().Configure(HudFrameGraphic.Shape.Chamfered, V.Paper, Color.clear, 0, 28f);
            arrow.GetComponent<CanvasRenderer>().cullTransparentMesh = false;
            ColorBlock colors = arrow.colors;
            colors.normalColor = Color.clear; colors.highlightedColor = new Color(1f, 1f, 1f, .18f);
            colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(1f, 1f, 1f, .32f);
            colors.disabledColor = Color.clear; colors.fadeDuration = .12f; arrow.colors = colors;
            var shape = direction < 0 ? HudFrameGraphic.Shape.ChevronLeft : HudFrameGraphic.Shape.ChevronRight;
            var shadow = V.Frame(arrow.transform, "ChevronShadow", shape, V.Forest, Color.clear, 3.5f);
            V.Place(shadow.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -2), new Vector2(36, 48));
            var chevron = V.Frame(arrow.transform, "Chevron", shape, V.Paper, Color.clear, 2.8f);
            V.Place(chevron.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(36, 48));
            arrow.onClick.AddListener(() => MoveFocus(direction));
        }
        public void Refresh(IReadOnlyList<PetSlotHudData> slots) {
            int active = -1, summoned = -1;
            for (int i = 0; i < 6; i++) {
                data[i] = slots != null && i < slots.Count ? slots[i] : default;
                if (data[i].selected) active = i;
                if (data[i].occupied && data[i].summoned) summoned = i;
            }
            if (!initialized) {
                initialized = true;
                focus = summoned >= 0 ? summoned : active >= 0 ? active : 0;
                phase = targetPhase = startPhase = focus;
            } else if (summoned >= 0 && summoned != lastSummoned) {
                // Also recenter when the same pet is recalled and summoned again.
                Focus(summoned);
            } else if (active >= 0 && active != lastActive) Focus(active);
            lastActive = active; lastSummoned = summoned;
            Layout();
        }
        public void MoveFocus(int direction) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, direction < 0 ? "previous" : "next")) return;
            if (InventoryInputController.GameplayInputBlocked) return;
            Focus(Wrap(focus + direction));
        }
        public void Focus(int index) {
            index = Wrap(index);
            int delta = index - focus;
            if (delta > 3) delta -= 6;
            if (delta < -3) delta += 6;
            if (delta == 0) return;
            focus = index; startPhase = phase; targetPhase += delta; elapsed = 0f;
        }
        public void ConfirmFocus() {
            if (!InventoryInputController.GameplayInputBlocked && data[focus].occupied) confirm?.Invoke(focus);
        }
        void ClickSlot(int index) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "slot:" + index)) return;
            if (InventoryInputController.GameplayInputBlocked) return;
            if (index == focus) ConfirmFocus(); else Focus(index);
        }
        public void OnScroll(PointerEventData evt) {
            if (Mathf.Abs(evt.scrollDelta.y) > .01f) MoveFocus(evt.scrollDelta.y < 0f ? 1 : -1);
        }
        void Update() {
            if (elapsed >= Duration) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            phase = Mathf.Lerp(startPhase, targetPhase, 1f - Mathf.Pow(1f - t, 3f));
            if (t >= 1f) phase = startPhase = targetPhase = focus;
            Layout();
        }
        public static float ScaleAt(float distance) {
            distance = Mathf.Abs(distance);
            return distance <= 1f ? Mathf.Lerp(1.1f, .95f, distance) : Mathf.Max(.55f, 1f - distance * .05f);
        }
        public static float PositionAt(float distance) {
            distance = Mathf.Abs(distance);
            int whole = Mathf.FloorToInt(distance);
            float x = 0f;
            // At 50% overlap, the next center sits at the inner card's outside edge.
            for (int i = 0; i < whole; i++) x += CardWidth * ScaleAt(i) * .5f;
            return x + CardWidth * ScaleAt(whole) * .5f * (distance - whole);
        }
        void Layout() {
            int center = Mathf.RoundToInt(phase);
            for (int i = 0; i < tiles.Length; i++) {
                Tile tile = tiles[i]; if (tile == null) continue;
                int logical = center + i - 3;
                tile.slot = Wrap(logical);
                float distance = logical - phase, abs = Mathf.Abs(distance);
                tile.distance = abs; drawOrder[i] = tile;
                float scale = ScaleAt(abs);
                tile.rect.anchoredPosition = new Vector2(Mathf.Sign(distance) * PositionAt(abs), 0f);
                tile.rect.localScale = Vector3.one * scale;
                tile.group.alpha = abs <= 2f ? 1f - abs * .25f : Mathf.Max(0f, .5f * (3f - abs));
                tile.group.blocksRaycasts = abs < 2.4f;
                PetSlotHudData slot = data[tile.slot];
                tile.button.interactable = true; // Empty slots can be focused, but never summoned.
                V.Icon(tile.icon, slot.occupied ? slot.icon : null);
                tile.emptyIcon.gameObject.SetActive(!slot.occupied);
                tile.empty.gameObject.SetActive(slot.occupied && slot.icon == null);
                tile.empty.text = "PET";
                tile.number.text = (tile.slot + 1).ToString();
                bool selected = tile.slot == focus && abs < .55f;
                tile.frame.SetBorder(selected ? V.Gold : V.Line, selected ? 1.9f : 1.2f);
                tile.diamond.SetActive(selected);
            }
            // Recycle only the invisible edge views; never deactivate them during a wrap.
            // Render distant cards first so both sides are covered by the nearer card.
            for (int i = 1; i < drawOrder.Length; i++) {
                Tile value = drawOrder[i]; int j = i - 1;
                while (j >= 0 && drawOrder[j].distance < value.distance) { drawOrder[j + 1] = drawOrder[j]; j--; }
                drawOrder[j + 1] = value;
            }
            for (int i = 0; i < drawOrder.Length; i++) {
                if (drawOrder[i].rect.GetSiblingIndex() != i) drawOrder[i].rect.SetSiblingIndex(i);
            }
        }
    }
}

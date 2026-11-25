using System;
using System.Text;
using Capstone.Game.CreatureSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UITK = UnityEngine.UIElements;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.HudSystem {
    // Both UI backends use the same ordering, sprite IDs and live status text.
    internal sealed class CombatStatusHud : IDisposable {
        static readonly (CombatAilment kind, string id, string caption)[] definitions = {
            (CombatAilment.Stun, "stun", "Choáng"), (CombatAilment.Root, "root", "Trói chân"),
            (CombatAilment.Poison, "poison", "Nhiễm độc"), (CombatAilment.Burn, "burn", "Bỏng"),
            (CombatAilment.Bleed, "bleed", "Chảy máu"), (CombatAilment.Shock, "shock", "Nhiễm điện"),
            (CombatAilment.Slow, "slow", "Chậm"), (CombatAilment.DefenseDown, "def_down", "Giảm giáp vật lý"),
            (CombatAilment.MagicDefenseDown, "spdef_down", "Giảm kháng phép"),
            (CombatAilment.Invulnerable, "invuln", "Miễn sát thương"), (CombatAilment.Shield, "shield", "Khiên HP"),
            (CombatAilment.Regeneration, "regen", "Hồi HP theo thời gian"), (CombatAilment.PowerUp, "power_up", "Tăng công"),
            (CombatAilment.DefenseUp, "defense_up", "Tăng phòng thủ"), (CombatAilment.Rain, "rain", "Mưa tăng cường"),
            (CombatAilment.FruitAttack, "power_up", "Tăng công vật lý từ trái cây"),
            (CombatAilment.FruitMagicAttack, "power_up", "Tăng công phép từ trái cây"),
            (CombatAilment.FruitDefense, "defense_up", "Tăng giáp vật lý từ trái cây"),
            (CombatAilment.FruitMagicDefense, "defense_up", "Tăng kháng phép từ trái cây"),
            (CombatAilment.FruitSpeed, "generic_buff", "Tăng tốc độ từ trái cây")
        };
        const int Limit = 8;
        static readonly Sprite[] sprites = new Sprite[definitions.Length];
        static bool loaded;
        readonly int[] active = new int[definitions.Length];
        readonly Action<int, Sprite, string, bool> render;
        readonly Action<string> showTip;
        readonly Action<bool> showRow;
        CombatBleed state;
        int count, hovering = -1;

        CombatStatusHud(Action<int, Sprite, string, bool> render, Action<string> showTip, Action<bool> showRow) {
            this.render = render; this.showTip = showTip; this.showRow = showRow;
            if (!loaded) {
                for (int i = 0; i < definitions.Length; i++)
                    sprites[i] = Resources.Load<Sprite>("StatusIcons/status_" + definitions[i].id);
                loaded = true;
            }
        }

        public void Bind(CombatBleed value) {
            if (ReferenceEquals(state, value)) return;
            if (state != null) state.Changed -= Refresh;
            state = value;
            if (state != null) state.Changed += Refresh;
            Hover(-1);
            Refresh();
        }

        void Refresh() {
            count = 0;
            for (int i = 0; i < definitions.Length; i++)
                if (sprites[i] != null && state != null && state.TryRead(definitions[i].kind, out _)) active[count++] = i;
            for (int i = 0; i < Limit; i++) {
                if (i >= count) { render(i, null, "", false); continue; }
                int index = active[i];
                state.TryRead(definitions[index].kind, out var status);
                render(i, sprites[index], status.Remaining > 0 ? Mathf.CeilToInt(status.Remaining) + "s" : "", true);
            }
            render(Limit, null, count > Limit ? "+" + (count - Limit) : "", count > Limit);
            showRow(count > 0);
            RefreshTooltip();
        }

        void Hover(int index) { hovering = index; RefreshTooltip(); }

        void RefreshTooltip() {
            if (hovering == Limit && count > Limit) {
                var text = new StringBuilder();
                for (int i = Limit; i < count; i++) {
                    if (text.Length > 0) text.AppendLine();
                    text.Append(Describe(active[i]));
                }
                showTip(text.ToString());
            } else showTip(hovering >= 0 && hovering < count ? Describe(active[hovering]) : "");
        }

        string Describe(int index) {
            var entry = definitions[index];
            if (state == null || !state.TryRead(entry.kind, out var info)) return "";
            string amount = entry.kind switch {
                CombatAilment.Shield => $" · {info.Value:0.#} HP khiên",
                CombatAilment.Regeneration => $" · {info.Value:0.#} HP/giây",
                CombatAilment.Burn or CombatAilment.Poison or CombatAilment.Bleed => $" · {info.Value:0.#} sát thương/giây",
                CombatAilment.Invulnerable or CombatAilment.Root or CombatAilment.Stun => "",
                _ => $" · {info.Value * 100:0.#}%"
            };
            return $"{entry.caption}{amount} · {info.Remaining:0.0} giây" +
                (info.Count > 1 ? $" · ×{info.Count}" : "");
        }

        public void Dispose() => Bind(null);

        public static CombatStatusHud BuildCanvas(RectTransform row) {
            const int size = 84, stride = 89, columns = 4;
            var icons = new Image[Limit + 1];
            var badges = new Text[Limit + 1];
            var tipRoot = V.Rect(row, "StatusDetails");
            V.Top(tipRoot, 0, -74, 260, 68);
            var background = V.Image(tipRoot, "Background"); V.Stretch(background.rectTransform);
            background.color = new Color(.06f, .09f, .16f, .98f); background.raycastTarget = false;
            var tip = HubR11Art.Label(tipRoot, "Text", "", 15, TextAnchor.MiddleLeft);
            V.Stretch(tip.rectTransform, 8);
            var hud = new CombatStatusHud((i, sprite, badge, visible) => {
                icons[i].sprite = sprite;
                icons[i].color = sprite != null ? Color.white : new Color(.15f, .20f, .29f, .94f);
                badges[i].text = badge;
                icons[i].gameObject.SetActive(visible);
            }, text => {
                tip.text = text;
                if (text.Length > 0) {
                    float h = Mathf.Max(36f, tip.preferredHeight + 16);
                    V.Top(tipRoot, 0, -stride * ((Limit + columns) / columns) - h - 4, 260, h);
                }
                tipRoot.gameObject.SetActive(text.Length > 0);
            }, visible => row.gameObject.SetActive(visible));
            row.gameObject.AddComponent<CombatStatusClock>().Tick = hud.Refresh;
            for (int i = 0; i <= Limit; i++) {
                int index = i;
                var icon = icons[i] = V.Image(row, "Status" + i);
                icon.preserveAspect = true; icon.raycastTarget = true;
                V.Top(icon.rectTransform, (i % columns) * stride, -(i / columns + 1) * stride, size, size);
                var badge = badges[i] = HubR11Art.Label(icon.transform, "Count", "", 12,
                    i == Limit ? TextAnchor.MiddleCenter : TextAnchor.LowerRight);
                V.Stretch(badge.rectTransform, 1);
                var trigger = icon.gameObject.AddComponent<CombatStatusHover>();
                trigger.Enter = () => hud.Hover(index);
                trigger.Exit = () => hud.Hover(-1);
            }
            hud.Refresh();
            return hud;
        }

        public static CombatStatusHud BuildToolkit(UITK.VisualElement parent) {
            var row = new UITK.VisualElement { name = "EnemyStatusRow", pickingMode = UITK.PickingMode.Ignore };
            row.style.alignSelf = UITK.Align.Center;
            row.style.flexDirection = UITK.FlexDirection.Row;
            row.style.marginTop = 4;
            row.style.flexWrap = UITK.Wrap.Wrap;
            row.style.width = 336;
            parent.Add(row);
            var icons = new UITK.Image[Limit + 1];
            var badges = new UITK.Label[Limit + 1];
            var tip = new UITK.Label { name = "EnemyStatusDetails", pickingMode = UITK.PickingMode.Ignore };
            tip.style.color = Color.white;
            tip.style.backgroundColor = new Color(.06f, .09f, .16f, .98f);
            tip.style.whiteSpace = UITK.WhiteSpace.Normal;
            tip.style.alignSelf = UITK.Align.Center;
            tip.style.paddingLeft = tip.style.paddingRight = 8;
            parent.Add(tip);
            UITK.IVisualElementScheduledItem clock = null;
            var hud = new CombatStatusHud((i, sprite, badge, visible) => {
                icons[i].sprite = sprite; badges[i].text = badge;
                icons[i].style.display = visible ? UITK.DisplayStyle.Flex : UITK.DisplayStyle.None;
            }, text => {
                tip.text = text;
                tip.style.display = text.Length > 0 ? UITK.DisplayStyle.Flex : UITK.DisplayStyle.None;
            }, visible => {
                row.style.display = visible ? UITK.DisplayStyle.Flex : UITK.DisplayStyle.None;
                if (visible && clock?.isActive == false) clock.Resume();
                if (!visible && clock?.isActive == true) clock.Pause();
            });
            clock = row.schedule.Execute(hud.Refresh).Every(100);
            for (int i = 0; i <= Limit; i++) {
                int index = i;
                var icon = icons[i] = new UITK.Image { scaleMode = ScaleMode.ScaleToFit };
                icon.style.width = icon.style.height = 81;
                icon.style.marginRight = 3;
                var badge = badges[i] = new UITK.Label { pickingMode = UITK.PickingMode.Ignore };
                badge.style.color = Color.white; badge.style.fontSize = 12;
                badge.style.position = UITK.Position.Absolute; badge.style.right = 0; badge.style.bottom = 0;
                icon.Add(badge); row.Add(icon);
                icon.RegisterCallback<UITK.PointerEnterEvent>(_ => hud.Hover(index));
                icon.RegisterCallback<UITK.PointerLeaveEvent>(_ => hud.Hover(-1));
            }
            hud.Refresh();
            return hud;
        }
    }

    internal sealed class CombatStatusHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        internal Action Enter, Exit;
        bool inside;
        public void OnPointerEnter(PointerEventData _) { inside = true; Enter?.Invoke(); }
        public void OnPointerExit(PointerEventData _) { inside = false; Exit?.Invoke(); }
        void OnDisable() { if (inside) OnPointerExit(null); }
    }

    internal sealed class CombatStatusClock : MonoBehaviour {
        internal Action Tick;
        float next;
        void Update() {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + .1f;
            Tick?.Invoke();
        }
    }
}

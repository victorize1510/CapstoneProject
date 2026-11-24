using System.Globalization;
using Capstone.Game.CreatureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Profiling;

namespace Capstone.Game.HudSystem {
    /// <summary>Thirty-two reusable labels; cosmetic scatter never consumes combat RNG.</summary>
    [DefaultExecutionOrder(90)]
    public sealed class FloatingCombatText : MonoBehaviour {
        const int Capacity = 32;
        const float Duration = .9f;
        static readonly Color CriticalDamageColor = new Color32(255, 77, 77, 255);
        static FloatingCombatText live;
        sealed class Entry {
            public Text label, matchup; public CombatDamageMark mark; public Vector3 world; public float born;
            public Transform anchor;
            public Vector2 control, end, start; public Color color, matchupColor; public bool active, critical, dot;
        }
        readonly Entry[] entries = new Entry[Capacity];
        RectTransform surface;
        Camera worldCamera;
        int next, serial;
        static readonly ProfilerMarker SpawnMarker = new ProfilerMarker("Capstone.Combat.DamageText.Spawn");
        static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("Capstone.Combat.DamageText.Update");
        public Font NumericFont { get; private set; }
        public int ActiveCount { get { int n = 0; foreach (var e in entries) if (e != null && e.active) n++; return n; } }
        public int SpawnedCount { get; private set; }
        public float LastDamage { get; private set; }
        public string LastText { get; private set; }
        public bool LastCritical { get; private set; }
        public int HealingSpawnedCount { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic() => live = null;
        public static void ShowDamage(Vector3 point, float amount, CreatureDamageKind kind) {
            ShowDamage(new CombatDamageResult(new CombatHit(null, "legacy", 0, amount, kind, false, false, point), null, amount, false));
        }
        public static void ShowDamage(CombatDamageResult result) =>
            ShowNumber(result.Hit.Point, result.HpLost, result.Hit.Critical, result.Hit.DamageOverTime, false, result.Effectiveness,
                anchor: result.VisualAnchor);
        /// <summary>Only call after an actual successful HP transaction, never from a preview or HP refresh.</summary>
        public static void ShowHealing(Vector3 point, float hpGained) => ShowNumber(point, hpGained, false, false, true, 1f);
        public static void ShowDodge(Vector3 point) => ShowNumber(point, 0f, false, false, false, 1f, dodge: true);
        static void ShowNumber(Vector3 hitPoint, float amount, bool critical, bool dot, bool healing, float effectiveness,
            bool dodge = false, Transform anchor = null) {
            if (!Application.isPlaying || (!dodge && amount <= 0) || float.IsNaN(amount) || float.IsInfinity(amount)
                || InventoryInputController.GameplayInputBlocked || PetEvolutionPresentationController.AnyPlaying) return;
            var camera = Camera.main;
            Vector3 point = anchor != null ? anchor.position : hitPoint + Vector3.up * .35f;
            if (camera == null) return;
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= 0 || viewport.x < -.05f || viewport.x > 1.05f || viewport.y < -.05f || viewport.y > 1.05f) return;
            if (live == null) {
                var go = new GameObject("Floating Combat Damage", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                live = go.AddComponent<FloatingCombatText>(); DontDestroyOnLoad(go);
            }
            live.worldCamera = camera; live.Push(point, amount, critical, dot, healing, effectiveness, dodge, anchor);
        }
        void Awake() {
            live = this; surface = (RectTransform)transform;
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 120;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            NumericFont = Resources.Load<Font>("WorldFeedback/Fonts/Rajdhani-SemiBold") ?? MenuTypography.Font;
            NumericFont.RequestCharactersInTexture("0123456789.-+×% ", 36, FontStyle.Normal);
            for (int i = 0; i < Capacity; i++) {
                var rect = new GameObject("Damage" + i, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(150, 56);
                var text = rect.gameObject.AddComponent<Text>(); text.font = NumericFont; text.fontSize = 29; text.fontStyle = FontStyle.Normal;
                text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
                AddOutline(text, 1.25f);
                var mark = new GameObject("Hit qualifier", typeof(RectTransform)).AddComponent<CombatDamageMark>();
                mark.transform.SetParent(rect, false); mark.rectTransform.sizeDelta = new Vector2(13, 16); mark.raycastTarget = false;
                var matchup = new GameObject("Element effectiveness", typeof(RectTransform)).AddComponent<Text>();
                matchup.transform.SetParent(rect, false);
                matchup.rectTransform.sizeDelta = new Vector2(110, 22);
                matchup.rectTransform.anchoredPosition = new Vector2(0, -24);
                matchup.font = MenuTypography.Font ?? NumericFont;
                matchup.fontSize = 16;
                matchup.alignment = TextAnchor.MiddleCenter;
                matchup.raycastTarget = false;
                AddOutline(matchup, 1);
                entries[i] = new Entry { label = text, mark = mark, matchup = matchup }; rect.gameObject.SetActive(false);
            }
            SceneManager.activeSceneChanged += SceneChanged;
        }
        void OnDestroy() { SceneManager.activeSceneChanged -= SceneChanged; if (live == this) live = null; }
        void SceneChanged(Scene previous, Scene current) => Clear();
        static void AddOutline(Graphic graphic, float width) {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.025f, .035f, .06f, .95f);
            outline.effectDistance = new Vector2(width, -width);
        }

        internal static string FormatEffectiveness(float value) {
            if (value <= 0 || float.IsNaN(value) || float.IsInfinity(value) || Mathf.Approximately(value, 1)) return string.Empty;
            if (Mathf.Approximately(value, .25f)) return "↓ ×¼";
            if (Mathf.Approximately(value, .5f)) return "↓ ×½";
            return (value > 1 ? "↑ ×" : "↓ ×") + value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        void Push(Vector3 point, float amount, bool critical, bool dot, bool healing, float effectiveness, bool dodge, Transform anchor) {
            using (SpawnMarker.Auto()) {
            var e = entries[next]; next = (next + 1) % Capacity; serial++;
            float variation = ((serial * 73) % 101) / 100f;
            float sign = serial % 2 == 0 ? 1 : -1;
            e.world = point; e.born = Time.unscaledTime; e.critical = critical; e.dot = dot;
            e.anchor = anchor;
            e.start = new Vector2(sign * (26 + variation * 18), (serial % 3) * 9);
            // Test a few fixed alternatives once at birth, not a moving layout every frame.
            Vector3 screen = worldCamera.WorldToScreenPoint(point);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screen, null, out var origin);
            for (int candidate = 0; candidate < 4; candidate++) {
                bool clear = true;
                foreach (var other in entries) {
                    if (other == e || !other.active || Time.unscaledTime - other.born > .18f) continue;
                    Vector3 projected = worldCamera.WorldToScreenPoint(other.world);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, projected, null, out var otherOrigin);
                    if (Vector2.Distance(origin + e.start, otherOrigin + other.start) < 32) { clear = false; break; }
                }
                if (clear) break;
                e.start += new Vector2(sign * 10, 12);
            }
            e.end = new Vector2(sign * (48 + variation * 55), e.dot ? 44 : 52 + variation * 40);
            e.control = new Vector2(sign * (30 + variation * 36), e.end.y + 24);
            e.color = critical ? CriticalDamageColor : Color.white;
            if (dodge) e.color = new Color(.82f,.93f,1f);
            if (healing) e.color = new Color(.65f, 1, .79f);
            e.label.fontSize = e.dot ? 23 : e.critical ? 38 : 30;
            e.label.text = dodge ? "Né" : (healing ? "+" : "") + amount.ToString(amount < 1 ? "0.##" : "0.#", CultureInfo.InvariantCulture);
            e.mark.Dot = e.dot; e.mark.gameObject.SetActive(e.dot || e.critical);
            e.mark.rectTransform.anchoredPosition = new Vector2(-e.label.preferredWidth * .5f - 10, 0);
            e.mark.color = e.critical && !e.dot ? new Color(1, .83f, .38f) : e.color;
            e.mark.SetAllDirty();
            e.matchup.text = dot || healing ? string.Empty : FormatEffectiveness(effectiveness);
            e.matchup.gameObject.SetActive(e.matchup.text.Length > 0);
            e.matchupColor = effectiveness > 1 ? new Color(1, .86f, .52f) : new Color(.82f, .82f, 1);
            e.matchup.color = e.matchupColor;
            e.label.color = e.color; e.label.enabled = true; e.active = true; e.label.gameObject.SetActive(true); e.label.transform.SetAsLastSibling();
            if (healing) HealingSpawnedCount++;
            else if (!dodge) { SpawnedCount++; LastDamage = amount; LastText = e.label.text; LastCritical = e.critical; }
            }
        }
        void LateUpdate() {
            using (UpdateMarker.Auto()) {
            if (InventoryInputController.GameplayInputBlocked || PetEvolutionPresentationController.AnyPlaying) { Clear(); return; }
            foreach (var e in entries) {
                if (!e.active) continue;
                float t = (Time.unscaledTime - e.born) / Duration;
                if (t >= 1 || worldCamera == null) { e.active = false; e.anchor = null; e.label.gameObject.SetActive(false); continue; }
                if (e.anchor != null && e.anchor.gameObject.activeInHierarchy) e.world = e.anchor.position;
                else e.anchor = null;
                Vector3 screen = worldCamera.WorldToScreenPoint(e.world);
                e.label.enabled = e.mark.enabled = e.matchup.enabled = screen.z > 0;
                if (screen.z <= 0) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screen, null, out var p);
                float travel = 1 - Mathf.Pow(1 - t, 1.5f);
                Vector2 arc = (1 - travel) * (1 - travel) * e.start + 2 * (1 - travel) * travel * e.control + travel * travel * e.end;
                p += arc;
                var bounds = surface.rect;
                p.x = Mathf.Clamp(p.x, bounds.xMin + 82, bounds.xMax - 82);
                p.y = Mathf.Clamp(p.y, bounds.yMin + 56, bounds.yMax - 36);
                e.label.rectTransform.anchoredPosition = p;
                float age = Time.unscaledTime - e.born;
                float scale = age < .065f ? Mathf.Lerp(.82f, e.critical ? 1.18f : 1.12f, age / .065f)
                    : Mathf.Lerp(e.critical ? 1.18f : 1.12f, 1, Mathf.Clamp01((age - .065f) / .10f));
                e.label.rectTransform.localScale = Vector3.one * scale;
                float alpha = 1 - Mathf.Clamp01((t - .6f) / .4f);
                var color = e.color; color.a = alpha; e.label.color = color;
                var mark = e.mark.color; mark.a = alpha; e.mark.color = mark;
                var matchup = e.matchupColor; matchup.a = alpha; e.matchup.color = matchup;
            }
            }
        }
        void Clear() { foreach (var e in entries) if (e != null) { e.active = false; e.anchor = null; e.label.gameObject.SetActive(false); } }
    }
}

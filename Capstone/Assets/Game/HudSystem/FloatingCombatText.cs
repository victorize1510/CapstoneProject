using System.Globalization;
using Capstone.Game.CreatureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Capstone.Game.HudSystem {
    /// <summary>Thirty-two reusable labels; cosmetic scatter never consumes combat RNG.</summary>
    public sealed class FloatingCombatText : MonoBehaviour {
        const int Capacity = 32;
        const float Duration = .9f;
        static FloatingCombatText live;
        sealed class Entry {
            public Text label; public Vector3 world; public float born;
            public Vector2 control, end, start; public Color color; public bool active, critical, dot;
        }
        readonly Entry[] entries = new Entry[Capacity];
        RectTransform surface;
        Camera worldCamera;
        int next, serial;
        public int ActiveCount { get { int n = 0; foreach (var e in entries) if (e != null && e.active) n++; return n; } }
        public int SpawnedCount { get; private set; }
        public float LastDamage { get; private set; }
        public string LastText { get; private set; }
        public bool LastCritical { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic() => live = null;
        public static void ShowDamage(Vector3 point, float amount, CreatureDamageKind kind) {
            ShowDamage(new CombatDamageResult(new CombatHit(null, "legacy", 0, amount, kind, false, false, point), null, amount, false));
        }
        public static void ShowDamage(CombatDamageResult result) {
            if (!Application.isPlaying || result.HpLost <= 0 || float.IsNaN(result.HpLost) || float.IsInfinity(result.HpLost)
                || InventoryInputController.GameplayInputBlocked || PetEvolutionPresentationController.AnyPlaying) return;
            var camera = Camera.main;
            Vector3 point = result.Hit.Point + Vector3.up * .35f;
            if (camera == null) return;
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= 0 || viewport.x < -.05f || viewport.x > 1.05f || viewport.y < -.05f || viewport.y > 1.05f) return;
            if (live == null) {
                var go = new GameObject("Floating Combat Damage", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                live = go.AddComponent<FloatingCombatText>(); DontDestroyOnLoad(go);
            }
            live.worldCamera = camera; live.Push(point, result);
        }
        void Awake() {
            live = this; surface = (RectTransform)transform;
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 120;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            for (int i = 0; i < Capacity; i++) {
                var rect = new GameObject("Damage" + i, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(150, 56);
                var text = rect.gameObject.AddComponent<Text>(); text.font = MenuTypography.Font; text.fontSize = 28; text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
                var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.02f, .025f, .05f, .95f); outline.effectDistance = new Vector2(1.25f, -1.25f);
                entries[i] = new Entry { label = text }; rect.gameObject.SetActive(false);
            }
            SceneManager.activeSceneChanged += SceneChanged;
        }
        void OnDestroy() { SceneManager.activeSceneChanged -= SceneChanged; if (live == this) live = null; }
        void SceneChanged(Scene previous, Scene current) => Clear();
        void Push(Vector3 point, CombatDamageResult result) {
            var e = entries[next]; next = (next + 1) % Capacity; serial++;
            float variation = ((serial * 73) % 101) / 100f;
            float sign = serial % 2 == 0 ? 1 : -1;
            e.world = point; e.born = Time.unscaledTime; e.critical = result.Hit.Critical; e.dot = result.Hit.DamageOverTime;
            e.start = new Vector2(sign * (8 + variation * 12), (serial % 3) * 7);
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
            e.end = new Vector2(sign * (24 + variation * 48), e.dot ? 48 : 50 + variation * 40);
            e.control = new Vector2(sign * (14 + variation * 28), e.end.y + 22);
            e.color = e.dot ? new Color(1, .48f, .50f) : e.critical ? new Color(1, .88f, .48f)
                : result.Hit.Kind == CreatureDamageKind.Magical ? new Color(.66f, .93f, 1) : new Color(1, .94f, .8f);
            e.label.fontSize = e.dot ? 23 : e.critical ? 36 : 28;
            e.label.text = result.HpLost.ToString(result.HpLost < 1 ? "0.##" : "0.#", CultureInfo.InvariantCulture);
            e.label.color = e.color; e.label.enabled = true; e.active = true; e.label.gameObject.SetActive(true); e.label.transform.SetAsLastSibling();
            SpawnedCount++; LastDamage = result.HpLost; LastText = e.label.text; LastCritical = e.critical;
        }
        void LateUpdate() {
            if (InventoryInputController.GameplayInputBlocked || PetEvolutionPresentationController.AnyPlaying) { Clear(); return; }
            foreach (var e in entries) {
                if (!e.active) continue;
                float t = (Time.unscaledTime - e.born) / Duration;
                if (t >= 1 || worldCamera == null) { e.active = false; e.label.gameObject.SetActive(false); continue; }
                Vector3 screen = worldCamera.WorldToScreenPoint(e.world);
                if (screen.z <= 0) { e.label.enabled = false; continue; }
                e.label.enabled = true;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screen, null, out var p);
                float travel = 1 - Mathf.Pow(1 - t, 1.5f);
                Vector2 arc = (1 - travel) * (1 - travel) * e.start + 2 * (1 - travel) * travel * e.control + travel * travel * e.end;
                p += arc;
                var bounds = surface.rect;
                p.x = Mathf.Clamp(p.x, bounds.xMin + 82, bounds.xMax - 82);
                p.y = Mathf.Clamp(p.y, bounds.yMin + 56, bounds.yMax - 36);
                e.label.rectTransform.anchoredPosition = p;
                float age = Time.unscaledTime - e.born;
                float scale = age < .065f ? Mathf.Lerp(.8f, e.critical ? 1.3f : 1.16f, age / .065f)
                    : Mathf.Lerp(e.critical ? 1.3f : 1.16f, 1, Mathf.Clamp01((age - .065f) / .10f));
                e.label.rectTransform.localScale = Vector3.one * scale;
                var color = e.color; color.a = 1 - Mathf.Clamp01((t - .6f) / .4f); e.label.color = color;
            }
        }
        void Clear() { foreach (var e in entries) if (e != null) { e.active = false; e.label.gameObject.SetActive(false); } }
    }
}

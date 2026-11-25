using Capstone.Game.CreatureSystem;
using Capstone.Game.Elements;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.WorldFeedback {
    /// <summary>One fixed HUD card, opened only by explicit inspection or a confirmed player pet skill hit.</summary>
    public sealed class EnemyHealthBars : MonoBehaviour {
        const float SkillDisplaySeconds = 6f;
        static EnemyHealthBars live;
        RectTransform card;
        Text title, details, numbers, captureTime;
        Image portrait, health, shield;
        CombatStatusHud statusIcons;
        DownedRing captureRing;
        GameObject captureBadge;
        PetCommandInput observer;
        DummyEnemy target;
        GameObject lastCaster;
        int lastCast;
        float showUntil, nextRefresh;

        public int VisibleCount => card != null && card.gameObject.activeInHierarchy ? 1 : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => live = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() {
            if (live == null)
                new GameObject("Enemy Target HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler))
                    .AddComponent<EnemyHealthBars>();
        }

        internal static void Inspect(PetCommandInput player, DummyEnemy enemy) {
            if (live == null || !live.isActiveAndEnabled || !IsWorldEnemy(enemy) || !enemy.IsSelectable) return;
            live.observer = player;
            live.target = enemy;
            live.showUntil = float.PositiveInfinity;
            live.Refresh();
        }

        internal static void Dismiss(PetCommandInput player) {
            if (live != null && live.observer == player) live.Clear();
        }

        internal static void ConfirmSkillHit(GameObject caster, int castId, DummyEnemy enemy) {
            if (live == null || !live.isActiveAndEnabled || caster == null || !IsWorldEnemy(enemy)) return;
            var pet = caster.GetComponent<PetController>();
            if (pet == null || pet.IsWildEncounter || pet.owner == null) return;
            var player = pet.owner.GetComponentInParent<PetCommandInput>();
            if (player == null || !player.isActiveAndEnabled) return;

            // Keep one stable target for a multi-hit / area cast instead of cycling through every victim.
            if (live.lastCaster == caster && live.lastCast == castId &&
                IsWorldEnemy(live.target) && live.target.IsSelectable && live.target != enemy &&
                Time.unscaledTime < live.showUntil) return;

            bool keepInspection = live.observer == player && live.target == enemy && enemy.IsSelectable &&
                float.IsPositiveInfinity(live.showUntil);
            live.observer = player;
            live.lastCaster = caster;
            live.lastCast = castId;
            live.target = enemy;
            if (!keepInspection) live.showUntil = Time.unscaledTime + SkillDisplaySeconds;
            live.Refresh();
        }

        static bool IsWorldEnemy(DummyEnemy enemy) => enemy != null && enemy.isActiveAndEnabled &&
            (enemy.GetComponent<PetController>() is not PetController pet || pet.IsWildEncounter);

        void Awake() {
            if (live != null && live != this) { Destroy(gameObject); return; }
            live = this;
            DontDestroyOnLoad(gameObject);
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -20;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;

            var frame = V.Frame(transform, "Target Info", HudFrameGraphic.Shape.Chamfered,
                new Color(.08f, .12f, .20f, .95f), V.Gold, 1.5f, 10f);
            card = frame.rectTransform;
            V.Place(card, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -22f), new Vector2(398f, 92f));

            portrait = V.Image(card, "Portrait");
            V.Top(portrait.rectTransform, 13, 13, 54, 54);
            title = Label(card, "Target Name", 20, 80, 9, 301, 26);
            details = Label(card, "Target Details", 14, 80, 36, 153, 20);
            numbers = Label(card, "Selected HP", 14, 243, 36, 138, 20, TextAnchor.MiddleRight);
            health = V.Bar(card, "Target HP", new Color(.89f, .39f, .43f), 80, 61, 301);
            shield = V.Image(health.transform.parent, "Target Shield");
            shield.preserveAspect = false;
            shield.color = Color.white;
            V.Stretch(shield.rectTransform, 2f);

            var statusRow = V.Rect(card, "Target Status");
            V.Top(statusRow, 80, 90, 300, 1);
            statusRow.localScale = Vector3.one * .42f;
            statusIcons = CombatStatusHud.BuildCanvas(statusRow, below: true);

            var badge = V.Rect(card, "Capture Countdown");
            V.Top(badge, 20, 22, 40, 40);
            captureBadge = badge.gameObject;
            captureRing = badge.gameObject.AddComponent<DownedRing>();
            captureRing.color = V.Gold;
            captureRing.raycastTarget = false;
            captureTime = V.Text(badge, "Seconds", "", 16, TextAnchor.MiddleCenter);
            captureTime.color = Color.white;
            V.Stretch(captureTime.rectTransform);

            card.gameObject.SetActive(false);
            SceneManager.activeSceneChanged += SceneChanged;
        }

        static Text Label(Transform parent, string name, int size, float x, float y, float width, float height,
            TextAnchor align = TextAnchor.MiddleLeft) {
            var label = V.Text(parent, name, "", size, align);
            label.color = new Color(.97f, .96f, .92f);
            V.Top(label.rectTransform, x, y, width, height);
            return label;
        }

        void OnDestroy() {
            SceneManager.activeSceneChanged -= SceneChanged;
            statusIcons?.Dispose();
            if (live == this) live = null;
        }

        void OnDisable() => Clear();
        void SceneChanged(Scene previous, Scene current) => Clear();
        void LateUpdate() => Refresh(false);

        void Clear() {
            observer = null;
            target = null;
            lastCaster = null;
            lastCast = 0;
            statusIcons?.Bind(null);
            if (card != null) card.gameObject.SetActive(false);
        }

        void Refresh(bool immediate = true) {
            if (card == null) return;
            if (observer == null || !observer.isActiveAndEnabled || !IsWorldEnemy(target) || Time.unscaledTime >= showUntil) {
                Clear();
                return;
            }
            if (!target.IsSelectable && float.IsPositiveInfinity(showUntil)) showUntil = Time.unscaledTime + SkillDisplaySeconds;
            bool visible = !InventoryInputController.GameplayInputBlocked && !PetEvolutionPresentationController.AnyPlaying;
            card.gameObject.SetActive(visible);
            if (!visible || (!immediate && Time.unscaledTime < nextRefresh)) return;
            nextRefresh = Time.unscaledTime + .1f;

            var stats = target.GetComponent<PetHudRuntimeStats>();
            var meta = target.GetComponent<PetCollectionMetadata>();
            string name = stats != null ? stats.DisplayName : target.InspectionName;
            if (meta != null) name = meta.ResolveDisplayName(name);
            title.text = stats != null ? $"{name}  ·  Cấp {stats.Level}" : name;
            details.text = Describe(target, meta);
            numbers.text = $"{target.CurrentHealth:0}/{target.maxHealth:0} HP";
            var status = target.GetComponent<CombatBleed>();
            statusIcons?.Bind(status);
            float shieldValue = status?.Magnitude(CombatAilment.Shield) ?? 0f;
            float total = Mathf.Max(target.maxHealth, target.CurrentHealth + shieldValue);
            float hpFraction = total > 0f ? Mathf.Clamp01(target.CurrentHealth / total) : 0f;
            V.Fill(health, hpFraction);
            shield.enabled = shieldValue > .01f && total > 0f;
            if (shield.enabled) {
                shield.rectTransform.anchorMin = new Vector2(hpFraction, 0f);
                shield.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(hpFraction + shieldValue / total), 1f);
            }
            bool downed = target.IsDowned;
            V.Icon(portrait, downed ? null : stats?.Icon);
            captureBadge.SetActive(downed);
            if (downed) {
                captureTime.text = Mathf.CeilToInt(target.DownedSecondsRemaining).ToString();
                captureRing.SetRemaining(target.DownedSecondsRemaining / Mathf.Max(.01f, target.DownedDuration));
            }
        }

        static string Describe(DummyEnemy enemy, PetCollectionMetadata meta) {
            if (enemy.IsDowned) return "Có thể thu phục";
            if (!enemy.IsAlive) return "Đã bị hạ";
            if (enemy.IsBoss) return "Boss";
            if (meta == null || !ElementCatalog.IsSupported(meta.Element)) return "Thú hoang";
            string primary = ElementCatalog.Name(meta.Element);
            return ElementCatalog.IsSupported(meta.SecondaryElement)
                ? $"Hệ {primary} / {ElementCatalog.Name(meta.SecondaryElement)}" : $"Hệ {primary}";
        }
    }
}

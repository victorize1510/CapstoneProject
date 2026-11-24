using System.Collections.Generic;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.WorldFeedback {


    public sealed class EnemyHealthBars : MonoBehaviour {
        const int Capacity = 32;
        sealed class Bar {
            public RectTransform Root, Fill; public CanvasGroup Group; public Text Title, Numbers, Cue;
            public DummyEnemy Enemy; public EnemyHealthAnchor Anchor; public BasicCreatureEnemy Brain;
            public float NextNumbers;public DownedRing Ring;
            public float ActiveUntil, Health = -1, Maximum = -1, NextSight; public bool InSight;
        }
        static EnemyHealthBars live;
        readonly Dictionary<DummyEnemy, float> recentHits = new Dictionary<DummyEnemy, float>();
        readonly List<DummyEnemy> expiredHits = new List<DummyEnemy>();
        internal static void RevealHit(DummyEnemy enemy) {
            if (live != null && enemy != null) live.recentHits[enemy] = Time.unscaledTime + 6f;
        }
        bool WasHitRecently(DummyEnemy enemy) => recentHits.TryGetValue(enemy, out var until) && until > Time.unscaledTime;
        readonly List<Bar> bars = new List<Bar>(Capacity);
        readonly List<DummyEnemy> candidates = new List<DummyEnemy>(64);
        readonly RaycastHit[] sight = new RaycastHit[32];
        RectTransform surface; Camera cameraSource; BasicCameraFollow cameraLock; PetCommandInput party;
        float refreshAt;
        public int VisibleCount { get; private set; }
        public int PooledCount => bars.Count;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => live = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install() {
            if (live == null) new GameObject("Enemy Overhead Health", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).AddComponent<EnemyHealthBars>();
        }
        void Awake() {
            if (live != null && live != this) { Destroy(gameObject); return; }
            live = this; DontDestroyOnLoad(gameObject); surface = (RectTransform)transform;
            // World annotations stay below the gameplay HUD/minimap and modal canvases.
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -20;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            for (int i = 0; i < Capacity; i++) bars.Add(Build(i));
            SceneManager.activeSceneChanged += SceneChanged;
        }
        void OnDestroy() { SceneManager.activeSceneChanged -= SceneChanged; foreach (var bar in bars) if (bar.Enemy != null) bar.Enemy.HealthChanged -= OnHealthChanged; if (live == this) live = null; }
        void OnDisable() { foreach (var bar in bars) if (bar.Root != null) bar.Root.gameObject.SetActive(false); VisibleCount = 0; }
        void OnEnable() { refreshAt = 0; }
        void SceneChanged(Scene previous, Scene current) { foreach (var b in bars) Bind(b, null); cameraSource = null; cameraLock = null; party = null; refreshAt = 0; }
        Bar Build(int index) {
            var root = V.Rect(surface, "EnemyHealth" + index);
            V.Place(root, new Vector2(.5f, .5f), new Vector2(.5f, 0), Vector2.zero, new Vector2(180, 34));
            var title = V.Text(root, "Name", "", 14, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold; title.raycastTarget = false; title.color = new Color(.97f, .96f, .92f);
            V.Top(title.rectTransform, 0, 0, 180, 23);
            var outline = title.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.04f, .05f, .09f, .95f);
            outline.effectDistance = new Vector2(1, -1);
            var track = V.Image(root, "Track"); track.preserveAspect = false; track.raycastTarget = false;
            track.color = new Color(.10f, .13f, .20f, .9f); V.Top(track.rectTransform, 36, 25, 108, 7);
            var fill = V.Image(track.transform, "Health"); fill.preserveAspect = false; fill.raycastTarget = false;
            fill.color = new Color(.84f, .43f, .52f); V.Stretch(fill.rectTransform);
            var numbers = V.Text(root, "Selected HP", "", 13, TextAnchor.MiddleCenter);
            numbers.raycastTarget = false; numbers.color = title.color;
            V.Top(numbers.rectTransform, 0, 32, 180, 18);
            var numberOutline = numbers.gameObject.AddComponent<Outline>();
            numberOutline.effectColor = outline.effectColor;
            numberOutline.effectDistance = outline.effectDistance;
            var group = root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var cue=V.Text(root,"Downed cue","",20,TextAnchor.MiddleCenter);cue.fontStyle=FontStyle.Bold;cue.color=new Color(1,.88f,.57f);cue.raycastTarget=false;V.Top(cue.rectTransform,70,0,40,40);cue.gameObject.SetActive(false);
            var ringRoot=V.Rect(cue.transform,"Countdown ring");V.Stretch(ringRoot);var ring=ringRoot.gameObject.AddComponent<DownedRing>();ring.color=cue.color;ring.raycastTarget=false;
            root.gameObject.SetActive(false);
            return new Bar { Root = root, Fill = fill.rectTransform, Group = group, Title = title, Numbers = numbers, Cue=cue,Ring=ring };
        }
        static bool Eligible(DummyEnemy enemy) {
            if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsSelectable) return false;
            var pet = enemy.GetComponent<PetController>();
            return pet == null || pet.IsWildEncounter;
        }
        float CenterDistance(DummyEnemy enemy) {
            Vector3 point = cameraSource.WorldToViewportPoint(enemy.TargetPosition);
            return point.z > 0 ? new Vector2((point.x - .5f) * cameraSource.aspect, point.y - .5f).sqrMagnitude : float.PositiveInfinity;
        }
        void RefreshCandidates() {
            expiredHits.Clear();
            foreach (var hit in recentHits) if (hit.Key == null || hit.Value <= Time.unscaledTime) expiredHits.Add(hit.Key);
            foreach (var enemy in expiredHits) recentHits.Remove(enemy);
            if (cameraSource == null) cameraSource = Camera.main;
            if (cameraLock == null) cameraLock = FindFirstObjectByType<BasicCameraFollow>();
            if (party == null) party = FindFirstObjectByType<PetCommandInput>();
            candidates.Clear(); if (cameraSource == null) return;
            Vector3 from = party != null ? party.transform.position : cameraSource.transform.position;
            DummyEnemy locked = null; cameraLock?.TryGetLockedEnemy(out locked);
            DummyEnemy selected = party != null ? party.SelectedEnemy : null;
            foreach (var enemy in DummyEnemy.ActiveEnemies)
                if (Eligible(enemy) && (enemy.transform.position - from).sqrMagnitude <= 625) candidates.Add(enemy);
            candidates.Sort((a, b) => {
                if (a == locked) return b == locked ? 0 : -1; if (b == locked) return 1;
                if (a == selected) return b == selected ? 0 : -1; if (b == selected) return 1;
                bool aa = CenterDistance(a) < .0064f, bb = CenterDistance(b) < .0064f;
                if (aa != bb) return aa ? -1 : 1;
                aa = WasHitRecently(a); bb = WasHitRecently(b);
                if (aa != bb) return aa ? -1 : 1;
                return (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude);
            });
            foreach (var bar in bars) if (bar.Enemy != null && (!Eligible(bar.Enemy) || candidates.IndexOf(bar.Enemy) < 0
                || candidates.IndexOf(bar.Enemy) >= Capacity)) Bind(bar, null);
            for (int i = 0; i < candidates.Count && i < Capacity; i++) {
                bool bound = false; foreach (var b in bars) if (b.Enemy == candidates[i]) { bound = true; break; }
                if (bound) continue;
                foreach (var b in bars) if (b.Enemy == null) { Bind(b, candidates[i]); break; }
            }
        }
        void Bind(Bar bar, DummyEnemy enemy) {
            if (bar.Enemy != null) bar.Enemy.HealthChanged -= OnHealthChanged;
            bar.Enemy = enemy; bar.Health = -1; bar.Maximum = -1; bar.ActiveUntil = 0; bar.NextSight = 0;
            bar.Root.gameObject.SetActive(false);
            if (enemy == null) return;
            enemy.HealthChanged += OnHealthChanged;
            bar.Health = enemy.CurrentHealth;
            bar.Anchor = enemy.GetComponent<EnemyHealthAnchor>(); if (bar.Anchor == null) bar.Anchor = enemy.gameObject.AddComponent<EnemyHealthAnchor>();
            bar.Brain = enemy.GetComponent<BasicCreatureEnemy>();
            var stats = enemy.GetComponent<PetHudRuntimeStats>();
            string name = stats != null ? stats.DisplayName : enemy.name;
            bar.Title.text = name + (stats != null ? " • Cấp " + stats.Level : "");
            bar.NextNumbers = 0;
        }
        void OnHealthChanged(DummyEnemy enemy) {
            foreach (var bar in bars) if (bar.Enemy == enemy) {
                if (bar.Health >= 0 && enemy.CurrentHealth < bar.Health) bar.ActiveUntil = Time.unscaledTime + 6;
                bar.Health = enemy.CurrentHealth;
                bar.Maximum = -1;
                break;
            }
        }
        void LateUpdate() {
            if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + .25f; RefreshCandidates(); }
            VisibleCount = 0;
            bool hidden = cameraSource == null || InventoryInputController.GameplayInputBlocked || PetEvolutionPresentationController.AnyPlaying;
            DummyEnemy locked = null; cameraLock?.TryGetLockedEnemy(out locked);
            DummyEnemy selected = party != null ? party.SelectedEnemy : null;
            DummyEnemy centered = null;
            float closestToCenter = .0064f;
            foreach (var bar in bars) {
                if (hidden || !Eligible(bar.Enemy)) continue;
                float distance = CenterDistance(bar.Enemy);
                if (distance >= closestToCenter || !ClearSight(bar.Enemy.TargetPosition, bar.Enemy)) continue;
                centered = bar.Enemy;
                closestToCenter = distance;
            }
            foreach (var bar in bars) {
                if (hidden || !Eligible(bar.Enemy)) { bar.Root.gameObject.SetActive(false); continue; }
                Vector3 anchor = bar.Anchor.Position;
                Vector3 screen = cameraSource.WorldToViewportPoint(anchor);
                bool inView = screen.z > 0 && screen.x > .01f && screen.x < .99f && screen.y > .02f && screen.y < .98f;
                if (inView && Time.unscaledTime >= bar.NextSight) {
                    bar.NextSight = Time.unscaledTime + .1f;
                    bar.InSight = ClearSight(anchor, bar.Enemy);
                }
                bool revealed = bar.Enemy == selected || bar.Enemy == locked || bar.Enemy == centered
                    || WasHitRecently(bar.Enemy) || Time.unscaledTime < bar.ActiveUntil;
                bar.Root.gameObject.SetActive(revealed && inView && bar.InSight);
                if (!bar.Root.gameObject.activeSelf) continue;
                VisibleCount++;
                if (bar.Health != bar.Enemy.CurrentHealth || bar.Maximum != bar.Enemy.maxHealth) {
                    bar.Health = bar.Enemy.CurrentHealth; bar.Maximum = bar.Enemy.maxHealth;
                    bar.Fill.anchorMax = new Vector2(bar.Maximum > 0 ? Mathf.Clamp01(bar.Health / bar.Maximum) : 0, 1);
                }
                bar.Cue.gameObject.SetActive(bar.Enemy.IsDowned);
                bar.Fill.parent.gameObject.SetActive(!bar.Enemy.IsDowned);
                if(bar.Enemy.IsDowned){bar.Cue.text=Mathf.CeilToInt(bar.Enemy.DownedSecondsRemaining).ToString();bar.Ring.SetRemaining(bar.Enemy.DownedSecondsRemaining/Mathf.Max(.01f,bar.Enemy.DownedDuration));}
                bool showNumbers = bar.Enemy == selected && !bar.Enemy.IsDowned;
                if (showNumbers && (!bar.Numbers.enabled || Time.unscaledTime >= bar.NextNumbers)) {
                    bar.NextNumbers = Time.unscaledTime + .1f;
                    bar.Numbers.text = $"{bar.Health:0}/{bar.Maximum:0} HP";
                }
                bar.Numbers.enabled = showNumbers;
                bar.Title.enabled = !bar.Enemy.IsDowned;
                Vector3 pixel = cameraSource.ViewportToScreenPoint(screen);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, pixel, null, out var point);
                bar.Root.anchoredPosition = point;
            }
        }
        bool ClearSight(Vector3 point, DummyEnemy enemy) {
            Vector3 start = cameraSource.transform.position, delta = point - start;
            int count = Physics.RaycastNonAlloc(start, delta.normalized, sight, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == sight.Length) return false;
            for (int i = 0; i < count; i++) {
                var collider = sight[i].collider;
                if (collider == null || collider.transform.IsChildOf(enemy.transform)) continue;
                if (party != null && collider.transform.IsChildOf(party.transform)) continue;
                return false;
            }
            return true;
        }
    }
}

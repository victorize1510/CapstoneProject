using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.UI {
    /// <summary>Presentation grouping in screen pixels; never changes marker world coordinates.</summary>
    internal sealed class MapMarkerClusters {
        sealed class Cluster {
            public readonly List<MapMarkerView> Members = new List<MapMarkerView>();
            public Button Button;
            public TMP_Text Count;
            public int DisplayedCount = -1;
        }
        readonly RectTransform parent;
        readonly WorldMapUITheme theme;
        readonly Action<MapMarkerData> select;
        readonly List<Cluster> pool = new List<Cluster>();
        readonly List<MapMarkerView> candidates = new List<MapMarkerView>();
        readonly List<Vector2> positions = new List<Vector2>();
        readonly List<bool> grouped = new List<bool>();
        readonly List<List<int>> groups = new List<List<int>>();
        readonly GroupWorkspace groupWorkspace = new GroupWorkspace();
        readonly List<MapMarkerView> previousCandidates = new List<MapMarkerView>();
        readonly List<Vector2> previousPositions = new List<Vector2>();
        float nextRegroup;
        RectTransform popup;
        GameObject returnFocus;
        public bool IsPopupOpen => popup != null && popup.gameObject.activeSelf;
        public Transform NavigationRoot => IsPopupOpen ? popup : parent;
        public MapMarkerClusters(RectTransform parent, WorldMapUITheme theme, Action<MapMarkerData> select) { this.parent = parent; this.theme = theme; this.select = select; }

        public void Update(List<MapMarkerView> views) {
            candidates.Clear(); positions.Clear(); grouped.Clear();
            foreach (var view in views) {
                if (view == null || !view.IsVisible) continue;
                if (view.Data.type == MapMarkerType.Player || view.Data.type == MapMarkerType.Waypoint) { view.SetClustered(false); continue; }
                candidates.Add(view); positions.Add(RectTransformUtility.WorldToScreenPoint(null, view.transform.position)); grouped.Add(false);
            }
            bool changed = candidates.Count != previousCandidates.Count;
            for (int i = 0; !changed && i < candidates.Count; i++)
                changed = candidates[i] != previousCandidates[i] || (positions[i] - previousPositions[i]).sqrMagnitude > 16f;
            bool membershipChanged = candidates.Count != previousCandidates.Count;
            for (int i = 0; !membershipChanged && i < candidates.Count; i++) membershipChanged = candidates[i] != previousCandidates[i];
            bool regrouped = membershipChanged || (changed && Time.unscaledTime >= nextRegroup);
            if (regrouped) {
                if (membershipChanged) Close();
                groupWorkspace.Build(positions, 36f, groups);
                previousCandidates.Clear(); previousCandidates.AddRange(candidates);
                previousPositions.Clear(); previousPositions.AddRange(positions);
                nextRegroup = Time.unscaledTime + .15f;
            }
            int used = 0;
            foreach (var group in groups) {
                if (group.Count < 2) continue;
                if (used >= pool.Count) pool.Add(CreateCluster());
                var cluster = pool[used++]; cluster.Members.Clear();
                Vector2 center = Vector2.zero;
                foreach (int index in group) {
                    var member = candidates[index]; cluster.Members.Add(member);
                    center += member.Data.normalizedPosition; grouped[index] = true;
                }
                center /= group.Count;
                var rect = (RectTransform)cluster.Button.transform;
                if (rect.anchorMin != center) { rect.anchorMin = rect.anchorMax = center; rect.anchoredPosition = Vector2.zero; }
                if (cluster.DisplayedCount != group.Count) { cluster.DisplayedCount = group.Count; cluster.Count.text = group.Count.ToString(); }
                if (!cluster.Button.gameObject.activeSelf) cluster.Button.gameObject.SetActive(true);
                if (regrouped) rect.SetAsLastSibling();
            }
            for (int i = 0; i < candidates.Count; i++) candidates[i].SetClustered(grouped[i]);
            for (int i = used; i < pool.Count; i++)
                if (pool[i].Button.gameObject.activeSelf) pool[i].Button.gameObject.SetActive(false);
            if (IsPopupOpen && popup.GetSiblingIndex() != parent.childCount - 1) popup.SetAsLastSibling();
        }

        // Transitive grouping avoids order-dependent overlapping pairs.
        public static void Group(IReadOnlyList<Vector2> points, float distance, List<List<int>> output) {
            new GroupWorkspace().Build(points, distance, output);
        }

        // The live map keeps this workspace across regrouping passes. Allocations
        // are needed only when a larger marker set exceeds the previous capacity.
        sealed class GroupWorkspace {
            int[] roots = Array.Empty<int>();
            readonly Dictionary<Vector2Int, List<int>> cells = new Dictionary<Vector2Int, List<int>>();
            readonly Dictionary<int, List<int>> byRoot = new Dictionary<int, List<int>>();
            readonly List<List<int>> buckets = new List<List<int>>();
            readonly List<List<int>> groupPool = new List<List<int>>();
            int Find(int i) {
                while (roots[i] != i) { roots[i] = roots[roots[i]]; i = roots[i]; }
                return i;
            }
            public void Build(IReadOnlyList<Vector2> points, float distance, List<List<int>> output) {
                output.Clear(); cells.Clear(); byRoot.Clear();
                if (roots.Length < points.Count) Array.Resize(ref roots, Mathf.NextPowerOfTwo(points.Count));
                for (int i = 0; i < points.Count; i++) roots[i] = i;
                int usedBuckets = 0, usedGroups = 0;
                float cellSize = Mathf.Max(.001f, distance);
                float threshold = Mathf.Max(0, distance) * Mathf.Max(0, distance);
                for (int i = 0; i < points.Count; i++) {
                    var cell = new Vector2Int(Mathf.FloorToInt(points[i].x / cellSize), Mathf.FloorToInt(points[i].y / cellSize));
                    for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) {
                        if (!cells.TryGetValue(cell + new Vector2Int(x, y), out var nearby)) continue;
                        foreach (int j in nearby)
                            if ((points[i] - points[j]).sqrMagnitude <= threshold) roots[Find(i)] = Find(j);
                    }
                    if (!cells.TryGetValue(cell, out var bucket)) {
                        if (usedBuckets == buckets.Count) buckets.Add(new List<int>());
                        bucket = buckets[usedBuckets++]; bucket.Clear(); cells.Add(cell, bucket);
                    }
                    bucket.Add(i);
                }
                for (int i = 0; i < points.Count; i++) {
                    int key = Find(i);
                    if (!byRoot.TryGetValue(key, out var group)) {
                        if (usedGroups == groupPool.Count) groupPool.Add(new List<int>());
                        group = groupPool[usedGroups++]; group.Clear(); byRoot.Add(key, group); output.Add(group);
                    }
                    group.Add(i);
                }
            }
        }

        Cluster CreateCluster() {
            var rect = MapUIFactory.Rect(parent, "MarkerGroup", 0, 0, 52, 52); rect.pivot = new Vector2(.5f, .5f);
            var button = MapUIFactory.Button(rect, theme, string.Empty, 22); button.image.sprite = theme.circle; button.image.type = Image.Type.Simple;
            button.image.color = theme.accent;
            var cluster = new Cluster { Button = button, Count = button.GetComponentInChildren<TMP_Text>() };
            cluster.Count.color = Color.white;
            button.onClick.AddListener(() => { if (Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "map-group")) Open(cluster); });
            return cluster;
        }

        void Open(Cluster cluster) {
            Close();
            returnFocus = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (popup != null) { popup.gameObject.SetActive(false); UnityEngine.Object.Destroy(popup.gameObject); }
            popup = MapUIFactory.Rect(parent, "MarkerGroupChoices", 0, 0, 320, Mathf.Min(360, 54 + cluster.Members.Count * 42));
            popup.anchorMin = popup.anchorMax = new Vector2(.5f, .5f); popup.pivot = new Vector2(.5f, .5f); popup.anchoredPosition = Vector2.zero;
            MapUIFactory.Panel(popup, theme, theme.background, true);
            var close = MapUIFactory.Button(MapUIFactory.Rect(popup, "Close", 270, 6, 42, 36), theme, "×", 24); close.onClick.AddListener(Close);
            MapUIFactory.Text(popup, theme, "Title", "Chọn điểm đánh dấu", 12, 4, 254, 40, 20, true);
            var viewport = MapUIFactory.Rect(popup, "Viewport", 8, 48, 304, popup.sizeDelta.y - 56);
            MapUIFactory.Panel(viewport, theme, Color.clear, true, false); viewport.gameObject.AddComponent<RectMask2D>();
            var content = MapUIFactory.Rect(viewport, "Content", 0, 0, 304, cluster.Members.Count * 42);
            for (int i = 0; i < cluster.Members.Count; i++) {
                var data = cluster.Members[i].Data;
                var button = MapUIFactory.Button(MapUIFactory.Rect(content, "Choice_" + i, 0, i * 42, 304, 40), theme, data.displayName, 17);
                button.onClick.AddListener(() => { select(data); Close(); });
            }
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(close.gameObject);
        }
        public void Close() {
            bool wasOpen = IsPopupOpen;
            if (popup != null) popup.gameObject.SetActive(false);
            if (wasOpen) Capstone.Game.UISystem.MenuKeyboardNavigation.Restore(returnFocus);
            returnFocus = null;
        }
    }
}

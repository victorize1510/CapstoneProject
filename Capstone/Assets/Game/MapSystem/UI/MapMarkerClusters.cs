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
        RectTransform popup, popupBlocker;
        readonly RectTransform popupHost;
        GameObject returnFocus;
        public bool IsPopupOpen => popup != null && popup.gameObject.activeSelf;
        public Transform NavigationRoot => IsPopupOpen ? popup : parent;
        public MapMarkerClusters(RectTransform parent, WorldMapUITheme theme, Action<MapMarkerData> select) : this(parent,theme,select,parent) { }
        public MapMarkerClusters(RectTransform parent, WorldMapUITheme theme, Action<MapMarkerData> select, RectTransform popupHost) { this.parent=parent;this.theme=theme;this.select=select;this.popupHost=popupHost; }

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
                bool selected=false;foreach(var member in cluster.Members)selected|=member.Data.isSelected;cluster.Button.GetComponent<MapR5Feedback>().SetSelected(selected);
                if (!cluster.Button.gameObject.activeSelf) cluster.Button.gameObject.SetActive(true);
                if (regrouped) rect.SetAsLastSibling();
            }
            for (int i = 0; i < candidates.Count; i++) candidates[i].SetClustered(grouped[i]);
            for (int i = used; i < pool.Count; i++)
                if (pool[i].Button.gameObject.activeSelf) pool[i].Button.gameObject.SetActive(false);
            // Clusters and late-spawned POIs must not cover the player at the same
            // projected point. Keep popup overlays above both priority markers.
            int top = parent.childCount - 1;
            if (popupHost == parent && IsPopupOpen) top -= popupBlocker != null && popupBlocker.gameObject.activeSelf ? 2 : 1;
            foreach (var view in views)
                if (view != null && view.IsVisible && view.Data.type == MapMarkerType.Player && view.transform.GetSiblingIndex() != top) view.transform.SetSiblingIndex(top);
            if (IsPopupOpen && popup.GetSiblingIndex() != popupHost.childCount - 1) popup.SetAsLastSibling();
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
            var rect=MapR5View.Rect(parent,"MarkerGroup",0,0,54,54);rect.pivot=new Vector2(.5f,.5f);
            var button=MapR5View.Button(rect,theme,"",null,"skill-oct","map-cluster",23);
            MapR5View.Icon(rect,"ClusterGlyph","cluster",18,3,18);
            var count=button.GetComponentInChildren<TMP_Text>();count.rectTransform.anchoredPosition=new Vector2(4,-20)*MapR5View.Native;count.rectTransform.sizeDelta=new Vector2(46,30)*MapR5View.Native;
            count.color=Color.white;
            var cluster=new Cluster{Button=button,Count=count};
            button.onClick.AddListener(()=>{if(Capstone.Game.UISystem.PlayerActionGate.TryAccept(this,"map-group"))Open(cluster);});return cluster;
        }
        void Open(Cluster cluster) {
            Close();returnFocus=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            DestroyPresentation(popup);DestroyPresentation(popupBlocker);
            popupBlocker=MapR5View.Rect(popupHost,"MarkerGroupBackdrop",40,128,1520,678.689956f);
            var hit=popupBlocker.gameObject.AddComponent<Image>();hit.color=Color.clear;popupBlocker.gameObject.AddComponent<Button>().interactable=false;
            popupBlocker.gameObject.AddComponent<MapR5Contour>().Viewport=true;
            var dim=MapR5View.FaceChild(popupBlocker,"panel",null,new Color(.125f,.169f,.27f,.16f));dim.Border=false;dim.Padding=0;
            popup=MapR5View.Rect(popupHost,"MarkerGroupChoices",578,304,444,300);MapR5View.Face(popup,"panel","map-cluster-popup");
            var heading=MapR5View.Rect(popup,"Heading",0,0,444,62);MapR5View.Face(heading,"panel",null,Capstone.Game.UISystem.GreenAngularGeometry.Primary,false);
            MapR5View.Text(heading,theme,"Title","Chọn điểm đánh dấu",20,4,354,54,24,true,Capstone.Game.UISystem.GreenAngularGeometry.Light,true);
            var close=MapR5View.Button(MapR5View.Rect(popup,"Close",392,14,34,34),theme,"",null,"diamond","map-tool");
            close.GetComponent<MapR5Feedback>().Glyph=MapR5View.Icon(close.transform,"CloseGlyph","close",9,9,16);
            close.onClick.AddListener(()=>{if(Capstone.Game.UISystem.PlayerActionGate.TryAccept(this,"map-group-close"))Close();});
            var scrollHost=MapR5View.Rect(popup,"ScrollHost",18,76,408,206);
            var viewport=MapR5View.Rect(scrollHost,"Viewport",0,0,408,206);viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=viewport.offsetMax=Vector2.zero;
            var viewportHit=viewport.gameObject.AddComponent<Image>();viewportHit.color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            float rowWidth=cluster.Members.Count*64-10>206?392:408;
            var content=MapR5View.Rect(viewport,"Content",0,0,408,Mathf.Max(206,cluster.Members.Count*64-10));
            for(int i=0;i<cluster.Members.Count;i++) {
                var data=cluster.Members[i].Data;
                var choice=MapR5View.Button(MapR5View.Rect(content,"Choice_"+i,0,i*64,rowWidth,54),theme,data.displayName,dark:false);
                var label=choice.GetComponentInChildren<TMP_Text>();label.rectTransform.anchoredPosition=new Vector2(65,-4)*MapR5View.Native;label.rectTransform.sizeDelta=new Vector2(rowWidth-116,46)*MapR5View.Native;label.alignment=TextAlignmentOptions.MidlineLeft;label.color=Capstone.Game.UISystem.GreenAngularGeometry.Ink;
                choice.GetComponent<MapR5Feedback>().enabled=false;
                choice.transform.Find("Face").gameObject.SetActive(false);choice.transform.Find("Feedback").gameObject.SetActive(false);
                UnityEngine.Object.Destroy(choice.GetComponent<MapR5Contour>());
                Capstone.Game.UISystem.RuntimeChoiceStyle.UguiRow(choice,label,MapR5View.Native);
                MapR5View.Icon(choice.transform,"MemberIcon",MapR5View.IconFor(data.type),15,10,34);MapR5View.Icon(choice.transform,"Chevron","chevron",rowWidth-36,18,18);
                choice.onClick.AddListener(()=>{select(data);Close();});
            }
            var scroll=scrollHost.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
            Capstone.Game.UISystem.RuntimeScrollbars.Apply(scroll,MapR5View.Native);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(close.gameObject);
        }
        static void DestroyPresentation(RectTransform rect) {if(rect==null)return;rect.gameObject.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(rect.gameObject);else UnityEngine.Object.DestroyImmediate(rect.gameObject);}
        public void Dispose() {Close();DestroyPresentation(popup);DestroyPresentation(popupBlocker);}
        public void Close() {
            bool wasOpen = IsPopupOpen;
            if (popup != null) popup.gameObject.SetActive(false);
            if(popupBlocker!=null)popupBlocker.gameObject.SetActive(false);
            if (wasOpen) Capstone.Game.UISystem.MenuKeyboardNavigation.Restore(returnFocus);
            returnFocus = null;
        }
    }
}

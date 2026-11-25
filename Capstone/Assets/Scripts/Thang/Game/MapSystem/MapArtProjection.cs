using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem {
    internal static class MapArtProjection {
        internal static void Apply(RawImage image, Camera projection, Sprite art, Rect worldBounds) {
            if (image == null || projection == null || art == null ||
                worldBounds.width <= 0 || worldBounds.height <= 0) return;
            Rect view = ViewBounds(projection);
            image.texture = art.texture;
            image.uvRect = new Rect(
                (view.xMin - worldBounds.xMin) / worldBounds.width,
                (view.yMin - worldBounds.yMin) / worldBounds.height,
                view.width / worldBounds.width,
                view.height / worldBounds.height);
        }

        internal static Rect ViewBounds(Camera projection) {
            var center = projection.transform.position;
            float halfHeight = projection.orthographicSize;
            float halfWidth = halfHeight * projection.aspect;
            return new Rect(center.x - halfWidth, center.z - halfHeight, 2f * halfWidth, 2f * halfHeight);
        }

        internal static Vector2 ViewportPoint(Camera projection, Vector3 point) =>
            Normalize(ViewBounds(projection), new Vector2(point.x, point.z));

        internal static Vector2 Normalize(Rect view, Vector2 point) => new Vector2(
            (point.x - view.xMin) / view.width,
            (point.y - view.yMin) / view.height);

        internal static RectTransform CreateOverlay(RectTransform parent, string name) {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }

    internal sealed class MapArtMarkers {
        readonly List<MapMarker> markers = new List<MapMarker>();
        readonly List<RawImage> icons = new List<RawImage>();
        readonly RectTransform root;

        internal MapArtMarkers(RectTransform parent) => root = MapArtProjection.CreateOverlay(parent, "Map Art Icons");

        internal void Refresh(Camera camera, MapMarkerManager manager, Transform viewer, bool forMinimap) {
            if (root == null) return;
            int count = 0;
            if (camera != null && manager != null) {
                manager.CopyMarkersTo(markers);
                var view = MapArtProjection.ViewBounds(camera);
                foreach (var marker in markers) {
                    if (!manager.IsMarkerTypeVisible(marker.MarkerType) || !marker.ShouldRender(viewer, forMinimap)) continue;
                    Vector3 world = marker.transform.position;
                    if (!view.Contains(new Vector2(world.x, world.z))) continue;
                    Texture texture = manager.IconTextureFor(marker);
                    if (texture == null) continue;
                    var position = MapArtProjection.Normalize(view, new Vector2(world.x, world.z));
                    RawImage icon = IconAt(count++);
                    icon.texture = texture;
                    icon.color = manager.IconColorFor(marker);
                    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = position;
                    icon.rectTransform.localRotation = marker.MarkerType == MapMarkerType.Player
                        || marker.MarkerType == MapMarkerType.CoopPlayer
                        ? Quaternion.Euler(0, 0, -marker.transform.eulerAngles.y) : Quaternion.identity;
                    float size = marker.MarkerType == MapMarkerType.Boss ? 24f : 18f;
                    if (forMinimap) size *= MapMarkerManager.MinimapIconScale;
                    icon.rectTransform.sizeDelta = new Vector2(size, size);
                    icon.gameObject.SetActive(true);
                }
            }
            for (int i = count; i < icons.Count; i++) icons[i].gameObject.SetActive(false);
        }

        RawImage IconAt(int index) {
            if (index < icons.Count) return icons[index];
            var icon = new GameObject("Map Icon", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            icon.rectTransform.SetParent(root, false);
            icon.raycastTarget = false;
            icons.Add(icon);
            return icon;
        }
    }
}

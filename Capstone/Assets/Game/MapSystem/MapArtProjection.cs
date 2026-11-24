using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem {
    internal static class MapArtProjection {
        internal static void Apply(RawImage image, Camera projection, Sprite art, Rect worldBounds) {
            if (image == null || projection == null || art == null ||
                worldBounds.width <= 0 || worldBounds.height <= 0) return;
            var center = projection.transform.position;
            float halfHeight = projection.orthographicSize;
            float halfWidth = halfHeight * projection.aspect;
            image.texture = art.texture;
            image.uvRect = new Rect(
                (center.x - halfWidth - worldBounds.xMin) / worldBounds.width,
                (center.z - halfHeight - worldBounds.yMin) / worldBounds.height,
                2f * halfWidth / worldBounds.width,
                2f * halfHeight / worldBounds.height);
        }
    }
}

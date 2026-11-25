using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.SettingsSystem {
    public static class DisplaySettings {
        public static List<Vector2Int> Available(Resolution[] resolutions, Vector2Int current) {
            var result = new List<Vector2Int>(); var seen = new HashSet<Vector2Int>();
            foreach (var resolution in resolutions) {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (size.x > 0 && size.y > 0 && seen.Add(size)) result.Add(size);
            }
            float aspect = current.y > 0 ? current.x / (float)current.y : 16f / 9;
            result.Sort((a, b) => {
                int order = Mathf.Abs(a.x / (float)a.y - aspect).CompareTo(Mathf.Abs(b.x / (float)b.y - aspect));
                return order != 0 ? order : ((long)b.x * b.y).CompareTo((long)a.x * a.y);
            });
            return result;
        }
        public static void Apply(GameSettingsData data) {
            // The Editor does not own a standalone game window. Keep these controls
            // disabled there instead of claiming that Editor fullscreen was changed.
            if (Application.isEditor) return;
            var available = Available(Screen.resolutions, new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height));
            var wanted = new Vector2Int(data.resolutionWidth, data.resolutionHeight);
            if (!available.Contains(wanted)) wanted = available.Count > 0 ? available[0] : new Vector2Int(Screen.width, Screen.height);
            Screen.SetResolution(wanted.x, wanted.y, data.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}

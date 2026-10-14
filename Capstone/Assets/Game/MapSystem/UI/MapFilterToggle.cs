using System;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.UI {
    [RequireComponent(typeof(Toggle))]
    public sealed class MapFilterToggle : MonoBehaviour {
        Toggle toggle;
        Action<MapFilterGroup, bool> changed;
        MapFilterGroup group;
        public void Bind(MapFilterGroup value, bool visible, Action<MapFilterGroup, bool> callback) {
            toggle = GetComponent<Toggle>(); toggle.onValueChanged.RemoveListener(OnChanged);
            group = value; changed = callback; toggle.SetIsOnWithoutNotify(visible); toggle.onValueChanged.AddListener(OnChanged);
        }
        void OnChanged(bool visible) => changed?.Invoke(group, visible);
        void OnDestroy() { if (toggle != null) toggle.onValueChanged.RemoveListener(OnChanged); changed = null; }
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;
using Capstone.Game.UISystem;

namespace Capstone.Game.MapSystem.UI {
    [RequireComponent(typeof(Button))]
    public sealed class MapActionButton : MonoBehaviour {
        Button button;
        Action action;
        object owner;
        string actionKey;
        public void Bind(object commandOwner, string key, Action callback) {
            button = GetComponent<Button>(); button.onClick.RemoveListener(Execute);
            owner = commandOwner; actionKey = key; action = callback; button.onClick.AddListener(Execute);
        }
        void Execute() { if (action != null && PlayerActionGate.TryAccept(owner, actionKey)) action(); }
        void OnDestroy() { if (button != null) button.onClick.RemoveListener(Execute); action = null; }
    }
}

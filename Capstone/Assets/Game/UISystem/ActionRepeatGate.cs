using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Capstone.Game.UISystem {
    // A leading-edge throttle: accepted commands run immediately; repeats are discarded.
    // Keep this at command boundaries, never at raw pointer/drag/double-click detection.
    public sealed class ActionRepeatGate {
        public const double Interval = .15;
        readonly Func<double> clock;
        readonly Dictionary<string, double> deadlines = new Dictionary<string, double>();
        readonly HashSet<string> running = new HashSet<string>();

        public ActionRepeatGate() : this(() => Time.unscaledTimeAsDouble) { }
        public ActionRepeatGate(Func<double> clock) {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public bool TryAccept(string actionKey) {
            if (string.IsNullOrEmpty(actionKey)) throw new ArgumentException("Missing action key.", nameof(actionKey));
            double now = clock();
            if (running.Contains(actionKey) || (deadlines.TryGetValue(actionKey, out double until) && now < until))
                return false;
            deadlines[actionKey] = now + Interval;
            return true;
        }

        public Action Guard(string actionKey, Action action) {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return () => {
                if (!TryAccept(actionKey)) return;
                running.Add(actionKey);
                try { action(); }
                finally { running.Remove(actionKey); }
            };
        }

        public bool TryBegin(string actionKey, out IDisposable lease) {
            lease = null;
            if (!TryAccept(actionKey)) return false;
            running.Add(actionKey);
            lease = new Lease(this, actionKey);
            return true;
        }

        sealed class Lease : IDisposable {
            ActionRepeatGate gate;
            readonly string key;
            public Lease(ActionRepeatGate gate, string key) { this.gate = gate; this.key = key; }
            public void Dispose() { gate?.running.Remove(key); gate = null; }
        }
    }

    public static class PlayerActionGate {
        // Weak owners avoid retaining closed UI cards or objects from an unloaded scene.
        static ConditionalWeakTable<object, ActionRepeatGate> owners = new ConditionalWeakTable<object, ActionRepeatGate>();
        static ActionRepeatGate player = new ActionRepeatGate();

        public static bool TryAccept(string actionKey) => player.TryAccept(actionKey);
        public static bool TryAccept(object owner, string actionKey) => For(owner).TryAccept(actionKey);
        public static Action Guard(object owner, string actionKey, Action action) => For(owner).Guard(actionKey, action);
        public static bool TryBegin(object owner, string actionKey, out IDisposable lease) => For(owner).TryBegin(actionKey, out lease);
        static ActionRepeatGate For(object owner) => owners.GetValue(owner, _ => new ActionRepeatGate());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() {
            owners = new ConditionalWeakTable<object, ActionRepeatGate>();
            player = new ActionRepeatGate();
        }
    }
}

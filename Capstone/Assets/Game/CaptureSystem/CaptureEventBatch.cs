using System;
using System.Collections.Generic;
using UnityEngine;
namespace Capstone.Game.CaptureSystem {
    // Synchronous, capture-only unit of work. Observers never see staged save mutations.
    internal sealed class CaptureEventBatch : IDisposable {
        static CaptureEventBatch current;
        public static bool IsActive => current != null;
        readonly Dictionary<(object,string),Action> notifications = new Dictionary<(object,string),Action>();
        bool committed;
        public CaptureEventBatch() { if (current != null) throw new InvalidOperationException("Capture transaction already active."); current = this; }
        public static void Publish(object owner, string key, Action notify) {
            if (current == null) notify(); else current.notifications[(owner,key)] = notify;
        }
        public void Commit() => committed = true;
        public void Dispose() {
            if (current != this) return; current = null;
            if (committed) foreach (var notify in notifications.Values) try { notify(); } catch (Exception e) { Debug.LogException(e); }
            notifications.Clear();
        }
    }
}

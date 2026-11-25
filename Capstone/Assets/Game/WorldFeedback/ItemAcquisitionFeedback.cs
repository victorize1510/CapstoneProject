using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.UISystem;
using GDS.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.WorldFeedback {
    public enum AcquisitionSource { Purchase, QuestReward, Pickup, Gold, Capture, Progression }

    /// <summary>A successful service receipt, never inferred from inventory deltas.</summary>
    public readonly struct ItemAcquisitionReceipt {
        public readonly string TransactionId;
        public readonly ItemBase Item;
        public readonly int Quantity;
        public readonly AcquisitionSource Source;
        public readonly Sprite Icon;
        public readonly string DisplayName;
        public ItemAcquisitionReceipt(string id, ItemBase item, int quantity, AcquisitionSource source) {
            TransactionId = id; Item = item; Quantity = quantity; Source = source;Icon=item!=null?item.Icon:null;DisplayName=item!=null?item.Name:"";
        }
        public ItemAcquisitionReceipt(string id,Sprite goldIcon,int gold){TransactionId=id;Item=null;Quantity=gold;Source=AcquisitionSource.Gold;Icon=goldIcon;DisplayName="Vàng";}
        public ItemAcquisitionReceipt(string id,Sprite petIcon,string name, AcquisitionSource source = AcquisitionSource.Capture){TransactionId=id;Item=null;Quantity=1;Source=source;Icon=petIcon;DisplayName=name;}
    }

    public static class ItemAcquisitionFeedback {
        static readonly Queue<ItemAcquisitionReceipt> pending = new Queue<ItemAcquisitionReceipt>();
        static readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        static readonly Queue<string> recentIds = new Queue<string>();
        public static event Action<ItemAcquisitionReceipt> Received;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { pending.Clear(); seen.Clear(); recentIds.Clear(); Received = null; }
        public static bool Publish(ItemAcquisitionReceipt receipt) {
            if (receipt.Item == null&&receipt.Source!=AcquisitionSource.Gold&&receipt.Source!=AcquisitionSource.Capture && receipt.Source!=AcquisitionSource.Progression || receipt.Quantity <= 0 || string.IsNullOrEmpty(receipt.TransactionId)
                || !seen.Add(receipt.TransactionId)) return false;
            recentIds.Enqueue(receipt.TransactionId);
            while (recentIds.Count > 256) seen.Remove(recentIds.Dequeue());
            // Bounded presentation queue. Gameplay has already committed independently.
            if (pending.Count == 64) pending.Dequeue();
            pending.Enqueue(receipt);
            try { Received?.Invoke(receipt); if (Application.isPlaying) AcquisitionToastView.Ensure(); }
            catch (Exception error) { Debug.LogWarning("Item receipt committed; presentation unavailable: " + error.Message); }
            return true;
        }
        internal static bool TryRead(out ItemAcquisitionReceipt receipt, bool hasSpace = true) {
            if (pending.Count == 0 || !hasSpace && pending.Peek().Source == AcquisitionSource.Progression) { receipt = default; return false; }
            receipt = pending.Dequeue(); return true;
        }
    }


}

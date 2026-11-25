using System;
using Capstone.Game.Inventory;
using UnityEngine;

namespace Capstone.Game.CaptureSystem {
    [Serializable] public sealed class CaptureBallPresentation {
        public string key, displayName;
        public Sprite icon;
        [Tooltip("Để trống khi chưa có cấu hình gameplay. Hình ảnh không tự tạo item hoặc hệ số bắt.")]
        public MonsterItemDefinition item;
    }
    [CreateAssetMenu(menuName="Capstone/Capture/Ball presentation catalog")]
    public sealed class CaptureBallPresentationCatalog : ScriptableObject {
        public CaptureBallPresentation[] choices = new CaptureBallPresentation[3];
    }
}

using System;
using TMPro;
using UnityEngine;

namespace Capstone.Game.MapSystem.UI {
    public enum MapFilterGroup { NPC, Pet, Enemy, Boss, Quest, Shop, Travel }

    [CreateAssetMenu(menuName = "Game/Map/UI Theme")]
    public sealed class WorldMapUITheme : ScriptableObject {
        [Serializable] public struct MarkerStyle { public Sprite icon; public Color color; }
        public TMP_FontAsset font;
        public Sprite roundedPanel;
        public Sprite circle;
        public Sprite roundedBorder;
        public Sprite circleBorder;
        public Sprite emblem;
        public Sprite playerIcon;
        public Sprite waypointIcon;
        public MarkerStyle[] markers = new MarkerStyle[7];
        public Color background = Capstone.Game.UISystem.MenuPalette.Background;
        public Color text = new Color(.09f, .29f, .19f);
        public Color border = new Color(.57f, .69f, .53f, .85f);
        public Color accent = new Color(.32f, .51f, .36f);
        public Color gold = new Color(.80f, .70f, .42f, .7f);

        public static MapFilterGroup? Group(MapMarkerType type) {
            switch (type) {
                case MapMarkerType.NPC: return MapFilterGroup.NPC;
                case MapMarkerType.Pet: return MapFilterGroup.Pet;
                case MapMarkerType.Enemy: return MapFilterGroup.Enemy;
                case MapMarkerType.Boss: return MapFilterGroup.Boss;
                case MapMarkerType.QuestAvailable: case MapMarkerType.QuestTarget:
                case MapMarkerType.MainQuest: case MapMarkerType.SideQuest: return MapFilterGroup.Quest;
                case MapMarkerType.Shop: return MapFilterGroup.Shop;
                case MapMarkerType.FastTravel: case MapMarkerType.Travel: return MapFilterGroup.Travel;
                default: return null;
            }
        }
    }

    [Serializable]
    public sealed class MapMarkerData {
        public string id;
        public string displayName;
        public MapMarkerType type;
        public Vector2 normalizedPosition;
        public bool isVisible = true;
        public bool isSelected;
    }
}

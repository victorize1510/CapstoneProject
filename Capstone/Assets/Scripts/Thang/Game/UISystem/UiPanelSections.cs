using Capstone.Game.QuestSystem.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Explicit content groups, not per-label patches. Runs once per document.</summary>
    public static class UiPanelSections {
        public static void Bind(VisualElement root) {
            // All of these are stable UXML groups; provider-generated cards remain independent.
            foreach (string cls in new[] {
                "companion-summary", "companion-resources", "companion-content", "companion-inline-detail",
                "ga-details-summary", "ga-details-basic", "ga-details-stats", "ga-details-advanced", "ga-details-skills", "ga-details-codex",
                "ga-summary", "ga-level-stats", "ga-level-item", "ga-level-new", "ga-level-evolution", "ga-heal-preview", "ga-heal-item",
                "quest-detail-section", "box-selected-meta-row", "box-nature-card", "box-health-card", "box-radar-area", "box-selected-skills",
                "codex-progress", "settings-group", "settings-modal-card", "shop-purchase", "ui-copy-section",
                "release-pet-card", "release-section", "box-purchase-card", "skills-pet-summary", "skills-hover-tooltip"
            }) foreach (var host in root.Query<VisualElement>(className: cls).ToList()) Frame(host);
            foreach (string id in new[] { "bag-detail-empty-group", "bag-evolution-card" }) Frame(root.Q(id));
        }

        public static void Frame(VisualElement host) {
            if (host == null || host.ClassListContains("ui-section")) return;
            host.AddToClassList("ui-section");
            host.style.backgroundColor = Color.clear;
            host.style.backgroundImage = StyleKeyword.None;
            host.style.borderTopWidth = host.style.borderBottomWidth = host.style.borderLeftWidth = host.style.borderRightWidth = 0;
            if (host is AngularSurface surface) { surface.NoFill = surface.NoBorder = true; surface.ReadingOnly = false; }
            foreach (var child in host.Children()) {
                if (child is UiReadingSurface) child.style.display = DisplayStyle.None;
                if (child is AngularSurface old && old.name == "green-background") old.NoFill = old.NoBorder = true;
            }
            if (host is QuestFrame quest) quest.Kind = "section";
            else UiSectionFrame.Attach(host);
            foreach (var label in host.Query<Label>().ToList()) {
                bool control=false,semantic=false;
                for(var parent=label.parent;parent!=null&&parent!=host;parent=parent.parent)
                    if(parent is Button||parent.ClassListContains("unity-base-field")){control=true;break;}
                foreach(var cls in label.GetClasses())
                    if(cls.Contains("rarity")||cls.Contains("element")||cls.Contains("status"))semantic=true;
                if(!control&&!semantic)label.style.color=GreenAngularGeometry.Ink;
            }
        }
    }
}

using System;
using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using V=Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.UISystem {
    public sealed class SkillHudTooltip:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler {
        Func<SkillHudData> data;
        RectTransform host, view;
        Text nameText, descriptionText, costText;
        public void Bind(RectTransform host,Func<SkillHudData> data){this.host=host;this.data=data;}
        public void OnPointerEnter(PointerEventData _)=>Show();public void OnSelect(BaseEventData _)=>Show();
        public void OnPointerExit(PointerEventData _)=>Hide();public void OnDeselect(BaseEventData _)=>Hide();void OnDisable()=>Hide();
        void Show() {
            if (host == null || data == null) return;
            var skill = data();
            if (string.IsNullOrWhiteSpace(skill.skillId)) { Hide(); return; }
            const float width = 300, height = 108;
            if (view == null) {
                view = V.Rect(host, "SkillTooltipR2");
                var face = view.gameObject.AddComponent<GreenAngularGraphic>();
                face.Shape = "panel"; face.Fill = GreenAngularGeometry.Panel; face.raycastTarget = false;
                nameText = Label("Name", 10, 20, 19);
                descriptionText = Label("Description", 34, 40, 15);
                costText = Label("CostCooldown", 80, 20, 15);
            }
            nameText.text = skill.displayName;
            nameText.fontStyle = FontStyle.Bold;
            string detail = string.IsNullOrWhiteSpace(skill.description) ? "" : skill.description.Replace('\n', ' ');
            descriptionText.text = FitDescription(detail);
            costText.text = $"MP {skill.manaCost:0.#}     ·     Hồi chiêu {skill.cooldownSeconds:0.#} giây";
            var button = (RectTransform)transform;
            Vector3 center = host.InverseTransformPoint(button.TransformPoint(new Vector3(button.rect.center.x, button.rect.yMax)));
            float x = Mathf.Clamp(center.x, host.rect.xMin + width / 2 + 6, host.rect.xMax - width / 2 - 6);
            float y = Mathf.Clamp(center.y + 12 + height / 2, host.rect.yMin + height / 2 + 6, host.rect.yMax - height / 2 - 6);
            V.Place(view, host.pivot, host.pivot, new Vector2(x, y), new Vector2(width, height));
            view.gameObject.SetActive(true);
            view.SetAsLastSibling();
        }
        Text Label(string id, float top, float height, int size) {
            var label = HubR11Art.Label(view, id, "", size, TextAnchor.UpperLeft);
            label.color = GreenAngularGeometry.Ink;
            V.Top(label.rectTransform, 12, top, 276, height);
            return label;
        }
        string FitDescription(string text) {
            var generator = descriptionText.cachedTextGeneratorForLayout;
            var size = descriptionText.GetGenerationSettings(new Vector2(276, 0));
            if (generator.GetPreferredHeight(text, size) / descriptionText.pixelsPerUnit <= 40) return text;
            int length = text.Length;
            while (length > 0) {
                int previousWord = text.LastIndexOf(' ', length - 1);
                length = previousWord >= 0 ? previousWord : length - 1;
                string candidate = text.Substring(0, length).TrimEnd() + "…";
                if (generator.GetPreferredHeight(candidate, size) / descriptionText.pixelsPerUnit <= 40) return candidate;
            }
            return "";
        }
        void Hide(){if(view!=null)view.gameObject.SetActive(false);}
        void OnDestroy(){if(view!=null)Destroy(view.gameObject);}
    }
}

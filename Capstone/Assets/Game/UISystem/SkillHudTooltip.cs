using System;
using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using V=Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.UISystem {
    public sealed class SkillHudTooltip:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler {
        Func<SkillHudData> data;RectTransform host,view;Text copy,element;GreenAngularGraphic badge;
        public void Bind(RectTransform host,Func<SkillHudData> data){this.host=host;this.data=data;}
        public void OnPointerEnter(PointerEventData _)=>Show();public void OnSelect(BaseEventData _)=>Show();
        public void OnPointerExit(PointerEventData _)=>Hide();public void OnDeselect(BaseEventData _)=>Hide();void OnDisable()=>Hide();
        void Show(){if(host==null||data==null)return;var skill=data();if(string.IsNullOrWhiteSpace(skill.skillId))return;
            if(view==null){view=V.Rect(host,"SkillTooltipR2");V.Place(view,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,142),new Vector2(440,238));var face=view.gameObject.AddComponent<GreenAngularGraphic>();face.Shape="panel";face.Fill=GreenAngularGeometry.Panel;face.SurfaceTexture=LatestMenuCompanionAssets.Load("Assets/Art/steel-enamel-r2")?.texture;face.raycastTarget=false;copy=HubR11Art.Label(view,"Copy","",18,TextAnchor.UpperLeft);V.Stretch(copy.rectTransform,16);var region=V.Rect(view,"ElementBadge");V.Top(region,16,44,132,28);badge=region.gameObject.AddComponent<GreenAngularGraphic>();badge.raycastTarget=false;element=HubR11Art.Label(region,"Type","",17);V.Stretch(element.rectTransform);}
            var token=ElementUi.Find(skill.element);badge.Fill=token?.color??GreenAngularGeometry.Raised;badge.SetVerticesDirty();element.color=token?.foreground??GreenAngularGeometry.Ink;element.text=token?.name??"Chưa phân loại";
            string kind=skill.uiMetadata==null||skill.uiMetadata.damageKind==Capstone.Game.Skills.SkillDamageLabel.Unspecified?"Loại sát thương: chưa thiết lập":skill.uiMetadata.damageKind==Capstone.Game.Skills.SkillDamageLabel.None?"Không gây sát thương":skill.uiMetadata.damageKind==Capstone.Game.Skills.SkillDamageLabel.Physical?"Vật lý":"Phép";
            copy.color=GreenAngularGeometry.Ink;copy.text=VietnameseUI.Skill(skill.displayName)+"\n\n\n"+kind+"\n"+VietnameseUI.SkillDescription(skill.description)+"\nHồi chiêu: "+skill.cooldownSeconds.ToString("0.#")+" giây · Yêu cầu cấp "+skill.RequiredPetLevel;
            view.gameObject.SetActive(true);view.SetAsLastSibling();
        }
        void Hide(){if(view!=null)view.gameObject.SetActive(false);}
        void OnDestroy(){if(view!=null)Destroy(view.gameObject);}
    }
}

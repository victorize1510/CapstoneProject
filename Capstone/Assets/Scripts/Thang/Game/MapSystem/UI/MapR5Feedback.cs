using System.Collections;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Capstone.Game.MapSystem.UI {
    /// <summary>No idle Update, layout mutation, shared-theme change or whole-button opacity transitionRoutine.</summary>
    public sealed class MapR5Feedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler {
        public MapR5Graphic Face;
        public Image Glyph;
        public TMPro.TMP_Text Caption;
        public MapR5Graphic BaseFace;
        public Color CaptionColor=GreenAngularGeometry.Light;
        Selectable control;
        Toggle toggle;
        bool hovered,pressed,focused;
        bool selected;
        Coroutine transitionRoutine;
        public void Bind(Selectable owner,MapR5Graphic face,Image glyph=null,TMPro.TMP_Text caption=null) {
            control=owner;Face=face;Glyph=glyph;Caption=caption;toggle=owner as Toggle;
            if(toggle!=null)toggle.onValueChanged.AddListener(Changed);
            Refresh();
        }
        void Changed(bool _) => Refresh();
        public void SetSelected(bool value) {if(selected==value)return;selected=value;Refresh();}
        public void Refresh() {
            if(Face==null)return;
            Face.Selected=selected;
            Face.Focused=focused&&control!=null&&control.IsInteractable();
            if(BaseFace!=null&&toggle!=null){var surface=GreenAngularGeometry.FilterOff;var material=GreenAngularGeometry.Panel;BaseFace.color=toggle.isOn?Color.white:new Color(surface.r/material.r,surface.g/material.g,surface.b/material.b,1);var light=GreenAngularGeometry.FilterOn;light.a=toggle.isOn?.65f:0;Face.color=light;}
            if(Glyph!=null)Glyph.color=toggle!=null?GreenAngularGeometry.FilterGlyph(toggle.isOn):new Color(1,1,1,control!=null&&!control.IsInteractable()?.38f:1);
            if(Caption!=null)Caption.color=toggle!=null?GreenAngularGeometry.Ink:CaptionColor;
            Face.SetVerticesDirty();
        }
        void Animate() {Refresh();if(transitionRoutine!=null)StopCoroutine(transitionRoutine);if(isActiveAndEnabled)transitionRoutine=StartCoroutine(Tween());}
        IEnumerator Tween() {
            do {
                float h=hovered&&control.IsInteractable()?1:0,p=pressed&&control.IsInteractable()?1:0;
                Face.Hover=Mathf.MoveTowards(Face.Hover,h,Time.unscaledDeltaTime/.14f);
                Face.Press=Mathf.MoveTowards(Face.Press,p,Time.unscaledDeltaTime/.08f);Face.SetVerticesDirty();
                if(Face.Hover==h&&Face.Press==p)break;
                yield return null;
            } while(Face!=null);
            transitionRoutine=null;
        }
        public void OnPointerEnter(PointerEventData _) {hovered=true;Animate();}
        public void OnPointerExit(PointerEventData _) {hovered=pressed=false;Animate();}
        public void OnPointerDown(PointerEventData _) {pressed=true;focused=false;Animate();}
        public void OnPointerUp(PointerEventData _) {pressed=false;Animate();}
        public void OnSelect(BaseEventData _) {focused=true;Refresh();}
        public void OnDeselect(BaseEventData _) {focused=false;Refresh();}
        void OnDisable() {if(transitionRoutine!=null)StopCoroutine(transitionRoutine);transitionRoutine=null;hovered=pressed=focused=false;if(Face!=null){Face.Hover=Face.Press=0;Face.Focused=false;Face.SetVerticesDirty();}}
        void OnDestroy() {if(toggle!=null)toggle.onValueChanged.RemoveListener(Changed);}
    }
}

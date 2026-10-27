using System.Collections;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Capstone.Game.ProfileSystem {
    public sealed partial class ProfilePanelController {
        void ApplyUnifiedBack(){
            var back=designSurface.Find("HeaderBar/BackButton").GetComponent<Button>();var legacy=back.GetComponent<ProfileR2Feedback>();if(legacy!=null)legacy.enabled=false;
            foreach(Transform child in back.transform)child.gameObject.SetActive(false);
            var fill=Graphic(back.transform,"UnifiedBackFill","parallel",UnifiedPanelHeader.Texture("Components/back-button__fill"),Color.white);fill.Cover=false;fill.LogicalScale=1.2f;
            var edge=Graphic(back.transform,"UnifiedBackBorder","parallel",UnifiedPanelHeader.Texture("Components/back-button__border"),Color.white);edge.Cover=false;edge.LogicalScale=1.2f;
            var wash=Graphic(back.transform,"UnifiedBackWash","parallel",null,Color.clear);wash.LogicalScale=1.2f;
            back.GetComponent<ProfileR2HitArea>().Surface=fill;
            var icon=Graphic(back.transform,"UnifiedBackIcon","rect",UnifiedPanelHeader.Texture("Icons/back"),Color.white);SetTopLeft(icon.rectTransform,19.2f,13.2f,28.8f,28.8f);
            var text=CreateText(back.transform,"UnifiedBackCaption","Trở lại",21,FontStyle.Bold,GreenAngularGeometry.Light,TextAnchor.MiddleCenter);SetTopLeft(text.rectTransform,52.8f,0,106,46);text.rectTransform.localScale=Vector3.one*1.2f;
            var feedback=back.gameObject.AddComponent<UnifiedProfileBackFeedback>();feedback.Bind(back,wash,text,icon);
        }
    }
    public sealed class UnifiedProfileBackFeedback:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler {
        Button control;ProfileR2Graphic layer,icon;Text caption;bool hover,press,focus;float h,p;Coroutine animation;
        public void Bind(Button button,ProfileR2Graphic surface,Text text,ProfileR2Graphic arrow){control=button;layer=surface;caption=text;icon=arrow;Paint();}
        void Paint(){if(layer==null)return;bool disabled=control!=null&&!control.interactable;layer.color=UnifiedPanelHeader.Wash(h,p,focus,disabled,false);layer.Edge=focus&&!disabled;layer.EdgeWidth=2.6f*1.2f;layer.EdgeColor=GreenAngularGeometry.Gold;layer.SetVerticesDirty();if(caption!=null)caption.color=disabled?GreenAngularGeometry.Ink:GreenAngularGeometry.Light;if(icon!=null)icon.color=disabled?GreenAngularGeometry.Ink:Color.white;}
        void Change(){if(animation!=null)StopCoroutine(animation);if(isActiveAndEnabled)animation=StartCoroutine(Tween());else Paint();}
        IEnumerator Tween(){float fromH=h,fromP=p,start=Time.unscaledTime;do{float dt=Time.unscaledTime-start;h=Mathf.Lerp(fromH,hover?1:0,Mathf.Clamp01(dt/.14f));p=Mathf.Lerp(fromP,press?1:0,Mathf.Clamp01(dt/.08f));Paint();yield return null;}while(Time.unscaledTime-start<.14f);animation=null;}
        public void OnPointerEnter(PointerEventData e){if(!control.interactable)return;hover=true;Change();}public void OnPointerExit(PointerEventData e){hover=press=false;Change();}public void OnPointerDown(PointerEventData e){focus=false;press=true;Change();}public void OnPointerUp(PointerEventData e){press=false;Change();}public void OnSelect(BaseEventData e){focus=!(e is PointerEventData);Change();}public void OnDeselect(BaseEventData e){focus=false;Change();}
        void OnDisable(){if(animation!=null)StopCoroutine(animation);animation=null;hover=press=focus=false;h=p=0;Paint();}
    }
}

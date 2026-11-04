using System.Collections.Generic;
using Capstone.Game.CaptureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UI;
using UIDocument=UnityEngine.UIElements.UIDocument;
using V=Capstone.Game.HudSystem.GameplayHudVisuals;
using Button=UnityEngine.UI.Button;

namespace Capstone.Game.CaptureSystem {
    // Optional authoritative combat contract. Missing source means no opportunity alert, never an inferred HP threshold.
    public interface ICaptureOpportunitySource { bool HasCaptureOpportunity {get;} }
}
namespace Capstone.Game.HudSystem {
    /// <summary>A press is tap or hold; releasing a hold emits close only. No capture/Inventory logic here.</summary>
    public sealed class HubPressGesture {
        public enum Intent {None,Tap,Hold,ReleaseHold}
        bool pending,held,waitRelease;float pressedAt;
        public Intent Step(bool down,bool pressed,bool up,float time,float threshold) {
            if(waitRelease){if(up||!pressed)waitRelease=false;return Intent.None;}
            if(down){pending=true;held=false;pressedAt=time;}
            if(pending&&pressed&&!held&&time-pressedAt>=threshold){held=true;return Intent.Hold;}
            if(pending&&(up||!pressed)){
                // A lost key-up or a long frame must never turn a hold into a throw.
                bool wasHold=held||time-pressedAt>=threshold;
                pending=held=false;
                return wasHold?Intent.ReleaseHold:up?Intent.Tap:Intent.None;
            }
            return Intent.None;
        }
        public void Cancel(){pending=held=false;waitRelease=true;}
    }
    [DisallowMultipleComponent]
    public sealed class HubCaptureHud : MonoBehaviour {
        [SerializeField] KeyCode captureKey=KeyCode.G;
        [SerializeField,Min(.1f)] float holdThreshold=.28f;
        [Tooltip("Nguồn combat triển khai ICaptureOpportunitySource. Để trống: không báo cơ hội bắt; không suy đoán từ HP.")]
        [SerializeField] MonoBehaviour opportunitySource;
        MonsterInventoryAdapter inventory;PetCaptureCoordinator coordinator;MonsterInventoryController bag;
        readonly List<InventoryItemSnapshot> balls=new List<InventoryItemSnapshot>();
        readonly HubPressGesture gesture=new HubPressGesture();
        RectTransform host,picker;CanvasGroup pulse;Image icon;Text quantity,notice,keyLabel;GameObject opportunity;
        string selectedBallId;float lookupAt,refreshAt,noticeUntil;bool pickerBlocked;int lastIntentFrame=-1,releaseFrame=-1;
        public bool IsPickerOpen=>picker!=null&&picker.gameObject.activeSelf;
        public bool IsPresenting=>coordinator!=null&&coordinator.IsCapturing;
        internal bool OwnsPickerInput=>pickerBlocked;
        public static bool OwnsCaptureKey=>GameplayHudController.Live!=null&&GameplayHudController.Live.isActiveAndEnabled;
        public static bool ModelSwitchModifier=>Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)
            ||Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift)
            ||Input.GetKeyUp(KeyCode.LeftShift)||Input.GetKeyUp(KeyCode.RightShift);
        public int ThrowIntents {get;private set;}
        public KeyCode CaptureKey=>captureKey;
        void Resolve(){inventory??=FindFirstObjectByType<MonsterInventoryAdapter>(FindObjectsInactive.Include);coordinator??=FindFirstObjectByType<PetCaptureCoordinator>(FindObjectsInactive.Include);bag??=FindFirstObjectByType<MonsterInventoryController>(FindObjectsInactive.Include);}
        public void Build(RectTransform root) {
            host=root;Resolve();var group=V.Rect(root,"CaptureG");V.Place(group,new Vector2(1,0),new Vector2(1,0),new Vector2(-76,164),new Vector2(80,80));
            pulse=group.gameObject.AddComponent<CanvasGroup>();
            var button=HubR11Art.Button(group,"Throw","CaptureButton/capture-circle");V.Top((RectTransform)button.transform,0,0,80,80);button.onClick.AddListener(()=>TryThrow());
            var art=HubR11Art.Mask(button.transform,"CaptureButton/capture-circle",80);icon=V.Image(art,"Ball");V.Stretch(icon.rectTransform,16);
            quantity=HubR11Art.Label(button.transform,"Quantity","",17,TextAnchor.MiddleRight);V.Top(quantity.rectTransform,49,42,25,22);
            keyLabel=HubR11Art.Badge(group,captureKey.ToString());
            var hint=V.Rect(group,"Opportunity");V.Top(hint,-35,-38,150,30);HubR11Art.Layer(hint,"CaptureButton/opportunity-label__fill");HubR11Art.Layer(hint,"CaptureButton/opportunity-label__border");
            var text=HubR11Art.Label(hint,"Label","Cơ hội bắt",16);V.Stretch(text.rectTransform);opportunity=hint.gameObject;opportunity.SetActive(false);
            notice=HubR11Art.Label(root,"CaptureFeedback","",18);V.Place(notice.rectTransform,new Vector2(1,0),new Vector2(1,0),new Vector2(-28,285),new Vector2(390,64));notice.gameObject.SetActive(false);
            Refresh();
        }
        public void Refresh(){Resolve();balls.Clear();if(inventory!=null)foreach(var item in inventory.GetItems())if(item.ItemBase is MonsterItemDefinition d&&d.IsCaptureBall)balls.Add(item);
            if(keyLabel!=null)keyLabel.text=captureKey==KeyCode.None?"—":captureKey.ToString().ToUpperInvariant();
            var selected=Selected();if(selected==null&&balls.Count>0){selectedBallId=balls[0].ItemId;selected=balls[0];}
            if(icon!=null){icon.sprite=selected?.Icon??HubR11Art.Load("capture-glyph");icon.color=Color.white;quantity.text=selected!=null?selected.Quantity.ToString():"0";}
        }
        InventoryItemSnapshot Selected(){foreach(var item in balls)if(item.ItemId==selectedBallId)return item;return null;}
        void Update(){
            if(host==null)return;
            if(Time.unscaledTime>=lookupAt){lookupAt=Time.unscaledTime+1;Resolve();}
            if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.15f;Refresh();}
            bool allowed=host.gameObject.activeInHierarchy&&!InventoryInputController.IsBlockedByOtherThan(this);
            if(!allowed||ModelSwitchModifier){gesture.Cancel();if(IsPickerOpen)ClosePicker();}
            else {
                ProcessCaptureKey(Input.GetKeyDown(captureKey),Input.GetKey(captureKey),Input.GetKeyUp(captureKey),Time.unscaledTime);
            }
            bool flag=allowed&&!IsPickerOpen&&opportunitySource is ICaptureOpportunitySource source&&source.HasCaptureOpportunity;
            if(opportunity!=null)opportunity.SetActive(flag);
            if(pulse!=null)pulse.alpha=flag?.775f-.225f*Mathf.Cos(Time.unscaledTime*Mathf.PI*2/1.2f):1;
            if(notice!=null&&Time.unscaledTime>noticeUntil)notice.gameObject.SetActive(false);
        }
        internal void ProcessCaptureKey(bool down,bool pressed,bool up,float time){
            var intent=gesture.Step(down,pressed,up,time,holdThreshold);
            if(intent==HubPressGesture.Intent.Hold)OpenPicker();
            else if(intent==HubPressGesture.Intent.ReleaseHold)ClosePicker();
            else if(intent==HubPressGesture.Intent.Tap&&!IsPickerOpen)TryThrow();
        }
        public bool TryThrow(){
            if(IsPickerOpen||InventoryInputController.GameplayInputBlocked||lastIntentFrame==Time.frameCount)return false;
            lastIntentFrame=Time.frameCount;ThrowIntents++;Refresh();var selected=Selected();
            if(coordinator==null){Feedback("Chưa kết nối dịch vụ thu phục.");return false;}
            if(!(selected?.ItemBase is MonsterItemDefinition ball)||selected.Quantity<1){Feedback("Chưa có bóng thu phục. Giữ G để chọn bóng.");return false;}
            if(!coordinator.TryCaptureFromBag(ball,inventory,out var result,out var error)){Feedback(error);return false;}
            coordinator.Present(result,bag!=null?bag.GetComponent<UIDocument>():null);Refresh();return true;
        }
        public void OpenPicker(){
            if(IsPickerOpen||InventoryInputController.GameplayInputBlocked||coordinator!=null&&coordinator.IsCapturing)return;
            Refresh();GameplayHudController.Live?.Radial?.SetOpen(false);
            if(picker==null){picker=V.Rect(host,"CaptureBallPicker");V.Place(picker,new Vector2(1,0),new Vector2(1,0),new Vector2(-28,270),new Vector2(416,350));}
            V.Clear(picker);picker.gameObject.SetActive(true);
            var bg=V.Frame(picker,"Panel",HudFrameGraphic.Shape.Chamfered,new Color(.12f,.16f,.25f,.97f),HubR11Art.Gold,1,14);V.Stretch(bg.rectTransform);bg.raycastTarget=true;
            var title=HubR11Art.Label(picker,"Title","CHỌN BÓNG THU PHỤC",20);V.Top(title.rectTransform,16,12,340,36);
            var close=V.Button(picker,"Close",HudFrameGraphic.Shape.Chamfered,4);V.Top((RectTransform)close.transform,368,12,32,32);var x=V.Text(close.transform,"Label","×",22,TextAnchor.MiddleCenter);V.Stretch(x.rectTransform);close.onClick.AddListener(ClosePicker);
            var hint=HubR11Art.Label(picker,"Hint","Thả G để đóng · Chọn bóng không ném.\nNhấn G ngắn để ném · Esc: đóng",16);V.Top(hint.rectTransform,16,280,384,56);
            var viewport=V.Rect(picker,"Viewport");V.Top(viewport,16,58,384,212);viewport.gameObject.AddComponent<RectMask2D>();
            var content=V.Rect(viewport,"Content");content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,Mathf.Max(212,balls.Count*58));content.anchoredPosition=Vector2.zero;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;RuntimeScrollbars.Apply(scroll);
            for(int i=0;i<balls.Count;i++){
                string id=balls[i].ItemId;var row=V.Button(content,"BallChoice"+i,HudFrameGraphic.Shape.Chamfered,8);V.Top((RectTransform)row.transform,0,i*58,384,52);
                var image=V.Image(row.transform,"Icon");image.sprite=balls[i].Icon;V.Top(image.rectTransform,8,5,42,42);
                var name=V.Text(row.transform,"Name",balls[i].Name,17);name.fontStyle=FontStyle.Bold;V.Top(name.rectTransform,60,4,260,44);
                var qty=V.Text(row.transform,"Quantity",balls[i].Quantity.ToString(),17,TextAnchor.MiddleRight);V.Top(qty.rectTransform,326,4,44,44);
                row.onClick.AddListener(()=>{SelectBall(id);});if(i==0)row.Select();
            }
            if(balls.Count==0){var empty=HubR11Art.Label(content,"Empty","Chưa có bóng thu phục trong Túi đồ.",18);V.Top(empty.rectTransform,8,48,368,80);}
            releaseFrame=-1;pickerBlocked=true;InventoryInputController.SetExternalGameplayInputBlocked(this,true);
        }
        public bool SelectBall(string id){Refresh();foreach(var ball in balls)if(ball.ItemId==id){selectedBallId=id;ClosePicker();Refresh();return true;}return false;}
        public void ClosePicker(){if(picker!=null)picker.gameObject.SetActive(false);if(pickerBlocked&&releaseFrame<0)releaseFrame=Time.frameCount+1;gesture.Cancel();}
        void LateUpdate(){if(releaseFrame>=0&&Time.frameCount>=releaseFrame&&!IsPickerOpen)ReleaseInput();}
        void ReleaseInput(){releaseFrame=-1;pickerBlocked=false;InventoryInputController.SetExternalGameplayInputBlocked(this,false);}
        public void CancelPending(){ClosePicker();gesture.Cancel();if(pulse!=null)pulse.alpha=1;if(opportunity!=null)opportunity.SetActive(false);}
        void Feedback(string value){if(notice==null)return;notice.text=value;noticeUntil=Time.unscaledTime+3;notice.gameObject.SetActive(true);}
        void OnApplicationFocus(bool focus){if(!focus){CancelPending();ReleaseInput();}}
        void OnDisable(){CancelPending();ReleaseInput();}
    }
}


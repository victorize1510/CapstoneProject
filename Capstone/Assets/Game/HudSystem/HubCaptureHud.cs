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
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class HubCaptureHud : MonoBehaviour {
        [SerializeField] KeyCode captureKey=KeyCode.G;
        [SerializeField,Min(.1f)] float holdThreshold=.28f;
        [Tooltip("Nguồn combat triển khai ICaptureOpportunitySource. Để trống: không báo cơ hội bắt; không suy đoán từ HP.")]
        [SerializeField] MonoBehaviour opportunitySource;
        MonsterInventoryAdapter inventory;PetCaptureCoordinator coordinator;MonsterInventoryController bag;
        readonly List<InventoryItemSnapshot> balls=new List<InventoryItemSnapshot>();
        readonly HubPressGesture gesture=new HubPressGesture();
        CaptureBallPresentationCatalog presentationCatalog;
        readonly Button[] choiceButtons=new Button[3];
        readonly CanvasGroup[] choiceGroups=new CanvasGroup[3];
        readonly Image[] choiceIcons=new Image[3];
        readonly Text[] choiceCounts=new Text[3];
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
        void Resolve(){if(inventory==null)inventory=FindFirstObjectByType<MonsterInventoryAdapter>(FindObjectsInactive.Include);if(coordinator==null)coordinator=FindFirstObjectByType<PetCaptureCoordinator>(FindObjectsInactive.Include);if(bag==null)bag=FindFirstObjectByType<MonsterInventoryController>(FindObjectsInactive.Include);}
        public void Build(RectTransform root) {
            if(opportunitySource==null){var source=GetComponent<LockedCaptureOpportunitySource>();if(source==null)source=gameObject.AddComponent<LockedCaptureOpportunitySource>();opportunitySource=source;}
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
            if(IsPickerOpen)RefreshChoices();
        }
        InventoryItemSnapshot Selected(){foreach(var item in balls)if(item.ItemId==selectedBallId)return item;return null;}
        void Update(){
            if(host==null)return;
            if(Time.unscaledTime>=lookupAt){lookupAt=Time.unscaledTime+1;Resolve();}
            if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.15f;Refresh();}
            bool allowed=host.gameObject.activeInHierarchy&&!InventoryInputController.IsBlockedByOtherThan(this);
            if(!allowed){gesture.Cancel();if(IsPickerOpen)ClosePicker();}
            else {
                ProcessCaptureKey(Input.GetKeyDown(captureKey),Input.GetKey(captureKey),Input.GetKeyUp(captureKey),Time.unscaledTime);
                if(IsPickerOpen)for(int i=0;i<3;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i)||Input.GetKeyDown(KeyCode.Keypad1+i))SelectChoice(i);
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
            if(IsPickerOpen||InventoryInputController.GameplayInputBlocked||lastIntentFrame==Time.frameCount||coordinator!=null&&coordinator.IsCapturing)return false;
            lastIntentFrame=Time.frameCount;ThrowIntents++;Refresh();var selected=Selected();
            if(coordinator==null){Feedback("Chưa kết nối dịch vụ thu phục.");return false;}
            if(!(selected?.ItemBase is MonsterItemDefinition ball)||selected.Quantity<1){Feedback("Chưa có bóng thu phục. Giữ G để chọn bóng.");return false;}
            if(!coordinator.TryCaptureFromBag(ball,inventory,out var result,out var error)){Feedback(error);return false;}
            coordinator.Present(result,bag!=null?bag.GetComponent<UIDocument>():null);Refresh();return true;
        }
        public void OpenPicker(){
            if(IsPickerOpen||InventoryInputController.GameplayInputBlocked||coordinator!=null&&coordinator.IsCapturing)return;
            Refresh();GameplayHudController.Live?.Radial?.SetOpen(false);
            bool build=picker==null;
            if(build){picker=V.Rect(host,"CaptureBallPicker");V.Place(picker,new Vector2(1,0),new Vector2(1,0),new Vector2(-108,278),new Vector2(320,192));}
            picker.gameObject.SetActive(true);
            presentationCatalog=Resources.Load<CaptureBallPresentationCatalog>("Capture/BallPresentations");
            if(build){
            for(int i=0;i<3;i++){
                int index=i;var row=V.Button(picker,"BallChoice"+i,HudFrameGraphic.Shape.Chamfered,10);
                V.Top((RectTransform)row.transform,i*108,i==1?0:34,96,112);choiceButtons[i]=row;
                choiceGroups[i]=row.gameObject.AddComponent<CanvasGroup>();
                var image=V.Image(row.transform,"Icon");image.preserveAspect=true;image.raycastTarget=false;V.Top(image.rectTransform,16,6,64,64);choiceIcons[i]=image;
                var name=V.Text(row.transform,"Name",Choice(i)?.displayName??new[]{"Thường","Rare","Ultra"}[i],17,TextAnchor.MiddleCenter);name.fontStyle=FontStyle.Bold;name.raycastTarget=false;V.Top(name.rectTransform,4,72,88,24);
                var qty=V.Text(row.transform,"Quantity","0",16,TextAnchor.MiddleRight);qty.raycastTarget=false;V.Top(qty.rectTransform,62,48,28,22);choiceCounts[i]=qty;
                var key=V.Text(row.transform,"Key",(i+1).ToString(),14,TextAnchor.UpperLeft);key.raycastTarget=false;V.Top(key.rectTransform,7,5,18,18);
                row.onClick.AddListener(()=>SelectChoice(index));
                var trigger=row.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var enter=new UnityEngine.EventSystems.EventTrigger.Entry{eventID=UnityEngine.EventSystems.EventTriggerType.PointerEnter};
                enter.callback.AddListener(_=>{var item=ChoiceItem(index);if(item==null)Feedback("Bóng "+(Choice(index)?.displayName??"này")+" chưa được cấu hình thu phục.");else if(inventory.GetQuantity(item)<1)Feedback("Bạn không còn bóng này.");});trigger.triggers.Add(enter);
            }
            var hint=HubR11Art.Label(picker,"Hint","1–3 chọn bóng · Thả G để đóng",16);V.Top(hint.rectTransform,0,158,320,28);
            }
            RefreshChoices();
            releaseFrame=-1;pickerBlocked=true;InventoryInputController.SetExternalGameplayInputBlocked(this,true);
        }
        CaptureBallPresentation Choice(int i)=>presentationCatalog!=null&&i<presentationCatalog.choices.Length?presentationCatalog.choices[i]:null;
        MonsterItemDefinition ChoiceItem(int i)=>Choice(i)?.item??(i==0?CaptureAssets.LoadBall():null);
        void RefreshChoices(){for(int i=0;i<3;i++){if(choiceButtons[i]==null)continue;var item=ChoiceItem(i);int count=item!=null&&inventory!=null?inventory.GetQuantity(item):0;choiceIcons[i].sprite=Choice(i)?.icon??item?.Icon??HubR11Art.Load("capture-glyph");choiceCounts[i].text=count.ToString();choiceGroups[i].alpha=item!=null&&count>0?1:.4f;choiceButtons[i].interactable=item!=null&&count>0;}}
        bool SelectChoice(int i){var item=ChoiceItem(i);if(item==null){Feedback("Bóng này chưa được cấu hình thu phục.");return false;}if(inventory==null||inventory.GetQuantity(item)<1){Feedback("Bạn không còn bóng này.");return false;}return SelectBall(item.StableId);}
        public bool SelectBall(string id){Refresh();foreach(var ball in balls)if(ball.ItemId==id){selectedBallId=id;Refresh();RefreshChoices();return true;}return false;}
        public void ClosePicker(){if(picker!=null)picker.gameObject.SetActive(false);if(pickerBlocked&&releaseFrame<0)releaseFrame=Time.frameCount+1;gesture.Cancel();}
        void LateUpdate(){if(releaseFrame>=0&&Time.frameCount>=releaseFrame&&!IsPickerOpen)ReleaseInput();}
        void ReleaseInput(){releaseFrame=-1;pickerBlocked=false;InventoryInputController.SetExternalGameplayInputBlocked(this,false);}
        public void CancelPending(){ClosePicker();gesture.Cancel();if(pulse!=null)pulse.alpha=1;if(opportunity!=null)opportunity.SetActive(false);}
        void Feedback(string value){if(notice==null)return;notice.text=value;noticeUntil=Time.unscaledTime+3;notice.gameObject.SetActive(true);}
        void OnApplicationFocus(bool focus){if(!focus){CancelPending();ReleaseInput();}}
        void OnDisable(){CancelPending();ReleaseInput();}
    }
}


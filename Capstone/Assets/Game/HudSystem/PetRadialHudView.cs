using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using V=Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.HudSystem {
    public enum PartyPickerMode {Toggle,Hold}
    public sealed class PetRadialHudView : MonoBehaviour {
        public static readonly float[] Angles={100.6666667f,78.4f,56.1333333f,33.8666667f,11.6f,-10.6666667f};
        sealed class Slot {public RectTransform Root;public Button Button;public CanvasGroup Group;public Image Portrait,Empty,Active;}
        readonly Slot[] slots=new Slot[6];
        Image portrait;Text nameLabel,levelLabel,hint,bindingLabel;HubCurvedText hpLabel,mpLabel;HubArcGraphic hp,mp;
        string lastName;
        Action<int> confirm;float changedAt,feedbackUntil;bool open;
        string feedback;
        public bool IsOpen=>open;
        public Transform IdentityRoot {get;private set;}
        public void Build(Action<int> callback) {
            confirm=callback;var center=V.Rect(transform,"PetIdentity");V.Bottom(center,128,160,0,0);IdentityRoot=center;
            var avatar=V.Rect(center,"AvatarFrame");V.Place(avatar,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(160,160));
            HubR11Art.Layer(avatar,"PetRadial/avatar-circle__fill");
            var mask=HubR11Art.Mask(avatar,"PetRadial/avatar-circle",135.384615f); // Separate portrait mask; frame/decor stay outside it.
            portrait=V.Image(mask,"Portrait");V.Stretch(portrait.rectTransform);portrait.preserveAspect=false;
            HubR11Art.Layer(avatar,"PetRadial/avatar-circle__border");HubR11Art.Layer(avatar,"PetRadial/avatar-circle__decoration");
            hp=Arc(center,"HPArc",126,6,new Color32(212,123,119,255));mp=Arc(center,"MPArc",104,5,new Color32(117,177,210,255));
            hpLabel=Curved(center,"HP",142);mpLabel=Curved(center,"MP",84);mpLabel.color=new Color32(172,192,224,255);
            nameLabel=HubR11Art.Label(center,"PetName","Chưa chọn thú",22);V.Top(nameLabel.rectTransform,-105,76,210,32);
            nameLabel.font=Capstone.Game.UISystem.TrainerMenuArt.Serif??V.Font;
            nameLabel.horizontalOverflow=HorizontalWrapMode.Wrap;
            levelLabel=HubR11Art.Label(center,"Level","Cấp —",18);V.Top(levelLabel.rectTransform,-70,103,140,28);levelLabel.fontStyle=FontStyle.Normal;
            var hintRoot=V.Rect(center,"PartyHint");V.Top(hintRoot,116,96,240,32);
            var key=HubR11Art.Layer(hintRoot,"Components/keycap__fill");V.Top(key.rectTransform,0,2,32,24);
            var p=V.Text(hintRoot,"Binding","P",20,TextAnchor.MiddleCenter);bindingLabel=p;p.fontStyle=FontStyle.Bold;V.Top(p.rectTransform,0,2,32,24);
            hint=HubR11Art.Label(hintRoot,"Hint","Đội thú",16,TextAnchor.MiddleLeft);V.Top(hint.rectTransform,40,0,200,28);
            for(int i=0;i<6;i++) {
                int index=i;float angle=Angles[i];var button=HubR11Art.Button(center,"PartySlot"+(i+1),"PetRadial/party-slot-arc-segment",96);
                var rect=(RectTransform)button.transform;float a=angle*Mathf.Deg2Rad;V.Place(rect,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(Mathf.Cos(a),Mathf.Sin(a))*201,new Vector2(96,96));rect.localRotation=Quaternion.Euler(0,0,angle);
                var artMask=HubR11Art.Mask(rect,"PetRadial/party-slot-arc-segment",96);
                var icon=V.Image(artMask,"PetPortrait");V.Stretch(icon.rectTransform);icon.rectTransform.localRotation=Quaternion.Euler(0,0,-angle);icon.preserveAspect=false;
                var empty=V.Image(artMask,"EmptyPet");empty.sprite=HubR11Art.Load("empty-pet");V.Place(empty.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(28,28));empty.rectTransform.localRotation=Quaternion.Euler(0,0,-angle);
                HubR11Art.Layer(rect,"PetRadial/party-slot-arc-segment__decoration");
                var active=HubR11Art.Layer(rect,"PetRadial/party-slot-arc-segment__active","Current");active.enabled=false;
                button.Layer.transform.SetAsLastSibling();
                var number=HubR11Art.Label(rect,"Number",(i+1).ToString(),20);
                // Source placement: point(210,7,[-201,0]); glyph counterrotates independently.
                var offset=new Vector2(210*Mathf.Cos(7*Mathf.Deg2Rad)-201,210*Mathf.Sin(7*Mathf.Deg2Rad));V.Place(number.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),offset,new Vector2(26,26));number.rectTransform.localRotation=Quaternion.Euler(0,0,-angle);
                var slot=new Slot{Root=rect,Button=button,Group=rect.gameObject.AddComponent<CanvasGroup>(),Portrait=icon,Empty=empty,Active=active};slots[i]=slot;
                button.onClick.AddListener(()=>{if(open&&!InventoryInputController.GameplayInputBlocked&&PlayerActionGate(index))confirm?.Invoke(index);});
                slot.Group.alpha=0;slot.Group.blocksRaycasts=false;rect.gameObject.SetActive(false);
            }
        }
        bool PlayerActionGate(int index)=>Capstone.Game.UISystem.PlayerActionGate.TryAccept(this,"radial:"+index);
        HubArcGraphic Arc(Transform parent,string name,float radius,float width,Color tint) {var r=V.Rect(parent,name);V.Place(r,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(320,320));var g=r.gameObject.AddComponent<HubArcGraphic>();g.Radius=radius;g.Width=width;g.Fill=tint;g.raycastTarget=false;return g;}
        HubCurvedText Curved(Transform parent,string name,float radius){var r=V.Rect(parent,name);V.Place(r,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(400,48));var t=r.gameObject.AddComponent<HubCurvedText>();t.font=V.Font;t.fontSize=18;t.fontStyle=FontStyle.Bold;t.alignment=TextAnchor.MiddleCenter;t.color=HubR11Art.Text;t.Radius=radius;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;var shadow=r.gameObject.AddComponent<Outline>();shadow.effectColor=new Color(.05f,.09f,.14f,1);shadow.effectDistance=new Vector2(1,-1);return t;}
        public void Refresh(PetStatusHudData status,IReadOnlyList<PetSlotHudData> data,PartyPickerMode mode,KeyCode binding=KeyCode.P) {
            bindingLabel.text=binding==KeyCode.None?"—":binding.ToString().ToUpperInvariant();
            string fullName=status.hasPet?status.displayName:"Chưa chọn thú";
            if(fullName!=lastName){lastName=fullName;nameLabel.text=FitName(fullName);}
            levelLabel.text=status.hasPet&&status.level>0?"Cấp "+status.level:"Cấp —";
            portrait.sprite=status.icon;portrait.enabled=status.icon!=null;hp.SetAmount(status.HealthPercent);mp.SetAmount(status.EnergyPercent);
            hpLabel.text="HP "+Amount(status.health,status.maxHealth);mpLabel.text="MP "+Amount(status.energy,status.maxEnergy);
            int count=0;for(int i=0;i<6;i++){var d=data!=null&&i<data.Count?data[i]:default;var s=slots[i];s.Button.interactable=d.occupied;s.Portrait.sprite=d.icon;s.Portrait.enabled=d.occupied&&d.icon!=null;s.Empty.enabled=!s.Portrait.enabled;s.Active.enabled=d.occupied&&d.selected;if(d.occupied)count++;}
            hint.text=Time.unscaledTime<feedbackUntil?feedback:mode==PartyPickerMode.Hold?(open?"Thả phím để đóng":"Giữ để chọn"):open?"Đóng đội thú":"Đội thú "+count+" / 6";
        }
        public void Feedback(string message){feedback=message;feedbackUntil=Time.unscaledTime+3;}
        string FitName(string value)=>Capstone.Game.UISystem.MenuTypography.FitSingleLine(nameLabel,value);
        static string Amount(float value,float max)=>max>0&&float.IsFinite(max)&&float.IsFinite(value)?Mathf.RoundToInt(value)+" / "+Mathf.RoundToInt(max):"— / —";
        public void SetOpen(bool value) {if(open==value)return;open=value;changedAt=Time.unscaledTime;foreach(var s in slots){s.Root.gameObject.SetActive(true);s.Group.blocksRaycasts=value;}}
        public void ResetState(){open=false;foreach(var s in slots)if(s!=null){s.Group.alpha=0;s.Group.blocksRaycasts=false;s.Root.gameObject.SetActive(false);}}
        void Update(){float elapsed=Time.unscaledTime-changedAt;for(int i=0;i<6;i++){var s=slots[i];if(s==null||!s.Root.gameObject.activeSelf)continue;float t=open?Mathf.Clamp01((elapsed-i*.018f)/.19f):1-Mathf.Clamp01(elapsed/.08f);s.Group.alpha=open?Mathf.Clamp01((elapsed-i*.018f)/.10f):t;s.Root.localScale=Vector3.one*Mathf.Lerp(.94f,1,Mathf.SmoothStep(0,1,t));if(!open&&t<=0)s.Root.gameObject.SetActive(false);}}
        void OnDisable()=>ResetState();
    }
}




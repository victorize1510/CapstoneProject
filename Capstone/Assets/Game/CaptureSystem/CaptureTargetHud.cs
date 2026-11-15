using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;
namespace Capstone.Game.CaptureSystem {
    /// <summary>Only player-facing live target health/level; never receives capture odds.</summary>
    public sealed class CaptureTargetHud:MonoBehaviour {
        BasicCameraFollow cameraSource;UIDocument document;VisualElement host,fill;Label title,value;
        DummyEnemy previous;WildPetCaptureTarget target;float next,health=-1,maximum=-1;int level=-1;
        void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+.15f;
            if(cameraSource==null)cameraSource=FindFirstObjectByType<BasicCameraFollow>();
            if(document==null)document=FindFirstObjectByType<MonsterInventoryController>()?.GetComponent<UIDocument>();
            if(host==null||host.panel==null){host?.RemoveFromHierarchy();var root=document?.rootVisualElement?.panel?.visualTree;if(root==null)return;Build(root);}
            DummyEnemy enemy=null;cameraSource?.TryGetLockedEnemy(out enemy);
            if(enemy!=previous){previous=enemy;target=enemy!=null?enemy.GetComponent<WildPetCaptureTarget>():null;health=-1;level=-1;title.text="Chưa có dữ liệu";}
            bool visible=target!=null&&target.IsWild&&enemy.IsAlive&&!InventoryInputController.GameplayInputBlocked;
            host.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;if(!visible)return;
            if(target.Stats!=null&&(level!=target.Stats.Level||health<0)){level=target.Stats.Level;title.text=(target.Metadata!=null?target.Metadata.ResolveDisplayName(target.Stats.DisplayName):target.Stats.DisplayName)+"  •  Cấp "+level;}
            if(health!=enemy.CurrentHealth||maximum!=enemy.maxHealth){health=enemy.CurrentHealth;maximum=enemy.maxHealth;value.text=$"HP {health:0.#} / {maximum:0.#}";fill.style.width=Length.Percent(maximum>0?Mathf.Clamp01(health/maximum)*100:0);}
        }
        void Build(VisualElement root){host=new VisualElement{name="capture-target-hud",pickingMode=PickingMode.Ignore};host.style.position=Position.Absolute;host.style.left=Length.Percent(37);host.style.right=Length.Percent(37);host.style.top=Length.Percent(4);host.style.paddingTop=host.style.paddingBottom=10;host.style.paddingLeft=host.style.paddingRight=16;host.style.backgroundColor=new Color(.18f,.23f,.34f,.85f);host.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
            title=new Label{pickingMode=PickingMode.Ignore};title.style.fontSize=20;title.style.whiteSpace=WhiteSpace.NoWrap;title.style.textOverflow=TextOverflow.Ellipsis;title.style.overflow=Overflow.Hidden;title.style.unityFontStyleAndWeight=FontStyle.Bold;title.style.color=GreenAngularGeometry.Light;title.style.unityTextAlign=TextAnchor.MiddleCenter;host.Add(title);
            var track=new VisualElement{pickingMode=PickingMode.Ignore};track.style.height=6;track.style.marginTop=6;track.style.backgroundColor=new Color(.1f,.13f,.2f,.65f);fill=new VisualElement{pickingMode=PickingMode.Ignore};fill.style.height=6;fill.style.backgroundColor=(Color)new Color32(198,126,146,255);track.Add(fill);host.Add(track);
            value=new Label{pickingMode=PickingMode.Ignore};value.style.fontSize=17;value.style.color=GreenAngularGeometry.Light;value.style.unityTextAlign=TextAnchor.MiddleCenter;host.Add(value);root.Add(host);
        }
        void OnDisable(){host?.RemoveFromHierarchy();host=null;}
    }
}

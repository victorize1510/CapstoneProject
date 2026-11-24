using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.UIElements;
namespace Capstone.Game.CaptureSystem {
    /// <summary>Only player-facing live target health/level; never receives capture odds.</summary>
    [DefaultExecutionOrder(120)]
    public sealed class CaptureTargetHud:MonoBehaviour {
        PetCommandInput input;UIDocument document;VisualElement host,fill,shieldFill,overlay,marker;Label title,value,reticle,status,aimStatus;
        CombatStatusHud statusIcons;
        CreatureSystem.CombatBleed targetStatus;
        PetCaptureCoordinator coordinator;
        MonsterInventoryAdapter inventory;
        DummyEnemy previous;float next,health=-1,maximum=-1;int level=-1;
        string feedback;
        Color feedbackColor;
        DummyEnemy feedbackEnemy;
        bool feedbackAim;
        void LateUpdate(){
            coordinator ??= GetComponent<PetCaptureCoordinator>();
            if (coordinator == null) return;
            bool blocked = !Application.isFocused || InventoryInputController.GameplayInputBlocked;
            bool hudCursor = GameplayHudController.WantsHudCursor;
            bool aim = !blocked && !hudCursor && coordinator.CaptureCamera?.IsCaptureAiming == true;
            var selection = blocked || hudCursor ? default : coordinator.UpdateTargetPreview(aim);
            if(input==null)input=FindFirstObjectByType<PetCommandInput>();
            if(document==null)document=FindFirstObjectByType<MonsterInventoryController>()?.GetComponent<UIDocument>();
            if(host==null||host.panel==null){host?.RemoveFromHierarchy();overlay?.RemoveFromHierarchy();var root=document?.rootVisualElement?.panel?.visualTree;if(root==null)return;Build(root);}
            DummyEnemy enemy = hudCursor ? previous : selection.Enemy;
            if (enemy == null && !aim && input != null)
                enemy = input.GetComponent<Capstone.Game.CreatureSystem.CombatEngagement>()?.HudTarget;
            if(enemy!=previous){previous=enemy;health=-1;level=-1;title.text="Chưa có dữ liệu";targetStatus=null;}
            bool visible=enemy!=null&&enemy.IsSelectable&&!blocked;
            if (visible && targetStatus == null) targetStatus = enemy.GetComponent<CreatureSystem.CombatBleed>();
            host.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
            overlay.style.display=aim?DisplayStyle.Flex:DisplayStyle.None;
            if (!blocked && (Time.unscaledTime >= next || feedbackEnemy != selection.Enemy || feedbackAim != aim)) {
                next = Time.unscaledTime + .09f;
                feedbackEnemy = selection.Enemy;
                feedbackAim = aim;
                if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryAdapter>();
                var hub = GameplayHudController.Live?.GetComponent<HubCaptureHud>();
                var ball = hub != null ? hub.SelectedBall : CaptureAssets.LoadBall();
                var failure = selection.Enemy != null && ball == null ? CaptureFailure.NoBall
                    : coordinator.PreviewFailure(selection, ball, inventory);
                if (failure == CaptureFailure.None && !coordinator.CanPresentThrow(out string why)) {
                    feedback = why;
                    failure = CaptureFailure.Busy;
                } else feedback = failure == CaptureFailure.None ? (aim ? "Chuột trái để ném" : "G để ném")
                    : CaptureMessages.For(failure);
                feedbackColor = failure switch {
                    CaptureFailure.None => new Color(.48f, .96f, .58f),
                    CaptureFailure.NoTarget => Color.white,
                    CaptureFailure.OutOfRange or CaptureFailure.OutOfView or CaptureFailure.Obstructed or CaptureFailure.Busy
                        => new Color(1, .84f, .42f),
                    _ => new Color(1, .46f, .44f)
                };
            }
            reticle.style.color = feedbackColor;
            status.text = !aim && selection.Enemy != null ? feedback : string.Empty;
            status.style.color = feedbackColor;
            aimStatus.text = aim ? feedback : string.Empty;
            aimStatus.style.color = feedbackColor;
            bool mark = aim && selection.Enemy != null && selection.Failure == CaptureFailure.None;
            marker.style.display = mark ? DisplayStyle.Flex : DisplayStyle.None;
            if (mark && coordinator.CaptureView is Camera view)
            {
                Vector2 point = RuntimePanelUtils.CameraTransformWorldToPanel(overlay.panel, selection.Enemy.TargetPosition, view);
                marker.style.left = point.x - 24;
                marker.style.top = point.y - 24;
            }
            statusIcons?.Bind(visible ? targetStatus : null);
            if(!visible)return;
            var stats=enemy.GetComponent<Capstone.Game.HudSystem.PetHudRuntimeStats>();var metadata=enemy.GetComponent<Capstone.Game.HudSystem.PetCollectionMetadata>(); if(stats!=null&&(level!=stats.Level||health<0)){level=stats.Level;title.text=(metadata!=null?metadata.ResolveDisplayName(stats.DisplayName):stats.DisplayName)+"  •  Cấp "+level;}
            if(health!=enemy.CurrentHealth||maximum!=enemy.maxHealth){health=enemy.CurrentHealth;maximum=enemy.maxHealth;value.text=$"HP {health:0.#} / {maximum:0.#}";}
            float shield = targetStatus?.Magnitude(CreatureSystem.CombatAilment.Shield) ?? 0;
            float total = Mathf.Max(maximum, health + shield);
            fill.style.width=Length.Percent(total>0?Mathf.Clamp01(health/total)*100:0);
            shieldFill.style.width=Length.Percent(total>0?Mathf.Clamp01(shield/total)*100:0);
        }
        void Build(VisualElement root){host=new VisualElement{name="capture-target-hud",pickingMode=PickingMode.Ignore};host.style.position=Position.Absolute;host.style.left=Length.Percent(37);host.style.right=Length.Percent(37);host.style.top=Length.Percent(4);host.style.paddingTop=host.style.paddingBottom=10;host.style.paddingLeft=host.style.paddingRight=16;host.style.backgroundColor=Color.clear;host.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
            title=new Label{pickingMode=PickingMode.Ignore};title.style.fontSize=20;title.style.whiteSpace=WhiteSpace.NoWrap;title.style.textOverflow=TextOverflow.Ellipsis;title.style.overflow=Overflow.Hidden;title.style.unityFontStyleAndWeight=FontStyle.Bold;title.style.color=GreenAngularGeometry.Light;title.style.unityTextAlign=TextAnchor.MiddleCenter;host.Add(title);
            var track=new VisualElement{pickingMode=PickingMode.Ignore};track.style.height=6;track.style.marginTop=6;track.style.backgroundColor=new Color(.1f,.13f,.2f,.65f);
            track.style.flexDirection=FlexDirection.Row;
            fill=new VisualElement{pickingMode=PickingMode.Ignore};fill.style.height=6;fill.style.backgroundColor=(Color)new Color32(105,224,132,255);track.Add(fill);
            shieldFill=new VisualElement{name="EnemyShieldFill",pickingMode=PickingMode.Ignore};shieldFill.style.height=6;shieldFill.style.backgroundColor=Color.white;track.Add(shieldFill);
            host.Add(track);
            statusIcons?.Dispose();
            statusIcons=CombatStatusHud.BuildToolkit(host);
            value=new Label{pickingMode=PickingMode.Ignore};value.style.fontSize=17;value.style.color=GreenAngularGeometry.Light;value.style.unityTextAlign=TextAnchor.MiddleCenter;host.Add(value);
            var shadow=new TextShadow{offset=new Vector2(0,1),blurRadius=2,color=new Color(0,0,0,.85f)};
            title.style.textShadow=shadow;value.style.textShadow=shadow;
            status=new Label{pickingMode=PickingMode.Ignore};status.style.fontSize=16;
            status.style.unityTextAlign=TextAnchor.MiddleCenter;status.style.whiteSpace=WhiteSpace.Normal;
            status.style.textShadow=shadow;host.Add(status);root.Add(host);
            overlay=new VisualElement{name="capture-aim",pickingMode=PickingMode.Ignore};
            overlay.style.position=Position.Absolute;
            overlay.style.left=overlay.style.right=overlay.style.top=overlay.style.bottom=0;
            reticle=new Label("+"){pickingMode=PickingMode.Ignore};
            reticle.style.position=Position.Absolute;reticle.style.left=Length.Percent(50);reticle.style.top=Length.Percent(50);
            reticle.style.marginLeft=reticle.style.marginTop=-16;reticle.style.width=reticle.style.height=32;
            reticle.style.unityTextAlign=TextAnchor.MiddleCenter;reticle.style.fontSize=28;reticle.style.textShadow=shadow;
            overlay.Add(reticle);
            aimStatus=new Label{pickingMode=PickingMode.Ignore};aimStatus.style.position=Position.Absolute;
            aimStatus.style.left=Length.Percent(30);aimStatus.style.right=Length.Percent(30);
            aimStatus.style.top=Length.Percent(50);aimStatus.style.marginTop=24;
            aimStatus.style.unityTextAlign=TextAnchor.MiddleCenter;aimStatus.style.fontSize=16;
            aimStatus.style.whiteSpace=WhiteSpace.Normal;aimStatus.style.textShadow=shadow;overlay.Add(aimStatus);
            marker=new VisualElement{name="capture-aim-target",pickingMode=PickingMode.Ignore};
            marker.style.position=Position.Absolute;marker.style.width=marker.style.height=48;
            marker.style.borderLeftWidth=marker.style.borderTopWidth=marker.style.borderRightWidth=marker.style.borderBottomWidth=2;
            var green=new Color(.48f,.96f,.58f,.95f);
            marker.style.borderLeftColor=marker.style.borderTopColor=marker.style.borderRightColor=marker.style.borderBottomColor=green;
            marker.style.borderTopLeftRadius=marker.style.borderTopRightRadius=marker.style.borderBottomLeftRadius=marker.style.borderBottomRightRadius=12;
            overlay.Add(marker);root.Add(overlay);
        }
        void OnDisable(){statusIcons?.Dispose();statusIcons=null;targetStatus=null;previous=null;host?.RemoveFromHierarchy();overlay?.RemoveFromHierarchy();host=overlay=null;}
    }
}

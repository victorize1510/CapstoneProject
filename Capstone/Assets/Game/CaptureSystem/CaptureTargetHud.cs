using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.UIElements;
namespace Capstone.Game.CaptureSystem {
    /// <summary>Capture reticle and throw feedback while aiming. Enemy HP belongs to the target HUD.</summary>
    [DefaultExecutionOrder(120)]
    public sealed class CaptureTargetHud:MonoBehaviour {
        UIDocument document;
        VisualElement overlay, marker;
        Label reticle, aimStatus;
        PetCaptureCoordinator coordinator;
        MonsterInventoryAdapter inventory;
        float next;
        string feedback;
        Color feedbackColor;
        DummyEnemy feedbackEnemy;
        bool wasAiming;
        void LateUpdate(){
            coordinator ??= GetComponent<PetCaptureCoordinator>();
            if (coordinator == null) return;
            bool blocked = !Application.isFocused || InventoryInputController.GameplayInputBlocked;
            bool hudCursor = GameplayHudController.WantsHudCursor;
            bool aim = !blocked && !hudCursor && coordinator.CaptureCamera?.IsCaptureAiming == true;
            // The throw and bag still use the latest preview. It has no authority to show enemy HP.
            var selection = blocked || hudCursor ? default : coordinator.UpdateTargetPreview(aim);
            if (!aim) {
                if (overlay != null) overlay.style.display = DisplayStyle.None;
                wasAiming = false;
                return;
            }
            if(document==null)document=FindFirstObjectByType<MonsterInventoryController>()?.GetComponent<UIDocument>();
            if(overlay==null||overlay.panel==null){overlay?.RemoveFromHierarchy();var root=document?.rootVisualElement?.panel?.visualTree;if(root==null)return;Build(root);}
            overlay.style.display = DisplayStyle.Flex;
            if (!wasAiming || Time.unscaledTime >= next || feedbackEnemy != selection.Enemy) {
                next = Time.unscaledTime + .09f;
                feedbackEnemy = selection.Enemy;
                if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryAdapter>();
                var hub = GameplayHudController.Live?.GetComponent<HubCaptureHud>();
                var ball = hub != null ? hub.SelectedBall : CaptureAssets.LoadBall();
                var failure = selection.Enemy != null && ball == null ? CaptureFailure.NoBall
                    : coordinator.PreviewFailure(selection, ball, inventory);
                if (failure == CaptureFailure.None && !coordinator.CanPresentThrow(out string why)) {
                    feedback = why;
                    failure = CaptureFailure.Busy;
                } else feedback = failure == CaptureFailure.None ? "Chuột trái để ném"
                    : CaptureMessages.For(failure);
                feedbackColor = failure switch {
                    CaptureFailure.None => new Color(.48f, .96f, .58f),
                    CaptureFailure.NoTarget => Color.white,
                    CaptureFailure.OutOfRange or CaptureFailure.OutOfView or CaptureFailure.Obstructed or CaptureFailure.Busy
                        => new Color(1, .84f, .42f),
                    _ => new Color(1, .46f, .44f)
                };
            }
            wasAiming = true;
            reticle.style.color = feedbackColor;
            aimStatus.text = feedback;
            aimStatus.style.color = feedbackColor;
            bool mark = selection.Enemy != null && selection.Failure == CaptureFailure.None;
            marker.style.display = mark ? DisplayStyle.Flex : DisplayStyle.None;
            if (mark && coordinator.CaptureView is Camera view)
            {
                Vector2 point = RuntimePanelUtils.CameraTransformWorldToPanel(overlay.panel, selection.Enemy.TargetPosition, view);
                marker.style.left = point.x - 24;
                marker.style.top = point.y - 24;
            }
        }
        void Build(VisualElement root) {
            var shadow=new TextShadow{offset=new Vector2(0,1),blurRadius=2,color=new Color(0,0,0,.85f)};
            overlay=new VisualElement{name="capture-aim",pickingMode=PickingMode.Ignore};
            overlay.style.unityFontDefinition = FontDefinition.FromFont(MenuTypography.Font);
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
        void OnDisable() { overlay?.RemoveFromHierarchy(); overlay = null; wasAiming = false; feedbackEnemy = null; }
    }
}

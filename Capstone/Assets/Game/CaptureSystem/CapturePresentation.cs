using System;
using System.Collections;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.CaptureSystem {
    public readonly struct CapturePresentationResult {
        public readonly bool Success, IsCritical;
        public readonly int ShakeCount;
        public readonly PetController CapturedPet;
        public readonly string Message;
        public readonly Vector3 WorldPosition;
        internal readonly WildPetCaptureTarget Target;
        internal CapturePresentationResult(CaptureRoll roll, WildPetCaptureTarget target) {
            Success=roll.Success;IsCritical=roll.IsCritical;ShakeCount=roll.ShakeCount;
            CapturedPet=roll.Success?target.Pet:null;Target=target;WorldPosition=target.Enemy.TargetPosition;
            Message=roll.Success?"Thu phục thành công! Thú đã được lưu vào đội hoặc Kho thú.":"Thú đã thoát khỏi bóng. Bạn có thể thử lại.";
        }
    }
    public sealed class CapturePresentation : MonoBehaviour {
        public bool IsPlaying { get; private set; }
        GameObject ball;Label notice;WildPetCaptureTarget target;bool success;
        Renderer[] hidden=Array.Empty<Renderer>();bool[] visibility=Array.Empty<bool>();
        public void Play(CapturePresentationResult result, Transform origin, UIDocument document) {
            Cancel();target=result.Target!=null?result.Target:null;success=result.Success;IsPlaying=true;
            InventoryInputController.SetExternalGameplayInputBlocked(this,true);
            if(target!=null)target.Lock(true);
            if(!success && target!=null){hidden=target.GetComponentsInChildren<Renderer>();visibility=new bool[hidden.Length];for(int i=0;i<hidden.Length;i++){visibility[i]=hidden[i].enabled;hidden[i].enabled=false;}}
            var root=document?.rootVisualElement?.panel?.visualTree;
            if(root!=null){notice=new Label("Đang thu phục…"){pickingMode=PickingMode.Ignore};notice.style.position=Position.Absolute;notice.style.left=Length.Percent(25);notice.style.right=Length.Percent(25);notice.style.bottom=Length.Percent(14);notice.style.minHeight=48;notice.style.whiteSpace=WhiteSpace.Normal;notice.style.fontSize=22;notice.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);notice.style.unityFontStyleAndWeight=FontStyle.Bold;notice.style.unityTextAlign=TextAnchor.MiddleCenter;notice.style.color=GreenAngularGeometry.Ink;notice.style.backgroundColor=RuntimeChoiceStyle.PanelColor;notice.style.paddingTop=notice.style.paddingBottom=12;root.Add(notice);}
            StartCoroutine(Animate(result,origin));
        }
        IEnumerator Animate(CapturePresentationResult result, Transform origin) {
            var settings=CaptureAssets.Settings;
            if(settings?.ballVisual!=null){ball=Instantiate(settings.ballVisual);ball.name="Capture ball presentation";foreach(var c in ball.GetComponentsInChildren<Collider>())c.enabled=false;foreach(var rb in ball.GetComponentsInChildren<Rigidbody>()){rb.isKinematic=true;rb.useGravity=false;}foreach(var script in ball.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
                var parts=ball.GetComponentsInChildren<MeshRenderer>();if(parts.Length>0){var bounds=parts[0].bounds;foreach(var part in parts)bounds.Encapsulate(part.bounds);float radius=Mathf.Max(bounds.extents.x,bounds.extents.y,bounds.extents.z);if(radius>0)ball.transform.localScale*=.18f/radius;}
            }
            Vector3 start=origin!=null?origin.position+Vector3.up:result.WorldPosition+Vector3.up*2,end=result.WorldPosition;
            for(float t=0;t<.55f;t+=Time.unscaledDeltaTime){if(ball!=null){float f=Mathf.Clamp01(t/.55f);ball.transform.position=Vector3.Lerp(start,end,f)+Vector3.up*(4*f*(1-f));}yield return null;}
            if(ball!=null)ball.transform.position=end;
            for(int i=0;i<result.ShakeCount;i++){
                for(float t=0;t<.36f;t+=Time.unscaledDeltaTime){if(ball!=null)ball.transform.rotation=Quaternion.Euler(0,0,Mathf.Sin(t/.36f*Mathf.PI*2)*16);yield return null;}
                if(ball!=null)ball.transform.rotation=Quaternion.identity;yield return new WaitForSecondsRealtime(.12f);
            }
            if(notice!=null)notice.text=result.Message;
            var sound=result.Success?settings?.successSound:settings?.escapeSound;
            if(sound!=null)AudioSource.PlayClipAtPoint(sound,end);
            RestoreTarget();InventoryInputController.SetExternalGameplayInputBlocked(this,false);
            yield return new WaitForSecondsRealtime(1.4f);Cleanup();
        }
        void RestoreTarget(){for(int i=0;i<hidden.Length;i++)if(hidden[i]!=null)hidden[i].enabled=visibility[i];hidden=Array.Empty<Renderer>();visibility=Array.Empty<bool>();if(target!=null)target.Lock(false);target=null;}
        void Cleanup(){RestoreTarget();InventoryInputController.SetExternalGameplayInputBlocked(this,false);if(ball!=null)Destroy(ball);ball=null;notice?.RemoveFromHierarchy();notice=null;IsPlaying=false;}
        public void Cancel(){StopAllCoroutines();Cleanup();}
        void OnDisable()=>Cancel();
    }
}

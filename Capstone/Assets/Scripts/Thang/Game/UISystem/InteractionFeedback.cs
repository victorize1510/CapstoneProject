using UnityEngine;
using UnityEngine.UIElements;
namespace Capstone.Game.UISystem {
    /// <summary>Shared UI state tokens. Animation never changes control or caption geometry.</summary>
    public static class InteractionFeedback {
        public const float HoverSeconds=.14f,PressSeconds=.08f,PressedInkAlpha=.12f;
        public static Color Wash(float hover,float press,bool focus){
            float amount=Mathf.Max(hover*.10f,focus ? .16f : 0);
            var tint=press>.001f?GreenAngularGeometry.Ink:GreenAngularGeometry.Primary;
            tint.a=Mathf.Max(amount,press*PressedInkAlpha);return tint;
        }
        public static bool KeyboardNavigation;
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,object> boundRoots=new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,object>();
        // Observe keyboard input before TextField/Button default navigation changes focus.
        public static void Bind(VisualElement root){
            UiReadability.Bind(root);
            if(root==null||boundRoots.TryGetValue(root,out _))return;boundRoots.Add(root,new object());
            root.RegisterCallback<KeyDownEvent>(e=>SetModality(e.currentTarget as VisualElement,true),TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(e=>SetModality(e.currentTarget as VisualElement,true),TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(e=>SetModality(e.currentTarget as VisualElement,false),TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(e=>{
                if(e.button==0 && ButtonAt(e.target as VisualElement)!=null) SettingsSystem.GameSettingsService.PlayUiSound(false);
            },TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(e=>{
                if(ButtonAt(e.target as VisualElement)!=null) SettingsSystem.GameSettingsService.PlayUiSound(false);
            },TrickleDown.TrickleDown);
            Button hovered=null;
            root.RegisterCallback<PointerOverEvent>(e=>{
                var button=ButtonAt(e.target as VisualElement);
                if(button!=null&&button!=hovered) SettingsSystem.GameSettingsService.PlayUiSound(true);
                hovered=button;
            },TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(_=>hovered=null);
        }
        static Button ButtonAt(VisualElement target){
            var button=target as Button??target?.GetFirstAncestorOfType<Button>();
            return button!=null&&button.enabledInHierarchy?button:null;
        }
        static void SetModality(VisualElement root,bool keyboard){
            KeyboardNavigation=keyboard;
            if(root?.focusController?.focusedElement is AngularButton button){button.Surface.IsFocused=keyboard;button.Surface.InvalidateFeedback();}
        }
        public static Color Edge(float intensity){var color=GreenAngularGeometry.Gold;color.a=Mathf.Clamp01(intensity)*.75f;return color;}
    }
}

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
        }
        static void SetModality(VisualElement root,bool keyboard){
            KeyboardNavigation=keyboard;
            if(root?.focusController?.focusedElement is AngularButton button){button.Surface.IsFocused=keyboard;button.Surface.InvalidateFeedback();}
        }
        public static Color Edge(float intensity){var color=GreenAngularGeometry.Gold;color.a=Mathf.Clamp01(intensity)*.75f;return color;}
    }
}

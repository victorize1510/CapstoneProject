using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace GDS.Core {

    public static class InputUtil {

        public static EventModifiers GetModifiers() {
            var mods = EventModifiers.None;
            var k = Keyboard.current;
            if (k.shiftKey.isPressed) mods |= EventModifiers.Shift;
            if (k.ctrlKey.isPressed) mods |= EventModifiers.Control;
            if (k.altKey.isPressed) mods |= EventModifiers.Alt;
            if (k.leftCommandKey.isPressed) mods |= EventModifiers.Command;
            if (k.rightCommandKey.isPressed) mods |= EventModifiers.Command;
            if (k.capsLockKey.isPressed) mods |= EventModifiers.CapsLock;
            return mods;
        }

        static List<RaycastResult> results = new();
        public static RaycastResult RaycastUi(PointerEventData eventData) {
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var result in results) {
                // TODO: For UI Toolkit, figure out if the UI element under cursor is actually blocking the ray
                if (result.module is GraphicRaycaster || result.module is PanelRaycaster) return result;
            }
            return default;
        }
        // public static bool IsPointerOverUI(Vector2 position) {
        //     PointerEventData eventData = new PointerEventData(EventSystem.current) { position = position };
        //     return IsPointerOverUI(eventData);
        // }

        // public static RaycastResult IsPointerOverUI(PointerEventData eventData) {
        //     EventSystem.current.RaycastAll(eventData, results);


        //     foreach (var r in results) {
        //         if (r.module is GraphicRaycaster) return true;
        //         // TODO: For UI Toolkit, figure out if the UI element under cursor is actually blocking the ray
        //         if (r.module is PanelRaycaster) return true;
        //     }
        //     return false;
        // }


    }
}
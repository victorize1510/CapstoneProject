using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem
{
    /// <summary>Gameplay reads physical key controls. Unity's text widgets still own composition.</summary>
    public static class GameplayKeyboard
    {
        static readonly HashSet<VisualElement> roots = new HashSet<VisualElement>();
        static readonly HashSet<Key> heldDuringText = new HashSet<Key>();
        static readonly Dictionary<KeyCode, Key> bindings = new Dictionary<KeyCode, Key>();
        static Keyboard imeKeyboard;
        static bool? imeEnabled;
        static bool wasEditing;
        static int blockedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            roots.Clear();
            heldDuringText.Clear();
            imeKeyboard = null;
            imeEnabled = null;
            wasEditing = false;
            blockedFrame = -1;
        }

        internal static void Track(VisualElement root) => roots.Add(root);
        internal static void Untrack(VisualElement root) => roots.Remove(root);

        public static bool IsEditingText
        {
            get
            {
                var selected = EventSystem.current?.currentSelectedGameObject;
                if (selected != null && selected.activeInHierarchy
                    && (selected.GetComponentInParent<InputField>()?.isFocused == true
                    || selected.GetComponentInParent<TMP_InputField>()?.isFocused == true))
                    return true;
                foreach (var root in roots)
                {
                    if (root.panel == null) continue;
                    for (var element = root.focusController?.focusedElement as VisualElement;
                        element != null; element = element.parent)
                    {
                        if (element.resolvedStyle.display == DisplayStyle.None || !element.enabledInHierarchy)
                            break;
                        if (element is TextField || element is IntegerField || element is FloatField)
                        {
                            bool visible = true;
                            for (var ancestor = element.parent; ancestor != null; ancestor = ancestor.parent)
                                if (ancestor.resolvedStyle.display == DisplayStyle.None) { visible = false; break; }
                            if (visible) return true;
                            break;
                        }
                    }
                }
                return false;
            }
        }

        internal static void SyncIme()
        {
            bool focused = Application.isFocused;
            bool editing = focused && IsEditingText;
            var keyboard = Keyboard.current;
            if (keyboard != null && wasEditing && !editing)
            {
                foreach (var control in keyboard.allKeys)
                    if (control.isPressed) heldDuringText.Add(control.keyCode);
                blockedFrame = Time.frameCount;
            }
            wasEditing = editing;
            if (keyboard != null)
                heldDuringText.RemoveWhere(key => !keyboard[key].isPressed);
            if (!focused)
            {
                Input.imeCompositionMode = IMECompositionMode.Auto;
                imeEnabled = null;
                return;
            }

            var mode = editing ? IMECompositionMode.On : IMECompositionMode.Off;
            if (Input.imeCompositionMode != mode) Input.imeCompositionMode = mode;
            if (keyboard != null && (keyboard != imeKeyboard || editing != imeEnabled))
            {
                keyboard.SetIMEEnabled(editing);
                imeKeyboard = keyboard;
                imeEnabled = editing;
            }
        }

        public static bool GetKey(KeyCode key, bool allowWhileEditing = false)
        {
            if (!CanRead(key, allowWhileEditing)) return false;
            var control = ReadControl(key);
            return control != null ? (allowWhileEditing || !IsSuppressed(control)) && control.isPressed : Input.GetKey(key);
        }

        public static bool GetKeyDown(KeyCode key, bool allowWhileEditing = false)
        {
            if (!CanRead(key, allowWhileEditing)) return false;
            var control = ReadControl(key);
            return control != null ? (allowWhileEditing || !IsSuppressed(control)) && control.wasPressedThisFrame : Input.GetKeyDown(key);
        }

        public static bool GetKeyUp(KeyCode key)
        {
            if (!CanRead(key, false)) return false;
            var control = ReadControl(key);
            return control != null ? !IsSuppressed(control) && control.wasReleasedThisFrame : Input.GetKeyUp(key);
        }

        static bool IsSuppressed(KeyControl control)
        {
            if (!heldDuringText.Contains(control.keyCode)) return false;
            // Release and the next press can both arrive between two rendered frames.
            if (!control.isPressed || control.wasReleasedThisFrame || control.wasPressedThisFrame) {
                heldDuringText.Remove(control.keyCode);
                return false;
            }
            return true;
        }

        static bool CanRead(KeyCode key, bool allowWhileEditing)
        {
            if (!Application.isFocused || key == KeyCode.None) return false;
            if (allowWhileEditing) return true;
            if (IsEditingText || wasEditing) return false;
            return blockedFrame != Time.frameCount;
        }

        static KeyControl ReadControl(KeyCode code)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return null;
            if (!bindings.TryGetValue(code, out Key key))
            {
                string name = code.ToString();
                if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9)
                    name = "Digit" + (code - KeyCode.Alpha0);
                else if (name.StartsWith("Keypad", StringComparison.Ordinal))
                    name = "Numpad" + name.Substring(6);
                else if (code == KeyCode.Return) name = "Enter";
                else if (code == KeyCode.LeftControl) name = "LeftCtrl";
                else if (code == KeyCode.RightControl) name = "RightCtrl";
                Enum.TryParse(name, true, out key);
                bindings.Add(code, key);
            }
            if (key == Key.None) return null;
            return keyboard[key];
        }

        public static Vector2 Movement
        {
            get
            {
                if (Keyboard.current == null) return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                var keyboard = new Vector2(
                    Axis(KeyCode.D, KeyCode.RightArrow, KeyCode.A, KeyCode.LeftArrow),
                    Axis(KeyCode.W, KeyCode.UpArrow, KeyCode.S, KeyCode.DownArrow));
                var stick = Gamepad.current?.leftStick.ReadValue() ?? Joystick.current?.stick.ReadValue() ?? Vector2.zero;
                return new Vector2(Mathf.Abs(stick.x) > Mathf.Abs(keyboard.x) ? stick.x : keyboard.x,
                    Mathf.Abs(stick.y) > Mathf.Abs(keyboard.y) ? stick.y : keyboard.y);
            }
        }

        static int Axis(KeyCode positive, KeyCode alternatePositive, KeyCode negative, KeyCode alternateNegative)
            => (GetKey(positive) || GetKey(alternatePositive) ? 1 : 0)
                - (GetKey(negative) || GetKey(alternateNegative) ? 1 : 0);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UI = UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    internal sealed class GameplayPointer : MonoBehaviour {
        readonly List<RaycastResult> hits = new();
        UI.UIDocument[] documents;
        PointerEventData pointer;
        Texture2D normal, action;
        bool? clickable;
        float nextCollect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() {
            var host = new GameObject("Gameplay Pointer");
            DontDestroyOnLoad(host);
            host.AddComponent<GameplayPointer>();
        }

        void Awake() {
            normal = Draw(new Vector2Int[] { new(3,2), new(3,24), new(9,19), new(13,28), new(17,26),
                new(13,17), new(22,17) }, new Color32(246, 248, 255, 255));
            action = Draw(new Vector2Int[] { new(10,2), new(14,2), new(15,12), new(19,11), new(22,12),
                new(26,15), new(26,22), new(22,29), new(13,29), new(8,24), new(4,17), new(7,15),
                new(10,18) }, new Color32(255, 218, 123, 255));
        }

        void Update() {
            if (!Cursor.visible || Cursor.lockState == CursorLockMode.Locked) return;
            Vector2 mouse = Input.mousePosition;
            // Chuột ngoài cửa sổ Game (vừa bấm Play, đang ở Inspector...) -> Unity trả về vị trí vô cực
            // làm Camera.ScreenPointToRay báo lỗi "Screen position out of view frustum". Bỏ qua frame đó.
            if (!OnScreen(mouse)) return;
            bool canClick = IsClickable(mouse);
            if (clickable == canClick) return;
            clickable = canClick;
            Cursor.SetCursor(canClick ? action : normal, canClick ? new Vector2(12, 3) : new Vector2(3, 3), CursorMode.Auto);
        }

        static bool OnScreen(Vector2 p) =>
            !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y)
            && p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height;

        bool IsClickable(Vector2 mouse) {
            var events = EventSystem.current;
            if (events != null) {
                if (pointer == null || pointer.currentInputModule != events.currentInputModule)
                    pointer = new PointerEventData(events);
                pointer.position = mouse;
                hits.Clear();
                events.RaycastAll(pointer, hits);
                foreach (var hit in hits) {
                    if (hit.gameObject.GetComponentInParent<Selectable>() is { } control)
                        return control.IsInteractable();
                    if (hit.gameObject.GetComponent<IPointerClickHandler>() != null) return true;
                }
            }
            if (documents == null || Time.unscaledTime >= nextCollect) {
                documents = FindObjectsByType<UI.UIDocument>(FindObjectsSortMode.None);
                nextCollect = Time.unscaledTime + 1;
            }
            foreach (var document in documents) {
                var panel = document != null && document.isActiveAndEnabled ? document.rootVisualElement?.panel : null;
                if (panel == null) continue;
                var position = UI.RuntimePanelUtils.ScreenToPanel(panel, new Vector2(mouse.x, Screen.height - mouse.y));
                for (var element = panel.Pick(position); element != null; element = element.parent)
                    if (element.enabledInHierarchy && element is UI.Button or UI.Toggle or UI.Slider or UI.SliderInt or UI.DropdownField)
                        return true;
            }
            if (hits.Count > 0 || Inventory.InventoryInputController.GameplayInputBlocked || Camera.main == null) return false;
            if (!Physics.Raycast(Camera.main.ScreenPointToRay(mouse), out var worldHit, 120f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            var enemy = worldHit.collider.GetComponentInParent<DummyEnemy>();
            return enemy != null && enemy.IsSelectable;
        }

        static Texture2D Draw(Vector2Int[] points, Color32 fill) {
            const int size = 32;
            var pixels = new Color32[size * size];
            bool Inside(float x, float y) {
                bool inside = false;
                for (int i = 0, prior = points.Length - 1; i < points.Length; prior = i++) {
                    var a = points[prior]; var b = points[i];
                    if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                        inside = !inside;
                }
                return inside;
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) {
                    bool isFill = Inside(x + .5f, y + .5f);
                    if (isFill || Inside(x - .5f, y + .5f) || Inside(x + 1.5f, y + .5f) ||
                        Inside(x + .5f, y - .5f) || Inside(x + .5f, y + 1.5f))
                        pixels[(size - 1 - y) * size + x] = isFill ? fill : new Color32(30, 31, 43, 255);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        void OnDestroy() {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (normal != null) Destroy(normal);
            if (action != null) Destroy(action);
        }
    }
}
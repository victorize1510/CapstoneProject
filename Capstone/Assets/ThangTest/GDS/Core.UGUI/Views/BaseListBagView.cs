using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GDS.Core.UGUI {

    public abstract class BaseListBagView : MonoBehaviour {
        [SerializeField, Required] protected GameObject slotPrefab;
        [SerializeField] protected RectTransform container;
        [SerializeField] protected bool showLocalData = true;
        [SerializeField, TextArea(3, 10)] protected string debugText;

        [NonSerialized] protected List<SlotView> slotViews = new();
        [NonSerialized] protected GhostContext ghost;
        [NonSerialized] protected SlotView lastGhostSlot;

        protected CountWarning warning;

        void Awake() { warning = GetComponent<CountWarning>(); }

        public void GenerateSlotsEditor(int count) {
#if UNITY_EDITOR
            int group = UnityEditor.Undo.GetCurrentGroup();
            ClearContainerEditor();
            CreateSlotViewsEditor(count);
            ResizeEditor();
            UnityEditor.Undo.SetCurrentGroupName("Generate slot views");
            UnityEditor.Undo.CollapseUndoOperations(group);
#endif
        }

        public void CreateSlotViewsEditor(int count) {
#if UNITY_EDITOR
            int group = UnityEditor.Undo.GetCurrentGroup();
            for (int i = 0; i < count; i++) {
                var go = UnityEditor.PrefabUtility.InstantiatePrefab(slotPrefab, container);
                SlotView instance = ((GameObject)go).GetComponent<SlotView>();
                instance.name = i.ToString();
                UnityEditor.Undo.RegisterCreatedObjectUndo(instance.gameObject, "Create Slot view: " + instance.name);
            }
            UnityEditor.Undo.SetCurrentGroupName("Generate Slots");
            UnityEditor.Undo.CollapseUndoOperations(group);
#endif
        }

        public void ClearContainerEditor() {
#if UNITY_EDITOR
            UnityEditor.Undo.SetCurrentGroupName("Clear container");
            var count = container.childCount;
            for (int i = count - 1; i >= 0; i--) {
                UnityEditor.Undo.DestroyObjectImmediate(container.GetChild(i).gameObject);
            }
#endif
        }

        public void ResizeEditor() {
#if UNITY_EDITOR
            var group = UnityEditor.Undo.GetCurrentGroup();
            UnityEditor.Undo.RecordObject(transform, "Resize container");
            UnityEditor.Undo.RecordObject(container, "Resize container");
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);
            float width = LayoutUtility.GetPreferredWidth(container);
            float height = LayoutUtility.GetPreferredHeight(container);
            var preferredSize = new Vector2(width, height);
            Debug.Log($"childCount: {container.childCount}, preferred size: {preferredSize}");
            ((RectTransform)transform).sizeDelta = preferredSize;
            UnityEditor.Undo.SetCurrentGroupName("Resize container");
            UnityEditor.Undo.CollapseUndoOperations(group);
#endif
        }



    }
}
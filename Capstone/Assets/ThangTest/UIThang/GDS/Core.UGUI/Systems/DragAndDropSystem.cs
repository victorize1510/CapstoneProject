using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using GDS.Core.Events;

namespace GDS.Core.UGUI {

    public class DragAndDropSystem : MonoBehaviour {
        [SerializeField, Required] GameObject GhostPrefab;
        [Tooltip("Minimum drag distance required to trigger a start drag operation")]
        [SerializeField] int DragThreshold = 32;

        [Space(12)]
        [SerializeField] LayerMask itemSpawnLayer;
        [SerializeField] LayerMask itemSpawnBlockingLayer;


        readonly List<RaycastResult> results = new();
        readonly PointerEventData eventData = new(EventSystem.current);

        IStore store;
        GameObject GhostView;
        IItemView GhostItemView;

        // Pointer position on mouse down, used to compute the drag delta
        Vector2 lastPointerPos = new();
        // Current pointer position 
        Vector2 currentPointerPos = new();
        [NonSerialized] ItemContext lastContext = new();


        void Awake() {
            store = StoreLocator.Get();
            GhostView = GhostPrefab.scene.IsValid() ? GhostPrefab : Instantiate(GhostPrefab, transform.root);
            GhostView.SetActive(false);
            GhostItemView = GhostView.GetComponent<IItemView>();
            if (GhostItemView == null) { Debug.LogError("GhostView needs to implement IItemView!", this); }
        }

        void OnEnable() { store.Ghost.OnChange += OnGhostChange; }
        void OnDisable() { store.Ghost.OnChange -= OnGhostChange; }
        void OnGhostChange(IItemContext context) {
            GhostView.transform.position = currentPointerPos;
            GhostView.SetActive(!store.Ghost.Empty);
            GhostItemView.Item = context.Item;
        }

        void Update() {
            currentPointerPos = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasReleasedThisFrame) {
                OnPointerUp();
                lastContext.Clear();
            }
            if (Mouse.current.leftButton.wasPressedThisFrame) {
                OnPointerDown();
            }
            OnPointerMove();
        }

        void OnPointerMove() {
            MoveGhostItem();
            TryDragPickItem();
        }

        void MoveGhostItem() { if (!store.Ghost.Empty) GhostView.transform.position = currentPointerPos; }
        void TryDragPickItem() {
            // Disallow drag-picking an item while dragging another item because it feels awkward and unintuitive
            // TODO: Revisit this mechanic perhaps???
            if (!store.Ghost.Empty) return;
            if (lastContext.Empty) return;
            // Check drag threshold
            Vector2 delta = currentPointerPos - lastPointerPos;
            if (delta.sqrMagnitude < DragThreshold * DragThreshold) return;
            store.Bus.Publish(new PickItem(lastContext));
            lastContext.Clear();
        }

        // Stores the pointer position and context at that position, which will be used 
        // to compute the drag distance and trigger a start drag operation
        void OnPointerDown() {
            eventData.position = currentPointerPos;
            var context = GetContextAtPointer(eventData);
            if (context == null) return;
            if (context.Item == null) return;
            lastContext.Copy(context);
            lastPointerPos = currentPointerPos;
        }

        public void OnPointerUp() {
            eventData.position = currentPointerPos;
            if (store.Ghost.Empty) { TryPickItem(); return; }
            var result = InputUtil.RaycastUi(eventData);
            if (result.isValid) { TryPlaceOrDropOnUi(result); return; }
            TryDropInWorld();
        }

        void TryPickItem() {
            var context = GetContextAtPointer(eventData);
            if (context?.Item != null) {
                lastContext.Copy(context);
                store.Bus.Publish(new PickItem(lastContext));
            }
        }

        void TryPlaceOrDropOnUi(RaycastResult result) {
            var context = GetContextAtPointer(eventData);
            if (context != null) {
                store.Bus.Publish(new PlaceGhostItem(context));
            } else {
                store.Bus.Publish(new DropGhostItem() { IsOverUi = true, GameObject = result.gameObject, ScreenPosition = result.screenPosition });
            }
        }

        void TryDropInWorld() {
            var ray = Camera.main.ScreenPointToRay(Mouse.current.position.value);
            // Raycast using both spawn and blocking layer
            Physics.Raycast(ray, out var hitInfo, 100, itemSpawnLayer | itemSpawnBlockingLayer);
            // Check that the collider is not the blocking layer
            if (hitInfo.collider != null && (itemSpawnBlockingLayer.value & (1 << hitInfo.collider.gameObject.layer)) != 0) return;
            store.Bus.Publish(new DropGhostItem() { WorldPosition = hitInfo.point });
        }

        IItemContext GetContextAtPointer(PointerEventData eventData) {
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var hit in results) {
                if (hit.gameObject.TryGetComponent<IItemContext>(out var context)) return context;
            }
            return null;
        }

    }

}
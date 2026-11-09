using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Capstone.Game.HudSystem {
    // Owns only presentation state. Disposing never rolls back an evolution already committed by the service.
    internal sealed class EvolutionPresentationScope : IDisposable {
        readonly object owner;
        readonly PetController pet;
        readonly Transform parent;
        readonly Scene scene;
        readonly Vector3 position;
        readonly Quaternion rotation;
        readonly Dictionary<Transform,int> layers = new();
        readonly Dictionary<Behaviour,bool> behaviours = new();
        readonly Dictionary<VisualElement,StyleEnum<DisplayStyle>> documents = new();
        readonly CursorLockMode cursorLock;
        readonly bool cursorVisible;
        readonly int defaultLayer;
        bool disposed;
        public EvolutionPresentationScope(object owner, PetController pet, MonoBehaviour[] extraScripts) {
            this.owner=owner; this.pet=pet; parent=pet.transform.parent; scene=pet.gameObject.scene;
            position=pet.transform.position; rotation=pet.transform.rotation; defaultLayer=pet.gameObject.layer;
            cursorLock=Cursor.lockState; cursorVisible=Cursor.visible;
            foreach(var t in pet.GetComponentsInChildren<Transform>(true)) layers[t]=t.gameObject.layer;
            Remember(pet);
            if(extraScripts!=null)foreach(var script in extraScripts)if(script!=null&&!ReferenceEquals(script,owner))Remember(script);
            foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if(camera.gameObject.scene==scene&&camera.cameraType==CameraType.Game&&camera.targetTexture==null)Remember(camera);
            foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(canvas.gameObject.scene==scene)Remember(canvas);
            foreach(var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                if(doc.gameObject.scene==scene&&doc.rootVisualElement!=null) {
                    documents[doc.rootVisualElement]=doc.rootVisualElement.style.display;
                    doc.rootVisualElement.style.display=DisplayStyle.None;
                }
            InventoryInputController.SetExternalGameplayInputBlocked(owner,true);
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }
        void Remember(Behaviour value) { if(behaviours.ContainsKey(value))return; behaviours[value]=value.enabled; value.enabled=false; }
        public void Dispose() {
            if(disposed)return; disposed=true;
            if(pet!=null) {
                pet.transform.SetParent(null,true);
                var destination=scene.IsValid()&&scene.isLoaded?scene:SceneManager.GetActiveScene();
                if(destination.IsValid()&&destination.isLoaded)SceneManager.MoveGameObjectToScene(pet.gameObject,destination);
                pet.transform.SetPositionAndRotation(position,rotation);
                if(parent!=null&&parent.gameObject.scene==pet.gameObject.scene)pet.transform.SetParent(parent,true);
                foreach(var t in pet.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layers.TryGetValue(t,out int layer)?layer:defaultLayer;
            }
            foreach(var pair in behaviours)if(pair.Key!=null)pair.Key.enabled=pair.Value;
            foreach(var pair in documents)if(pair.Key.panel!=null)pair.Key.style.display=pair.Value;
            pet?.RefreshVisualPresence();
            InventoryInputController.SetExternalGameplayInputBlocked(owner,false);
            Cursor.lockState=cursorLock; Cursor.visible=cursorVisible;
        }
    }
}

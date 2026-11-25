using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Capstone.Game.Inventory {
    public sealed class LocalPlayerControlLock : MonoBehaviour {
        [SerializeField] Transform playerRoot = null;
        [SerializeField] bool autoFindLocalPlayer = true;
        [SerializeField] List<MonoBehaviour> componentsToDisable = new List<MonoBehaviour>();

        readonly List<ComponentState> savedStates = new List<ComponentState>();
        readonly List<MonoBehaviour> softLockedComponents = new List<MonoBehaviour>();
        readonly HashSet<object> lockOwners = new HashSet<object>();

        public bool IsLocked => lockOwners.Count > 0;

        public void LockControls() {
            LockControls(this);
        }

        public void LockControls(object owner) {
            owner = owner ?? this;
            if (!lockOwners.Add(owner) || lockOwners.Count > 1) return;

            ResolveComponents();
            savedStates.Clear();
            softLockedComponents.Clear();

            foreach (var component in componentsToDisable) {
                if (component == null || component == this) continue;
                if (!IsLocalAuthority(component.gameObject)) continue;

                if (TrySetSoftInputLock(component, true)) {
                    softLockedComponents.Add(component);
                    continue;
                }

                savedStates.Add(new ComponentState(component, component.enabled));
                component.enabled = false;
            }

        }

        public void UnlockControls() {
            UnlockControls(this);
        }

        public void UnlockControls(object owner) {
            owner = owner ?? this;
            if (!lockOwners.Remove(owner) || lockOwners.Count > 0) return;

            RestoreComponents();
        }

        void RestoreComponents() {

            foreach (var component in softLockedComponents) {
                if (component != null) TrySetSoftInputLock(component, false);
            }

            foreach (var state in savedStates) {
                if (state.Component != null) state.Component.enabled = state.WasEnabled;
            }

            softLockedComponents.Clear();
            savedStates.Clear();
        }

        void OnDisable() {
            lockOwners.Clear();
            RestoreComponents();
        }

        void ResolveComponents() {
            AddIfMissing(FindPlayerMovement());
            AddIfMissing(FindPlayerCamera());
        }

        MonoBehaviour FindPlayerMovement() {
            if (playerRoot != null) {
                var movement = FindBehaviourInHierarchy(playerRoot.gameObject, "BasicPlayerMovement");
                if (movement != null) return movement;
            }

            if (!autoFindLocalPlayer) return null;

            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (behaviour == null || behaviour.GetType().Name != "BasicPlayerMovement") continue;
                if (!IsLocalAuthority(behaviour.gameObject)) continue;

                playerRoot = behaviour.transform;
                return behaviour;
            }

            return null;
        }

        MonoBehaviour FindPlayerCamera() {
            if (Camera.main != null) {
                var cameraFollow = FindBehaviourOnObject(Camera.main.gameObject, "BasicCameraFollow");
                if (cameraFollow != null) return cameraFollow;
            }

            if (playerRoot != null) {
                foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                    if (behaviour == null || behaviour.GetType().Name != "BasicCameraFollow") continue;
                    if (ReferencesTransform(behaviour, "target", playerRoot)) return behaviour;
                }
            }

            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (behaviour != null && behaviour.GetType().Name == "BasicCameraFollow") return behaviour;
            }

            return null;
        }

        void AddIfMissing(MonoBehaviour component) {
            if (component == null || componentsToDisable.Contains(component)) return;
            componentsToDisable.Add(component);
        }

        static MonoBehaviour FindBehaviourInHierarchy(GameObject source, string typeName) {
            var behaviour = FindBehaviourOnObject(source, typeName);
            if (behaviour != null) return behaviour;

            foreach (var item in source.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (item != null && item.GetType().Name == typeName) return item;
            }

            foreach (var item in source.GetComponentsInParent<MonoBehaviour>(true)) {
                if (item != null && item.GetType().Name == typeName) return item;
            }

            return null;
        }

        static MonoBehaviour FindBehaviourOnObject(GameObject source, string typeName) {
            foreach (var item in source.GetComponents<MonoBehaviour>()) {
                if (item != null && item.GetType().Name == typeName) return item;
            }

            return null;
        }

        static bool ReferencesTransform(MonoBehaviour behaviour, string memberName, Transform target) {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = behaviour.GetType();

            var field = type.GetField(memberName, flags);
            if (field != null && ReferenceEquals(field.GetValue(behaviour), target)) return true;

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.GetIndexParameters().Length == 0 && ReferenceEquals(property.GetValue(behaviour), target)) return true;

            return false;
        }

        static bool TrySetSoftInputLock(MonoBehaviour component, bool locked) {
            if (component == null) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var method = component.GetType().GetMethod("SetGameplayInputLocked", flags);
            if (method == null) return false;

            var parameters = method.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(bool)) return false;

            method.Invoke(component, new object[] { locked });
            return true;
        }

        static bool IsLocalAuthority(GameObject source) {
            var markers = source.GetComponentsInParent<MonoBehaviour>(true);
            foreach (var marker in markers) {
                if (marker == null) continue;
                if (TryReadBoolMember(marker, "HasInputAuthority", out bool hasInputAuthority)) return hasInputAuthority;
                if (!TryReadObjectMember(marker, "Object", out object networkObject)) continue;
                if (TryReadBoolMember(networkObject, "HasInputAuthority", out bool hasObjectInputAuthority)) {
                    return hasObjectInputAuthority;
                }
            }

            // Single-player objects usually do not expose network authority members.
            return true;
        }

        static bool TryReadBoolMember(object source, string memberName, out bool value) {
            value = false;
            if (source == null) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = source.GetType();

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0) {
                value = (bool)property.GetValue(source);
                return true;
            }

            var field = type.GetField(memberName, flags);
            if (field != null && field.FieldType == typeof(bool)) {
                value = (bool)field.GetValue(source);
                return true;
            }

            return false;
        }

        static bool TryReadObjectMember(object source, string memberName, out object value) {
            value = null;
            if (source == null) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = source.GetType();

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.GetIndexParameters().Length == 0) {
                value = property.GetValue(source);
                return value != null;
            }

            var field = type.GetField(memberName, flags);
            if (field != null) {
                value = field.GetValue(source);
                return value != null;
            }

            return false;
        }

        readonly struct ComponentState {
            public readonly MonoBehaviour Component;
            public readonly bool WasEnabled;

            public ComponentState(MonoBehaviour component, bool wasEnabled) {
                Component = component;
                WasEnabled = wasEnabled;
            }
        }
    }
}

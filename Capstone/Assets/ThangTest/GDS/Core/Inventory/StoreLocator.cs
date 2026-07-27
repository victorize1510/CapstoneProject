using UnityEngine;

namespace GDS.Core {
    public static class StoreLocator {
        static IStore store;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() {
            store = null;
            Debug.Log($"[StoreLocator::Reset] cleared store: {store == null}");
        }

        public static void Register(IStore service) { store = service; }

        public static IStore Get() => Get<IStore>();
        public static T Get<T>() where T : IStore {
            // Guard agains fake null
            if (store is UnityEngine.Object obj && obj == null) store = null;
            if (store is not T typedStore) {
                Debug.LogError($"Could not find a store of type {typeof(T)}, did you forget to register it or add it to the scene?");
                return default;
            }
            return typedStore;
        }
    }
}
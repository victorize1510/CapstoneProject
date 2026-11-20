using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.Elements
{
    [CreateAssetMenu(menuName = "Capstone/Combat/Bang khac che", fileName = "ElementMatchups")]
    public sealed class ElementMatchupTable : ScriptableObject
    {
        [Header("He tan cong va cac he phong thu")]
        [Tooltip("Khong ghi cap nao thi cap do nhan he so 1. Khong tu suy quan he nguoc.")]
        public List<ElementMatchupRow> rows = new List<ElementMatchupRow>();
        ElementMatchupLookup lookup;
        public ElementMatchupLookup Lookup => lookup ??= new ElementMatchupLookup(rows);
        void OnEnable() => Rebuild();
        void OnValidate() => Rebuild();
        public void Rebuild() => lookup = new ElementMatchupLookup(rows);
    }

    public static class ElementCombatSettings
    {
        const string ResourcePath = "Combat/ElementMatchups";
        static bool initialized;
        static ElementMatchupTable table;
        public static ElementMatchupLookup Lookup
        {
            get
            {
                Initialize();
                return table != null ? table.Lookup : ElementMatchupLookup.Neutral;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            initialized = false;
            table = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            table = Resources.Load<ElementMatchupTable>(ResourcePath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (table == null) Debug.LogError("Thieu Combat/ElementMatchups. Can gan bang khac che truoc khi kiem gameplay.");
            else if (!table.Lookup.IsValid) Debug.LogError("Bang khac che khong hop le: " + string.Join("; ", table.Lookup.Errors));
#endif
        }
    }
}

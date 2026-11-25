using System.Collections.Generic;
using UnityEngine;
namespace Capstone.Game.CaptureSystem {
    internal static class CaptureDiagnostics {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly HashSet<int> reported=new HashSet<int>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>reported.Clear();
#endif
        [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Missing(Object source){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int key=source!=null?source.GetInstanceID():0;
            if(reported.Add(key))Debug.LogWarning("Capture configuration is missing or invalid. Check the species/form profile and the capture-ball item.",source);
#endif
        }
    }
}

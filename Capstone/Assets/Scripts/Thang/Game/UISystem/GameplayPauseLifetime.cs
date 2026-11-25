using UnityEngine;
namespace Capstone.Game.UISystem {
    public sealed class GameplayPauseLifetime:MonoBehaviour {
        void LateUpdate()=>GameplayPause.Prune();
        void OnDestroy()=>GameplayPause.ReleaseAll();
    }
}


using System.IO;
using UnityEngine;

namespace Capstone.Game.UISystem {
    /// <summary>Local verification outputs live outside the repository and Unity Assets.</summary>
    public static class QaArtifactPaths {
        public static string Root => Path.Combine(Application.persistentDataPath, "QA", "Artifacts") + Path.DirectorySeparatorChar;
        public const int MaxRetainedFrames = 10;
    }
}

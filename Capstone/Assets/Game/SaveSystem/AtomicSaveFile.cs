using System;
using System.IO;
using System.Text;

namespace Capstone.Game.SaveSystem {
    internal static class AtomicSaveFile {
        public static string Slot(string slotId) {
            string value = string.IsNullOrWhiteSpace(slotId) ? "main" : slotId.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value;
        }

        // Never delete the destination first: failure must leave the old save readable.
        public static void Write(string path, string json, bool backUpPrimary) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            try {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, backUpPrimary ? path + ".bak" : null);
                else File.Move(temporary, path);
            } finally {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}

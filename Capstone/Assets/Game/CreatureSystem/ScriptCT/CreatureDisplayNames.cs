using System;

namespace Capstone.Game.CreatureSystem
{
    // Chi trinh bay. Khong dung ket qua nay lam khoa catalog, species hay save.
    public static class CreatureDisplayNames
    {
        static bool Same(string a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
        static bool IsDragon(string id) => Same(id, "Dragon Dusk") || Same(id, "Dragon Nightfall")
            || Same(id, "Dragon Darkness") || Same(id, "Dragon Dusk Winged") || Same(id, "Dragon Dusk Elder");

        public static string Species(string definitionId, string formId, string fallback) {
            // An unspecified legacy form can still carry a known system name.
            if (!string.IsNullOrWhiteSpace(formId)) {
                string name = CreatureAvatarCatalog.ResolveDisplayName(definitionId, formId);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            return IsDragon(definitionId) ? LocalizeSystemName(fallback) : fallback ?? string.Empty;
        }

        public static string Name(string definitionId, string formId, string fallback) {
            // Giu ten tuy chinh; chi chuyen ten he thong da biet.
            return IsDragon(fallback) || Same(fallback, "Dragon Bóng Tối") || Same(fallback, "Rồng Chạng Vạng")
                || Same(fallback, "Rồng Đêm Tối") || Same(fallback, "Rồng Bóng Tối")
                ? Species(definitionId, formId, fallback) : fallback ?? string.Empty;
        }

        public static string LocalizeSystemName(string value) {
            if (Same(value, "Dragon Dusk")) return "Rồng Chạng Vạng";
            if (Same(value, "Dragon Nightfall") || Same(value, "Dragon Dusk Winged")) return "Rồng Đêm Tối";
            if (Same(value, "Dragon Darkness") || Same(value, "Dragon Dusk Elder") || Same(value, "Dragon Bóng Tối")) return "Rồng Bóng Tối";
            return value ?? string.Empty;
        }
    }
}

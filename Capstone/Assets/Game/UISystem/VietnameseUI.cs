using System.Globalization;
using System.Text;
using Capstone.Game.HudSystem;

namespace Capstone.Game.UISystem {
    /// <summary>Presentation only: never use translated text as an identity or save key.</summary>
    public static class VietnameseUI {
        public const string Back = "←  TRỞ LẠI";
        public const string NoSelection = "Chọn một mục để xem thông tin.";
        public const string Unknown = "Chưa rõ";
        public const string Unavailable = "Chưa có thông tin";
        public const string Active = "Đang dùng";
        public const string Viewing = "Đang xem";
        public static string ItemName(string value){switch(value){case "Leaf Berry":return "Quả lá";case "Dusk Stone":return "Đá hoàng hôn";case "Potion":return "Bình hồi máu";case "Potion nhỏ":return "Bình hồi máu nhỏ";case "Coin":case "Currency":return "Vàng";case "Experience":return "EXP";case "Item":return "Vật phẩm";default:return value??string.Empty;}}

        public static string ItemText(string value) {
            switch (value) {
                case "A basic restorative carried by traveling beast tamers.": return "Thuốc hồi phục cơ bản dành cho hành trình của nhà huấn luyện.";
                case "Restores 400 pet HP.": return "Hồi phục 400 HP cho thú đồng hành.";
                case "A dark stone that evolves Dragon Dusk or Dragon Nightfall.": return "Viên đá bóng tối dùng để tiến hóa Dragon Dusk hoặc Dragon Nightfall.";
                case "Evolve one eligible pet in the Party without increasing its level.": return "Tiến hóa một thú tương thích trong đội hình, giữ nguyên cấp độ. Sử dụng từ Túi đồ.";
                default: return value ?? string.Empty;
            }
        }
        public static string SystemText(string value)=>(value??string.Empty).Replace("Leaf Berry","Quả lá").Replace("Dusk Stone","Đá hoàng hôn");

        public static string Element(PetElement value) {
            switch (value) {
                case PetElement.Nature: return "Cỏ";
                case PetElement.Fire: return "Lửa";
                case PetElement.Water: return "Nước";
                case PetElement.Wind: return "Gió";
                case PetElement.Earth: return "Đất";
                case PetElement.Electric: return "Điện";
                case PetElement.Ice: return "Băng";
                case PetElement.Light: return "Ánh sáng";
                case PetElement.Dark: return "Bóng tối";
                case PetElement.Dragon: return "Rồng";
                default: return Unknown;
            }
        }

        public static string Rarity(PetRarity value) => Rarity(value.ToString());
        public static string Rarity(string value) {
            switch (value) {
                case "Common": return "Thường";
                case "Uncommon": return "Ít gặp";
                case "Rare": return "Hiếm";
                case "Epic": return "Sử thi";
                case "Legendary": return "Huyền thoại";
                default: return Unknown;
            }
        }

        public static string Screen(string key) {
            switch (key) {
                case "Profile": return "Hồ sơ";
                case "Pets": case "Party": return "Thú đồng hành";
                case "Box": return "Kho thú";
                case "Bag": case "Inventory": return "Túi đồ";
                case "Quest": case "Quest Journal": return "Nhiệm vụ";
                case "Map": return "Bản đồ";
                case "Codex": return "Cẩm nang Pet";
                case "Settings": return "Cài đặt";
                case "Shop": case "Store": return "Cửa hàng";
                default: return key;
            }
        }

        public static string Skill(string name) {
            switch (name) {
                case "Bite": return "Cắn";
                case "Blast": return "Bộc phá";
                case "Projectile": return "Đạn năng lượng";
                case "Wing Strike": return "Đánh cánh";
                default: return name;
            }
        }

        public static string Gender(string value) {
            switch (value?.Trim().ToLowerInvariant()) {
                case "male": return "Đực";
                case "female": return "Cái";
                case null: case "": case "unknown": return Unknown;
                default: return value;
            }
        }

        public static string Personality(string value) {
            switch (value?.Trim()) {
                case "Curious": return "Tò mò";
                case null: case "": case "Unknown": return Unknown;
                default: return value.Trim();
            }
        }

        public static string Nature(string value) {
            switch (value) {
                case "Hardy": return "Bền bỉ"; case "Lonely": return "Độc lập"; case "Brave": return "Dũng cảm";
                case "Adamant": return "Kiên định"; case "Naughty": return "Tinh nghịch"; case "Bold": return "Táo bạo";
                case "Docile": return "Ngoan ngoãn"; case "Relaxed": return "Thư thái"; case "Impish": return "Lém lỉnh";
                case "Lax": return "Vô tư"; case "Timid": return "Nhút nhát"; case "Hasty": return "Nóng vội";
                case "Serious": return "Nghiêm túc"; case "Jolly": return "Vui vẻ"; case "Naive": return "Ngây thơ";
                case "Modest": return "Khiêm tốn"; case "Mild": return "Ôn hòa"; case "Quiet": return "Trầm lặng";
                case "Bashful": return "E thẹn"; case "Rash": return "Bốc đồng"; case "Calm": return "Điềm tĩnh";
                case "Gentle": return "Dịu dàng"; case "Sassy": return "Bướng bỉnh"; case "Careful": return "Thận trọng";
                case "Quirky": return "Khác biệt"; default: return string.IsNullOrWhiteSpace(value) ? Unknown : value;
            }
        }
        public static string PetDescription(string value) {
            if (string.IsNullOrWhiteSpace(value)) return "Chưa có mô tả cẩm nang cho thú này.";
            switch (value.Trim()) {
                case "A young dusk dragon learning to master its wings.": return "Một chú rồng hoàng hôn non đang học cách làm chủ đôi cánh.";
                default: return value.Trim();
            }
        }
        public static string SkillDescription(string value) {
            switch(value?.Trim()) {
                case "Basic close-range bite attack.": case "Test close-range bite attack.": return "Cắn mục tiêu ở cự ly gần.";
                case "A roar-like magic blast.": case "Test roar-like magic blast.": return "Giải phóng một luồng sức mạnh ma thuật bằng tiếng gầm.";
                case "Fires a ranged projectile.": case "Test ranged projectile attack.": return "Bắn một viên đạn năng lượng về phía mục tiêu.";
                case "A wing attack that knocks the air forward.": case "Test wing attack.": return "Vỗ cánh tạo luồng gió tấn công phía trước.";
                default: return string.IsNullOrWhiteSpace(value)?"Chưa có mô tả.":value.Trim();
            }
        }

        public static string SearchKey(string value) {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string normalized = value.Trim().Normalize(NormalizationForm.FormD);
            var result = new StringBuilder(normalized.Length);
            foreach (char c in normalized) {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                result.Append(c == 'đ' || c == 'Đ' ? 'd' : char.ToLowerInvariant(c));
            }
            return result.ToString().Normalize(NormalizationForm.FormC);
        }

        public static string StatTooltip(string key) {
            switch (key) {
                case "HP": return "Sinh lực hiện tại và tối đa.";
                case "MP": return "Năng lượng dùng để thi triển kỹ năng.";
                case "EXP": return "Kinh nghiệm để đạt cấp tiếp theo.";
                case "ATK": return "Sức tấn công vật lý.";
                case "SP.ATK": return "Sức tấn công đặc biệt.";
                case "DEF": return "Phòng thủ vật lý.";
                case "SP.DEF": return "Phòng thủ đặc biệt.";
                case "SPD": return "Tốc độ.";
                case "CRI Rate": case "CRIT RATE": return "Tỷ lệ gây đòn chí mạng.";
                case "CRI DMG": case "CRIT DMG": return "Sát thương bổ sung của đòn chí mạng.";
                default: return string.Empty;
            }
        }
    }
}

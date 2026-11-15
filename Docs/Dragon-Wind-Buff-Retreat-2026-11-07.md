# Chém gió, Gầm cường hóa và lùi nhanh — 07/11/2026

## Đã áp dụng trong project

Đã áp dụng dữ liệu skill, tạo VFX runtime và kiểm trong Unity 6000.3.16f1 sau khi người dùng mở lại Editor. Lượt hồi quy cuối đạt **146 assertion**, không có assertion lỗi. DemoScene giữ nguyên SHA256 trước/sau QA. Save thử độc lập trên E; không ghi save thật. Không đổi skill ID, GUID hiện có, prefab/model nguồn, công thức chỉ số gốc hoặc save schema.

Buff chỉ nhân phần ATK/SP.ATK, không nhân bonus damage cộng thêm của đánh thường. Đã nạp và chạy bản công thức này trong engine. Không dùng kết quả 177 assertion của task trước để đại diện cho cơ chế mới.

## Quyết định đã chốt

- `wing strike`: Chém gió, đòn bay một hit vật lý (ATK × 1,2), tầm 10 m, tốc độ 16 m/s, radius 0,38 m, tối đa 1,2 s. Giữ ID và hồi chiêu 4,2 s. Bản đầu chọn chém gió, chưa thêm lốc xoáy nhiều tick/AoE.
- `blast`: Gầm cường hóa, dùng Blast Attack làm động tác gầm vì rồng chưa có clip Roar riêng. Buff **ATK và SP.ATK +20%, 6 giây**, dùng lại làm mới thời gian, không cộng dồn. Không cần địch; không gây damage; giữ ID và hồi chiêu 3,8 s. Buff chỉ tồn tại runtime, gỡ khi thu thú/gục/đổi model/disable, không ghi save.
- Cắn không lùi. Chém gió và Đạn năng lượng lùi khi mục tiêu gần hơn 5 m; tối đa 2,4 m, tốc độ 12 m/s. Mặt vẫn hướng về địch. Thời gian lùi được cộng vào pre-animation windup để hit/release không xảy ra lúc đang lùi.
- Vật cản/mép nền ngăn lùi thì pet dừng lùi rồi tiếp tục cast nếu mục tiêu còn hợp lệ. Không có fallback teleport. Giữ raycast/range/target lock khi phát đạn.

## File nguồn đã sửa/thêm

- `Capstone/Assets/Game/Skills/PetSkillCombatProfile.cs`: append SelfBuff vào enum, cấu hình buff/retreat/socket, không đổi ordinal enum cũ.
- `Capstone/Assets/Game/Skills/PetSkillCombatExecutor.cs`: self buff tại release; retreat plan cho cast được chấp nhận; giữ projectile collision/damage một lần.
- `Capstone/Assets/Game/CreatureSystem/CreatureCombatant.cs`: modifier tạm thời, aura đi theo pet, expiry/cleanup; không đổi chỉ số gốc/công thức tiến triển hoặc save.
- `Capstone/Assets/Game/HudSystem/PetHudRuntimeStats.cs`: đồng bộ windup animation/presentation với thời gian lùi đã chuẩn bị.
- `Capstone/Assets/Scripts/Thắng/PetSystem/PetController.cs`: bước lùi có kiểm ground, capsule cast và giới hạn NavMesh; không xoay lưng về địch.
- `Capstone/Assets/Game/Skills/PetSkillEffectMotion.cs` + `.meta`: xoay ring VFX, không chứa damage/lifetime gameplay.
- `Capstone/Assets/Game/Skills/Editor/DragonSkillTacticsSetup.cs` + `.meta`: builder dùng Unity API tạo VFX/material/texture và cập nhật định nghĩa hiện có, giữ GUID.
- `Capstone/Assets/Game/CreatureSystem/Editor/BatRuntimeSetup.cs`: không ghi đè combat/presentation của skill đã cấu hình khi build family dơi lại.
- Asset đã cập nhật: `Skill_blast.asset`, `Skill_wing_strike.asset`; Presentation_blast/wing_strike; Combat_blast/wing_strike/projectile; learnset Dragon01/02/03. Giữ meta/GUID của các asset này.
- Runtime asset mới trong `Capstone/Assets/Game/Skills/Data/Tactics/`: `WindSlash.prefab`, `RoarBuffAura.prefab`, `WindBuff.mat`, `SoftParticle.asset`, cùng meta và meta thư mục. Chỉ khoảng 300 KB; cần giữ trong Git để hiệu ứng chạy.
- Tài liệu này. Các thay đổi khác trong working tree giữ nguyên, không commit/push.

## Chỉnh cấu hình trong Inspector

1. Mở `Skill_blast` → Combat → `Offense Buff Percent = 20`, `Buff Duration = 6`, `Buff Visual = RoarBuffAura`. Delivery là SelfBuff. Hồi chiêu ở Skill Definition giữ 3,8 s.
2. Mở Combat_wing_strike hoặc Combat_projectile: `Retreat Distance = 2.4`, `Retreat Speed = 12`, `Preferred Distance = 5`. Đặt Retreat Distance về 0 để skill đó đứng đánh. Cắn vẫn có Retreat Distance 0.
3. Chém gió dùng Projectile, Physical, Power 1,2; các trường Range/Projectile Speed/Radius/Lifetime chỉnh vùng bay. Nó vẫn là một hit lên mục tiêu khóa, chưa là AoE hoặc nhiều tick.
4. Presentation_blast: release 0,65 s sau khi animation bắt đầu, cộng thêm windup 0,1 s. Animation duration 1,5 s trên cả ba learnset. Presentation_wing_strike: release 0,3 s; clip duration 0,667 s. Nếu có lùi, cùng thời gian lùi được cộng vào cả clock animation và clock release.
5. Âm thanh gầm dùng `shout_frostbreath_006.wav`, pitch 0,75; gió dùng `Wind.wav`, đều từ KriptoFX đang có. Engine xác nhận AudioSource của gầm thực sự phát. Chưa đánh giá nghe bằng tai/loa thực; gầm là Blast Attack được tái sử dụng, không phải clip Roar mới.
6. Menu `Tools → Capstone → Creatures → 12. Apply dragon wind, roar buff and retreat` đã chạy. Chỉ dùng lại khi muốn dựng lại preset này: builder sẽ đặt lại các thông số nêu trên. Chỉnh tay sau đó thì không bấm builder để tránh ghi đè tuning.

## Kết quả thực chạy

- DemoScene Play Mode, ba production prefab OwnedPet_Dragon01/02/03, dữ liệu skill và animation thật. Mẫu đích Wild_Bat02 có 50.000 HP tạm để không chết giữa bài kiểm. Khu thử và camera đều tạm trong Play Mode; không có encounter hoặc scene mới được lưu.
- 146 assertion: tự triệu hồi/command-ready, HUB hiện đúng; gầm không cần mục tiêu, không gây damage, đúng 1,2× phần tấn công vật lý và phép, duration 6 s tính từ release; recast làm mới không cộng dồn; expiry/recall dọn aura và modifier; recall ẩn bốn ô HUB.
- Chín đòn damage định lượng (Cắn/Đạn năng lượng/Chém gió × ba form), đúng một hit/cast, có cooldown, damage theo profile và loại phòng thủ. DEF mẫu thử là 59, SP.DEF 71, phân biệt được hai nhánh mitigation. Sai số HP lớn nhất **0,001782 HP** do trừ số float trên HP tạm 50.000. CSV dùng dấu chấm thập phân, không phụ thuộc locale Windows.
- Chém gió có projectile thật, chưa trúng thì chưa trừ HP. Hai skill xa lùi khi bị áp sát và vẫn nhìn về địch; không gây damage trong lúc lùi. Form 01 xác nhận agent thực sự ở trên NavMesh thử; form 02/03 dùng fallback. Kiểm tường chặn lùi và mép nền không để lùi rơi xuống.
- Tương tác dùng raycast EventSystem và pointer callback trên nút HUB thật. Đây là engine test tự động, không phải click chuột hay phím vật lý. Probe có reset cooldown, giữ khóa camera thử và so sánh RNG để cô lập điều kiện; không thay catalog hoặc save thật.
- Chụp/xem sáu ảnh runtime: form 01/03 tại 1920×1080, form 02 tại 1366×768. Aura tím/vàng, lưỡi gió xanh có alpha, không có material hồng hoặc nền chữ nhật. Hai dải chém bổ sung nghiêng để đọc được từ camera bên hông. Camera thử có thể cắt cánh form 03 ở ảnh chém gió; chưa đánh giá framing của mọi camera gameplay.
- Minimap đen trong ảnh do khu thử ở ngoài world bounds của bản đồ. Không sửa minimap/UI để làm ảnh kiểm đẹp hơn. Lần thử đầu bị mất target vì camera thật tự bỏ khóa mục tiêu ở rất xa; fixture cuối giữ adapter camera ổn định, không sửa cơ chế khóa mục tiêu của game.
- Sau khi gỡ probe: compile cuối thành công 6,45 s, Mono reload 8,359 s, không có lỗi C# trong lượt cuối. Meta của toàn bộ asset có trước trong baseline không đổi. Script QA đã được gỡ, không để lệnh Play/recorder/build chạy nền.

Ảnh: [Gầm form 02, 1366×768](E:/UnityWork/CapstoneProjectData/Artifacts/dragon-wind-buff-retreat-2026-11-07/roar-form2.png), [Chém gió form 02](E:/UnityWork/CapstoneProjectData/Artifacts/dragon-wind-buff-retreat-2026-11-07/wind-form2.png), [Gầm form 03, 1920×1080](E:/UnityWork/CapstoneProjectData/Artifacts/dragon-wind-buff-retreat-2026-11-07/roar-form3.png). Cả sáu ảnh và `checks.txt`/`damage.csv` nằm trong artifact root.

## Giới hạn còn lại

- Cast ngoài tầm, vật cản, target chết/khóa bắt giữa windup; không tiêu thụ hit/token hai lần.
- Đạn hiện chỉ gây damage cho mục tiêu khóa; một địch khác có thể chặn nhưng không nhận damage. Chưa tự đổi chính sách này.
- Buff chỉ tăng phần damage từ ATK/SP.ATK; bonus damage cộng thêm của đánh thường không được nhân 20%. Crit dùng luồng cũ. Các ô stats cơ bản chưa thêm dòng hiển thị modifier tạm; aura là feedback buff.
- Chuỗi cast nhanh, thu pet, mở UI, tiến hóa/đổi scene; dừng bước lùi/visual đúng cách và không lưu buff.
- Bề rộng lưỡi gió/mesh so với radius hit, cơ thể lớn form 03, NavMesh sát vách/địa hình dốc, âm lượng gầm, shader không hồng hoặc quầng nền vuông.
- Chưa đo Profiler, standalone hoặc phím vật lý. Chưa kiểm mọi scene/camera, target di chuyển nhanh, hủy bằng scene unload hoặc nhiều địch cùng chắn đạn. Không tuyên bố đã đạt các nhánh chưa chạy.

Artifact: `E:/UnityWork/CapstoneProjectData/Artifacts/dragon-wind-buff-retreat-2026-11-07/`. Giữ backup nguồn, bản probe ngoài Git để tái hiện, báo cáo nhỏ và sáu ảnh cuối. Gỡ probe và meta khỏi Assets sau QA, dọn chẩn đoán thất bại/trung gian theo ledger. Không có recorder hoặc build thử đang chạy.

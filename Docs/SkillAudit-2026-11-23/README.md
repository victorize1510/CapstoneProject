# Audit kỹ năng và VFX Trung — 23/11/2026

## Bảng để duyệt

| Tệp CSV UTF-8 BOM | Nội dung |
|---|---|
| [04_CHON_STATS_27.csv](04_CHON_STATS_27.csv) | 27 định nghĩa với Combat Profile, loại sát thương, tầm, MP, hồi chiêu, crit, hiệu ứng và mốc Pet học |
| [05_VFX_TRUNG_27.csv](05_VFX_TRUNG_27.csv) | 27 dòng theo Skill ID, tham chiếu Cast/Release/Projectile/Impact/Buff, socket, thời điểm, SFX, Scale và ứng viên Trung |
| [02_CHIA_33_FORM.csv](02_CHIA_33_FORM.csv) | 33 Form thuộc 11 họ, learnset, Animator, loadout mặc định, socket và override |
| [PHU_LUC_FORM_SKILL.csv](PHU_LUC_FORM_SKILL.csv) | 124 cặp Form–Skill với state/clip và Transform Scale theo từng pha |
| [PHU_LUC_PREFAB.csv](PHU_LUC_PREFAB.csv) | 57 prefab phân biệt bằng đường dẫn, gồm 30 prefab Trung và 27 prefab runtime được tham chiếu |

Workspace hiện thiếu workbook `BANG_SKILL_KHONG_LEVEL_EXP_27_VFX_TRUNG_33_FORM.xlsx`. Mỗi bảng chính giữ các cột lựa chọn trống. Dữ liệu hiện tại được xuất từ prefab và ScriptableObject mà Unity 6.3 LTS đã import. Đường dẫn và GUID nằm chung trong từng ô reference. Khi cần tái xuất, giữ bản CSV đã điền riêng trước khi chạy menu **Tools → Capstone → Pet Skills → Export current skill and Trung VFX audit**.

## Level và EXP

`SkillHudData.skillLevel` trước đây được nạp từ `PetSkillProgressSaveData.level`, sao chép qua luồng chuẩn hóa loadout, gán mặc định bởi definition và công cụ dựng prefab, rồi hiện trên các thẻ và tooltip kỹ năng. Công thức sát thương của executor đọc `PetSkillCombatProfile.damageKind` và `power` cùng chiến lực Pet. Audit không tìm thấy bộ tích lũy EXP Skill, công thức tăng sát thương theo cấp Skill hoặc ba vật phẩm EXP Skill trong dữ liệu Inventory, Shop, Loot, Quest và mã sử dụng vật phẩm.

Save giữ tên mảng `learnedSkillProgress`, `skillId`, `unlocked`, `usable` và thứ tự `equippedSkillIds`, kể cả ô trống. Khi đọc JSON cũ, Unity bỏ qua thuộc tính Level/EXP dư trên từng Skill. Pet Level, Pet EXP, ngưỡng học theo Form và đường đọc Legacy ID vẫn theo luồng hiện tại. Prefab từng serialize `skillLevel` có thể còn khóa YAML cũ tới lần serialize tiếp theo. Kiểu runtime đã bỏ field đó nên Unity bỏ qua khóa cũ lúc import.

## Phát hiện từ asset và mã chạy

- **Combat.** 8 Physical, 18 Special và 1 Support. Có 7 Melee, 7 Projectile, 12 Targeted và 1 SelfBuff. Targeted dùng bán kính kiểm tra quanh điểm ngắm để xác nhận **một** mục tiêu. Chùm tia, Hơi Thở, Đàn Dơi, Bão Lá và Lốc Xoáy vì vậy dùng cơ chế Targeted hiện tại. Mô tả hiệu ứng và hình ảnh được ghi cùng các trường thực thi để thấy khi nào một hiệu ứng chỉ nằm ở phần trình bày. Chính executor kiểm va chạm và áp sát thương. Prefab projectile là phần nhìn. Cờ theo dõi mục tiêu chỉ cập nhật đến release, sau đó projectile đi theo hướng đã chốt.
- **Crit và hiệu ứng.** 27 định nghĩa đều có hai bonus crit bằng 0 ở lần xuất này. Nếu cấu hình sau, mã hiện cộng bonus của các Skill trang bị đã mở vào snapshot crit của Pet. Bleed của `bite` là 3 tick vật lý, mỗi giây một tick, làm mới theo nguồn. `blast` tăng công 20% trong 6 giây, hồi chiêu 10 giây và dùng 15 MP. `wing strike` rải một ngân sách Power cho 2–5 đạn, mỗi đạn tối đa một mục tiêu.
- **Thiếu icon.** Năm định nghĩa thiếu tài sản icon riêng là `bite.dark`, `pulse.dark`, `strike.fighting`, `strike.normal` và `touch.dark`. Cả năm vẫn resolve được Combat Profile. Hai chiêu gió `gust.flying` và `wing strike` chia sẻ icon Gió cắt. Đây là các điểm cần đối chiếu khi chốt hình hoặc nhãn kỹ năng.
- **Liên kết Trung.** 27 prefab chạy hiện tại resolve từ Combat/Presentation bằng GUID. Không có dependency trực tiếp hoặc bắc cầu tới 30 prefab trong `Assets/Prefabs/Trung/Skills`. Một số bản runtime có hình và Transform trùng với source Trung, được lưu thành asset riêng. Cột ứng viên ghi bằng chứng trùng tên, tên đối chiếu trong hồ sơ authoring hoặc cùng nhóm hệ. Các cặp **Gió cắt ↔ Chém Gió**, **Cơn Lốc Lá ↔ Bão Lá**, **Cầu điện ↔ Điện Cầu** và **Tia lửa điện ↔ Tia Lửa** đã được đối chiếu với GUID của prefab trên đĩa. Các asset có `csDestroyEffect`, `csParticleMove`, `CFX_AutoDestructShuriken` hoặc `CFXR_Effect` cần xét vòng đời trước khi đưa vào pool.
- **Scale.** Cue giữ root của prefab con rồi nhân với `max(0.01, cue.scale) × VisualScale`. Projectile ghi đè Scale root bằng `combat.visualScale × VisualScale`. Buff đặt Scale theo collider và kế thừa Transform Pet. 31 Form dùng `VisualScale = 1`, hai Form Rồng Đêm Tối và Rồng Bóng Tối dùng 1,15 và 1,35. Phụ lục ghi Particle Start Size, Shape, Scaling Mode, Simulation Space và tham số Graph expose. Kích thước node nội bộ và kích thước thực trên camera cần xem riêng. [Unity ParticleSystem scaling mode](https://docs.unity3d.com/ja/current/ScriptReference/ParticleSystem.MainModule-scalingMode.html) · [Unity VFX Graph exposed properties](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/vfx/visualeffectasset/getexposedproperties)
- **Animation và spawn.** 124 cặp Form–Skill resolve được state/clip và socket đã gán trong asset. Thời điểm release theo giây trong phụ lục là ước tính từ clip và hệ số phát. Trường normalized của driver, chuyển state, tốc độ Animator và tư thế ở frame release quyết định kết quả khi chạy. Impact đặt tại điểm va chạm hoặc điểm nhắm và dùng normal để xoay. Release có thể lấy trục pet hay socket theo `actorSpace`. Có thể dùng các cột socket/offset để kiểm miệng hoặc tay trước khi duyệt đồ họa.
- **Tính năng chỉ có trong hình hoặc đề xuất.** `PetSkillCombatExecutor` hiện không có profile Heal, knockback theo Skill, kéo vùng Hố Đen, poison, hoặc đạn homing sau release. Riêng Hố Đen của Trung có VFX Graph đang expose `Dust_Size = 0,06` và `Trails_Width = 1,7` trong prefab.

## Đề xuất để duyệt

| Vấn đề | Hiện tại | Đề xuất thử |
|---|---|---|
| Cận chiến | Range 2,5–3 m, gồm cả `touch.dark` là Special | A/B trong dải hiện tại với hitbox thấp và cao. So Physical/Special trên cùng Pet và cùng mục tiêu để hiểu ảnh hưởng của ATK/SP.ATK và DEF/SP.DEF. |
| Tầm xa | Projectile 9–14 m. Orb thường 9 m/s. Hai chiêu gió 16 m/s. Quả Cầu Lửa 14 m, 12 m/s | Thử dải chuẩn 9–12 m cho orb thường. Đánh dấu Quả Cầu Lửa là ứng viên tầm xa. Kiểm khả năng né ở 9–12 m/s với orb và 14–16 m/s với gió trước khi đổi tốc độ. |
| Hiệu suất | `bite.normal` 1,0/1,6 s, `bite` 1,2/2 s cộng Bleed, `projectile` 1,6/3 s với 12 MP | Đo sát thương trúng thực mỗi giây và mỗi MP qua ít nhất hai loại phòng thủ. Tính cả thời gian lấy đà, trượt và thời gian tạm lùi. So trực tiếp hai đòn Cắn và hai đòn gió trước khi chỉnh số. |
| Vùng va chạm | Cả 12 Targeted dùng bán kính 1 m và một mục tiêu | A/B 0,75–1,25 m nếu cần chỉnh cửa sổ né. Chốt giới hạn và cách chia Power trước khi chuyển kỹ năng nào sang AoE nhiều mục tiêu. |
| VFX cần xem trước | Lốc Xoáy Trung và bản runtime đều có root khoảng (3,40022, 3,40022, 3,11042). Cue impact thực khoảng (1,29208, 1,29208, 1,18196). Laser release khoảng (0,25, 0,25, 0,25). Bão Lá impact khoảng (0,19, 0,19, 0,19). | Ưu tiên xem Lốc Xoáy có che đối tượng hoặc gợi nhầm AoE không. Kiểm Laser/Bão Lá còn dễ đọc ở tầm 9 m. So Particle Scaling Mode và kích thước child trước khi quyết định thay root. |
| Hướng và release | Phun Lửa, Hơi Thở Rồng và Siêu Súng Nước có release từ socket. Orb projectile phát từ socket rồi executor di chuyển. | So đầu clip với frame release trên Form thấp và cao. Chụp góc bên, kiểm VFX khớp hướng miệng và không chậm hơn hit. Dùng offset XYZ trong frame socket đã ghi để bù lệch. |

### Phạm vi kiểm thử

- **Editor, 7 kiểm tra đạt.** Đọc JSON cũ có trường Level và EXP riêng của Skill, giữ cờ đã học và thứ tự 4 ô. JSON ghi mới bỏ trường cấp Skill. Xác minh Power, MP và hồi chiêu đi từ definition tới dữ liệu HUD.
- **Combat Play Mode, 75 kiểm tra đạt.** Dơi Nhỏ dùng Special projectile và Physical melee gây mất HP thật. Kiểm release, particle đang phát, cooldown và khởi tạo 33 prefab Wild trong scene thử.
- **Companion Play Mode, 39 kiểm tra đạt.** Kiểm tooltip, EXP Pet, học Xung bóng tối ở cấp Pet 12, thu phục cùng cá thể, ghi vào BOX, tải lại, rồi tiến hóa Dơi Nhỏ thành Dơi Ma Cà Rồng. ID kỹ năng đã học và thứ tự trang bị còn đủ và được ghi vào save thử của Form mới.

Bảng ghi kết quả đọc asset toàn bộ danh mục. Kích thước trên màn hình, hướng phát và độ khớp frame của từng Form cần xem trực tiếp từng clip. Các giá trị cân bằng trong mục đề xuất đang chờ duyệt.

### Source đã chỉnh

- Runtime và tương thích Save: `PetHudData.cs`, `PetHudRuntimeStats.cs`, `PetCommandHudProvider.cs`, `PlaceholderPetHudProvider.cs`, `PetSkillDefinition.cs`, `PlayerSaveData.cs` và mẫu authoring `DragonDuskPetSetup.cs`.
- UI: `PetSkillsPanelController.cs`, `PetPartyPanelController.cs`, `SkillPresentation.cs`, `SkillHudTooltip.cs`, `SkillsPanel.uxml`.
- Audit và kiểm thử: `PetSkillAudit.cs`, `CompanionVerification.cs`, `CreatureSkillV2PlayVerification.cs`, `PetSkillLibraryVerification.cs` và `SaveSystemValidation.cs`.

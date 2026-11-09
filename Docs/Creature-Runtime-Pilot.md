# Bộ thử runtime ba dạng rồng — 05/11/2026

**Cập nhật 06/11:** DemoScene đã dùng bộ production riêng ở `Assets/Game/CreatureSystem/Runtime/Dragons/`.
Xem [Dragon-DemoScene-Runtime.md](Dragon-DemoScene-Runtime.md). Các mô tả "chưa đổi catalog/scene"
bên dưới là kết quả lịch sử của đợt Pilot; bản Pilot vẫn độc lập, không phải nguồn production hiện hành.

## Những gì có thể dùng để kiểm

Mở `Assets/Game/CreatureSystem/Pilot/`:

- `Visual/`: ba model dùng chung, giữ mesh/material/controller gốc; không có PetController, health, AI hoặc save. Component CreatureVisualBinding chỉ giữ profile animation, bộ skill và socket.
- `OwnedPet/`: ba root đồng hành, có PetController, stats/metadata và visual. Mặc định thu hồi, không tự đọc P/G.
- `Wild/`: ba root thú hoang, dùng cờ wild và capture adapter hiện có; PetController không chạy điều khiển đồng hành trong lúc còn hoang.
- `Enemy/`: ba root đích nhận sát thương, không có PetController/capture adapter. Có nối Idle/Death và HP theo profile. **Chưa có AI chọn mục tiêu hoặc thi triển skill.** Reward/EV để 0 trong bộ thử, chưa phải cân bằng enemy production.
- `Animation/`: profile từng dạng; code dùng tên hành động chung, profile chỉ rõ state thật.
- `DragonPilotCatalog.asset`: dùng chữ ký PetPrefabCatalog hiện có với 01/02/03. **Catalog production và scene hiện tại chưa được đổi sang bộ thử.**

Các prefab tại `Assets/Prefabs/Trung/Pets/` được giữ nguyên, không đổi tên/move/reparent hay ghi ngược nguồn.
Không duplicate FBX/texture. Ba role của một dạng tham chiếu cùng visual.

## Cách thử thủ công trong scene sao chép

1. Dùng scene và save thử riêng. Không thả cả Pet lẫn Enemy brain lên một source prefab.
2. Thả một `OwnedPet_Dragon01` vào scene; gán Owner/player và nối qua hệ Party/PetCommandInput hiện có nếu kiểm HUD/summon bằng phím.
3. Thả một `Wild_Dragon01` hoặc `Enemy_Dragon02` tùy tình huống. Wild có thể được camera khóa để đưa vào luồng bắt; Enemy không tự trở thành đối tượng bắt được.
4. Nếu kiểm nạp lại bằng bộ mới, chọn DragonPilotCatalog trên SaveController của scene thử. Không thay catalog production trước khi kiểm đủ game flow.
5. Kiểm triệu hồi/thu hồi, đổi dạng và nạp lại, avatar đúng dạng, collider khớp, input trở về. Các bước chưa thực chạy được ghi trong báo cáo task trên E.

## Gắn animation

Mở `Animation/Dragon01.asset` (hoặc 02/03):

- `Actions`: ví dụ Bite → Bite Attack; Move → Move/Fly Forward In Place. State có thể khai báo path đầy đủ Base Layer.X.
- `Skills`: ID `bite` → Bite, `blast` → Blast, `projectile` → Projectile, `wing strike` → WingStrike.
- Không đổi tên clip/controller thư viện hoặc Skill ID đã lưu để đổi chữ hiển thị.
- Thiếu action/state phải báo thiếu; không tự thay Cắn bằng Cast/Attack. Profile không cấu hình Walk/Run cho rồng bay này.

Gameplay pet vẫn gọi `PetHudRuntimeStats.RequestSkill(slotIndex)` để giữ gate, active pet và cooldown.
`CreatureAnimationDriver.TryPlay(action)` chỉ phát hình ảnh, không phải API gây damage hay cấp quyền dùng skill.

## Gắn VFX/âm thanh và socket

Giữ PetSkillDefinition / PetSpeciesSkillSet / PetSkillPresentationProfile đang có.
Đổi model sẽ bind lại profile, skill presentation và socket theo visual mới; không reset bộ trang bị/progress/cooldown.
Mouth gắn xương RigJaw có thật; vị trí/hướng cuối ở đầu mõm vẫn cần artist chỉnh nếu gắn VFX cụ thể.
Body dùng RigSpine và Ground dùng root visual. Không dùng avatar slot làm portrait lớn.

## Công cụ

- `Tools > Capstone > Creatures > 4. Build three-form dragon runtime pilot`: tạo/cập nhật riêng asset Pilot và các reference runtime của Family rồng; giữ chỉnh sửa profile animation đã có. Không ghi nguồn Trung hoặc catalog production.
- `Tools > Capstone > Creatures > 5. Verify isolated dragon runtime pilot`: vào Play với save sao chép trên E, chạy callback runtime rồi thoát và phục hồi cấu hình Enter Play Mode.

Đổi profile/state cần kiểm lại Animator và các timing. Xây lại prefab Pilot có thể ghi đè chỉnh tay trong **Pilot**; chỉnh source art tại nơi authoring phù hợp và giữ backup trước khi rebuild.

Đợt sau: profile/mapping cho loài khác, hoàn thiện root/skill executor của enemy trước khi thêm AI; chưa tự mở rộng ra 33 dạng.

## Kết quả thực chạy 05/11/2026

Đã tạo đủ **3 visual + 9 root Pet/Wild/Enemy**, 3 animation profile và catalog pilot.
Unity 6000.3.16f1 đã chạy **64 kiểm tra Play Mode**, gồm triệu hồi/thu hồi,
gọi Bite/cooldown, thiếu mapping không tiêu cooldown, tiến hóa thật 01→02→03,
avatar/bộ skill/socket theo model, giữ genetics/tính khí, khôi phục snapshot
metadata/stats qua catalog dạng 03 mà không tạo hai Animator, HP đầy theo công
thức đang có và callback Death của Enemy. Đây là callback/API runtime, chưa
phải kiểm bằng bàn phím/chuột trên toàn bộ game flow.

Hai ảnh camera runtime 1920×1080 và 1366×768 chỉ chứng minh ba visual render
trong engine; không phải ảnh QA toàn HUD hoặc chứng nhận chất lượng animation/VFX.
Không chạy standalone, Profiler, capture end-to-end, cinematic tiến hóa hay
vòng save/load qua UI thật trong lượt này. Không đổi scene/catalog production.

Đã sửa hai lỗi phát hiện khi chạy: factory collider dùng kiểm null của Unity
thay `??`; HP authoring dùng tỷ lệ đầy 1/1 để SyncRuntimeHealth tính ra HP
thật, vì 0/0 được hệ hiện có hiểu là 0 HP. Không đổi công thức stat.

Báo cáo và danh sách file:
`E:/UnityWork/CapstoneProjectData/Artifacts/creature-runtime-pilot-2026-11-05/REPORT.md`.
Lệnh chạy tự động tạm thời đã gỡ; hai menu 4/5 chỉ chạy khi được chủ động chọn.

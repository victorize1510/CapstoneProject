# PvE, HUB và phần thưởng — 12/11/2026

Triển khai tiếp sau `Pve-Model-Camera-Feedback-2026-11-11.md`. Cảnh tích hợp chính là **DemoScene**; các component dùng lại ở scene khác khi có đủ provider, owner, NavMesh và save controller.

## Điều khiển

| Thao tác | Kết quả |
|---|---|
| Click trái vào địch | Chọn địch vừa click và ra lệnh đánh, không cần aim. Mục tiêu khóa cũ không giành lệnh click. |
| Z/X/C/V | Dùng skill thực đang trang bị trong ô tương ứng. |
| Backspace | Hủy phần cast chưa phát, gọi pet chạy về cạnh player; vẫn được triệu hồi. |
| Backspace hai lần ≤0,3 giây | Recall bằng director hiện có. |
| 1–6 | Hiện radial 1,2 giây và yêu cầu summon/switch. Avatar chính chỉ đổi sau xác nhận provider. |
| P | Giữ Toggle/Hold hiện có; thả sau Hold sẽ đóng. |
| G tap | Một ý định ném bóng đang chọn. Service kiểm mục tiêu, bóng, chỗ nhận và lưu trước trình diễn. |
| G hold ≥0,28 giây | Mở ba ô Thường/Rare/Ultra. 1–3 chọn bóng, không đổi pet. Thả G đóng và không ném. |
| F8 | Đổi model nam/nữ. Chỉ binding G cũ được chuyển; phím tùy chỉnh khác được giữ. |

Rare/Ultra có model và icon nhưng **chưa có item/gameplay bắt**. Hai ô vẫn hiện, bị khóa và giải thích lý do; không tự cấp bóng hoặc bịa hệ số.

## Mana, animation và skill

`PetSpeciesSkillSet.startingMana = 100` cho ba dạng rồng. Save có MaxEnergy hợp lệ được giữ; dữ liệu chưa khởi tạo mana được bổ sung. MP không phụ thuộc cấp/ATK và không thay EXP. HUB có thanh EXP riêng.

| Skill mặc định | MP | Sức mạnh trước phòng thủ |
|---|---:|---|
| Cắn | 8 | Giữ 120% ATK và bleed đã có |
| Gầm cường hóa | 15 | +20% ATK/SP.ATK, 6 giây; refresh, không stack |
| Đạn năng lượng | 12 | Một đạn, 160% SP.ATK |
| Chém gió | 18 | 2/3/4/5 đạn theo 10/40/40/10%; **tổng 180% ATK**, chia đều mỗi đạn |

Chi phí và cooldown chỉ ghi một lần khi cast được nhận, không khi nằm trong buffer. Một cast gió chụp buff/crit một lần; mỗi đạn có `EmissionIndex`, va chạm và mất phần damage riêng nếu hụt/va tường. Mốc phát lấy normalized animation, không có timer damage song song.

Controller runtime riêng giữ nguyên tên clip nguồn. Variant `WingVolley_Dragon01/02/03.anim` dài khoảng 1,05 giây; các lần phát trong normalized 0,30–0,78. State `Downed` và clip không có AnimationEvent, tách khỏi sự kiện chết. Không sửa FBX/controller nguồn của Trung hoặc BoyBasic.

Mở Inspector của `Skill_*` → `manaCost`, `combat`, `presentation`; learnset và override theo dạng ở `Data/Species/Dragon01/02/03.asset`. Muốn thêm loài dùng cùng skill, cấu hình learnset/animation profile của loài đó; không nhân bản logic damage trong VFX.

## AI, hồi phục và gục

- `BasicCreatureEnemy`: Idle/Patrol → Chase/Attack → Return → Recover. Mặc định phát hiện 10 m, leash 18 m; `territory` tùy chọn. Thiếu đường NavMesh hoàn chỉnh sẽ dừng an toàn.
- DemoScene có NavMesh riêng `Runtime/DemoNavigation/NavMesh_DemoPvE.asset`; không chỉnh NavMesh TestChar. Bake bounded quanh player 80×16×80. Scene khác phải có surface/data phù hợp.
- Gọi pet về không xóa target/aggro của quái. Quái đang Return không bắt lại target ngoài vùng.
- Pet sống và đang triệu hồi: sau 8 giây không bị truy đuổi/gây/nhận damage, hồi thật 5% MaxHP/s và 10 MP/s. Quái chỉ hồi 10% MaxHP/s khi đã về nhà và an toàn 8 giây. Pet gục/đang thu không hồi sinh tự động.
- Wild cho phép bắt, không phải boss/dummy: HP 0 chuyển Gục 5 giây, vẫn chọn được nhưng không nhận damage. Chưa phát Defeated/EXP/EV/loot.
- Ném trước hạn giữ cửa sổ cho trình diễn. Thất bại không gia hạn 5 giây mới; hết hạn chốt chết một lần. Thành công giữ đúng cá thể/0 HP và không có thưởng hạ quái. Phải hồi phục trước summon.

## EXP và loot

`PveBalanceRules` chỉ điều chỉnh thưởng trận, không đổi công thức stat/EXP lên cấp. Đóng góp ghi theo petId, cấp tại lúc gây đòn và HP thực mất. EXP chia theo đóng góp; pet cao hơn ≥10 cấp nhận 0. EV giữ nguồn pet kết liễu hiện có.

`Resources/Pve/LootCatalog.asset` giữ tham chiếu item thật: vàng 2–5, 35% một item, trong đó Potion/Apple/Mana 40/40/20. Chỉ áp dụng quái thường; boss/dummy không dùng bảng này.

`PveRewardService` roll một lần theo encounterId → lưu journal → chốt EXP/EV → dựng glow chưa lộ item. Trong 2,5 m, glow bay cong 0,35 giây; sau 20 giây tự nhận. Quá xa/khác scene dùng hiệu ứng nhận ngắn cạnh player. Chỉ hiện toast icon/tên/×số lượng sau khi thêm inventory/wallet và lưu thành công. Lỗi nhận hoặc save giữ pending và rollback; không đoán receipt từ biến động inventory.

Save **v14** thêm section tùy chọn `pveRewards`, đọc v13/cũ và backup theo pipeline hiện có. Không ghi RNG/xác suất. Không mở save đã nâng lên v14 bằng client cũ. Nếu ổ đĩa không ghi được, journal mới chỉ giữ trong RAM cho tới khi retry thành công; không thể bảo đảm bền vững qua mất điện/crash trong thời gian đĩa lỗi.

## Kiểm và dữ liệu ngoài Git

- `Tools → Capstone → PvE`: Apply asset; bake Demo navigation; verify input/mana/volley; verify lifecycle/rewards/navigation.
- Các lượt kiểm dùng save riêng trước gameplay Start, không ghi save thật. Fixture chỉ tồn tại trong Play Mode.
- Báo cáo kiểm cuối, số đo, ảnh thực và giới hạn ở `E:/UnityWork/CapstoneProjectData/Artifacts/pve-combat-hub-2026-11-11/`.
- Số kiểm đã chạy và các giới hạn cụ thể nằm trong `handoff-report.md`; không coi ảnh preview/model render là Game view, không coi callback engine là thao tác phím vật lý.

Không commit/push. Bàn hồi phục chỉ là model/collider đã đặt cạnh Shop; chưa triển khai chức năng hồi phục tại bàn.

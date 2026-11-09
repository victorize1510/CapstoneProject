# Dơi, enemy đánh thường và skill runtime

Phạm vi: ba dạng Dơi, combat dùng chung với ba dạng Rồng hiện có, trình diễn skill và ưu tiên marker người chơi. Không tạo AI NPC, không sửa schema save hoặc prefab nguồn của Trung.

## Asset và cách dùng

`Capstone/Assets/Game/CreatureSystem/Runtime/Bats/` chứa:

- `Visual/`: model + rig + Animator + socket, không chứa logic sở hữu hoặc AI.
- `OwnedPet/`: pet có controller, metadata, tiến hóa, bộ kỹ năng và nhận sát thương.
- `Wild/`: cá thể hoang có adapter của hệ bắt hiện có, chưa có AI tấn công.
- `Enemy/`: đối thủ có HP, collider, nhận sát thương và AI đánh thường; không có adapter bắt hoặc controller pet sở hữu.
- `Animation/`: profile hành động và controller runtime tham chiếu clip gốc. Không đổi tên clip/controller nguồn.
- `Skills/`, `Combat/`, `Effects/`: dữ liệu kỹ năng và trình diễn, không phải trạng thái của từng cá thể.

Trong Unity, kéo `Enemy/Enemy_Bat01.prefab` vào scene, đặt chân trên mặt đất có collider. Hai dạng còn lại dùng tương tự. `BasicCreatureEnemy` có Aggro Radius, Leash Radius, Attack Range, Move Speed, Attack Interval, Hit Delay và Attack Duration để chỉnh trong Inspector.

Enemy tìm **pet đang được triệu hồi của party hiện tại**, không tự tạo máu trainer. Chưa gọi pet thì enemy đứng yên. Khi mất mục tiêu hoặc vượt phạm vi, enemy quay về. Ưu tiên NavMeshAgent; nếu thiếu NavMesh, có di chuyển trực tiếp kiểm capsule/vật cản và mặt đất. Cách dự phòng này dừng trước chướng ngại, không tự tìm đường vòng; cần bake NavMesh cho địa hình phức tạp. Enemy mới có EXP thưởng 30 và EV tốc độ 1 trong DummyEnemy; có thể chỉnh Inspector. Không thêm loot, respawn, NPC hoặc bộ skill riêng cho enemy.

DemoScene có nhóm `CreatureEncounters_Bat`, gồm một enemy và một dơi hoang, cùng catalog sáu dạng `Runtime/CreatureRuntimeCatalog.asset`. Không cấp dơi sở hữu, bóng hoặc item vào save thật. Các starter rồng và tùy chọn save sẵn có được giữ.

Để thử: gọi pet bằng luồng HUB hiện có, đến gần enemy để nó phát hiện pet; khóa mục tiêu bằng camera/lệnh tấn công rồi dùng các phím skill Z/X/C/V đang có. Skill thiếu mục tiêu, ngoài tầm hoặc bị vật cản sẽ không chạy và không tiêu cooldown. Enemy chỉ nhắm pet đang hiện diện; pet hết HP tự thu hồi và phải hồi phục trước khi gọi lại.

Khi mang Dơi sang scene khác, dùng prefab runtime theo vai trò ở trên và gán `CreatureRuntimeCatalog.asset` cho trường Pet Prefab Catalog của PlayerSaveController tại scene đó. Đợt này chỉ thay tham chiếu scene DemoScene; không tự sửa các scene khác. Nếu dùng catalog riêng, cần có đủ mapping `bat/01`, `bat/02`, `bat/03` để khôi phục cá thể bị thiếu khi load.

## Thông số Dơi

| Dạng | HP | ATK | SP.ATK | DEF | SP.DEF | SPD | BST | Tiến hóa |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| 01 – Dơi Nhỏ |45|55|35|35|40|90|300|Cấp 18 → 02|
| 02 – Dơi Ma Cà Rồng |60|80|50|50|55|115|410|Cấp 36 → 03|
| 03 – Dơi Chúa Tể |75|105|65|65|70|140|520|Dạng cuối|

Đây là chỉ số gốc trong `Assets/Resources/PetStats/Bat01/02/03.asset`, không phải chỉ số cố định ở mọi cấp. Giữ nguyên công thức cấp/IV/EV/tính khí. Hệ Bóng tối/Bay; CRIT gốc 6,25%, CRIT DMG 50%. BaseCatchRate nội bộ 210/100/45; không hiển thị xác suất ra UI. Prefab mẫu để trống ID cá thể và thứ tự nhận thú (`obtainedOrder=0`); mỗi instance được cấp danh tính riêng, không sao chép thứ tự 1 từ template.

Các root mới bắt đầu cấp 5; đó là giá trị authoring của prefab, không sửa cá thể đã lưu. Tiến hóa dùng chung cá thể, giữ skill ID/genetics; avatar/model/profile/stat chuyển theo dạng.

`CreatureCombatant` khớp lại capsule và kích thước NavMeshAgent một lần khi bind model mới, bao gồm khởi tạo, load và tiến hóa. Không tính lại theo từng pose/frame. Collider được giữ phía trên mặt đất để dạng lớn không kẹt trong nền khi dùng di chuyển dự phòng. Root runtime nên giữ scale mặc định; chỉnh kích thước nghệ thuật ở visual/model thay vì kéo méo root gameplay.

## Skill, animation, VFX và âm thanh

Danh sách chung vẫn là `Assets/Game/Skills/Resources/PetSkills/PetSkillCatalog.asset`.

1. `PetSkillDefinition`: ID ổn định, tên, ảnh, hệ, cooldown và tham chiếu Combat/Presentation.
2. `PetSkillCombatProfile`: Physical/Magical, Melee/Projectile/Targeted, power, tầm, tốc độ/thời hạn đạn và visual đạn. Không lưu cooldown/cá thể vào asset này.
3. `PetSpeciesSkillSet`: loài/dạng được học skill nào, cấp học và loadout khởi đầu. `presentationOverride` cho phép cùng skill dùng VFX/SFX khác theo loài. `overrideAnimation` cho phép timing khác.
4. `CreatureAnimationProfile`: ánh xạ tên hành động chung Bite/Cast/Projectile… sang state thật của từng model. Khi có profile, mapping tại đây là nguồn state chính.
5. `CreatureVisualBinding`: animation profile, skill set và socket Mouth/Body/Ground trên model. Khi đổi model, hệ hiện có rebind những tham chiếu này.

Ba skill mới: `bite.normal` (Cắn, cấp 1, hồi 1,6 s), `orb.dark` (Cầu bóng tối, cấp 1, hồi 3,6 s), `pulse.dark` (Xung bóng tối, cấp 12, hồi 5 s). Pet mới chỉ trang bị hai skill đầu; hai ô còn lại trống. Tiến hóa không tự lấp hoặc ghi đè các ô đã lưu.

Giữ ID, hệ và cooldown của bốn skill rồng cũ; thêm combat/presentation. Đòn thường của pet giữ `attackDamage + ATK` trong controller cũ; enemy mới dùng ATK trước CRIT/DEF. Skill Physical dùng ATK, Magical dùng SP.ATK. Mục tiêu có `CreatureCombatant` giảm sát thương theo `100/(100+DEF)` hoặc `100/(100+SP.DEF)`. Cube cũ không có adapter giữ nhận sát thương thô như trước. SPD điều chỉnh di chuyển trong 0,75–1,35 lần; nhịp đánh thường trong 0,8–1,25 lần; không tua clip hoặc rút cooldown skill theo SPD.

`PetSkillPresentationPlayer` phát Cast → Release → Impact theo token. `PetSkillCombatExecutor` chốt mục tiêu khi cast được chấp nhận, kiểm tầm/vật cản, gây hit một lần; projectile dùng sphere cast thật. Thu pet/đóng actor dừng đạn và hiệu ứng. VFX/audio có thời hạn và pool giới hạn; skill không tính damage lần nữa trong animation event.

Asset trình diễn hiện dùng particle tím từ Eric VFX Studio và clip Hand1/Ray2/Explosion1 trong bộ KriptoFX đã có trong project. Đây là bộ âm thanh tạm để kiểm luồng; có thể thay tại Presentation mà không đổi skill ID hay save. Không tạo portrait pet mới.

## Dựng lại asset và bảo toàn dữ liệu

Menu `Tools → Capstone → Creatures → 9. Build Bat family and basic combat` dựng lại asset **đã sinh** của đợt này. Dùng sau khi chủ động sao lưu thay đổi authoring: lệnh này áp lại cấu hình mặc định Bat/combat/presentation. Không chạy để sửa một cá thể của người chơi. Không ghi scene hoặc save thật từ lệnh dựng asset.

Catalog mới có ba dạng rồng và ba dạng dơi. Công cụ tích hợp rồng số 7 đã được sửa để không thay catalog mở rộng hợp lệ bằng catalog chỉ có rồng.

## Kiểm chứng

Kết quả và giới hạn thực chạy lưu tại `E:/UnityWork/CapstoneProjectData/Artifacts/bat-combat-enemy-2026-11-06/REPORT.md`. Đã qua 73 kiểm tra chính và 23 kiểm tra bổ sung trong Unity thật bằng save tách biệt: ba skill dơi, enemy đánh/đuổi/quay về, va chạm khi thiếu NavMesh, pet hết HP, bắt và tiến hóa qua service, danh tính độc lập, save/load, collider đổi dạng và Map raycast ở hai độ phân giải. Có ảnh render/capture và ProfilerRecorder trong Editor. Đây không phải xác nhận standalone, chuột thật hoặc kiểm toàn bộ bốn skill rồng ở mọi dạng.

Mã QA tự chạy đã gỡ khỏi project sau kiểm chứng; source tái hiện nằm trên E. Không để request hay recorder chạy ngầm.

UI trình bày để làm sau theo yêu cầu của người dùng. Không cần triển khai UI mới để sử dụng bộ prefab/skill này.

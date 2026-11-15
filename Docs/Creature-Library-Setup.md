# Thư viện dòng thú và dạng — đợt nền tảng 05/11/2026

## Mở và gắn avatar

Trong Project mở `Assets/Game/CreatureSystem/Data/CreatureLibrary.asset`.
Danh sách có 11 Family. Mở một Family trong `Data/Families/`, mở `Forms`:

- `Form Id`: 01 / 02 / 03; không đổi ID đã được dùng trong save.
- `Display Name`: tên dạng; tên asset/hiển thị không phải ID cá thể.
- `Source Prefab`: prefab Trung đã đưa vào project, giữ nguyên GUID và nội dung.
- **`Avatar`: gắn ảnh nhỏ đúng dạng ở đây.** Không gắn vào Family hoặc chỉ vào dạng 01.
- `Hero Portrait`: ảnh lớn 2D độc lập, để trống cho đến khi người làm art thêm.
- `Stat Profile` và `Skill Set`: tham chiếu cấu hình hiện có; chưa có thì để trống và đọc báo cáo, không sao chép số/bốn skill của rồng cho tất cả thú.

Sau khi sửa Avatar, nhấn **Cập nhật avatar runtime từ thư viện** trong Inspector hoặc
`Tools > Capstone > Creatures > 2. Bake lightweight avatar catalog`.
Giữ nguyên ID `Dragon Dusk` và các alias cũ để save đã tiến hóa nhận đúng ảnh.

## Runtime và hiệu năng

### Ba lớp prefab

- `Assets/Prefabs/Trung/Pets/`: nguồn gốc, giữ nguyên. Không đổi trực tiếp chỉ để làm biến thể pet/enemy.
- Visual dùng chung: mesh/rig/Animator/material/socket của đúng dạng; không chứa PetController hoặc Enemy AI.
- Root Pet và root Enemy/Wild: dùng visual chung nhưng có điều khiển và dữ liệu cá thể riêng.

Đây là hướng phân lớp cho đợt sau, chưa sinh root/variant hàng loạt trong đợt nền tảng.
Không gắn thêm Enemy AI lên prefab nguồn vốn đã có PetController rồi để cả hai chạy.
Không duplicate FBX/texture cho từng vai trò. Prefab Variant chỉ phù hợp khi base đã tách visual sạch;
không tự giải quyết xung đột script chỉ bằng cách đặt tên thư mục Pet/Enemy.

`Resources/Creatures/CreatureAvatarCatalog.asset` là bản chiếu sinh từ Family:
chỉ chứa khóa và Sprite, không tham chiếu Family, model, prefab, profile stat hay bộ skill.
HUD không phải nạp tất cả model chỉ để hiện avatar. Không sửa bản chiếu bằng tay.

`PetHudRuntimeStats.Icon` và preview tiến hóa đọc ảnh theo `DefinitionId + CurrentFormId`.
Khóa loài chưa thuộc danh mục giữ ảnh cũ để tương thích. Một mục đã cấu hình nhưng thiếu
Avatar trả ảnh rỗng để UI hiện trạng thái thiếu ảnh; không mượn ảnh dạng 01.

## Đợt này có và chưa có

- Có 11 Family / 33 dạng, ảnh có sẵn của Trung và tham chiếu prefab nguồn.
- Ba dạng rồng dùng lại profile stat / skill set hiện có và có avatar riêng.
- 10 Family còn lại có ID authoring mới nhưng **chưa ghi ID vào prefab/scene/save**.
- `Evolution Rules Reviewed` chỉ là cờ rà dữ liệu; không kích hoạt hoặc thay điều kiện tiến hóa.
- `Source Prefab` KHÔNG phải hợp đồng model-only hoặc prefab spawn: 11 prefab nguồn có script pet,
  22 prefab nguồn còn lại chủ yếu là model/Animator. Không đem toàn bộ source này vào trường Target Visual Prefab một cách tự động.
- Chưa tạo enemy AI, bảng ánh xạ animation, bộ học/cân bằng chỉ số mới, điều kiện tiến hóa mới hoặc portrait.
- Chưa tự cấp thú/vật phẩm, không sửa catalog giá, save schema, scene hoặc nội dung prefab Trung.

## Công cụ

`Tools > Capstone > Creatures`:

1. **Create missing family assets**: tạo các Family còn thiếu; không ghi đè Family đã sửa bằng tay.
2. **Bake lightweight avatar catalog**: cập nhật ảnh runtime, kiểm khóa/alias trùng.
3. **Validate library and write report on E**: kiểm nguồn/ảnh/Animator, tính nhất quán,
   nguồn thiếu stat/skill/rule; lưu JSON ngoài Git.

Báo cáo: `E:/UnityWork/CapstoneProjectData/Artifacts/creature-library-foundation-2026-11-05/validation.json`.
Các cảnh báo còn thiếu không đồng nghĩa dữ liệu đã sẵn sàng spawn hoặc chiến đấu.

## Đợt tiếp theo

Hoàn thiện root gameplay và binding ID từng dạng; bảng hành động Idle/Move/Bite/... theo
Animator thật; bộ skill đúng loài. Thiếu động tác phải báo thiếu, không tự thay bằng Cast/Attack.
Sau đó mới dùng chung phần thực thi skill cho input pet và AI enemy, giữ tiến trình mỗi cá thể.

### Cập nhật sau đợt nền tảng

Bộ thử runtime cho riêng 3 dạng rồng đã được tạo và kiểm trong Play Mode.
Xem [Creature-Runtime-Pilot.md](Creature-Runtime-Pilot.md) để biết các thư mục
Visual/OwnedPet/Wild/Enemy, cách gắn animation/skill/socket và giới hạn đã kiểm.
Các cảnh báo dữ liệu của 10 Family còn lại vẫn cần xử lý trước khi mở rộng.

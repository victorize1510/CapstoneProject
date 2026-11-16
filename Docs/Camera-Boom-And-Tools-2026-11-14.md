# Camera và Toolbox — 14/11/2026

Cập nhật tiếp ngày 15/11: phần thu vào có dự đoán và làm mượt riêng trong [Camera-Smooth-Entry-2026-11-15.md](Camera-Smooth-Entry-2026-11-15.md). Các số đo bên dưới thuộc bản giữ hướng ban đầu, không thay cho kiểm chuyển động mới.

Bổ sung sau phản hồi ảnh sát shop: đã thêm xử lý model ở khoảng cách gần trong [Camera-Near-Player-2026-11-14.md](Camera-Near-Player-2026-11-14.md), compile thành công và **đạt 34/34 kiểm trong DemoScene** sau khi người dùng báo Editor sẵn sàng. Các số đo bên dưới chỉ thuộc đợt sửa boom trước đó.

## Đã thay đổi

Camera giữ hướng người chơi đã chọn khi gặp vật cản. Nó thu khoảng cách về gần pivot, sau đó trả khoảng cách mượt khi đường nhìn thông. Không tìm một vai khác, không nhảy sang vị trí thế giới đã lưu trước đó. Free orbit, khóa mục tiêu, zoom và input hiện có vẫn đi qua rig hiện tại.

Lỗi cũ nằm trong `BasicCameraFollow.TryClearShoulder`: khi camera quá gần nhân vật, code thử các góc 45/90/135/180 độ để tìm chỗ trống. Fixture trong DemoScene tái hiện góc lệch **99,421°**. Sau sửa, cùng fixture cho **0°** ở cả 1920×1080 và 1366×768.

## Cấu trúc code

| File trong Assets/Scripts/Thắng | Vai trò |
|---|---|
| CameraBoomCollision.cs | Solver sphere sweep/overlap không cấp phát mỗi query; chỉ đổi chiều dài boom. CameraBoomRecovery giữ trạng thái hồi khoảng cách riêng cho mỗi camera. |
| BasicCameraFollow.cs | Cấu hình, bộ lọc collider và API chung cho rig/legacy. Không còn tìm vai hoặc fallback world position. |
| PlayerCameraCollision.cs | Extension của từng camera Cinemachine; xử lý Body, kiểm lại Finalize khi có dịch chuyển bổ sung. |
| PlayerFreeLookRig.cs | Giữ một Brain ManualUpdate; kiểm collision thêm trên vị trí render cuối sau blend/impulse. |

Collider của player đang điều khiển, PetController, DummyEnemy và trigger không đẩy camera. Tường/địa hình vẫn chặn theo mask. Collider đánh nhau, projectile và di chuyển không bị thay đổi. Radius bảo vệ cả near plane theo lens/aspect thực tế.

`cameraCollisionMinDistance` chỉ còn giữ để tương thích dữ liệu/API cũ, ẩn trong Inspector. Không ép khoảng cách tối thiểu khiến camera xuyên tường. Code collision cũ trong `CombatVisualsEngineChecks` đã đổi kỳ vọng sang cùng hướng boom.

## Chỉnh trong Unity

1. Mở **Tools → Capstone → Camera → Chọn camera hiện tại**.
2. Trong BasicCameraFollow, giữ **Enable Camera Collision** bật.
3. **Camera Collision Mask:** chỉ các lớp môi trường cần chắn camera; trigger luôn bỏ qua.
4. **Camera Collision Radius:** mặc định 0,28 m, có bảo vệ near-plane lớn hơn khi cần. **Buffer:** mặc định 0,16 m; không tăng quá nhiều ở lối hẹp.
5. **Camera Collision Return Time:** mặc định **0,24 giây**, là thời gian làm mượt phục hồi, không phải cam kết đến đích đúng sau 0,24 giây. Gặp vật cản mới thu vào ngay; giữ ngắn 0,06 giây rồi trả ra. Nếu người chơi tắt camera smoothing trong Settings thì tôn trọng lựa chọn đó.

Không thêm Deoccluder/Decollider thứ hai lên cùng rig để tránh hai solver giành vị trí. Không cần chạy lại setup TestChar hoặc lưu lại DemoScene để dùng bản sửa.

Nếu pivot bị đặt **bên trong vật thể đặc**, không có nghiệm vừa giữ đúng hướng vừa nằm ngoài vật thể. Solver báo `HasClearPosition=false`, giữ tại pivot thay vì dịch ngang/xuyên qua tường. Cần sửa collider/pivot/khả năng đi xuyên của nhân vật nếu gặp tình huống này. Tại tường sát lưng, khung hình có thể rất gần nhân vật; đợt này không thêm tính năng làm mờ cơ thể.

## Tools gọn hơn

- **Tools → Capstone → Toolbox**: xem scene/backend/collision, chọn camera; mở **Nâng cao** khi cần tìm lệnh.
- Hai lệnh Camera thường dùng: **Chọn camera hiện tại**, **Kiểm cấu hình**.
- Lệnh setup/QA cũ của các hệ Capstone được chuyển vào **Tools → Capstone → Advanced**; callback và shortcut giữ nguyên. Toolbox có tìm kiếm và nhóm Camera, Pet/Combat, UI, Map, Save/Inventory.
- Không xóa tools thư viện hay tools riêng ngoài phạm vi Capstone đã kiểm kê. Không chạy setup phá/làm lại scene để thử danh sách.
- Đường dẫn menu cũ trong tài liệu cũ cần tìm qua Toolbox/Advanced. Bảng chuyển đường dẫn: `tools-menu-map.csv` trong thư mục QA bên dưới.

Danh sách được cache khi mở/làm mới; không reflection toàn bộ assemblies mỗi lần vẽ. Không còn cầu nối QA tự poll file/chạy Play nền sau bàn giao.

## Kiểm thực tế và giới hạn

DemoScene, save thử trên E; fixture chỉ tồn tại trong Play Mode, không lưu scene. **29 kiểm tra đạt**: tường, nhiều hướng orbit, gần góc/pivot bị chôn, trigger/player/enemy bị bỏ qua, lens/aspect hai độ phân giải, free/lock, thu vào, trả ra và target warp/UI pause. Đây là camera được điều khiển bởi bộ kiểm trong engine; không phải một lượt chơi hết bản đồ bằng chuột/gamepad.

Lượt cuối: hồi ra tối đa **0,177 m giữa hai frame render**, quan sát 194 frame. Chỉ áp dụng lượt fixture này; không phải giới hạn tốc độ toàn game. 1.000 probe đã warm, **1,7075 ms tổng / 0 B managed allocation** trong Editor. Đây là microbenchmark solver, không phải FPS hoặc số đo Profiler toàn scene. Main và Editor assembly compile thành công; warnings sẵn có của project/library vẫn còn.

Chưa kiểm standalone, gamepad, chơi hết mọi scene, vật cản di chuyển nhanh, mọi tổ hợp khóa mục tiêu/địa hình. Không tuyên bố camera giống chính xác Pokémon Violet/Z-A.

Chạy lại chủ động khi DemoScene đang mở và không Play:

- **Tools → Capstone → Advanced → Camera → Verify isolated camera regression**.
- **Audit Toolbox menus** kiểm đăng ký và mở Toolbox, không vào Play.

Ảnh trước–sau, kết quả và danh sách file sửa:

`E:/UnityWork/CapstoneProjectData/Artifacts/camera-boom-tools-2026-11-14/`

## Căn cứ chọn phương án

[Unity Cinemachine Deoccluder](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineDeoccluder.html) mô tả Pull Camera Forward giữ trục camera và thu về target. Đây là hành vi phù hợp lỗi hiện tại. Giữ rig đang có, dùng một solver chung thay vì thay toàn bộ framing bằng [Third Person Follow](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineThirdPersonFollow.html).

[Gameplay chính thức Pokémon Z-A](https://legends.pokemon.com/en-gb/gameplay) nhấn mạnh di chuyển/giao chiến realtime; [cập nhật Scarlet/Violet của Nintendo](https://en-americas-support.nintendo.com/app/answers/detail/a_id/60277/~/how-to-update-pokmon-scarlet-and-pokmon-violet) có thay đổi tùy chọn camera. Những nguồn này không công bố thuật toán collision của game. Lấy chúng làm định hướng khả năng quan sát; lựa chọn kỹ thuật ở đây là của project và được kiểm riêng trong Unity.

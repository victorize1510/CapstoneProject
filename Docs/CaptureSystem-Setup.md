# Bắt thú và chỉ số người chơi

## Luồng sử dụng

1. Khóa mục tiêu thú hoang bằng hệ khóa camera hiện có.
2. Mở Túi đồ, chọn **Dụng cụ bắt → Bóng thu phục cơ bản**.
3. Bấm **BẮT THÚ**. Nút bị khóa và có lý do khi thiếu mục tiêu, cấu hình, bóng hoặc chỗ trống.
4. Lần ném hợp lệ tiêu hao một bóng, kể cả khi thú thoát. Đội còn chỗ được ưu tiên; đủ sáu ô thì gửi vào BOX.

Code không tự cấp bóng, thêm vào Shop/loot hoặc đặt thú hoang lên bản đồ. Dùng asset item này trong nguồn cấp vật phẩm do nhóm thiết kế chọn sau:

`Capstone/Assets/Resources/Capture/BasicCaptureBall.asset`

Icon 128×128 được render từ model bóng xanh hiện có. Đây là asset runtime, không phải screenshot QA và không nên xóa cùng frame kiểm thử.

## Inspector

- `Assets/Resources/PetStats/DragonDusk*.asset`: **Capture → Is Catchable / Base Catch Rate**. Ba dạng 01/02/03 lần lượt 255/120/45. Không chỉnh các trường chiến đấu để thay độ khó bắt.
- `Assets/Resources/Capture/BasicCaptureBall.asset`: item bắt thật, hệ số 1, không bảo đảm, không phụ thuộc lượt. Stable ID `capture-ball-basic-01`.
- `Assets/Resources/Capture/CaptureSettings.asset`: model trình diễn và audio tùy chọn. Critical Capture tắt; gameplay chưa có adapter số loài đã bắt đáng tin cậy cho chế độ này.
- `Assets/Game/CaptureSystem/Prefabs/WildDragon01.prefab` đến `WildDragon03.prefab`: prefab hoang riêng từ ba prefab pet gốc. Có PetController ở chế độ Wild Encounter, WildPetCaptureTarget, DummyEnemy và metadata/stats hiện có. Không dùng prefab hoang làm template pet sở hữu trong catalog save.
- `WildPetCaptureTarget.Encounter Capture Allowed`: khóa bắt theo encounter. Boss hoặc loài Is Catchable tắt luôn bị từ chối, kể cả bóng bảo đảm.

Các prefab này giữ nguyên model/loài/dạng/kỹ năng gốc; không bổ sung AI di chuyển hoặc hệ spawn mới. Khi cần, designer đặt prefab vào vùng encounter đang dùng, chỉnh collider/layer phù hợp và tiếp tục dùng damage/khóa mục tiêu hiện có.

Pet sở hữu không bật Wild Encounter. Enemy thường không tự bắt được chỉ vì có collider hoặc đang bị khóa. Thiếu cấu hình bắt không dùng Catch Rate dự phòng.

## Chỉ số và dữ liệu

- Giữ công thức HP, ATK, SP.ATK, DEF, SP.DEF, SPD, CRIT RATE và CRIT DMG hiện tại.
- IV/EV/tính khí/tổng chỉ số gốc ở **Thông tin thú → Chi tiết nâng cao**, đóng mặc định. Mở lại popup sẽ thu gọn.
- Mục tiêu hoang đang khóa hiển thị HP thật và cấp; UI không nhận bất kỳ xác suất/roll bắt nào.
- Rarity, cấp, dạng tiến hóa và spawn weight không tự nhân vào công thức bắt.
- IV/tính khí khởi tạo một lần khi cá thể hoang xuất hiện. Bắt chỉ chuyển quyền sở hữu của chính cá thể đó, không roll lại.
- Khi có hệ major status thật, nối mapping rõ ràng ở adapter; hiện dùng None. Không lấy slow/buff tùy ý làm trạng thái bắt.
- Calculator hỗ trợ Quick/Timer để mở rộng sau, nhưng gameplay realtime từ chối bóng cần lượt khi chưa có nguồn lượt thật.

## Save và API

Save v13 bổ sung `customization.captureBallId`; save v12 trở xuống đọc với giá trị trống, giữ archive/backup theo save store hiện có. Không lưu xác suất hoặc RNG.

`PetCaptureCoordinator.TryCaptureFromBag` dùng mục tiêu camera đang khóa. `CanCaptureFromBag` chỉ kiểm khả dụng, không roll hoặc tiêu hao. `CaptureCommitted` phát sau khi save thành công. Các lệnh/callback hoạt ảnh không được gọi lại TryCapture để quyết định kết quả.

`TryCommitCapture` cũ còn được giữ cho luồng nhận pet/pending tương thích; luồng bóng mới phải đi qua TryCaptureFromBag để kiểm item, encounter và giao dịch.

Kết quả trình diễn chỉ chứa thành công, critical, số rung, thông báo và cá thể nhận được. Hủy hoạt ảnh không hoàn tác hoặc lặp giao dịch đã lưu.

## Kiểm thử

Các lệnh chạy chủ động trong Unity:

- Tools → Capstone → Capture → Verify capture math.
- Tools → Capstone → Capture → Verify isolated capture runtime.
- Tools → Capstone → Capture → Verify release player scripts (chỉ compile script, không chạy exe).

Không còn hook tự polling để bắt đầu QA. HUD G cũng gọi `TryCaptureFromBag`; chọn bóng không gọi giao dịch. Xem thêm `HUB-R11-Setup.md` về input và cờ cơ hội bắt.

Source kiểm thử nằm trong CaptureVerification.cs và bị loại khỏi player bằng `#if UNITY_EDITOR`. Dữ liệu kiểm nằm ngoài Git dưới:

`E:/UnityWork/CapstoneProjectData/Artifacts/capture-stats-visibility-2026-11-04/`

Save kiểm được chuyển sang thư mục riêng trước Start; không dùng save thật để thử thiếu vật phẩm, đầy BOX hoặc lỗi ghi file. Kết quả thực chạy và giới hạn xem trong báo cáo task; không coi asset preview là bằng chứng runtime.

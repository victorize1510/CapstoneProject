# Camera thu vào mượt khi gặp vật cản

Phản hồi ngày 14/11: camera ép từ góc xa sang quá gần rất nhanh. Nhánh cũ thu vào ngay để chống xuyên tường, chỉ làm mượt chiều trả ra. Việc mờ model khi quá cận không giải quyết được thay đổi khoảng cách này.

## Thay đổi runtime

- Dò sớm hướng xoay boom và chuyển động pivot, tối đa ba mẫu trong khoảng nhìn trước **0,28 giây**. Có biên mềm **0,30 m** để bắt đầu thu trước khi lens thật chạm góc vật thể.
- Tách làm mượt chiều thu vào (**0,16 giây**) và chiều trả ra (**0,24 giây**); đây là tham số SmoothDamp, không phải thời lượng cố định của một animation.
- Khoảng cách render luôn bị giới hạn bởi clearance vật lý hiện tại. Vật cản xuất hiện đột ngột hoặc camera bị đặt vào tường vẫn phải thu ngay để tránh xuyên.
- Không đổi vai, yaw, FOV, model, gameplay hay save. Nhánh xử lý model quá cận được giữ riêng.
- Bỏ dự đoán pivot nằm trong tường mà character controller thật sẽ chặn lại. Sửa trường hợp contact khoảng cách 0 của sphere bắt đầu sát tường nhưng camera đang hướng ra xa.
- Dùng lại buffer physics và trạng thái riêng của từng camera; không tạo texture/material hoặc cấp phát managed cho mỗi query.

## Chỉnh trong Unity

**Tools → Capstone → Camera → Chọn camera hiện tại**, mở nhóm Camera Collision trong BasicCameraFollow:

| Trường | Mặc định | Công dụng |
|---|---:|---|
| Camera Collision Enter Time | 0,16 | Mức làm mượt lúc thu vào |
| Camera Collision Look Ahead | 0,28 | Thời gian dò trước chuyển động |
| Camera Collision Return Time | 0,24 | Mức làm mượt lúc trả ra |

Nếu tắt Camera Smoothing trong Settings, code tôn trọng lựa chọn đó. Enter Time hoặc Look Ahead bằng 0 dùng nhánh không dự đoán, phù hợp đối chiếu chẩn đoán. Không ép khoảng cách tối thiểu xuyên qua vật thể để giữ góc xa.

Không cần chạy lại setup scene/prefab. Sửa nằm trong `BasicCameraFollow.cs` và `CameraBoomCollision.cs`; `CameraSmoothEntryVerification.cs` là bộ kiểm Editor chọn chạy chủ động.

## Kiểm và bằng chứng

Thư mục task tiếp nối: `E:/UnityWork/CapstoneProjectData/Artifacts/camera-smooth-entry-2026-11-14/`.

Đọc REPORT.md / results.json trong đó để lấy kết quả cuối và giới hạn. Bộ kiểm dùng save riêng, vật cản tạm và ShopNPC thật trong DemoScene; không lưu scene.

Lượt cuối ngày 15/11 đạt **24 PASS / 0 FAIL**. Đường xoay qua ShopNPC cho peak thu vào **126,29 → 15,61 m/s** tại 1920×1080, **161,91 → 15,65 m/s** tại 1366×768 (giảm khoảng 88–90% trong fixture). Đây là tốc độ thay đổi khoảng cách camera, không phải tốc độ nhân vật; không phải giới hạn cố định áp cho mọi tình huống.

Phép thử deterministic 60 Hz giảm bước thu lớn nhất từ **4,075 xuống 0,336 m**. Cả 30/60/120 Hz giữ đúng hướng và clearance. Biên chạy tới tường phía trước nhưng camera phía sau thông thoáng đã qua; 500 query đã warm ghi nhận **0 B managed allocation**. Main và Editor assembly compile thành công.

Lượt render lạnh đầu tiên có frame dài hơn một giây, khiến input replay bỏ qua điểm va chạm; dữ liệu đó không được dùng so sánh. Bộ kiểm cuối giữ góc đầu trong 24 frame ổn định rồi mới chạy đường xoay. Không dùng warm-up để che số đo FPS: lượt này không đo FPS/Profiler toàn game.

Chạy lại qua **Tools → Capstone → Advanced → Camera → Verify smooth obstruction entry**. Bài kiểm so nhánh không dự đoán với nhánh mới trên cùng đường xoay; có warm-up ban đầu giới hạn 20 giây/lượt để frame import/JIT đầu tiên không bỏ qua toàn bộ vật cản.

Nhịp 30/60/120 Hz trong phép thử deterministic là nhịp bước mô phỏng, không phải bằng chứng engine đạt FPS tương ứng. Rig thật được đo ở 1920×1080 và 1366×768 với dt thực. Đường chạy được phát bằng code trong engine, không phải một lượt duyệt hết bản đồ bằng chuột/gamepad.

Standalone, Profiler toàn scene, flick camera cực nhanh và mọi scene/model chưa được xác minh trong lượt này. Tốc độ/độ giật có thể tăng ở tình huống bất ngờ không thể dự đoán; physical clamp vẫn được ưu tiên.

Tài liệu tham khảo: [Cinemachine Deoccluder — Damping When Occluded](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineDeoccluder.html). Project giữ solver chung hiện có và tách tham số theo chiều chuyển động, không chồng thêm extension collision thứ hai.

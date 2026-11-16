# Camera sát shop / nhân vật — đã kiểm runtime

Ảnh người dùng cho thấy camera thu sát lưng shop, đi vào model nữ và near plane cắt đầu/tóc thành mảng. Sửa đổi boom trước đó ngăn nhảy vai nhưng chưa xử lý hiển thị model ở khoảng cách quá gần.

## Mã đã bổ sung

- `CameraNearPlayerVisibility.cs`: cache renderer/material theo model đang chọn. Đo khoảng cách từ camera đến bounds thực của model; mờ đều toàn model trong dải 0,65 m, ẩn hoàn toàn trước vùng near-plane. Vào vùng nguy hiểm thì ẩn ngay, ra ngoài phục hồi trong khoảng 0,18 giây.
- `CameraVisibility.cs`: dùng cùng vòng begin/end camera đang có; chỉ áp dụng cho camera gameplay. Phục hồi material khi render minimap/camera khác, khi đổi model, disable hoặc chuyển scene.
- Shader Lit/SimpleLit/Unlit hiện có thêm `_CapstoneActorFade` riêng, không dùng vùng cắt ellipse của vật cản để cắt lỗ trên thân người. Giữ texture, màu, alpha mode, shadow pass của material gốc. Custom shader không hỗ trợ chỉ ẩn ở khoảng cách nguy hiểm, không đổi thành material sai.
- Generator `Tools/CombatVisuals/generate_camera_adapters.py` đã cập nhật để lần sinh shader sau không mất thay đổi.
- Không đổi hướng camera, scene, prefab, model gốc, collider hay gameplay/save.

Ngưỡng ẩn có tính near clip, FOV và aspect. Không đặt một khoảng cách tối thiểu rồi đẩy camera xuyên tường để giữ thấy nhân vật. Cache chỉ tạo khi lần đầu cần mờ, không clone material mỗi frame.

Icon AAMAP/minimap được loại khỏi cả phép đo bounds và material của nhân vật. Điều này quan trọng khi profile nam dùng chính PlayerRoot: nếu tính cả icon cao phía trên, model có thể bị ẩn quá sớm.

## Tình trạng kiểm

Main và Editor assembly compile thành công. Sau khi người dùng báo đã mở lại Editor, đã chạy bài kiểm bằng lệnh menu thật trong DemoScene, save riêng trên E, ở **1920×1080 và 1366×768**. Kết quả cuối: **34 PASS / 0 FAIL**.

- Model GirlFinal và Boy đều ẩn hoàn toàn ở vùng nguy hiểm, hiện lại khi ra xa. Đã mở trực tiếp ảnh trước/sau và ảnh chuyển tiếp; ảnh trước đối chiếu là cùng cảnh runtime khi tạm tắt near-player handling.
- Camera giữ đúng hướng boom yêu cầu: **0,000°** ở cả bốn tổ hợp model/độ phân giải.
- Chuyển tiếp thực tại shop có fade trung gian **0,7361**, không chỉ bật/tắt model.
- Material gốc được phục hồi sau render và khi camera khác render lồng nhau; icon map không nhận actor fade. Kiểm callback thật, không phải snapshot thiết kế.
- Cache không tăng qua bốn chu kỳ sát/rời shop. **500 vòng Update/Apply/Restore đã warm, hiệu ứng đang hoạt động: 0 B managed allocation**. Đây là phép đo riêng của helper trong Editor, không phải Profiler/FPS toàn scene.
- Disable giải phóng material clone và phục hồi model. Ba adapter shader import không lỗi.
- Khối ShopNPC thực dùng mesh **Cylinder**, bounds extents `(0,39; 1,79; 0,50)`; CapsuleCollider `(0,50; 1,79; 0,50)`. Không có chênh lệch chiều cao như suy đoán từ tên primitive. Giữ nguyên collider và scene.

Bộ kiểm tự lái pose trong engine; không phải đi hết scene bằng chuột/gamepad. Lỗi ba lần đo ngang ban đầu thuộc harness cập nhật Brain hai lần/frame và so sai trục X; đã sửa harness và kiểm hướng yêu cầu. Kiểm chuyển tiếp sau đó phát hiện icon map lọt vào bounds nam; đã sửa runtime và chạy lại toàn bộ. Đợt đầu từng dừng khi Editor không phản hồi; không tự restart Editor.

Đã gỡ request bridge tự poll khỏi source. Bộ kiểm chỉ chạy chủ động qua:

`Tools → Capstone → Advanced → Camera → Verify near-shop player visibility`

Bộ kiểm chuẩn bị save thử riêng trước Start, chỉ chạy khi chủ động chọn menu; không lưu scene, không ghi đè save thật.

## Giới hạn còn lại

1. Chưa standalone, gamepad, Profiler toàn scene hoặc stress test vật cản chuyển động nhanh.
2. Chưa duyệt mọi scene, mọi model/custom shader hoặc cinematic camera. Fallback custom shader chỉ ẩn trong vùng nguy hiểm; chưa thử đủ shader thư viện.
3. Khi sát tường quá mức, nhân vật phải mờ/ẩn để không chắn cảnh hoặc bị cắt xuyên; không thể vừa giữ camera ngoài tường, giữ đúng hướng và luôn thấy toàn thân trong mọi khe hẹp.

QA riêng: `E:/UnityWork/CapstoneProjectData/Artifacts/camera-near-shop-2026-11-14/`.

Ảnh người dùng cung cấp được giữ nguyên. Không tự commit/push.

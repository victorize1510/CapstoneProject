# Camera và combat visuals — 2026-11-13

Đợt sửa theo `E:/AIWork/Capstone/Combat_Visuals_Camera_Rework_2026-11-12/00_PROMPT_Unity.md`.

Báo cáo, số đo, ảnh runtime, manifest file/hash và giới hạn kiểm chứng nằm tại:

`E:/UnityWork/CapstoneProjectData/Artifacts/combat-visuals-camera-rework-2026-11-12/handoff-report.md`

## Phần triển khai

- Tách camera collision khỏi scenery cutaway và alpha của actor/UI/VFX. Actor không còn tự đăng ký fade. Camera bị tường ép sát thân thử hướng vai có đường đi vật lý thông thoáng; không đẩy camera xuyên tường để đạt khoảng cách mong muốn.
- Ray-test từng renderer, loại terrain/mặt sàn/actor/particle/UI/transparent glass; vùng cắt mềm chỉ ở trước player/pet đang xuất chiến. Có hysteresis và cache giới hạn. Material được khôi phục sau từng camera; thay material từ hệ khác được giữ.
- Ba adapter giữ họ URP Lit/Simple Lit/Unlit; forward/GBuffer/depth/normals dùng cùng hàm clip, shadow giữ geometry thật. Không dùng `_Parallax` hoặc `_DetailNormalMapScale` làm biến fade. Source URP và license kèm shader; generator ở `Tools/CombatVisuals/generate_camera_adapters.py`.
- Font số Rajdhani SemiBold có SIL OFL; crit/DOT bằng mesh UI, API damage hiện có giữ nguyên và dùng `HpLost`. Label pool 32, quỹ đạo lệch ngang không lấy RNG combat. Hồi máu chỉ phát sau giao dịch HP đã lưu thành công, menu không phủ số lên panel.
- Bốn icon kỹ năng SVG/PNG alpha; particle soft-disc tách khỏi material ribbon/trail. Scale cue theo visual binding của ba form. Clear trail/audio/cast khi mất focus, đổi visual, đóng UI hoặc hủy; giới hạn audio/effect.
- Giữ ID/enum, gameplay balance, dữ liệu save, controller/FBX nguồn và các UI khác. Thay đổi gameplay có sẵn trong working tree đầu phiên không được reset/revert.

## Thiết lập / mở rộng

DemoScene đã có rig; không cần kéo lại asset bằng tay. `CameraVisibility` được gắn runtime khi có `BasicCameraFollow`. Với shader tùy biến ngoài ba họ URP, giữ material gốc và thêm adapter có kiểm thử thay vì cưỡng ép sang Lit. `CameraOccluder.Kind` cho phép phân loại Wall/Column/Tree/Foliage/Glass/Ignore; renderer list chỉ là ứng viên, không phải lệnh fade toàn nhóm.

`BasicCameraFollow.cameraBodyClearance` điều chỉnh khoảng tránh sát thân. `CreatureVisualBinding.presentationScale` chỉ chỉnh VFX. Form mới cần profile semantic, socket Mouth/Body và skill set hiện có; release/cooldown/MP tiếp tục do driver và dữ liệu kỹ năng quản lý. Không chạy lại công cụ balance để chỉ đổi art.

Tái tạo icon bằng `Tools/CombatVisuals/generate_skill_icons.ps1`. Menu Editor `Tools/Capstone/Combat visuals/Apply presentation assets` chỉ gán icon, particle material và scale visual; không sửa scene hoặc save. Asset đã được tạo/gán trong đợt này.

## Kiểm thử và giới hạn

Editor QA hoàn toàn opt-in, dùng `QaArtifactPaths.Root`; không tự Enter Play nếu không có request. Các fixture chỉ tồn tại trong Play Mode và save được redirect sang E trước Start. Dừng phiên sẽ xóa cờ session; mọi recorder có hạn frame/thời gian và được Dispose.

Xem báo cáo ngoài E để biết lượt kiểm cuối và số đo thực. Không suy ra standalone/GPU hoặc hiệu năng trận đông từ mẫu idle Editor. Trong mẫu đối chứng cuối, 1366×768 vẫn chậm hơn baseline; chưa kết luận tối ưu toàn cảnh. Shadow/depth phức tạp, mọi loại cây/kính/custom shader và native hold gesture phải được phân biệt với những ca đã thực sự chạy.

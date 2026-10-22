# GUI — các mục nên làm tiếp

Ngày rà: 14/10/2026. Chỉ ghi đề xuất; chưa triển khai các mục dưới đây.

| Ưu tiên | Mục | Hướng giải quyết |
|---|---|---|
| Đã xong | Lượt QA từng bị chặn | Chạy lại đạt 185 kiểm tra UI và 26 kiểm tra Settings, gồm dropdown/Esc và minimap; build C# thành công, 0 lỗi. QA nhiều tỷ lệ màn hình và bản PC vẫn làm riêng. |
| P0 | Bố cục nhiều màn hình | Kiểm tra 1366×768, 1920×1080 và màn siêu rộng; kiểm tra fullscreen/độ phân giải trong bản PC, chú ý footer, popup và tên dài. |
| P1 | Giảm cập nhật HUD | HUD hiện vừa nhận sự kiện vừa refresh toàn vùng định kỳ. Tách cập nhật theo vùng thay đổi; chỉ cooldown/hiệu ứng cần cập nhật liên tục. Đo Profiler trước và sau. |
| P1 | BAG/BOX nhiều phần tử | Đo chi phí tìm kiếm, lọc và dựng lại danh sách; tái dùng ô hoặc ảo hóa khi số liệu chứng minh cần thiết. |
| P1 | Tổ chức controller | Tách dần điều hướng màn, popup và binding khỏi controller lớn; giữ API hiện tại, tránh viết lại toàn bộ panel đang hoạt động. |
| P1 | Hoàn thiện thao tác nhỏ | Rà focus sau đóng popup, tự cuộn tới nút đang focus, giữ tìm kiếm/vị trí cuộn khi quay lại; bổ sung nơi còn thiếu lý do khóa, tooltip và gợi ý phím. |
| P1 | Cảnh báo và hồi quy | Build lại có 187 cảnh báo; phân loại cảnh báo package và code project, cùng cảnh báo handshake Unity MCP trong Console. Giữ kiểm tra mở–đóng, input và lifecycle để tránh lỗi quay lại. |
| P2 | Dữ liệu đang hoãn | Giá Shop, provider Cẩm nang, thống kê Hồ sơ và nguồn item ngoài thế giới: nối sau khi chốt dữ liệu; không tự tạo số liệu giả. |
| P2 | Âm thanh giao diện | Khi có nội dung audio, thêm Mixer với nhóm nhạc/SFX/UI và âm thanh phản hồi nút; kiểm tra các tùy chọn âm lượng tác động đúng nhóm. |

Thứ tự đề nghị: hoàn tất QA → đo hiệu năng → sửa điểm tốn chi phí → hoàn thiện thao tác → kết nối dữ liệu sau.

Chưa cần thêm plugin. Rebinding/gamepad chỉ bổ sung khi có nhu cầu cụ thể.

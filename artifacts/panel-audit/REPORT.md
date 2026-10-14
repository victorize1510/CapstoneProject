# Kiểm tra và sửa vòng đời panel

## Những lỗi và chi phí thực tế đã xử lý

| Phần | Thay đổi | Kết quả |
| --- | --- | --- |
| BAG / INVENTORY | Theo dõi callback nút, search, sort, filter và geometry; gỡ trước rebind và khi disable. Giữ search/sort/list view khi gắn lại document. | Không tích lũy listener sau nhiều lần bật/tắt; search tiếp tục hoạt động. |
| PETS / Skills | Gỡ đầy đủ callback hover của nút thao tác và slot skill. | Tooltip không có nhiều handler chạy chồng sau rebind. |
| Trainer Menu | Dọn owner khóa gameplay, khóa router, hội thoại và quyền con trỏ khi disable; trả quyền khi panel con đóng trực tiếp. | Không kẹt input nếu Profile hoặc router bị tắt ngoài nút Back. Menu vẫn theo camera và rộng 35%. |
| Profile | Đóng panel khi component disable; chỉ cập nhật dữ liệu khi đang mở. | Không để màn Profile còn hiện nhưng gameplay đã mở khóa; thông tin và đội hình hiện đầy đủ. |
| BAG / PETS / BOX / QUEST | Bỏ refresh hiển thị khi panel tương ứng không mở; vẫn đọc dữ liệu mới lúc mở. BOX dọn popup và trạng thái swap khi disable. | BOX ẩn không dựng lại các card khi provider đổi; Quest ẩn không cập nhật nhãn mỗi frame. Logic gameplay vẫn chạy. |
| MAP | Cache transform icon, kích thước viewport và controller; dùng HashSet để tra marker đã đăng ký; ẩn nhãn selection khi marker bị lọc. | Giảm tìm transform và ghi layout lặp; không còn nhãn của marker đã ẩn. |
| MAP khi mở lại | Đưa màu backdrop vào đúng bước cấu hình hình ảnh, dùng theme hiện tại. | Nền không bị cấu hình map cũ ghi đè thành tối khi viewport không đổi. |
| MAP khi thoát Play Mode | Không tìm lại object / gọi đóng map trong quá trình application teardown. Disable component khi game đang chạy vẫn đóng map và trả khóa. | Không còn assertion GameObject.Find/go.IsActive trong lần Stop cuối, kể cả MAP đang mở. |
| Phần thưởng QUEST | Thêm tham chiếu item definition trực tiếp, giữ fallback TargetId; gắn Potion asset vào nhiệm vụ Cube. | Sửa lỗi Potion thiếu trong catalog TestChar. Phần thưởng đang chờ được hệ thống giao dịch hiện có cấp và lưu bình thường. |

## Kiểm chứng

- Unity TestChar Play Mode: **62/62** kiểm tra lifecycle qua; log từng mục ở `validation.txt`.
- World Map: **22 kiểm tra** qua, gồm viewport, filter nhóm, cập nhật marker, zoom/center và màu nền sau đóng/mở lại.
- Ball Director: kiểm tra tích hợp qua sau chỉnh panel; bóng idle ẩn, Spawn được chấp nhận mới hiện, Recall xong mất, hủy sequence dọn bóng, Throw/Pickup, ba Cast Spell và hàng đợi đổi pet.
- Quan sát trực tiếp Profile, Trainer Menu, PETS, BAG, QUEST và MAP tại Game View Full HD. BOX dùng kiểm tra runtime mở/đóng và preview UXML/USS thật; preview xác nhận phủ toàn viewport 1366×700.
- Build `Assembly-CSharp-Editor.csproj`: **0 lỗi, 187 cảnh báo dependency MSB3277 hiện có**. Xem `build.log`.
- Lần Stop cuối khi MAP còn mở: không có assertion, NullReferenceException, MissingReferenceException hoặc lỗi compile mới trong phần log vừa sinh.

## Phạm vi và giới hạn

- Không đổi SampleScene, màu/độ trong HUD hoặc format save. Không reset dữ liệu.
- Các giá trị Profile chưa có backend cung cấp vẫn hiển thị dấu `-`; không tạo thống kê giả.
- Plugin AI/MCP vẫn có cảnh báo version handshake và disposed log storage. Đây là cảnh báo ngoài hệ panel đang sửa; Console không được tuyên bố sạch toàn bộ.
- Không đo FPS/Profiler nên không đưa ra phần trăm tăng hiệu năng. Tối ưu ở đây là giảm refresh và callback lặp được kiểm chứng.
- Baseline trong thư mục này giữ trạng thái source trước lượt sửa để đối chiếu với những thay đổi đã có sẵn trong project.

Ảnh kiểm tra: `profile.jpg`, `pets.jpg`, `bag.jpg`, `quest.jpg`, `map.jpg`, `box-layout.jpg`. BOX preview không chạy provider nên không chứa pet runtime.

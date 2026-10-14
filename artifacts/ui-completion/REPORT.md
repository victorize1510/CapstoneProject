# Bàn giao Cẩm nang Pet và chuẩn hóa panel

## Đã triển khai

- Cẩm nang Pet toàn màn hình bằng UI Toolkit, nối vào Trainer Menu; ba cột 23/47/30%, 25 thẻ được tái sử dụng, 5 cột, phân trang rút gọn.
- Tìm tên/số thứ tự không phân biệt dấu tiếng Việt; lọc hệ/trạng thái và sắp xếp số thứ tự, hệ, vùng, độ hiếm, tên. Đổi bộ lọc về trang đầu, giữ lựa chọn còn hợp lệ. Mục chưa khám phá không tiết lộ tên hay chi tiết.
- Hợp đồng hiển thị `PetCodexEntryViewData`/`IPetCodexProvider`; dữ liệu 0/1/25/26/320 mục chỉ phục vụ Editor. Game chưa có provider: hiện thông báo rỗng, tiến độ — và khóa thao tác cần dữ liệu.
- Nguồn định dạng tiếng Việt dùng chung cho trạng thái, hệ, độ hiếm, giới tính, tính cách và kỹ năng. Giữ tên riêng, ID, enum và khóa lưu hiện có.
- Chuẩn hóa tiêu đề và Quay lại; sửa scale UI Toolkit theo mốc 1920×1080 thay vì phụ thuộc DPI. Công cụ setup giữ cấu hình mới.
- Quay lại/Esc về màn mở trước đó; popup giữ focus Tab/Shift+Tab và trả focus khi đóng. Ô nhập chữ không kích hoạt phím điều hướng/lệnh gameplay của panel.
- Hồ sơ hiện đủ nội dung, phân biệt số 0 với thông tin chưa biết, chữ thiếu dữ liệu không còn bị cắt trong thẻ thống kê.
- Thú đồng hành dùng camera/mesh preview riêng của đúng dạng đang chọn; không clone gameplay. Fit theo hình học, cập nhật tối đa 10 lần/giây khi mở và dọn tài nguyên khi đóng. Có nhãn Đang xem/Đang dùng và giải thích chỉ số.
- Kho thú/Túi đồ có xóa tìm kiếm và thông báo rỗng phù hợp; Kho thú phân biệt chưa có thú với không có kết quả. Túi đồ giữ Tất cả, túi vô hạn, lời nhắc chưa chọn và hạn chế phím tắt theo item hợp lệ.
- Nhiệm vụ có lời nhắc nhóm rỗng/chưa chọn. Các màn phụ dùng nhãn tiếng Việt và xử lý focus dùng chung; giữ điều kiện/chi phí từ API hiện có.
- Bản đồ gom marker gần nhau theo khoảng cách trên màn hình, mở danh sách chọn đúng marker; giữ tọa độ thật, bộ lọc, waypoint và có tooltip.
- Binding sự kiện được đăng ký/hủy đối xứng; query và ô Cẩm nang được cache, chi tiết chỉ dựng lại khi cần; provider chỉ cập nhật panel đang mở. Giữ ngưỡng chống nhấn lặp hiện có.

## Kiểm tra đã chạy

| Kiểm tra | Kết quả | Bằng chứng |
|---|---:|---|
| Editor: query/privacy/filter/paging/rebind/layout/preview/focus/buffer reuse | 232 đạt, 0 lỗi | [validation.txt](validation.txt) |
| Play Mode TestChar: mở/đóng, điều hướng, lock, callback, preview, MAP | 73 đạt, 0 lỗi | [runtime-validation.txt](runtime-validation.txt) |
| UXML/USS, tên binding, tham chiếu tài nguyên và meta | 145 đạt | [asset-validation.txt](asset-validation.txt) |
| Build Assembly-CSharp-Editor và dependencies sau tối ưu | 0 lỗi; 187 cảnh báo | [build-lightweight.log](build-lightweight.log) |

Editor kiểm tra bố cục ở 1920×1080, 1366×768, 1920×1200 và 2560×1080. Preview pet kiểm tra Dusk/Nightfall/Darkness với scale 0,25/1/2,5 và các tư thế animation. Play Mode được chạy lại sau sửa nhãn và PanelSettings; ảnh dưới đây được chụp ở 1920×1080 và đã xem trực tiếp.

Cảnh báo build chủ yếu là xung đột assembly/package cùng API cũ đang có trong project. Console còn thông báo kết nối/version handshake từ plugin Unity MCP và log plugin khi thoát Play; không khẳng định toàn bộ Console sạch. Kiểm tra panel cuối cùng đã hoàn tất, không có lỗi C#.

## Ảnh để đối chiếu

- [Trainer Menu](screenshots/TrainerMenu.png)
- [Hồ sơ](screenshots/Profile.png)
- [Thú đồng hành](screenshots/PETS.png)
- [Kho thú](screenshots/BOX.png)
- [Túi đồ](screenshots/BAG.png)
- [Nhiệm vụ](screenshots/QUEST.png)
- [Bản đồ](screenshots/MAP.png)
- [Cẩm nang trong game chưa kết nối dữ liệu](screenshots/Codex.png)
- [Cẩm nang với dữ liệu xem thử Editor](screenshots/Codex-editor.png)

## Cách xem thử và phần để sau

- Trong game: **Huấn luyện viên → Cẩm nang Pet**.
- Bản có dữ liệu thử: **Game Tools → GameToolThang → HUD → Preview Pet Codex**.
- Tài liệu tích hợp: `Capstone/Assets/Game/CodexSystem/README_Codex.md`.
- Hoãn provider runtime, theo dõi khám phá/thu phục, yêu thích theo loài được lưu, catalog chính thức và nguồn thống kê mới. Không tạo thông tin gameplay để lấp chỗ trống.
- Đợt này không thực thi tiêu hao/thả pet trên save thật. Ma trận thao tác tiêu hao bằng fixture và thử bàn phím thủ công toàn bộ trạng thái chưa được kiểm tra đầy đủ; các API nghiệp vụ hiện có được giữ lại.
- BOX không bổ sung pooling khi chưa có đo Profiler chứng minh cần thiết. Chưa có số liệu Profiler để tuyên bố giảm thời gian CPU/GC; tối ưu hiện tại dựa trên cache, tái dùng ô và loại bỏ cập nhật khi ẩn.
- Không chỉnh Shop, SampleScene, Spawn/Recall hoặc schema save trong đợt này. Unity được giữ mở và đã thoát Play Mode.

## Rà lỗi và tối ưu bổ sung — 13/10/2026

### Lỗi đã sửa

1. **Cẩm nang bind lại:** ô tìm kiếm/dropdown từng bị đưa về mặc định trong khi query vẫn giữ điều kiện cũ. Nay giữ trạng thái hiển thị và query đồng bộ; Reset xóa toàn bộ điều kiện. Không đăng ký thêm callback trùng.
2. **Focus popup:** loại bỏ các điểm Tab trùng trong TextField và xử lý `NavigationMoveEvent` hướng Next/Previous, để vòng focus không thoát ra thanh cuộn ngoài popup. Kiểm tra bỏ qua nút ẩn/vô hiệu hóa, đi tới/lùi, quay vòng và trả focus về nút mở đều đạt. Cách xử lý dùng sự kiện điều hướng được Unity mô tả trong [FocusController](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/UIElements/Core/FocusController.cs).
3. **Quyền riêng tư mục chưa khám phá:** nút chi tiết không còn tiết lộ trạng thái “Đã yêu thích” khi thông tin sinh vật chưa được khám phá.

### Tối ưu có kiểm tra

- Bản đồ tái sử dụng mảng union-find, dictionary, danh sách ô không gian và nhóm; runtime giữ workspace qua các lần gom marker. Bộ nhớ tăng khi vượt dung lượng cũ, không tạo lại các bộ đệm mỗi lần regroup.
- **Đính chính sau kiểm tra ngày 13/10:** `GC.GetAllocatedBytesForCurrentThread` trả 0 cả khi cấp phát đối chứng 4 KB trong Unity Mono này. Kết quả “0 byte” ở lượt trước không phải bằng chứng hợp lệ về GC. Test hiện xác nhận tham chiếu mảng và nhóm được tái sử dụng qua 100 lượt; chưa có số đo GC đáng tin cho toàn GUI.
- Kiểm tra workspace qua danh sách rỗng, giảm số marker và bán kính 0 đều đạt, không giữ chỉ số nhóm cũ.
- Cẩm nang tái dùng hai bộ đệm phân trang; bỏ lượt Refresh trùng khi bind trong trạng thái đang mở.
- 145 kiểm tra UXML/USS vẫn đạt. 71 kiểm tra Play Mode là kết quả đợt trước ngày 12/10; lượt bổ sung này chạy lại Editor với 227 kiểm tra và build C#.

Unity đang ngoài Play Mode, cửa sổ Cẩm nang xem thử được giữ mở. Phạm vi dữ liệu hoãn và các giới hạn kiểm tra ở trên vẫn giữ nguyên.

## Tối ưu nhẹ GUI và phần còn thiếu

Xem [GUI_AUDIT.md](GUI_AUDIT.md) cho thay đổi mới, phép đo thời gian, các mục chưa hoàn thành và thứ tự đề xuất. Kết quả mới nhất: 232 kiểm tra Editor, 73 kiểm tra Play Mode, 145 kiểm tra tài nguyên; build C# 0 lỗi. Cửa sổ xem thử tự đóng sau yêu cầu kiểm tra tự động để trả lại cửa sổ project.


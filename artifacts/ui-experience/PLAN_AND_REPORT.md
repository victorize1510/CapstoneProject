# Chuẩn hóa trải nghiệm giao diện

## Kế hoạch triển khai

| Bước | Công việc | Tiêu chí |
|---|---|---|
| 1 | Rà input router, popup/focus, font và header | Ghi nguyên nhân cụ thể; giữ dữ liệu gameplay |
| 2 | TAB bật/tắt Menu; I/M/Q/P bật/tắt màn tương ứng | Chuột/phím cùng route; một key edge không chạy hai lần |
| 3 | Esc đóng popup trước; dùng chung đường Trở lại | Đúng màn trước, trả focus cho nơi đã mở |
| 4 | Font LiberationSans, nhãn và kiểu nút chung | Tiếng Việt rõ; header, Map và Hồ sơ cùng cỡ/font |
| 5 | Loại cập nhật UI/renderer không cần thiết | Không refresh Trainer ẩn; minimap ẩn ngừng camera |
| 6 | Kiểm tra engine và ảnh thực tế | Input, panel, focus, font, compile và hồi quy Settings |

## Những lỗi xác định và xử lý

- TAB trong Trainer Menu bị nhánh chuyển focus bắt mất, không đến lệnh đóng. TAB thường được dành cho mở/đóng Menu; Shift+Tab và mũi tên giữ điều hướng menu. Trong panel toàn màn hình, Tab vẫn điều hướng UI Toolkit/uGUI.
- Router trả về quá sớm khi panel mở nên I/M/Q/P không đóng màn tương ứng. Router mới xác định màn đang mở và dùng chung lệnh Trở lại.
- UI Toolkit và Legacy Input có thể nhận cùng phím trong một frame. Router ghi nhận cạnh phím đã xử lý; không đóng rồi mở lại ở cùng frame. Giữ chống nhấn lặp 0,15 giây của các hành động.
- Hồ sơ bỏ qua popup đổi tên khi dùng Esc, và Esc khác đường của nút Trở lại. Cả hai dùng chung logic popup rồi nguồn đã mở màn.
- Dropdown native tự đóng bằng Esc nhưng router vẫn có thể đóng màn bên dưới. Callback ở panel ghi nhận key edge, để dropdown xử lý native và giữ màn mở.
- Khi đóng màn bằng code, dropdown native cũng được hủy để không còn danh sách nổi trên game. Dropdown mở bằng chuột hoặc Enter dùng cùng font/theme sáng, có cuộn dọc và cache stylesheet.
- Trainer/Hồ sơ/compass dùng font built-in, HUD dùng Fontin Small Caps, các panel Toolkit/Map dùng LiberationSans. Giờ dùng cùng file LiberationSans có sẵn; không tải hoặc nhân bản font.
- Phần body của PETS/BOX/màn phụ chưa có quy ước font kế thừa chung. `MenuTheme.uss` cung cấp font chung cho các template và màn độc lập.
- Nút chính thống nhất `←  TRỞ LẠI`, cỡ 15, khung 146×42 theo đơn vị thiết kế. Map/Hồ sơ dùng cùng focus viền vàng như UI Toolkit; Trainer Menu giữ vị trí footer.
- Header Map/Hồ sơ đã căn lại icon, điểm bắt đầu title và subtitle để khớp theme chung.

## Tối ưu thực hiện

- Không gọi GetSnapshot/cập nhật summary khi Trainer Menu ẩn; giảm lịch polling từ 0,5 giây thành 1 giây khi mở, giữ sự kiện cập nhật tức thời.
- Không áp lại input mode, cursor, control lock và trạng thái minimap mỗi frame khi trạng thái không đổi. Chữ ký trạng thái gồm cả Menu/Hồ sơ để không nhầm vì hai màn cùng dùng input mode Menu.
- Kết hợp nhu cầu minimap của UI và tùy chọn người dùng tại MapSystemController. Không tự bật lại minimap sau đóng Menu khi người dùng đã tắt; ẩn minimap dừng camera/controller minimap, không khóa Full Map.
- Tái sử dụng danh sách Selectable, bỏ lambda tạo cho mỗi lần điều hướng; loại các control disabled/không tham gia navigation.
- Cache font và tham chiếu asset; không phụ thuộc font hệ điều hành, không đọc PlayerPrefs mỗi frame.
- Callback panel/attach/dropdown được đăng ký và gỡ tương ứng. Trả focus về nút Trainer đã mở màn sau khi quay lại.
- Không đổi schema save, nguồn thống kê, giá Shop, dữ liệu Cẩm nang, model, Spawn/Recall hoặc SampleScene.

## Kiểm tra

Kết quả engine trong `runtime-validation.txt`, C# trong `build.log`, ảnh thực tế trong `screenshots/`. Bộ kiểm tra mở panel và thay focus, không mua, dùng item, sửa tên/avatar hoặc ghi fixture vào save. Tùy chọn minimap phục hồi về giá trị của Settings sau kiểm tra.

**Trạng thái kiểm tra cập nhật 14/10/2026:** lượt chạy lại Play Mode đạt 185 kiểm tra UI và 26 kiểm tra Cài đặt. 176 kiểm tra asset/UXML/USS đạt. Build `Assembly-CSharp-Editor.csproj` thành công (exit 0), 0 lỗi và 187 cảnh báo từ quá trình build project cùng các package. Không kết luận Console hoàn toàn sạch: còn cảnh báo kết nối/version handshake Unity MCP.

- Đã xác nhận TAB, I/M/Q/P, chặn phím lúc nhập chữ, route Hồ sơ/Menu/gameplay và font của các panel/HUD.
- Dropdown mở bằng NavigationSubmit dùng font/theme chung; NavigationCancel rồi legacy Escape trong cùng frame không đóng Shop. Legacy Escape tới trước cũng chỉ hủy dropdown.
- Minimap tắt dừng camera; mở/đóng Menu không ghi đè tùy chọn; Bản đồ lớn vẫn mở được.
- Settings kiểm tra âm lượng/mute, debounce lưu, đảo Y, damping của hai camera Cinemachine, minimap, gợi ý tương tác, quest tracker, thông báo lưu, popup hướng dẫn và đường Trở lại. Các thay đổi thử dùng repository trong bộ nhớ và được khôi phục.
- Bài kiểm tra font ban đầu tính cả nhãn hướng minimap legacy đang ẩn. Đã kiểm tra riêng trạng thái ẩn của nhãn này, rồi kiểm tra font của HUD tự dựng; không sửa minimap để đáp ứng một lỗi của fixture.
- Cờ tự dừng kiểm tra được chuyển sang SessionState để tồn tại qua domain reload. Unity đã trở về Edit Mode; không còn file request chờ chạy.
- Đã xem ảnh Settings thực tế ở 1920×1080. Bản PC fullscreen/độ phân giải và QA toàn bộ màn ở các tỷ lệ khác vẫn thuộc danh sách làm tiếp; các bài kiểm tra hiện tại không thay thế toàn bộ QA gameplay.

Dropdown được hủy bằng `NavigationCancelEvent` để Unity tự dọn và trả focus; không dùng reflection trong code runtime. Tham chiếu [cơ chế navigation của Unity 6000](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Modules/UIElements/Core/KeyboardNavigationManipulator.cs) và [GenericDropdownMenu](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Modules/UIElements/Core/Controls/GenericDropdownMenu.cs).

## Phần còn thiếu, ưu tiên sau

| Hạng mục | Đề xuất | Lý do để sau |
|---|---|---|
| Giá/mặt hàng Shop | Nhập cấu hình giá đã tạo, kiểm tra cân bằng kinh tế | Cần dữ liệu do bạn quyết định |
| Cẩm nang và thống kê Hồ sơ | Nối provider/catalog/khám phá chính thức | Đã thống nhất hoãn dữ liệu gameplay |
| Nhặt item ngoài thế giới | Nối nguồn world item với pickup hiện có | Cần phân loại đối tượng item và luật thưởng |
| Music/SFX/UI riêng | Phân tuyến âm thanh qua mixer khi đã có audio content | Project chưa có mixer group |
| Fullscreen/độ phân giải | Kiểm tra lại trong bản PC | Editor không kiểm tra được cửa sổ standalone |
| Rebinding và gamepad | Chỉ thêm khi có nhu cầu/input profile cụ thể | Không giả lập tùy chọn chưa có hệ thống |

Không cần thêm plugin cho đợt này. Việc đổi font HUD áp dụng qua code và setup tool; scene trong Unity được giữ nguyên sau khi Reload bị automatic approval review chặn.

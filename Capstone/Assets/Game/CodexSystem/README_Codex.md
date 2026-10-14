# Cẩm nang Pet và quy ước giao diện

## Mở và xem thử

- Trong game: **Huấn luyện viên → Cẩm nang Pet**. Quay lại/Esc về màn mở trước đó.
- Trong Editor: `Game Tools > GameToolThang > HUD > Preview Pet Codex`.
- Kiểm tra: `Game Tools > GameToolThang > HUD > Validate UI Completion`.
- Tự động kiểm tra sau reload script: tạo `Temp/RunUICompletion.request` trong project Unity trước khi refresh. Editor tiêu thụ yêu cầu một lần; không có hook runtime.
- Bộ xem thử nằm hoàn toàn trong thư mục `Editor`, có 0/1/25/26/320 mục. Không tạo hoặc ghi save.

## Kết nối dữ liệu sau này

`PetCodexPanelController.SetProvider(IPetCodexProvider)` nhận snapshot hiển thị. Provider phát `Changed` sau khi snapshot thay đổi. Controller chỉ refresh khi đang mở.

`PetCodexEntryViewData` chứa ID ổn định, số thứ tự, tên riêng, trạng thái khám phá, hệ, vùng, độ hiếm, ảnh và chuỗi tiến hóa. Trường chưa biết để null/rỗng. Controller hiển thị “Chưa rõ”; mục chưa khám phá chỉ có số thứ tự, bóng mờ và dấu hỏi.

`CanSetFavorite` và `TrySetFavorite` dành cho yêu thích theo loài trong Cẩm nang, độc lập với yêu thích từng thú ở đội/kho. Chưa có provider runtime hoặc cơ chế lưu cho tính năng này.

Không có provider: thông báo “Cẩm nang chưa có thông tin sinh vật.”, tiến độ `—`, khóa tìm/lọc và giải thích qua tooltip. Không tự tạo dữ liệu chính thức.

## Tổ chức và hiệu năng

- UXML giữ cấu trúc; USS giữ bố cục/theme; controller cache thẻ và đăng ký/hủy sự kiện đối xứng.
- Mỗi trang dùng lại 25 thẻ; chi tiết chỉ cập nhật khi lựa chọn hoặc dữ liệu thay đổi; các ô tiến hóa được tái dùng.
- `PetCodexQuery` xử lý tìm không dấu, bộ lọc và thứ tự ổn định. ID/enum không dùng chuỗi tiếng Việt làm khóa.
- `VietnameseUI` dùng chung tên hệ, độ hiếm, trạng thái, chỉ số và từ khóa tìm kiếm cho uGUI/UI Toolkit.
- `PanelFocusUtility` giữ Tab/Shift+Tab trong popup, trả focus khi đóng. Enter dùng submit của nút; Esc do router màn xử lý.
- PETS preview chỉ dựng mesh hiển thị từ dạng được chọn, không clone logic gameplay. Snapshot tư thế tối đa 10 lần/giây khi màn hiện; URP render một lần cho mỗi tư thế mới rồi giữ texture, dọn callback/camera/texture khi đóng.
- Bản đồ gom các marker cách nhau tối đa 36 pixel theo nhóm liên thông; không thay tọa độ thật. Chọn nhóm mở danh sách, lọc/đóng màn hủy popup.
- Bản đồ giữ lại bộ đệm gom nhóm giữa các lần cập nhật; cấp phát thêm khi số marker vượt dung lượng cũ. Cẩm nang dùng lại bộ đệm phân trang và giữ bộ lọc hiển thị đồng bộ khi bind lại.
- Marker tách trạng thái đủ điều kiện hiển thị khỏi trạng thái bị che bởi nhóm, tránh bật/tắt GameObject mỗi frame. Cẩm nang chuẩn hóa tên một lần cho mỗi mục trong một lượt tìm/sắp xếp; không giữ cache cũ qua thay đổi dữ liệu.

## Giới hạn đợt này

Không thêm theo dõi khám phá/thu phục, catalog loài chính thức, save mới, dữ liệu thống kê mới hoặc Shop. Không sửa SampleScene hay hệ Spawn/Recall.

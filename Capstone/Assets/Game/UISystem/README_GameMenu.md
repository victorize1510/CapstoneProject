# Trainer Menu và điều phối panel

## Thiết lập

Mở `TestChar`, chạy `Game Tools > GameToolThang > HUD > Create Main Menu` để tạo/cập nhật `GameMenu` và gán các owner có sẵn. Tool sử dụng nhãn tiếng Việt trong controller, không dựng lại nhãn cũ. Thiếu owner thì màn báo chưa khả dụng.

## Điều khiển

- `Tab`: mở Trainer Menu trong gameplay; bấm lần nữa đóng Menu. `Shift+Tab` chuyển focus ngược trong Menu; phím mũi tên và Enter chọn nút.
- Trong các panel toàn màn hình, `Tab`/`Shift+Tab` chuyển focus; không dùng Tab để đóng panel.
- `Enter`: xác nhận nút đang focus. `Esc`/Trở lại: đóng popup trước, sau đó quay đúng màn trước. Dropdown native xử lý Esc trước màn bên dưới.
- `I`: Túi đồ; `M`: Bản đồ; `Q`: Nhiệm vụ; `P`: Thú đồng hành. Bấm lại đúng phím đóng màn tương ứng (hoặc popup đang mở trước). Mở từ Menu thì quay về Menu; mở từ gameplay thì quay về gameplay.
- Khi nhập tìm kiếm/đổi tên, không phát lệnh gameplay.

## Bố cục và input

Trainer Menu chiếm 35% chiều ngang bên phải, toàn chiều cao, nội dung co vừa màn. HUD và minimap vẫn ở dưới. Player giảm tốc rồi dừng; camera tiếp tục theo player/mục tiêu ghim, khóa thao tác xoay/zoom mới. Panel toàn màn hình giữ quy tắc khóa gameplay/cursor riêng; đóng khôi phục trạng thái trước đó.

## Các màn

- Túi đồ, Thú đồng hành, Kho thú, Nhiệm vụ: các template của `MonsterInventory.uxml` và controller riêng.
- Hồ sơ: `ProfilePanelController`; Bản đồ: `MapInputController`/`WorldMapUIController`.
- Cẩm nang Pet: `PetCodexPanelController`, thay placeholder cũ. Runtime chưa có provider thì hiện trạng thái rỗng; xem thử chỉ trong Editor (xem README_Codex).
- Cửa hàng và Cài đặt có controller riêng dùng UI Toolkit. Shop chưa nhập giá thì không cho mua; Cẩm nang chưa nối provider thì hiện trạng thái rỗng.

## Font và cập nhật

Các panel, Trainer Menu, Hồ sơ, HUD và compass dùng font LiberationSans hiện có. Font uGUI lấy qua `MenuTypography` với asset tham chiếu trong Resources; UI Toolkit kế thừa `MenuTheme.uss`; Map TMP dùng cùng file font nguồn. Nút Trở lại dùng chung nhãn, cỡ chữ, kích thước và focus vàng.

Thông tin Trainer chỉ cập nhật khi Menu mở, tối đa một lần mỗi giây ngoài sự kiện thay đổi. Input mode/hiển thị minimap chỉ áp dụng lại khi trạng thái thay đổi. Minimap kết hợp yêu cầu của UI với tùy chọn người dùng; khi ẩn thì ngừng camera minimap. Full Map hoạt động độc lập. Không thêm dữ liệu gameplay hay trường save.

## Tương tác E trong thế giới

- Shop/BOX/NPC nhiệm vụ dùng chung `WorldInteractionView` cho cả hiện prompt và
  thực thi. Mục tiêu phải nằm trong khoảng cách đã cấu hình, góc nhìn 100° phía
  trước model đang chọn, và không bị collider vật cản che từ độ cao mắt.
- Hướng camera FreeLook không thay hướng nhìn của model. Trigger không che tầm
  nhìn; collider của player và chính mục tiêu được bỏ qua.
- Mỗi lần nhấn E chỉ một listener nhận lệnh; giữ chống lặp 0,15 giây.
- Nhặt đồ dùng `BasicPlayerMovement.SetPickupTarget(Transform)` để gắn/xóa mục
  tiêu item. Khi hợp lệ mới hiện `[E] Nhặt đồ` và chạy animation; không có mục
  tiêu thì E không chạy animation nhặt trong không khí. Đây là binding cho hệ
  world item; không tự coi mọi collider là loot hay tự thêm item vào Bag.
- `TryPlayScriptedPickup` của Recall vẫn dùng animation nhặt riêng, không phụ
  thuộc mục tiêu loot.

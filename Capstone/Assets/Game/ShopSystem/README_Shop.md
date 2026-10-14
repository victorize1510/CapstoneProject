# Cửa hàng

## Sử dụng trong TestChar

Đứng trước NPC cửa hàng trong phạm vi tương tác, nhấn **E**. Shop mở toàn màn hình qua `GameMenuController.OpenShopFromWorld`; **Quay lại / Esc** trở về gameplay. Hệ tương tác vẫn kiểm tra hướng nhìn và vật cản.

`MonsterInventoryController` tự gắn và bind `ShopScreenController`. NPC tự lấy `ShopSource` trên chính nó; nếu thiếu, tự thêm component và dùng catalog mặc định. Không cần gán Inspector để mở Shop mặc định.

## Nhập giá sau

Mở **Assets/Game/ShopSystem/Resources/Shop/DefaultShopCatalog.asset** trong Inspector.

- Hai mục khởi tạo tham chiếu trực tiếp **Potion.asset** và **DuskStone.asset** đang có.
- `GoldPrice = -1`: chưa có giá, khóa mua và hiện lý do. **Không tự coi là miễn phí**.
- `GoldPrice = 0`: miễn phí, chỉ dùng nếu chủ động cấu hình.
- `Stock = -1`: không giới hạn. `Stock >= 0`: tồn kho trong phiên chơi của từng NPC.
- `MaxPerPurchase`: giới hạn cho một lần mua, mặc định cấu hình 99. Bag vẫn vô hạn và chia stack theo Item Definition.
- `Unlocked` / `LockedReason`: trạng thái bán và thông báo khóa.
- Nhiều cửa hàng: tạo asset `Game > Shop > Catalog`, gắn `ShopSource` lên NPC và chọn catalog riêng. Catalog không bị sửa lúc mua.

Giá và danh sách Shop được cấu hình ngoài UI. Không có tiền kim cương giả hoặc item mới được tạo trong game. Các giá trong công cụ **Preview Shop (Editor fixture)** chỉ là dữ liệu kiểm thử, không được đưa vào build.

## Năm danh mục hiển thị

| Danh mục Shop | Dữ liệu sử dụng |
|---|---|
| Vật phẩm tiêu hao | Medicine / Food khi `Automatic` |
| Vật phẩm thu phục | CaptureBall khi `Automatic` |
| Nguyên liệu | Material khi `Automatic` |
| Đặc biệt | Chỉ các offer được catalog gán rõ `Special` |
| Đá tiến hóa | Offer gán rõ `Evolution`; Dusk Stone giữ ID `dusk-stone` và API tiến hóa hiện có |

Không tự biến KeyItem / QuestItem / Equipment thành hàng bán. Không có Tất cả, bán lại hoặc chế tạo.

## Luồng mua

`ShopScreenController → ShopPurchaseService → PlayerCurrencyWallet + MonsterInventoryAdapter`.

Service kiểm tra lại listing, khóa, giá đang hiển thị, số lượng, vàng, tồn kho và giới hạn. Tổng giá tính bằng `long`, ví vàng vẫn giữ định dạng `int` hiện có. Giới hạn mua là nhỏ nhất giữa khả năng trả tiền, tồn kho, giới hạn offer và giới hạn tổng lượng item lưu bằng `int`.

Ví dự phòng khoản vàng trong cùng thao tác đồng bộ. Inventory dựng các stack trong bộ đệm trước khi thay đổi; thất bại không thêm một phần item và trả lại khoản dự phòng. Sau commit, các sự kiện nhìn thấy inventory, vàng và stock đã đồng bộ. `PlayerSaveController` tiếp tục nghe các sự kiện hiện có; UI không ghi save riêng. Item có trong save được đối chiếu ID trước khi cộng thêm, tránh hai definition trùng ID.

Chống lặp 0,15 giây nằm tại service cho cả chuột và xác nhận bàn phím. UI khóa nút khi đang xử lý. Evolution Stone chỉ được mua vào Bag; Shop không tiến hóa pet.

## Giao diện và hiệu năng

- `ShopScreen.uxml/.uss`: bố cục ba cột 19% / phần còn lại / 28%, grid 5 cột, mô tả cuộn riêng, mua cố định bên dưới.
- Reuse `MenuTheme.uss`, `MenuCommon.uss`, `MenuScreenHeader.uss`, font **LiberationSans.ttf**, title 38, header 76, Back 146×42.
- Currency chip được chuyển nguyên style từ Bag sang `MenuCommon.uss`; không đổi màu hay độ trong suốt của Bag.
- `ShopProductCard.uxml` và `ShopProductCardView`: dùng sprite thật, giữ tỷ lệ; các ô tái sử dụng khi lọc/đổi danh mục.
- Cập nhật qua sự kiện, không có `Update` trong controller/service. Callback của view được tháo trước khi bind lại; đóng Shop hủy theo dõi nguồn dữ liệu.
- Tìm không dấu trong danh mục đang chọn; sort mặc định/tên/giá/độ hiếm từ Item Definition. Không có nút list giả.
- Thuật ngữ và mô tả hai item hiện có được Việt hóa ở lớp hiển thị; ID, tên riêng và dữ liệu gốc được giữ.

Tham chiếu kỹ thuật: [USS của Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/uitoolkits/uielements/uie-uss/properties/supported-properties), [ảnh nền và Sprite trong UI Toolkit](https://docs.unity.com/en-us/engine/6000.3/manual/uitoolkits/uielements/uie-uss/properties/uib-styling-ui-backgrounds).

## Kiểm tra

Menu **Game Tools > GameToolThang > HUD**:

- **Validate Shop**: giao dịch bằng fixture độc lập; tìm/lọc/sort, layout 1920×1080, 1600×900, 2560×1440, 1366×768, 1920×1200, 2560×1080, rebind và callback.
- **Validate Shop in Play Mode**: mở/đóng, khóa gameplay, Back và disable/rebind owner trong TestChar. Không mua bằng save thật.
- **Preview Shop (Editor fixture)**: xem nhiều ô bằng dữ liệu minh họa riêng.

Kết quả được lưu trong `artifacts/shop-ui` ở gốc repository.

## Giới hạn đã chốt

Giá chính thức chờ người dùng nhập. Shop hiện chỉ bán bằng vàng. Tồn kho hữu hạn hỗ trợ trong phiên chơi, **chưa lưu stock giữa các lần mở game** vì project chưa có stock save. Chưa bổ sung item cho những danh mục đang trống. Những phần này không được lấp bằng dữ liệu tự bịa.

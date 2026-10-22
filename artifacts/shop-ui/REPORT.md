# Shop — kết quả triển khai

## Đã hoàn thành

- Shop UI Toolkit toàn màn hình theo bố cục ảnh: danh mục trái, toolbar/grid ở giữa, thông tin và mua bên phải. Đúng 5 danh mục; grid 5 cột; không có All, Gear hoặc nút list chưa hoạt động.
- Font LiberationSans.ttf, title 38, header 76 và Back 146×42 từ theme/header chung. Dùng lại nút primary, focus, chống lặp và currency chip của hệ UI hiện tại.
- Tìm theo tên, không dấu, trong danh mục đang chọn; sắp xếp mặc định/tên/giá tăng/giảm/độ hiếm thực tế. Chi tiết tự chọn mục đầu, có trạng thái rỗng, mô tả cuộn và footer mua cố định.
- Ví vàng thật, số lượng đang có thật, icon và metadata từ MonsterItemDefinition. Không tạo gem, item hay giá chính thức giả. Sprite thấp phân giải giữ kích thước thiết kế gốc để không phóng lớn bị nhòe.
- ShopPurchaseService tái kiểm tra khi mua; chống nhấn lặp 0,15 giây; tính giá bằng long; nhận item nguyên vẹn hoặc giữ nguyên vàng. Bag tiếp tục vô hạn, non-stackable tách ô. Stock hữu hạn được cập nhật riêng cho mỗi NPC trong phiên chơi.
- Dùng PlayerSaveController qua sự kiện Wallet/Inventory hiện có. Mua đá tiến hóa chỉ đưa đá vào Bag, không chạy tiến hóa trong Shop.
- NPC và điều hướng được nối tự động; Back/Esc đóng về gameplay. Không có Inspector reference bắt buộc gán để mở Shop mặc định.

## Cấu hình chờ người dùng

`Capstone/Assets/Game/ShopSystem/Resources/Shop/DefaultShopCatalog.asset` có hai reference thật: Potion và DuskStone. **GoldPrice = -1** là chưa có giá và khóa mua; nhập giá chính thức sau theo lựa chọn của người dùng.

Phân loại: Medicine/Food → tiêu hao; CaptureBall → thu phục; Material → nguyên liệu. Special và Evolution phải được catalog chọn rõ; Dusk Stone chọn Evolution và giữ ID dusk-stone. Không tự lấy quest/key/equipment thành hàng bán.

Stock = -1 là vô hạn. Stock hữu hạn chưa lưu qua lần mở game. Giới hạn mua = nhỏ nhất của số vàng đủ trả, stock, MaxPerPurchase cấu hình và giới hạn tổng lượng item int; Bag không áp dụng giới hạn dung lượng.

## Lỗi đã tìm và sửa

1. UI bind trước khi PlayerSaveController.Start tạo ví: lấy lại nguồn khi mở Shop, không tạo ví thứ hai.
2. CanAdd của ListBag xem số ô đang cấp phát là dung lượng tối đa: đường thêm nguyên vẹn dùng đúng Bag mở rộng và giữ quy tắc từ chối của custom Bag.
3. Giao dịch có thể thêm một phần rồi thất bại: dựng stack vào bộ đệm trước commit, vàng được dự phòng và hoàn lại nếu nhận thất bại.
4. Definition được khôi phục từ save khác reference catalog: đối chiếu ID trước khi cộng, tránh item trùng ID làm save không hợp lệ.
5. Cột thứ năm xuống hàng do vùng cuộn/sai số làm tròn: lấy chiều rộng viewport và chừa sai số pixel cho từng cột.
6. Theme runtime làm Label nhạt và cỡ chữ danh mục quá nhỏ: gán màu/cỡ chữ theo component, giữ style của các panel khác.
7. Callback/focus khi bind lại: tháo callback cũ, khôi phục trạng thái hiển thị và focus; pool không nhân đôi.

## Kiểm tra đã chạy

| Kiểm tra | Kết quả |
|---|---|
| Unity import/compile | File Shop/UXML/USS được import, không có lỗi compile Shop |
| Build Assembly-CSharp-Editor và dependency C# | **0 lỗi**, 193 cảnh báo dependency/API cũ |
| Shop Editor fixture | **167 PASS**, gồm giao dịch, rollback, item trong save, tìm/sort, quantity, callback và layout |
| TestChar Play Mode | **13 PASS**, gồm bind ví muộn, mở/đóng, submit nút Back, khóa input và disable/rebind owner |
| Kiểm tra UXML/USS toàn thư mục UI | **154 PASS** |
| Responsive | 1920×1080, 1600×900, 2560×1440, 1366×768, 1920×1200, 2560×1080 |
| Quan sát ảnh thực tế | Đã xem ảnh Shop-actual.png sau bản sửa cuối |

Mua thành công/thất bại được thử bằng fixture riêng không chứa PlayerSaveController; không mua hay tiêu hao item từ save thật. Play Mode chỉ kiểm tra presentation/navigation. Các cảnh báo mạng/MCP đang có vẫn hiện trong Unity, không thuộc luồng Shop.

### Bằng chứng

- `validation.txt`: 167 kiểm tra Editor.
- `runtime-validation.txt`: 13 kiểm tra Play Mode.
- `build-final.log`: build C#.
- `screenshots/Shop-actual.png`: ảnh Shop thực tế 1920×1080, giá chưa cấu hình.
- Kiểm tra cấu trúc UXML/USS: `artifacts/ui-completion/asset-validation.txt`.

## File mới

- `Assets/Game/ShopSystem/ShopCatalog.cs`, `ShopSource.cs`, `ShopPurchaseService.cs`, `ShopScreenController.cs`, `ShopProductCardView.cs`.
- `Assets/Game/ShopSystem/Resources/Shop/DefaultShopCatalog.asset`.
- `Assets/Game/ShopSystem/Resources/UI/ShopProductCard.uxml`.
- `Assets/Game/ShopSystem/Editor/ShopValidation.cs`, `README_Shop.md` và metadata tương ứng.
- `Assets/ThangTest/UIThang/Inventory/UI/ShopScreen.uxml`, `ShopScreen.uss`.

## File chỉnh

- `MonsterInventoryController.cs`: host/lifecycle của Shop và bind lại nguồn ví khi mở.
- `MonsterInventoryAdapter.cs`: thêm nguyên vẹn, đối chiếu quy tắc Bag, phát sự kiện sau commit.
- `PlayerCurrencyWallet.cs`: dự phòng/hoàn lại vàng khi nhận item thất bại.
- `GameMenuController.cs`, `NpcPanelInteractable.cs`: thay Shop placeholder bằng màn thật, giữ điều hướng, Việt hóa prompt NPC.
- `VietnameseUI.cs`: ánh xạ mô tả hai item sẵn có ở lớp hiển thị.
- `MonsterInventory.uxml`: thêm template Shop.
- `MenuScreenHeader.uss`: Shop dùng header chung.
- `MenuCommon.uss`, `InventoryPanel.uss`: chuyển nguyên style currency chip sang stylesheet chung.

Các đường dẫn Assets ở trên đều nằm trong thư mục Capstone. Hướng dẫn cấu hình và menu kiểm tra: `Assets/Game/ShopSystem/README_Shop.md`.

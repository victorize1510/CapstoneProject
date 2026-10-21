# Bộ GUI dùng chung — đợt 1 và 2

Phong cách: nền ngoài ngà, mặt nội dung trắng, xanh lá, viền vàng mảnh và lá trang trí tách riêng. Font Liberation Sans đã có trong project. Không chứa dữ liệu gameplay.

Nền minh họa dùng chung: `Resources/UI/GuiKit/Backgrounds/menu_canvas.png`. UI Toolkit thêm class `gui-background` ở lớp ngoài; uGUI dùng `GuiKitControls.Background(parent)` hoặc `GuiKitAssets.Background()`. Nền giữ tỷ lệ và crop theo viewport; nằm dưới nội dung và không nhận click. Các khung bên trong tiếp tục trắng, chữ nằm trên lớp UI thật. Master PNG được giữ riêng trong `GUI_Source/Backgrounds`.

## Xem và kiểm tra trong Unity

- **Game Tools → GameToolThang → GUI → Open Shared GUI Gallery**: mẫu UI Toolkit có tìm kiếm, nút xóa, tab, dropdown, slider, toggle và focus thật. Nội dung chỉ dùng để thử giao diện.
- **Validate Shared GUI Kit**: kiểm tra import, glyph tiếng Việt, template, input/focus và render mẫu ở bốn viewport. Kết quả nằm ở `artifacts/gui-production/stages-1-2` ngoài thư mục project Unity.
- **Rebuild Shared Kit Prefabs**: tạo/cập nhật các prefab trong `Prefabs`, giữ GUID của prefab đã tồn tại. Chỉ chạy khi muốn dựng lại bộ chuẩn; không chạy tự động lúc mở project.

## Dùng lại

### UI Toolkit

Nạp `Inventory/UI/MenuTheme.uss` và `GUIArt/UI/GuiKit.uss`, thêm class `menu-theme` ở tổ tiên. Dùng các template trong `UI` hoặc class `.gui-button`, `.gui-surface`, `.gui-slot`, `.gui-field`, `.gui-dropdown`, `.gui-progress`, `.gui-tabs`, `.gui-tooltip`.

- `GuiScreenHeader`: `screen-icon`, `screen-title`, `screen-description`, `screen-back`.
- `GuiSearchField`: `search-input`, `search-clear`.
- `GuiConfirmation`: `confirmation-title`, `confirmation-message`, `confirmation-cancel`, `confirmation-accept`.
- `GuiSlot`: `slot-portrait`, `slot-favorite`, `slot-count`; thêm `is-selected`, `is-favorite` khi cần.

Query bên trong instance template để tránh trùng tên giữa nhiều slot. Controller của màn sở hữu callback và hủy đăng ký khi đóng/hủy. Template xác nhận là phần nội dung; controller modal hiện có vẫn sở hữu blocker, focus trap, Esc và trả focus. Không dùng template này như một modal độc lập thiếu xử lý input.

Icon không nhận input. Ảnh trắng có alpha để tint theo theme bằng `-unity-background-image-tint-color`. Không dùng opacity của cha để làm mờ cả chữ/icon.

### uGUI

Dùng prefab hoặc factory `GuiKitControls.Frame`, `Icon`, `Label`, `Button`. `GuiKitButton` kế thừa `Button`, giữ API `onClick`, `interactable` và navigation native; cập nhật label/icon/focus theo trạng thái, không có vòng `Update`. Caller chịu trách nhiệm layout, listener và luồng gameplay.

`GuiKitAssets.Icon(id)`, `Frame(id)`, `Decoration(id)` tải lười từ Resources và cache tham chiếu. Image trang trí tắt raycast. Các prefab là view, không tự trừ item, sửa save hoặc thực thi thao tác.

## Source và xuất lại

Source SVG, `tokens.json`, manifest và nguồn tài nguyên nằm tại `D:/Download/ProjectVTC/Asset Capstone/ẢnhGUi/GUI_Source`. Sửa token tại đó rồi chạy `artifacts/gui-production/create_gui_kit.cjs` bằng Node có module `sharp`, hoặc cung cấp `--modules <node_modules>`.

Generator xuất cùng màu/kích thước sang `MenuTheme.uss` và `GuiKitPalette.cs`. Không sửa tay hai file sinh này. Source icon màu xanh để dễ xem trong Inkscape; PNG runtime màu trắng để tint. Text, số và nhãn phím không được bake vào ảnh.

Import preset chỉ áp dụng trong `GUIArt/Resources/UI/GuiKit`: Sprite Single, Full Rect, Clamp, Bilinear, alpha, sRGB, mipmap off, compression None, read/write off. Slice được lưu trong manifest. Không tự thêm tất cả vào một atlas; cần đo batch và kiểm tra hai hệ UI trước.

## Ranh giới

Đây là nền tảng GUI và màn mẫu, chưa phải đợt đổi toàn bộ Menu/Túi đồ. Các panel tiếp tục dùng binding và bố cục hiện tại. Data, gameplay, save, SampleScene và Spawn/Recall không thuộc đợt này.

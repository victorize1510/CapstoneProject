# BAG 22/10/2026

## Sử dụng

- Mở **Menu → Túi đồ**. Nút ở cuối hàng tìm kiếm đổi giữa ô vuông và hàng ngang.
- Đổi view giữ vật phẩm đang chọn, từ khóa, danh mục, sắp xếp và bộ lọc. Scroll được neo vào vật phẩm; thao tác này không lưu game.
- Chọn vật phẩm để xem thông tin. Mô tả dài cuộn trong inspector, các nút hành động ở vùng riêng.
- **Sử dụng** vật phẩm hồi phục mở controller hồi phục có sẵn, áp dụng đúng vật phẩm cho thú đang xuất chiến.
- **Gán phím tắt** mở hai ô R/T. Click ô hoặc nhấn R/T để lưu ngay. Gán không tiêu hao vật phẩm; lỗi save giữ popup và hoàn tác binding.
- **Bỏ** mở số lượng: 1, một nửa, tất cả. Chỉ trừ sau xác nhận và handler thành công. Vật phẩm không cộng dồn cố định 1; vật phẩm quan trọng/nhiệm vụ được bảo vệ theo controller hiện có.
- Vật phẩm tiến hóa dùng luồng **Thú đồng hành → chọn mục tiêu → xác nhận trong BAG**. Điều kiện/kết quả do PetEvolutionService cung cấp.
- Q/E đổi danh mục; ↑/↓ chọn vật phẩm; Enter thao tác; Esc đóng lớp hiện tại. Khi đang nhập từ khóa, phím nhập được dành cho TextField.

## Nguồn trình bày

`InventoryPanel.uxml` hiện có vẫn là template BAG trong `MonsterInventory.uxml`; giữ GUID và các tên binding.

- `BagSurface` / `BagButton`: lớp PNG fill/motif/border độc lập, polygon dùng chung cho render/state/picking, không đổi metrics khi hover/focus.
- `BagPresentation`: fit cục bộ canvas 1600×900, không đổi PanelSettings chung; modal/dropdown chỉ được theme trong ngữ cảnh BAG.
- `BagHealPresentation`: tạm đặt lại các binding hồi phục hiện có; khôi phục cây Companion và hủy callback fit khi đóng.
- `BagTooltip`: tooltip không nhận input, ưu tiên trên/dưới và kẹp trong viewport.
- `BagChoicePopup`: popup sắp xếp/bộ lọc có font và polygon của BAG, bind lại `DropdownField.value` hiện có. Phím ↑/↓/Enter/Esc thuộc popup, không kích hoạt action phía sau.
- Sprite vật phẩm lấy trực tiếp từ `InventoryItemSnapshot.Icon`; không tạo sprite vật phẩm thay thế.
- `ArtSource` lưu SVG và manifest từ gói `E:/AIWork/Capstone/BAG_2026-10-22`. Runtime chỉ tải các lớp trong `Resources/UI/BAG20261022`. Mask/state không dùng được dọn khỏi Resources; polygon/state được dựng bằng code.
- PNG phải giữ kích thước gốc: importer đặt **Non Power of 2 = None**, không mipmap, Clamp, Bilinear. Không thay đổi importer toàn project.

## QA

`Editor/BagVerification` nhận request có chủ đích tại `QaArtifactPaths.Root + bag-implementation-2026-10-22/request.txt`:

- `after`: chụp 9 trạng thái ở hai độ phân giải, chạy giao dịch thử, kiểm bounds và ProfilerRecorder trong Editor.
- `native`: quan sát thao tác chuột/bàn phím thật tối đa 300 giây, tối đa 10 frame cuộn; dừng bằng `native.stop` trong cùng thư mục.

QA copy save sang E và chuyển save store trước Start. Không dùng slot thật để thử giao dịch. Mã cô lập này chỉ chạy trong Editor; standalone cần cơ chế save thử riêng trước khi kiểm giao dịch.

Ảnh/report/log ở E, không trong Assets/Git. Kết quả kiểm thực tế và những bước còn thiếu xem REPORT.md trong thư mục artifact của task. Biên dịch thành công không thay thế kiểm hình ảnh hay thao tác runtime.


# Thiết kế lại tương tác Menu và Thú đồng hành

## 1. Mục tiêu và các quyết định đã chốt

Nâng chất lượng toàn bộ **Menu, ba mục Thú đồng hành và bốn popup**, gồm asset, typography, chuyển trạng thái và luồng thao tác. Giữ màu **thép–tím khói, đồng lạnh**, Menu uGUI và Companion UI Toolkit.

Các thay đổi được chốt:

- Thay ba tai tab bằng **thanh điều hướng nằm bên trong một panel cố định**.
- Avatar dock phủ kín khung; HP, cấp và marker hiển thị đè lên ảnh.
- Gán kỹ năng có bước xem trước và nút **Trang bị**, không thay ngay khi chọn ô.
- Đổi vị trí hỗ trợ click và kéo thả, có mũi tên nguồn/đích khác màu và bước xác nhận.
- Click avatar **chỉ đổi thú đang xem**, không tự đổi active pet hoặc yêu cầu lưu.
- Giữ hero 2D riêng do người dùng thêm; giữ biểu tượng BOX hai dấu chân và nền tròn.

### Những điểm đã xác định từ code

| Hiện trạng | Hướng xử lý |
|---|---|
| Focus luôn vẽ đường trắng, kể cả sau click chuột | Phân biệt chuột với điều hướng bàn phím; bỏ viền trắng |
| Hover Menu phủ màu sáng lên toàn thẻ | Thay bằng phản hồi ở viền, endcap và chevron; giữ nguyên màu ảnh |
| Tooltip đặt tại cạnh phải của nút | Đặt trên/dưới, đo kích thước thật và tránh vùng điều khiển |
| Tab được đổi `top/height`; skin cập nhật style và repaint lặp lại | Giữ hình học và chữ cố định, chỉ thay lớp trạng thái |
| Learned grid bị xóa rồi dựng lại khi refresh | Cập nhật theo thay đổi dữ liệu, giữ phần tử và trạng thái chọn |
| Click avatar gọi `SetActivePet` và yêu cầu lưu | Tách thú đang xem khỏi active/summoned |
| Kiểm cũ gọi handler và bỏ focus tab trước ảnh chụp | Bổ sung chuột thật, quay chuyển trạng thái và đo chữ khi hover |

**Chưa kết luận nguyên nhân duy nhất của chữ giật.** Đợt triển khai phải tái hiện và đo runtime trước khi đánh dấu lỗi đã sửa.

## 2. Thiết kế hình ảnh, asset và typography

### Thanh điều hướng mới

- Giữ inspector tại vùng thiết kế hiện có, kích thước tổng **864×620** trên canvas 1600×900.
- Một contour ngoài cố định, không còn ba đỉnh tab hoặc ba bộ frame thay nhau.
- Thanh điều hướng nằm trong panel: inset ngang 16, top 12, cao 48; ba mục rộng 272, cách 8.
- Cả ba mục giữ nguyên vị trí, kích thước, font và độ đậm ở mọi trạng thái.
- Mục chọn dùng nền tím khói nhẹ và chỉ báo đồng ở chân mục. Chỉ báo là decoration, không bắt input.
- Chuyển nội dung: tắt nội dung cũ trong 60 ms, hiện nội dung mới trong 100 ms; không trượt chữ, không chồng hai lớp chữ.
- Bấm nhanh nhiều mục: lựa chọn cuối thắng; hủy animation cũ, không khóa người dùng chờ hết hiệu ứng.

### Bộ trạng thái dùng chung

Thiết kế một bộ normal/hover/pressed/selected/focus/disabled cho cả uGUI và Toolkit:

- **Hover:** chuyển 140 ms; viền trong và endcap sáng nhẹ theo màu đồng, chevron được nhấn rõ hơn. Không phủ trắng/xám toàn ảnh, không giảm alpha chữ hoặc artwork.
- **Pressed:** phản hồi tối nhẹ ở mặt nút trong 80 ms; không dịch hoặc scale cả nút/chữ.
- **Focus bàn phím:** bracket đồng gọn theo contour, không đường trắng bao quanh.
- **Click chuột:** vẫn nhận focus phục vụ input nhưng không hiện trang trí focus bàn phím.
- **Selected:** độc lập với hover và focus; không mất khi rời chuột.
- **Disabled:** màu chữ rõ, icon khóa/lý do nếu phù hợp; không dùng giảm opacity toàn cây UI.
- TextField vẫn có caret và dấu nhận biết đang nhập, dùng viền đồng mảnh.

Trạng thái chỉ tác động các lớp decoration; nền, art mask, hit area và focus phải dùng cùng hình học.

### Avatar dock phủ kín ảnh

- Giữ sáu slot **210×112**, vị trí hiện tại; ảnh cover toàn polygon và cắt đúng góc.
- Không kéo méo ảnh; cho phép đặt crop/focal point trong cấu hình art riêng.
- Dải thông tin dưới cao khoảng 26 px, nền cơ bản opacity 18%.
- Chữ và HP giữ alpha 100%. Với ảnh sáng, thêm bóng chữ hoặc nền nhỏ cục bộ sau nội dung để đủ rõ; không làm tối toàn avatar.
- Slot có HP bên trái, cấp bên phải; tên đầy đủ nằm trong tooltip và inspector.
- Phân biệt marker:
  - Đang xem: accent tím khói.
  - Đang xuất chiến: dấu check riêng.
  - Yêu thích: ngôi sao đồng.
  - Nguồn đổi vị trí: mũi tên đồng.
  - Đích đổi vị trí: mũi tên xanh lam `#76A6C3`.
- Marker nguồn/đích có thêm nhãn “Nguồn/Đích”, không chỉ dựa vào màu.
- Ô trống giữ glyph BOX và vòng tròn gốc.

### Typography và căn chữ

Dùng font đã có trong project:

| Vai trò | Font | Cỡ logic | Độ đậm |
|---|---|---:|---|
| Tiêu đề màn/tên thú lớn | Noto Serif | 30–32 | Bold cố định |
| Tiêu đề khối | Noto Serif | 23–25 | Bold |
| Điều hướng/CTA | Liberation Sans | 20–21 | Bold |
| Nội dung chính | Liberation Sans | 18 | Regular |
| Metadata/helper/tooltip | Liberation Sans | 16 | Regular |
| Dock HP/cấp | Liberation Sans | 14–15 | Bold |

Quy tắc bắt buộc:

- Không thay font, cỡ, weight, spacing, padding hoặc border width khi hover/click.
- Mỗi nút chỉ có **một lớp caption thực sự được render**; loại tình trạng native text và caption tùy biến cùng xuất hiện.
- Chữ chính dùng màu đậm trên nền sáng, chữ sáng trên nền tối; không hạ alpha cả nhóm để tạo trạng thái.
- Tên thú/player: một dòng và ellipsis; tên đầy đủ xem qua tooltip.
- Tên kỹ năng: tối đa hai dòng, chiều cao thẻ cố định.
- Mô tả/lỗi: được xuống dòng trong vùng riêng; dài hơn vùng thì cuộn, không đẩy CTA.
- Giá trị stat, cấp, HP và số lượng căn phải; baseline icon–label đồng nhất.
- Chữ thường đạt tương phản tối thiểu 4,5:1 trên vùng đọc; kiểm riêng trên avatar sáng và tối.
- Giữ nhãn **CRIT RATE / CRIT DMG**.

### Asset bàn giao

Tạo revision mới, giữ gói LATEST làm nguồn tham chiếu; không ghi đè các bản thiết kế trước.

Cần có các component rời:

- Panel inspector cố định và thanh điều hướng.
- Indicator chọn, hover edge, pressed overlay, keyboard focus bracket.
- Frame/avatar mask, overlay thông tin dock.
- Mũi tên nguồn/đích và drag ghost.
- Tooltip surface, pointer và shadow.
- CTA Trang bị, Xác nhận, Hủy.

Ưu tiên SVG cho hình học và icon; PNG alpha cho vật liệu/artwork. Manifest ghi kích thước, contour, inset, layer và kiểu sử dụng. Không bake chữ/dữ liệu vào ảnh; không dùng screenshot làm UI sprite.

## 3. Luồng tính năng và quản lý trạng thái

### Tách thú đang xem khỏi active pet

Thêm context trình bày cho Companion, giữ tham chiếu thú đang xem và ID bền vững hiện có.

- Mở panel: mặc định xem active pet hợp lệ; nếu không có thì thú đầu tiên đang có.
- Click avatar chỉ cập nhật context này và inspector, **không gọi `SelectPetSlot`, `SetActivePet` hoặc save**.
- Không tạm đổi active pet rồi khôi phục để lấy dữ liệu.
- Active/summoned vẫn lấy trạng thái gameplay thật.
- Tăng cấp, hồi phục, tiến hóa, đổi tên, favorite và trang bị kỹ năng phải nhắm đúng thú đang xem.
- Dùng các overload `Open(pet)` đang có cho popup.
- Với thao tác hiện còn phụ thuộc active pet, tách helper nhận target rõ ràng và tái sử dụng đúng validation/save/rollback cũ.
- Giữ chữ ký API công khai và hành vi của các caller gameplay hiện tại; không sửa schema save, công thức hoặc luật item.

Thú bị xóa/chuyển khỏi đội trong lúc đang xem: hủy thao tác chờ, chọn lại mục hợp lệ và báo rõ; không tiếp tục dùng index cũ.

### Trang bị kỹ năng

Luồng mới:

1. Chọn kỹ năng đã học.
2. Chọn ô đích đang mở.
3. Inspector hiện kỹ năng cũ → kỹ năng mới.
4. Bấm **Trang bị** mới gọi lệnh lưu.

- Cho phép chọn kỹ năng hoặc ô đích trước.
- CTA chỉ bật khi cặp lựa chọn hợp lệ và thực sự tạo thay đổi.
- Hover chỉ xem nhanh, không thay lựa chọn đã chốt.
- Giữ lựa chọn theo `skillId` và target thú, không theo index của danh sách đã lọc.
- Trước xác nhận, resolve lại từ snapshot mới; nếu target/kỹ năng không còn hợp lệ thì hủy preview.
- Lưu thất bại: giữ dữ liệu cũ, hiện lỗi, cho thử lại; chống gửi hai lệnh khi click nhanh.
- Đổi thú hoặc rời mục Kỹ năng: bỏ preview chưa xác nhận. Giữ bộ lọc và vị trí cuộn trong phiên mở panel.

### Đổi vị trí bằng click và kéo thả

State machine: **Idle → Chọn đích → Sẵn sàng xác nhận → Đang lưu → Idle**.

- Chỉ bắt đầu bằng nút **Đổi vị trí**, nguồn là thú đang xem.
- Bỏ nhấp đúp để bắt đầu đổi chỗ; click/double-click ngoài chế độ đổi chỉ xem thú.
- Trong chế độ đổi, click ô khác chỉ chọn đích, không đổi inspector hoặc active pet.
- Hỗ trợ đổi với thú khác hoặc di chuyển sang ô trống theo service hiện có.
- Chọn chính nguồn: bỏ đích, giữ chế độ chọn.
- Kéo thả chỉ hoạt động sau khi vào chế độ đổi; threshold 8 đơn vị logic, dùng pointer capture.
- Drag ghost dùng sprite có sẵn, không tạo RenderTexture.
- Khi thả vào ô hợp lệ: hiện hai mũi tên và preview; **chưa đổi dữ liệu**.
- Thả ngoài dock: bỏ đích, không thực hiện lệnh.
- Khu vực action trong dock chuyển thành hai nút Xác nhận/Hủy xếp dọc, không đẩy sáu slot.
- Khi xác nhận, kiểm lại ID thú và vị trí hiện tại rồi gọi service đúng một lần.
- Sau đổi thành công, tiếp tục xem đúng thú theo danh tính, không chỉ theo số ô.
- Hủy/Esc, mất focus cửa sổ hoặc đóng panel phải giải phóng pointer capture/ghost và không lưu thay đổi chờ.

### Tooltip và popup

Dùng một cơ chế tooltip thống nhất cho phạm vi tám màn:

- Delay hiện 350 ms; ẩn ngay khi click, kéo, đổi mục, mở popup hoặc rời anchor.
- Tooltip không bắt picking/raycast, không chiếm focus.
- Ưu tiên phía trên nút, cách 10 px; không đủ chỗ thì phía dưới.
- Đo kích thước thật trước khi đặt; clamp cách biên màn hình 16 px.
- Không đặt đè anchor hoặc hàng nút liền kề. Nếu trên/dưới đều vướng, dùng vùng trợ giúp cố định trong panel.
- Chỉ một tooltip xuất hiện; tắt tooltip native trùng nội dung trong phạm vi đã quản lý.
- Kỹ năng đã chọn dùng inspector cố định; không để hover ghi đè preview đang chuẩn bị.
- Tooltip của nút khóa vẫn giải thích được lý do.

Esc xử lý theo thứ tự: popup trên cùng → drag/đổi vị trí chờ → preview trang bị → đóng panel. Một lần Esc chỉ xử lý một tầng.

## 4. Tổ chức code, tối ưu và thứ tự triển khai

### Đợt A — Tái hiện và ghi baseline

- Quay lỗi hover chữ, focus trắng, tooltip và chuyển mục bằng chuột thật.
- Ghi world bounds, cỡ/weight font và số lần layout/repaint khi đứng yên/lia chuột.
- Lập bảng luồng xem thú, đổi vị trí, trang bị và popup; ghi lỗi đã tái hiện riêng với nghi vấn.
- Sao lưu working tree và dùng profile/save thử nghiệm tách biệt cho các thao tác có ghi dữ liệu.

### Đợt B — Nền tảng hiển thị

- Hợp nhất state renderer của `AngularButton` và `GreenAngularGraphic`, có nhận biết phương thức input.
- Loại caption trùng, style hover kế thừa và các override USS chồng chéo trong phạm vi Companion.
- Dựng navigation rail, typography và asset state board trước khi nối animation.
- Khởi tạo skin/callback một lần; chỉ đổi thuộc tính khi giá trị thực sự thay đổi.

### Đợt C — Binding và tương tác

- Nối context thú đang xem, CTA trang bị, state machine đổi vị trí và tooltip.
- Learned grid cập nhật theo ID/diff, không `Clear()` và dựng lại khi chỉ HP/cooldown đổi.
- Giữ phần tử UI đang hover/focus để tránh mất pointer state và nhấp nháy.
- Cache geometry/triangulation theo shape và kích thước; resolve surface/material khi kích thước hoặc role đổi, không tìm asset theo chuỗi trong mỗi lần vẽ.
- Tách asset runtime đang dùng khỏi source/asset không sử dụng; giữ GUID và catalog art hiện có.
- 3D chỉ chạy khi người dùng chọn; đóng/rời panel giải phóng tài nguyên đúng vòng đời.

### Đợt D — Kiểm tích hợp và bàn giao

Chỉ sang bước đánh giá thẩm mỹ cuối sau khi luồng dữ liệu và tương tác đã ổn định. Không sửa gameplay ngoài việc loại bỏ tác dụng phụ không mong muốn của thao tác xem trong UI.

## 5. Kiểm thử và tiêu chí nghiệm thu

### Giao diện và chuyển trạng thái

- Đủ tám màn ở 1920×1080 và 1366×768.
- Quay normal → hover → pressed → release → leave; keyboard focus kiểm riêng.
- Lia chuột qua ba mục liên tục 30 giây: không đổi kích thước/weight chữ, không lặp layout làm chữ rung.
- Click chuột không để lại viền trắng. Điều hướng bàn phím vẫn có dấu focus rõ.
- Avatar sáng/tối, tên tiếng Việt dài, số lớn, thiếu ảnh/icon/provider, roster rỗng, slot khóa và CTA disabled.
- Không tràn chữ, cắt dấu tiếng Việt, tooltip che nút hoặc footer bị đẩy.
- Kiểm contour/mask/alpha trên nền sáng, tối và nhiều chi tiết; scale component 100/125/150%.
- Overlay/diff dựa trên **thiết kế revision mới**, không ép khớp ảnh tai tab cũ.

### Luồng có thay đổi dữ liệu

Chạy trên save thử nghiệm và tải lại để xác nhận:

- Chọn/xem thú không đổi active pet hoặc ghi save.
- Mọi action tác động đúng thú đang xem.
- Trang bị đúng ô, không đổi trước xác nhận; kiểm slot khóa, cùng kỹ năng, snapshot thay đổi và lưu thất bại.
- Đổi chỗ thú–thú, thú–ô trống bằng click và drag; hủy, thả ngoài, click nhanh, mất focus và target bị thay đổi.
- Tăng cấp/hồi phục/đổi tên/favorite thành công, thiếu item, HP đầy, lỗi tên và lỗi lưu.
- Không tiêu hao hoặc lưu hai lần; không để UI hiển thị thành công khi service thất bại.
- Đóng popup/menu khôi phục HUD, cursor và input; không còn tooltip/drag ghost hoặc callback đăng ký lặp.

### Hiệu năng và bằng chứng

- Profiler thật trước/sau trong cùng scene, cấu hình và chuỗi thao tác.
- Đo layout/style/render, GC, rebuild, draw calls và bộ nhớ; không suy FPS Release từ Editor.
- Hover thuần không được thay đổi layout; không phát sinh cấp phát lặp từ state logic sau warmup. Phần engine còn cấp phát phải được ghi riêng.
- Kiểm 50 chu kỳ mở/đóng, đổi mục và 2D/3D; xác nhận không tăng liên tục callback, element hoặc RenderTexture.
- Bàn giao: mockup/state board mới, SVG/PNG rời, manifest/layout-grid, ảnh runtime, video tương tác, log chức năng, Profiler, diff và danh sách file.
- Báo riêng phần đạt, sai khác còn lại và chưa chạy; không dùng tổng số assertion hoặc ảnh đã bỏ focus làm kết luận trải nghiệm đã hoàn thiện.


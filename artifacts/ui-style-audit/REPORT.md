# Rà soát UI và chuẩn hóa USS/UXML — 10/10/2026

## Phạm vi

Rà soát các panel đã có: Trainer Menu, Profile, BOX, PETS, BAG/INVENTORY,
MAP, QUEST và các màn phụ Skills, Details, Level Up, Heal, Evolution, Release.
Shop/Codex chưa triển khai nên không dựng thêm trong lượt này.

BAG/INVENTORY là một màn. Trainer Menu, Profile và MAP dùng uGUI; chúng không
có USS/UXML. Lượt này chuẩn hóa tài nguyên UI Toolkit và đánh giá các màn uGUI,
giữ thiết kế/màu hiện tại cùng các luồng gameplay đã chốt.

## Những thay đổi đã thực hiện

1. Tách stylesheet theo trách nhiệm. `MonsterInventory.uss` trước đây chứa cả
   BOX/PETS/BAG/QUEST và nhiều lớp sửa chồng. Nay chỉ giữ ghép màn, thứ tự
   template và hộp xác nhận tiến hóa từ BAG.
2. Đưa control dùng chung sang `MenuCommon.uss`; theme tiêu đề/Back sang
   `MenuTheme.uss`; popup số lượng sang `InventoryQuantityPopup.uss`.
3. Đưa hình học fullscreen về file của từng panel. `MenuScreenHeader.uss`
   sở hữu bố cục và trạng thái header, không quyết định kích thước thân màn.
4. Loại **1.324 khai báo bị ghi đè** trên cùng selector/property. Giữ thứ tự
   các selector khác nhau và các override có chủ đích. Đây là giảm nợ bảo trì;
   chưa đo CPU/memory để tuyên bố tăng FPS.
5. Chuẩn hóa thụt lề UXML/USS, mỗi khai báo style một dòng, giữ tên element,
   template và binding mà controller đang query.
6. Chuyển hai `display:none` mặc định của BAG từ UXML sang class USS.
   Runtime vẫn quyết định hiển thị theo item đang chọn.
7. Mark trang trí không nhận picking/focus; thêm trạng thái focus của Back
   bằng màu viền, không đổi độ dày làm xê dịch layout.
8. Thay kiểm tra BOX dựa vào chuỗi USS đúng khoảng trắng bằng kiểm tra bounds
   thực của viewport. Thêm so sánh resolved style/layout trong Unity.

Hướng dẫn bảo trì nằm ở
`Capstone/Assets/ThangTest/UIThang/Inventory/UI/README_UIStyles.md`.

## Đề xuất theo panel

P1: nên làm trước đợt hoàn thiện UI tiếp theo. P2: cải thiện trải nghiệm. P3:
tối ưu khi dữ liệu thực đã đủ lớn. Các mục dưới là đề xuất, chưa tự thêm gameplay.

| Panel | Ưu tiên | Nên làm thêm/chỉnh | Lý do và cách kiểm tra |
| --- | --- | --- | --- |
| Trainer Menu | P1 | Dùng nhãn/icon thống nhất với các màn con; chuẩn hóa focus và chọn bằng bàn phím | Tránh chữ viết tắt và trạng thái hover/focus khác nhau. Kiểm tra Tab/Enter/Esc; giữ menu rộng 35%, HUD phía dưới và camera tracking |
| Profile | P1 | Hoàn thiện nguồn dữ liệu Lv/EXP/các thống kê chưa có, phân biệt “chưa có dữ liệu” với giá trị 0 | Hiện dấu `-` chưa thể đánh giá là lỗi giao diện. Tiến trình truyện hiện tính theo số Main Quest trong catalog; cần catalog chính thức trước khi hiểu 100% là hết game |
| Profile | P2 | Tên khu vực hiển thị theo dữ liệu game, thay tên scene kỹ thuật | Người chơi nên thấy tên vùng, không phải `TestChar`; kiểm tra chuyển scene/vùng |
| PETS | P1 | Căn preview để vừa thân/cánh từng pet; kiểm tra tên dài, sáu slot và nhãn đang xem/đang dùng | Camera preview đang lấy hình trong world nên dễ cắt mất model. Đây là công việc preview camera, không chữa bằng USS |
| PETS | P2 | Tooltip cho chỉ số/hệ và nhãn lựa chọn không chỉ dựa vào màu | Giải thích ATK/SP.ATK và tránh nhầm viền chọn với ACTIVE; giữ sao chỉ cho pet fav |
| BOX | P1 | Đồng bộ TextField tìm kiếm và footer thao tác ở chiều cao thấp | Kiểm tra với dữ liệu thật tại 1366x768, tên dài và panel chi tiết cuộn; không để nút nhận/thả bị che |
| BOX | P2 | Nút xóa tìm kiếm, phân biệt ô trống với kết quả lọc rỗng | Người chơi biết vì sao không thấy pet; giữ double-click/đổi vị trí/fav |
| BOX | P3 | Pool/ảo hóa card khi số pet tăng lớn | Chỉ triển khai sau khi profiler chứng minh rebuild/layout tốn thời gian; kiểm tra đúng pet sau cuộn và đổi trang |
| BAG / INVENTORY | P1 | Đồng nhất ngôn ngữ tiêu đề/danh mục và thông báo lọc rỗng | Hiện có tiếng Anh/Việt lẫn nhau. Giữ ALL, túi vô hạn, trạng thái chưa chọn chỉ hiện lời nhắc |
| BAG / INVENTORY | P2 | Nút xóa tìm kiếm, nhãn lý do không dùng được và bố cục toolbar khi màn hẹp | Tách “hết item”, “không dùng được ở đây” và “không tìm thấy”; quick slot vẫn chỉ cho nhóm item hợp lệ |
| QUEST | P1 | Thêm lời nhắc ở vùng chi tiết khi chưa chọn nhiệm vụ/nhóm không có nhiệm vụ | Hiện controller ẩn detail khi không có lựa chọn; vùng trống cần thông điệp rõ, không dựng nhiệm vụ giả |
| QUEST | P2 | Đồng nhất trạng thái nhiệm vụ, nhãn phần thưởng và focus | Kiểm tra tên dài/nhiều mục tiêu; giữ kiểm tra và cấp reward một lần tại transaction |
| MAP | P1 | Giãn/nhóm marker khi nhiều NPC/shop/pet cùng vị trí | Tránh icon và label chồng nhau. Kiểm tra zoom/filter/chọn marker; giữ map vị trí thật |
| MAP | P2 | Tooltip marker có tên/hành động; tên vùng và nhãn filter thống nhất | Tách tọa độ khỏi tên vùng; kiểm tra marker biến mất khi lọc và clear waypoint |
| Skills / Details | P2 | Thống nhất thuật ngữ, đơn vị chỉ số, footer và focus | Kiểm tra tên skill dài, mô tả nhiều dòng và cuộn không che nút |
| Level Up / Heal | P1 | Hiển thị rõ chi phí/số lượng trước và sau, lý do nút khóa | Giao diện phải phản ánh kiểm tra ở điểm thực thi; không tự thay quy tắc tiêu hao |
| Evolution | P1 | Giữ cảnh báo cấp và tiến độ ngay cạnh xác nhận; nhãn riêng cho luồng cấp/item | PETS và popup cấp chỉ xác nhận Level; Stone đi qua BAG. Kiểm tra 1/18,18/18,18/36,36/36 |
| Release | P1 | Việt hóa title/Level/Rarity; xác nhận nhận diện đúng pet và tính không hoàn tác | Kiểm tra pet fav/active theo luật hiện hành; không kiểm thử layout bằng thả pet thật |

## Quy tắc chung nên áp dụng tiếp

- Có một bảng thuật ngữ/nguồn localization cho cả uGUI và UI Toolkit.
- Theme màu/font/spacing dùng chung có thể chuyển thành dữ liệu cho cả hai hệ
  UI; hiện theme USS chỉ dùng cho UI Toolkit.
- Kiểm tra bàn phím: thứ tự focus, Enter/Esc, trả focus về nút mở popup, không
  nhận lệnh gameplay khi nhập chữ.
- Dùng tên dài, số lượng lớn, thiếu icon và danh sách rỗng trong bộ dữ liệu QA.
- Kiểm tra font tiếng Việt và các glyph đặc biệt; icon chữ cần fallback hoặc
  sprite project khi font không có glyph.
- Mở rộng QA cho 16:10/ultrawide và tăng cỡ chữ sau khi quyết định hỗ trợ các
  cấu hình đó. Hai độ phân giải 16:9 chưa chứng minh mọi màn hình đều đạt.

## Kiểm tra

- Static audit: PASS XML, stylesheet/template/font references, tên binding được
  giữ nguyên, không trùng tên local, không inline presentation/unsupported CSS.
- Unity resolved comparison: **4.916 element, 0 khác biệt** tại **1920x1080 và
  1366x768**, 11 tài liệu. `resolved/baseline.json`, `resolved/current.json` và
  `resolved/changes.txt`. Bản gốc được dựng lại từ snapshot nguồn sau khi Unity
  dọn Temp khi khởi động lại; đường dẫn font tương đối trong bản tạm được đổi
  để trỏ đúng cùng asset. Snapshot nguồn được giữ nguyên.
- Play Mode TestChar: **62 PASS**, mở/đóng Menu/Profile/BAG/PETS/BOX/QUEST/MAP,
  callback rebind, refresh ẩn, input restoration. `lifecycle-validation.txt`.
- Quan sát runtime: PETS đầy đủ sáu slot/fav/thanh HP, BAG chưa chọn/chọn item,
  QUEST nhóm rỗng và preview Level Up. Ảnh tương ứng nằm cùng báo cáo.
  Không xác nhận tăng cấp, dùng/bỏ item, tiến hóa hoặc thả pet.
- Unity import/compile thành công. Lỗi thiếu baseline ở lần kiểm tra đầu sau
  restart đã được xử lý bằng nơi lưu bền và guard; lỗi không xảy ra ở lần so
  sánh cuối. Chưa ghi nhận parser warning trên stylesheet đang dùng.
- C# build Editor: **0 errors**, **187 MSB3277 warnings** về xung đột phiên bản
  dependency/plugin hiện có. `build.log`. Không phải build executable game.

Chi tiết từng khai báo bị loại: `migration.txt`. Backup gốc: `baseline/`.
Style comparison chỉ bao phủ thuộc tính và trạng thái mặc định được đo; không
thay thế kiểm tra hover/focus/dữ liệu động hay tất cả tính năng gameplay.

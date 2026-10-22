# Quy trình tạo GUI dành cho Codex — Capstone

Ngày khảo sát: 13/10/2026. Tài liệu kế hoạch; chưa tạo artwork, tải gói GUI, cài plugin hoặc chỉnh giao diện trong project.

## 1. Khả năng và hướng làm

Có thể tự thiết kế và dựng component Unity, tạo icon/khung dạng vector, tạo ảnh minh họa/hoa văn bằng công cụ tạo ảnh, tìm tài nguyên có nguồn và giấy phép rõ khi cần. Thứ tự ưu tiên: **tạo phù hợp project → tái dùng tài nguyên hiện có đạt yêu cầu → tìm/tải phần còn thiếu**.

Ưu tiên tạo không có nghĩa dùng AI raster cho mọi thứ. Hình học, khung, đường viền và icon đơn giản được dựng bằng vector hoặc hình UI native để kiểm soát kích thước. Công cụ tạo ảnh dành cho minh họa và trang trí. Chữ, số, thanh tiến độ, nút và trạng thái được dựng bằng element thật trong Unity.

Project đang dùng Unity 6000.3.16f1, URP 17.3.0 và cả UI Toolkit/uGUI. Tiếp tục theo hệ của từng panel; không chuyển toàn bộ UI hoặc đổi package chỉ để áp dụng asset mới. Nguồn SVG được giữ chỉnh sửa được; PNG là phương án xuất dùng chung cho hai hệ. Chỉ dùng SVG trực tiếp trong runtime sau khi kiểm tra importer và render trên đúng phiên bản project.

Không cam kết tuyệt đối không có lỗi. Component và asset phải qua các cổng kiểm tra dưới đây, lỗi được ghi và sửa trước khi chuyển sang nhóm kế tiếp.

## 2. Bảng thực hiện

| Bước | Công việc Codex phải làm | Đầu ra | Điều kiện qua bước |
|---|---|---|---|
| 1. Chốt phạm vi | Liệt kê màn/component cần tạo, phần cần giữ, trạng thái và luồng mở/đóng. Ghi ranh giới data/gameplay | Scope và danh sách hành vi | Không có quyết định ngầm thay vị trí, tỷ lệ, save hay điều khiển đã chốt |
| 2. Khảo sát hiện trạng | Đọc UXML/USS/C#, theme, font, atlas, import settings, binding và controller. Chụp baseline đúng độ phân giải | Bảng tài nguyên và ảnh trước | Xác định rõ mỗi element thuộc hệ nào và ai sở hữu logic |
| 3. Thiết kế bộ quy chuẩn | Chốt màu, font, tỷ lệ icon, độ dày viền, khoảng cách, radius, opacity và các trạng thái | Design tokens và bảng mẫu | Màu/độ trong hiện tại được giữ trừ phần được yêu cầu đổi |
| 4. Danh mục asset | Tách core UI, icon, khung, ornament, background và illustration. Ghi kích thước sử dụng lớn nhất, padding, slice và định dạng | Asset manifest | Mỗi asset có mục đích, consumer, source và tiêu chí riêng |
| 5. Wireframe | Dựng bố cục đơn giản bằng component thật, xem vùng cuộn và hierarchy | Wireframe ở các tỷ lệ màn hình | Title/Back/footer luôn có chỗ; luồng thao tác rõ, không chồng hitbox |
| 6. Mẫu chuẩn | Tạo một header, button, card/slot, popup và icon đại diện; thử mọi trạng thái | Bộ component chuẩn trong màn preview | Cỡ icon/viền/độ tương phản đạt ở kích thước chơi thật trước khi nhân rộng |
| 7. Tạo vector/native | Vẽ icon hình học, mũi tên, sao, dấu đóng, khung và separator theo một lưới | Source SVG và component style | Cùng viewBox, optical weight và khoảng trống; không đứt nét hoặc lệch tâm |
| 8. Tạo raster | Dùng cùng bộ tham khảo/prompt cho minh họa, nền và hoa văn; tạo từng asset có mục đích rõ | Master PNG có alpha nếu cần | Đúng phong cách, không chữ rác/watermark/checkerboard giả; không cắt đổ bóng |
| 9. Soát mép và xuất | Kiểm tra alpha trên nền sáng/tối/màu game; nhìn 100%, phóng lớn và kích thước cuối. Xuất đủ độ phân giải từ source | Asset runtime và ảnh QA | Không viền trắng/đen, halo bất thường, màu lạ ở mép hoặc dấu ghép |
| 10. Import pilot | Import một icon, một khung sliced và một ảnh minh họa trước. Chỉnh filter, alpha, compression, slice, atlas | Preset import đã thử | Nét và màu ổn trong cả UI Toolkit/uGUI liên quan; góc khung không méo |
| 11. Tích hợp component | Dựng UXML/prefab, USS/theme và binding có tên ổn định; dùng chung mẫu đã đạt | Component hoạt động | Decoration không bắt input; text và icon giữ độ rõ khi nền có alpha |
| 12. Nối hành vi | Nối API hiện có, focus, Esc/Back, kiểm tra trạng thái khóa và chống lặp ở điểm thực thi | Controller/binding | Không callback kép, click xuyên modal, lệnh gameplay khi nhập chữ, hoặc trừ item lặp |
| 13. QA đồ họa/bố cục | Kiểm tra độ phân giải, DPI, UI scale, tên dài, thiếu ảnh, số lớn và các trạng thái | Ma trận QA và ảnh thật từ Game View/build | Không clipping, kéo méo, sai tỷ lệ, mất dấu hoặc bị cuộn che nút |
| 14. QA hiệu năng | Đo CPU/GPU, render preview, batches, texture memory, GC.Alloc và mở/đóng nhiều lần | So sánh trước/sau với điều kiện đo | Không refresh liên tục khi ẩn; không giữ camera/texture/callback sau khi đóng |
| 15. Tích hợp từng nhóm | Mở rộng mẫu đã đạt sang các panel; sửa setup tools để không dựng lại kiểu cũ | Nhóm panel đã nghiệm thu | Kiểm tra lại màn vừa đổi và các màn dùng chung component |
| 16. Bàn giao | Lưu source, import preset, manifest, license, screenshot, danh sách giới hạn và rollback | Gói tài nguyên + báo cáo | Bản chơi kiểm tra được; asset runtime nằm trong workspace; tham chiếu/GUID ổn định |

## 3. Quy chuẩn đồ họa đề xuất

Các giá trị sau là điểm khởi đầu để thử, không phải cấu hình áp dụng hàng loạt.

### Tỷ lệ và kích thước

- Thiết kế theo mốc 1920×1080 của project. Kiểm tra 1366×768, 1920×1080, 1920×1200 và 2560×1080; thêm mức UI scale và DPI khác nhau.
- Header hiện có: cao 76 đơn vị thiết kế, title 38, Back 146×42. Đây là baseline giữ để đối chiếu, không nhân đôi scaling giữa Canvas/PanelSettings và Transform.
- Lưới khoảng cách đề xuất 4/8/12/16/24/32; chọn giá trị theo nhóm thay vì mỗi ô có một kiểu.
- Icon nguồn cùng viewBox, chẳng hạn 64×64; export raster ở 1×/2× theo kích thước sử dụng lớn nhất. Nếu hiển thị 32–48 px, thử PNG 64/128 px rồi đánh giá tại kích thước thật.
- Khung 9-slice có vùng góc cố định; artwork lá/hoa ở góc được tách thành ornament riêng khi cần. Độ dày viền nhìn trên màn hình phải được kiểm tra sau scale.

### Chống lem, halo và sai màu

1. Xuất PNG RGBA thật; nền caro chỉ dùng để xem alpha. Kiểm tra alpha channel, không chấp nhận ảnh đã vẽ caro vào pixel.
2. Kiểm tra trên ít nhất nền trắng, gần đen và nền ngà/xanh của game. Kiểm tra thêm khi chồng lên cảnh 3D sáng/tối.
3. Giữ đủ khoảng trống quanh bóng/glow; đo biên alpha để phát hiện phần bị cắt. Padding nguồn và padding atlas là hai vấn đề riêng.
4. Với UI màu thông thường, thử sRGB; alpha giữ vai trò mask. Kiểm tra màu trong Game View và build, kể cả khác biệt material tint giữa hai hệ UI.
5. Khung và icon bắt đầu với compression None để có bản chuẩn. Chỉ giảm kích thước/nén sau khi so sánh ảnh và memory; không mặc định để mọi ảnh 4K hoặc tất cả texture không nén.
6. Atlas sprite bắt đầu thử padding 4–8 px, Alpha Dilation; với Canvas UI tắt rotation. Tăng padding hoặc tách atlas khi ảnh thực tế còn bleeding; không hứa một con số chữa mọi trường hợp. [Unity: Sprite Atlas](https://docs.unity.com/en-us/engine/6000.3/manual/unity2d/sprite/atlas/sprite-atlas-reference).
7. UI screen-space bắt đầu thử Clamp, mipmap off, Bilinear; pixel art thử Point. Nền lớn, UI trong thế giới hoặc ảnh minify mạnh có thể cần cấu hình khác. Chốt bằng quan sát ở kích thước cuối.
8. Kiểm tra slice trên khung hẹp/rộng/cao. Slice trong USS phải được rà cùng border trong Sprite Editor để tránh cấu hình không khớp. [Unity: image import settings](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-image-import-settings.html).

### Font và icon

- Chọn font thật có bộ dấu tiếng Việt, các weight cần dùng và giấy phép phù hợp. Giữ cùng họ chữ/hệ kích thước giữa uGUI, TMP và UI Toolkit; mỗi hệ vẫn cần cấu hình riêng.
- Test đủ chữ thường/hoa có dấu, Đ/đ, chuỗi dấu kết hợp khi nhập, số, %, +/−, HP/MP/EXP và tên dài. Xem glyph trong build, không chỉ font preview.
- Xác định font atlas/fallback theo tập ký tự và khả năng nhập tên. Không dựa vào font chỉ có trên máy phát triển. [Unity: Font assets](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-font-asset.html).
- Icon thao tác dùng sprite/vector trong bộ chuẩn. Giữ ý nghĩa qua màu, hình và nhãn; không phụ thuộc riêng màu đỏ/xanh hoặc emoji/font hệ điều hành.

## 4. Kiến trúc và input

- UXML/prefab: cấu trúc. USS/theme: trình bày. Controller/presenter: binding và cập nhật. Provider/service: trạng thái và dữ liệu.
- Cache element query, sử dụng tên binding ổn định. Bind/unbind đối xứng. Chỉ cập nhật vùng đổi; đóng màn thì dừng preview và cập nhật trang trí.
- Mỗi thuộc tính quan trọng có nguồn sở hữu rõ. USS override được rà cascade trước khi gom/xóa. Không thay controller bằng một lớp abstraction lớn chỉ để dùng vài Button.
- Mỗi button có Normal/Hover/Pressed/Disabled/Focus; card thêm Selected/Empty/Unknown/Favorite theo tính năng. Sao card chỉ hiện khi yêu thích.
- Decoration dùng picking-mode Ignore hoặc tắt raycast; modal có blocker đúng lớp. Phần mờ không làm mờ text/icon con ngoài chủ đích.
- Tab/Shift+Tab và submit thống nhất; popup trả focus về opener. Kiểm tra sự kiện điều hướng thực tế của phiên bản Unity, không chỉ mô phỏng KeyDown.
- Esc/Back xử lý popup trên cùng trước rồi trở về màn trước. Trainer Menu giữ quy tắc player dừng, camera theo dõi và HUD bên dưới.
- Giữ chống lặp 0,15 giây ở action boundary; không chặn nhập chữ, kéo/cuộn hay nhận diện double-click.
- Giai đoạn GUI không tự thêm dữ liệu/gameplay chưa được duyệt; thông tin thiếu có nhãn rỗng/Chưa rõ.

## 5. Ma trận nghiệm thu

| Nhóm | Các trường hợp bắt buộc | Lỗi cần bắt |
|---|---|---|
| Asset | Nền sáng/tối/game, kích thước thật, phóng lớn, alpha, atlas pack | Halo, bleeding, checkerboard giả, đứt/cắt glow |
| Layout | 4 viewport, DPI khác nhau, UI scale, đổi kích thước cửa sổ | Hai lần scale, clipping, tràn footer, lệch title/Back |
| Nội dung | Tên dài, nhiều dòng, 0/số lớn, thiếu ảnh, rỗng/không có kết quả, fixture 300+ | Nhảy layout, mất dấu, fake data, text không đọc được |
| Trạng thái | Các trạng thái button/card và filter | Disabled trông như active, favorite outline còn sót, sai nhãn chọn |
| Input | Chuột, phím, scroll, drag, double-click, IME, mất/nhận focus | Click xuyên, Tab thoát modal, lệnh gameplay trong ô nhập |
| Lifecycle | Mở/đóng, disable/enable, reload scene, hủy sequence liên quan | Callback kép, render texture/camera sót, lock không trả |
| Hiệu năng | Panel đóng/mở, marker/ảnh nhiều, animation preview, build đại diện | CPU/GPU/GC tăng vô cớ, batches/atlas/memory vượt mức đã chốt |
| Build | Unity import/Console, UXML/USS, C#, chạy build | Missing glyph/shader/asset, editor fixture lọt vào game |

Không dùng một API đo bộ nhớ nếu chưa kiểm tra bằng cấp phát đối chứng. Counter .NET trong Unity Mono hiện tại đã trả 0 sai lệch; dùng Profiler/GC.Alloc và xác thực cách đo. Atlas có thể giảm batch break, nhưng vẫn phải kiểm tra texture đủ điều kiện và atlas viewer/frame debugger. [Unity: runtime UI performance](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-performance-consideration-runtime.html), [dynamic atlas](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-control-textures-of-the-dynamic-atlas.html).

## 6. Tổ chức đầu ra dự kiến

```text
ArtSource/GUI/                         # source chỉnh sửa, master và các bản lựa chọn
  Icons/
  Frames/
  Illustrations/
  asset-manifest.json
  LICENSES.md
Assets/ThangTest/UIThang/GUIArt/         # asset runtime đã kiểm tra
  Icons/
  Frames/
  Decor/
  Fonts/
  ImportPresets/
artifacts/gui-production/              # kế hoạch, ảnh QA và báo cáo
```

Đây là cấu trúc đề xuất; chưa tạo các thư mục asset. Template/USS/controller tiếp tục ở chỗ đang dùng, không di chuyển hàng loạt. Manifest ghi đường dẫn source/runtime, kích thước, alpha, slice, consumer, revision và nguồn/license. Giữ `.meta` GUID khi cập nhật asset đã được tích hợp.

## 7. Thứ tự triển khai khi được yêu cầu

1. Bộ chuẩn: font, icon, header/Back, button, slot và popup.
2. Pilot: Trainer Menu và một màn toàn màn hình, thử hai hệ UI.
3. Nhân rộng sang PETS/BOX/BAG/Profile/Quest/Codex/Map theo nhóm, giữ layout đã chốt.
4. Toast và hướng dẫn thao tác.
5. Cài đặt và polish nhẹ; data, Shop và gameplay mới chỉ làm theo phạm vi riêng.

Mỗi nhóm có ảnh và kiểm tra đạt trước khi mở rộng. Không tạo hàng chục icon trước khi bộ mẫu ổn.

## 8. Công cụ, skill và plugin — báo cuối

### Công cụ và skill tối thiểu

| Công cụ/skill | Vai trò | Có cần cài thêm? |
|---|---|---|
| Unity + UI Builder/Sprite Editor/Profiler/Frame Debugger | Dựng UI thật, slice, font, thử build, đo CPU/GPU/atlas | Unity và các package UI đã có trong project |
| `unity-3d-production` | Tích hợp theo pipeline/asset ownership và kiểm tra Unity | Skill đã có |
| `imagegen` + công cụ tạo ảnh tích hợp | Tạo minh họa, hoa văn, asset raster có alpha | Có sẵn; không cần plugin tạo ảnh riêng cho đường mặc định |
| Biên tập SVG/code và kiểm tra file | Hình học/icon/khung có source, manifest và QA alpha/kích thước | Công cụ code đã có; renderer SVG cần xác nhận khi triển khai |
| `computer-use` | Kiểm tra Game View/Inspector/UI Builder khi cần thao tác Editor | Có sẵn |
| `plugin-management` | Kiểm tra khả năng và tình trạng plugin thay vì đoán | Có sẵn |

Inkscape/Illustrator có thể hỗ trợ chỉnh/xuất vector nhưng không bắt buộc cho thiết kế source bằng SVG. Chưa thấy lệnh Inkscape/ImageMagick/Blender trên PATH; không suy ra chúng chắc chắn chưa cài. Không cài công cụ chỉ để lập kế hoạch.

### Plugin tùy chọn đã khảo sát

| Plugin | Tình trạng tìm được | Dùng khi nào |
|---|---|---|
| Adobe | Đã cài; chưa xác minh đăng nhập cho từng tác vụ | Nếu cần workflow Creative Cloud, font hoặc chỉnh asset bằng Adobe theo yêu cầu |
| Canva | Đã cài; chưa xác minh tài khoản | Nếu cần bảng tham khảo/mẫu để review; không thay công đoạn tích hợp Unity |
| tldraw | Đã cài; chưa xác minh phiên làm việc | Wireframe/sơ đồ luồng nếu cần canvas chung |
| Figma | Tìm thấy, chưa cài | Tùy chọn nếu cần file thiết kế có layer/component và cộng tác; không phải điều kiện bắt đầu |
| Unity MCP | Package đã có trong project; lịch sử kiểm tra có handshake warning | Chỉ dùng sau khi xác minh kết nối; có Editor tools/Computer Use làm phương án sẵn có |

Kết luận: **không cần cài thêm plugin để bắt đầu quy trình cơ bản**. Cần hình raster thì dùng tạo ảnh tích hợp; cần source và component thì dùng vector/code/Unity. Plugin bên ngoài được chọn theo bước thật sự cần và chỉ dùng sau khi xác nhận kết nối.

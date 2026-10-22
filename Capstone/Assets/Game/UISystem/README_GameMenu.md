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

Trainer Menu V4 dùng khung thiết kế 1672×941 theo `01_MENU_V4.png`, co đồng tỷ lệ và căn giữa viewport (letterbox khi khác aspect). Một shell xanh đá liên tục bao hồ sơ inset và toàn bộ menu. Surface xanh ngọc/mineral sky, chữ/icon navy opaque; không lót nền trắng. Hai thẻ chính: Thú đồng hành / Túi đồ; lưới phụ 2×2: Hồ sơ / Nhiệm vụ, Bản đồ / Cẩm nang. Footer có Cài đặt và Trở lại; chỉ hiện keycap Tab. Kho thú giữ đường vào qua NPC, không có entry trong Menu.

`TrainerMenuView` chỉ dựng/bind UI; `GameMenuController` giữ input, provider, điều phối và khóa gameplay. Khi Menu mở, `TrainerMenuPresentation` ẩn nhóm HUD bằng CanvasGroup, ẩn render nhãn world/debug và quest tracker. Nó lưu/khôi phục alpha, interactable, raycast, forceRenderingOff và visibility ban đầu; không disable component gameplay. Minimap dùng visibility request hiện có, giữ tùy chọn người dùng. Player giảm tốc rồi dừng; camera tiếp tục theo player/mục tiêu ghim, khóa thao tác xoay/zoom mới. Panel toàn màn hình giữ quy tắc khóa gameplay/cursor riêng.

### Asset Menu và ảnh Thú đồng hành

Chín PNG gốc từ `ẢnhGUi/ảnh ui thô sơ/Menu/Assets` nằm trong `Assets/ThangTest/UIThang/GUIArt/Resources/UI/TrainerMenu`. V4 dùng hai border mới 10/11 trong thư mục con `V4`, crop alpha bounds bằng Sprite metadata, border 180 native px và 9-slice; góc giữ tỷ lệ, rail có offset hiệu chỉnh riêng với hitbox. Các khung 06/07 cũ được giữ làm lịch sử, không dùng trong V4. `Polish` tiếp tục cung cấp skin không viền, icon, avatar ring, sky và Noto Serif. Ribbon V4 có SVG nguồn tại `ẢnhGUi/GUI_Source/TrainerMenuV4`. Không dùng ảnh mockup làm nền runtime. Card chỉ có một lớp viền art, caption riêng. Vignette neo phải, fade trái 22% trên mesh; text/icon không giảm alpha theo background. Importer chỉ áp dụng thư mục Menu, tắt mipmap/Read-Write, giới hạn texture theo vai trò.

Để thêm ảnh pet: chọn object `GameMenu`, gán sprite 2D vào **Trainer Pet Artwork**, rồi vào Play Mode (hoặc dùng **Rebuild Game Menu** trong context menu component ở Edit Mode). Chỗ ảnh trống mặc định, không có upload, dấu cộng hoặc model 3D. Ảnh này chỉ là minh họa thẻ Menu, không thay avatar của từng pet.

### Kiểm tra Menu V4

`Game Tools > GameToolThang > HUD > Validate Trainer Menu V4 Layout`: fixture riêng, hai kích thước 1920×1080 và 1366×768, tên dài, thiếu provider, màu V4, asset import và hit target. Không sửa scene/save.

`Validate Trainer Menu V4 in Play Mode`: kiểm tra Tab, tám nút điều hướng và bút sửa tên tái sử dụng modal Hồ sơ, mở–quay lại bảy panel, focus, HUD/minimap/nhãn thế giới và khóa gameplay. Không tiêu hao vật phẩm hoặc xác nhận sửa tên. Kết quả hiện tại ở `artifacts/trainer-menu-v4`; các đợt cũ giữ riêng. `TrainerMenuPolishCapture` chụp Game view thật tại hai fixed resolution, xuất bounds screen-space và trả size về ban đầu. Setup Main Menu gọi cùng controller/view mới.

Để chạy qua file request trong Unity đang mở: `Temp/RunTrainerMenu.request` chứa `layout` hoặc `play`; `Temp/CaptureTrainerMenu.request` chứa `before`/`after`; `Temp/ProfileTrainerMenu.request` chứa `profile`. Chạy từng tác vụ riêng. `TrainerMenuProfiler` chỉ bật khi có request, thu ProfilerRecorder + binary `.raw` trong Editor Play Mode, 120 warmup/240 đo mỗi trạng thái đóng/mở và 120 frame chu kỳ tại mỗi độ phân giải. Log ghi counter thực có/không có, cấu hình máy, median/p95 và camera. Nó trả lại thiết lập Profiler/Game view/Menu và thoát Play nếu tự vào. Đây là số đo Editor có overhead, không phải benchmark Release. So ảnh bằng `artifacts/trainer-menu-v4/compare.cjs`: reference giữ aspect, mask dữ liệu thật, bảng bounds và heuristic đo rail riêng. Guide thủ công không chứng minh mọi nét render khớp ≤2 px.

## Các màn

- Túi đồ, Thú đồng hành, Kho thú, Nhiệm vụ: các template của `MonsterInventory.uxml` và controller riêng.
- Hồ sơ: `ProfilePanelController`; Bản đồ: `MapInputController`/`WorldMapUIController`.
- Cẩm nang Pet: `PetCodexPanelController`, thay placeholder cũ. Runtime chưa có provider thì hiện trạng thái rỗng; xem thử chỉ trong Editor (xem README_Codex).
- Cửa hàng và Cài đặt có controller riêng dùng UI Toolkit. Shop chưa nhập giá thì không cho mua; Cẩm nang chưa nối provider thì hiện trạng thái rỗng.

## Font và cập nhật

Menu dùng Noto Serif Regular riêng cho tiêu đề/nhãn và LiberationSans cho thông tin nhỏ; icon đặc của Menu là SVG nguồn xuất PNG có alpha. Font của Hồ sơ, HUD, compass và các panel khác vẫn lấy LiberationSans qua `MenuTypography`/`MenuTheme.uss`. Không sửa theme hay font toàn project. Menu có skin jade/navy riêng cho footer; các panel khác giữ nút Trở lại đang dùng.

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

## Active visual design: Green Angular (2026-10-18)

The current Menu presentation is `TrainerMenuView` (uGUI), using a 1600 x 900 logical canvas. Companion stays UI Toolkit with the existing party/skill/popup controllers. The new presentation primitives are `GreenAngularGeometry`, `GreenAngularGraphic`, `AngularSurface`, `AngularButton`, and `GreenAngularCompanionSkin`.

- Runtime fills, stencil/textured art masks, outlines, hit testing and focus share the geometry functions. Keep absolute corner insets when changing a target size.
- Inspector + active tab use one concave union polygon. Its body begins at local y=52. The active tab focus is a cap without a bottom stroke.
- Toolkit templates use `AngularButton : Button`; style the `.unity-button` class to include subclasses. Reset background/border/radius for every state. Text and icons remain independent layers.
- Popup stylesheet: `Resources/UI/GreenAngular/GreenAngular.uss`. UXML and the document adapter reference this same file.
- Editable vector/spec sources: `ArtSource/GreenAngular`. Runtime sprites: `Resources/UI/GreenAngular`. Backpack uses the existing TrainerMenuArt crop/texture.
- Pet illustration is user supplied: `GameMenuController.trainerPetArtwork` for Menu, and `PetCompanionArtCatalog` for separate large/slot art. Default Companion mode is 2D.
- QA request hooks: `Temp/VerifyGreenAngular.request` and `Temp/ProfileGreenAngular.request`. Diagnostics fixtures are guarded by UNITY_EDITOR and have no gameplay/save commands.
- Current captures, geometric comparison, source hashes, Profiler and limitations: project-root `artifacts/green-angular-2026-10-18/REPORT.md`.

The older V4 / companion presentation generators in artifacts describe their earlier revisions. Use the active Green Angular source files for subsequent edits; those older generators overwrite presentation layouts when executed.

## Current visual package: LATEST 2026-10-20

Use UI_Menu_Companion_LATEST_2026-10-20 for Menu/Companion. The existing GreenAngular class names remain for compatibility; their geometry/palette/material assets now implement the steel/smoke-purple LATEST design. Menu remains uGUI; Companion remains Toolkit. Runtime loose components are in Resources/UI/Latest20261020; editable SVG/spec sources are in ArtSource/Latest20261020.

Do not run the older presentation generators to overwrite this layout. Three tab feet share the union contour, skill frames use skill-oct, and player avatar is display-only. Hero2D art remains user supplied. QA hooks VerifyGreenAngular/ProfileGreenAngular produce current evidence under artifacts/ui-latest-2026-10-20; consult REPORT.md for checks and limitations.

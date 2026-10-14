# Gameplay HUD — TestChar

## Bố cục và thiết kế

uGUI, CanvasScaler 1920×1080, Scale With Screen Size. Đúng năm vùng: minimap trái trên; Active Pet Info và Party Carousel trái dưới; bốn skill phía dưới giữa; hai quick item phải dưới. Viền vector mảnh, nền trắng ngà, xanh sage/forest, focus vàng; HP xanh lá, MP xanh lam. Compass và QuestTracker HUD cũ được tắt. Menu gameplay có sẵn vẫn dùng bình thường.

Bố cục cập nhật ngày 03/10/2026: bảng pet, carousel, skill và quick item dùng scale 1 (gấp đôi bản 0.5 trước đó); minimap giữ nguyên đường kính 350px và vị trí (10, -10). Các nhóm dưới cách đáy 5–8px ở baseline. Khung pet nằm trên carousel, đáy ở 167px để không đè các ô. Menu Trainer không chỉnh.

## Các hệ thống tái sử dụng

- `PetCommandHudProvider` / `PetCommandInput`: pet được gọi, party sáu slot, stats và skill runtime.
- `PetSummonDirector`: xác nhận card và phím số đi qua sequence gọi/thu pet có sẵn.
- `PetHudRuntimeStats` và skill loadout: portrait, tên, level, HP/MP, icon, khóa skill và cooldown thật.
- AA Map / `MinimapController`: camera, RenderTexture, mask và marker có sẵn. HUD chỉ đổi vị trí/khung tròn; không tạo camera khác.
- `MonsterInventoryAdapter`, `MonsterInventoryController`, `PetHealService`, `PlayerSaveController`: quick item dùng quantity thật và giao dịch hồi máu/lưu/rollback hiện có. Hai ID quick item được lưu trong InventorySaveData; save cũ không có trường này vẫn đọc được.
- Fontin SmallCaps và circle mask trong tài nguyên project.

## Input và carousel

- 1–6 / Numpad 1–6: gọi pet theo logic cũ; Backspace: thu pet.
- Click mũi tên, cuộn chuột trên carousel hoặc click card chưa focus: chỉ đổi focus UI. Không thay pet gameplay.
- Click lại card đang nằm giữa: xác nhận gọi pet nếu slot có pet.
- Khi đổi pet hoặc gọi lại pet đã thu, carousel tự cuốn ô của pet đó về chính giữa. Nhấn lại phím số của pet đang ở ngoài cũng đưa slot về giữa; yêu cầu gọi bị từ chối không kéo focus sang slot đó. Cuộn để xem slot khác không bị refresh dữ liệu kéo ngược về pet đang gọi. Ô pet trống dùng dấu chân cùng kiểu PETS/BOX, vẽ bằng vector để không phụ thuộc font emoji.
- Carousel vòng đủ sáu slot, chỉ năm ô hiện khi dừng: hai trái, một giữa, hai phải. Tâm các ô cùng đường ngang; scale 1.10 ở giữa, 0.95 bên cạnh, 0.90 ngoài rìa; alpha 1 / 0.75 / 0.50. Mỗi ô gần tâm che đúng 50% ô kế bên, kể cả hai ô rìa. Animate ease-out 0.22s; hai view ngoài vùng nhìn thấy được tái dùng để ô thứ sáu cuốn vào/ra mượt ở cả hai mép. Không deactivate view khi wrap. Slot trống vẫn có thể focus.
- Hai nút trước/sau dùng chevron sáng với bóng mảnh; nền trong suốt, chỉ hiện nền nhẹ khi hover/press. Vùng nhận click 56×88px.
- M3: ghim mục tiêu bằng camera và yêu cầu pet đang sẵn sàng tấn công ngay mục tiêu đó. M3 lần nữa bỏ ghim theo cơ chế cũ. Click trái ưu tiên mục tiêu đã ghim; khi chưa ghim vẫn chọn bằng raycast. Lệnh vào pet bị chặn khi menu mở, chưa gọi pet hoặc sequence gọi/thu đang chạy.
- Z / X / C / V: bốn skill theo binding hiện tại; click ô skill tương đương. Cả nhóm ẩn cho đến khi pet gọi xong và sẵn sàng nhận lệnh, ẩn lại khi thu/đổi pet. API provider và runtime stats cũng chặn yêu cầu khi chưa sẵn sàng, không kích hoạt animation/event/cooldown. Ô trống/khóa/cooldown giữ vị trí khi nhóm đang hiện.
- Nhãn ACTIVE chỉ hiện cho pet đang ở ngoài. Cuộn chuột trên HUD không đồng thời zoom camera hoặc đổi mục tiêu ghim; M3 trên HUD không ghim mục tiêu xuyên qua UI.
- R / T: dùng quick item. Trong Bag, chọn thuốc hồi HP → ASSIGN QUICK → chọn R hoặc T. Gán không trừ item. Dùng item qua PetHealService, kiểm tra lại HP/quantity và chỉ giữ thay đổi khi lưu thành công.
- M/Tab/menu dùng luồng UI hiện tại; input HUD bị khóa và HUD ẩn khi UI gameplay đang mở.

## Tự bind và thiết lập

TestChar đã được gắn và lưu. Không cần gán Inspector thủ công.

Để dựng lại view: **Game Tools > GameToolThang > HUD > Create Gameplay HUD**, rồi lưu scene. Tool tự tìm Canvas/provider/player/minimap và gán font/mask. Chạy ngoài Play Mode. Camera minimap được bật, ẩn khi mở bản đồ lớn và phục hồi khi đóng.

**Validate Gameplay HUD** kiểm tra: đúng năm module, nằm trong viewport và không chồng nhau; scale 1 và minimap 350px; đúng năm card khi dừng, tâm cùng hàng, độ che 50% và thứ tự vẽ ở cả hai phía; bốn skill/hai item; skill chỉ hiện khi pet sẵn sàng, yêu cầu bị từ chối không phát event hoặc tiêu cooldown; CanvasRenderer; carousel wrap và không đổi active pet khi focus; camera/RenderTexture minimap hoạt động trong Play Mode; quick slot save round-trip và tương thích save cũ. Bộ kiểm tra save dùng adapter tạm, không sửa save người chơi.

**Watch Next M3 Attack** dùng trong Play Mode sau khi pet sẵn sàng: chọn menu, rồi ghim một enemy gần bằng M3 (bỏ ghim trước nếu đã ghim). Công cụ chỉ theo dõi: xác nhận pet nhận đúng enemy do camera ghim và health của enemy giảm trong 20 giây. Không tự gửi lệnh đánh, tạo enemy hay chỉnh dữ liệu combat.

## File mới

- HudFrameGraphic.cs: hình học vector khung HUD.
- GameplayHudVisuals.cs: các primitive uGUI và palette.
- PetPartyCarouselView.cs: focus/loop/animation của carousel.
- GameplayHudQuickItemsView.cs: hiển thị và điều phối quick item qua hệ có sẵn.
- Editor/GameplayHudValidation.cs: kiểm tra tích hợp HUD.

## File sửa

GameplayHudController, Editor/GameplayHudSetupEditor, PetHealService; PetCommandInput (thêm API gọi sequence); InventorySaveData trong PlayerSaveData; MonsterInventoryAdapter và MonsterInventoryController (gán/lưu quick item); MinimapController, WorldMapController, MapSystemController, Editor/MapSetupEditor (khung và điều khiển minimap); TestChar.unity; tài liệu này.

## Giới hạn dữ liệu và kiểm chứng

- Kiểm tra ngày 03/10: đã dựng và lưu bố cục mới vào TestChar, chạy Play Mode và Validate Gameplay HUD đạt. Đã thao tác nút Next/Previous, kiểm tra minimap và mở/đóng Trainer Menu; menu giữ kích thước cũ. GameMenuController không thay đổi trong lượt chỉnh này.
- Kiểm tra bổ sung ngày 03/10 ở scale 1: Validate Gameplay HUD đạt khi pet chưa gọi; cả API provider và runtime stats từ chối skill mà không phát event hay tiêu cooldown. Thanh skill và ACTIVE hiện sau spawn, ẩn lại sau recall. Watch Next M3 Attack đạt bằng click chuột giữa thực tế: pet nhận đúng Cube đang ghim, tiếp cận và gây sát thương (HP 100 → 51), không cần click trái để ra lệnh.
- Carousel ngày 03/10: quan sát Play Mode, cuộn sang slot 2 rồi nhấn lại phím 1 của pet đang ở ngoài đã cuốn về slot 1; đổi pet bằng phím 3 đưa slot 3 về giữa; recall pet 3, cuộn sang ô trống 4 rồi gọi lại pet 3 đã trở về giữa và hiện lại ACTIVE/skill sau spawn. Dấu chân hiện rõ khi focus ô trống slot 4. Build cho thay đổi carousel/icon đạt 0 lỗi.
- Pet hiện có MaxEnergy = 0: MP hiển thị `— / —`, không tạo chỉ số giả.
- Skill chưa có sprite hiển thị tên skill thật; không dùng icon giả lấy từ bên ngoài.
- Quick use hỗ trợ thuốc hồi HP có effect hiện có. Chưa có service/effect thuốc MP nên không tự tạo loại thuốc mới. Slot trống hiện dấu `—`; item đã gán hết số lượng giữ icon và hiện 0, vô hiệu hóa.
- Play Mode đã quan sát minimap có địa hình/marker, focus riêng, xác nhận đổi slot cập nhật level/HP, skill cooldown, ẩn/hiện HUD khi mở/đóng UI, hai kích thước Game View. Validation đạt. Bag hiện không có thuốc hồi HP, nên chưa kiểm chứng thao tác dùng thuốc end-to-end trên save hiện tại.
- Console còn lỗi handshake/authorization của plugin AI/MCP. Bản đồ lớn hiện nền xanh trong lần kiểm tra; minimap vẫn render đúng và phục hồi sau Back. Đây là phần cần khảo sát riêng ngoài thiết kế HUD này.

- Build Assembly-CSharp-Editor.csproj mới nhất đạt: 0 lỗi, 188 cảnh báo. Unity compile đạt. Kiểm tra thoát Play Mode lúc menu mở không phát sinh lại assertion go.IsActive sau guard teardown ở MapSystemController.

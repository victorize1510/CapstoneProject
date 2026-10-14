# Cài đặt — bàn giao

## Mở màn hình

Trong TestChar: **Tab → Cài đặt**, hoặc **Esc** khi ở gameplay. Quay lại/Esc trở về đúng nơi đã mở. Khi có hộp xác nhận, Esc đóng hộp trước. Màn dùng UI Toolkit, bố cục hai cột × hai hàng, cùng tiêu đề, font và nút Quay lại với các màn hiện có.

## Các tùy chọn đã nối

| Nhóm | Chức năng | Nguồn thực tế |
|---|---|---|
| Âm thanh | Tổng âm lượng 0–100%, tắt/bật tiếng và khôi phục mức trước đó | `AudioListener.volume`; project chưa có AudioMixer |
| Điều khiển & Camera | Độ nhạy chuột, đảo Y, làm mượt | `BasicCameraFollow`, `PlayerFreeLookRig`, damping của hai camera Cinemachine |
| Trò chơi & Giao diện | Gợi ý tương tác, minimap, tracker nhiệm vụ, thông báo lưu | NPC/quest giver/pickup; HUD; QuestTracker; `PlayerSaveController.SaveCompleted` |
| Khác | Tiếng Việt, toàn màn hình, độ phân giải, hướng dẫn phím, đặt lại, về màn hình chính | Ngôn ngữ hiện có; Screen API; binding gameplay; reset preferences; SaveNow và MainMenu hiện có |

- Độ nhạy 50% giữ nguyên giá trị Inspector của camera; 0% vẫn có mức tối thiểu để camera xoay được. Đảo Y chỉ đổi chiều input dọc.
- Tắt làm mượt bỏ damping follow/rotation của camera FreeLook và camera ghim; bật lại khôi phục thông số scene.
- Gợi ý tương tác tắt chỉ ẩn chữ E ở NPC, quest giver và đồ nhặt. Điều kiện nhìn thấy/khoảng cách và lệnh tương tác giữ nguyên.
- Tắt minimap chỉ ẩn minimap HUD. Màn Bản đồ và tọa độ marker giữ nguyên.
- Tracker nhiệm vụ mặc định **Tắt**, đúng thiết lập TestChar trước thay đổi này. Tắt tracker không bỏ theo dõi nhiệm vụ.
- Thông báo “Đã lưu trò chơi.” xuất hiện sau sự kiện lưu thành công, trong 1,8 giây. Tắt thông báo không tắt save.
- Ngôn ngữ chỉ hiển thị Tiếng Việt và có giải thích; chưa giả lập English.
- Toàn màn hình/độ phân giải dùng `Screen.resolutions`, bỏ trùng kích thước do refresh rate. Hai tùy chọn này bị khóa có giải thích trong Editor, khả dụng trong bản PC.
- Reset có xác nhận và chỉ đặt lại preferences. Về màn hình chính có xác nhận, gọi `SaveNow()` trước; save bị chặn/thất bại thì ở lại và hiện lý do.

## Các mục ẩn do chưa có hệ thống

Music/BGM, SFX và UI riêng (chưa có mixer group); độ nhạy ngang/dọc riêng (camera dùng chung một độ nhạy); tự xoay camera sau di chuyển; rung camera đang được gameplay sử dụng; tay cầm; số sát thương; hướng dẫn tutorial; tốc độ hội thoại; tùy chọn giảm VFX. Không thêm hệ gameplay hoặc nút giả cho những mục này.

## Lưu cài đặt

- `PlayerPrefs` có một khóa JSON: **`Capstone.UserSettings.v1`**. Không thêm trường vào save nhân vật.
- Áp dụng tức thời; gom lần ghi sau **0,6 giây** không có thay đổi; flush khi đóng màn, mất focus, pause hoặc thoát game.
- JSON thiếu trường giữ mặc định; JSON hỏng/phiên bản không hỗ trợ có cảnh báo và dùng mặc định; clamp giá trị không hợp lệ.
- Không đọc PlayerPrefs hay tìm component mỗi frame. Scene binding khi scene tải hoặc mở Cài đặt.

## File chính

Trong `Capstone/Assets/Game/SettingsSystem/`:

- `GameSettingsData.cs`: dữ liệu preferences và state thay đổi qua sự kiện.
- `SettingsRepository.cs`: persistence riêng, storage có thể thay trong test.
- `GameSettingsService.cs`: lifetime, binding hệ hiện có, áp dụng và debounce.
- `DisplaySettings.cs`: danh sách và áp dụng chế độ cửa sổ.
- `SettingsScreenController.cs`: cache element, bind/unbind, capability, slider/toggle và hộp xác nhận.
- `Resources/UI/SettingsFeedback.uss`: thông báo lưu.
- `Editor/SettingsValidation.cs`: fixture Editor và kiểm tra Play Mode; dữ liệu fixture không ghi preferences thật.

Trong `Capstone/Assets/ThangTest/UIThang/Inventory/UI/`:

- `SettingsScreen.uxml`, `SettingsScreen.uss`.
- `SettingsSliderRow.uxml`, `SettingsToggleRow.uxml`, `SettingsDropdownRow.uxml`, `SettingsButtonRow.uxml`.
- `MonsterInventory.uxml`, `MonsterInventoryController.cs`: đăng ký màn và routing.
- `MenuScreenHeader.uss`: thêm Settings vào selector header dùng chung.

Tích hợp ở `GameMenuController`, `PanelFocusUtility`, `GameplayHudController`, `GameplayHudQuickItemsView`, `NpcPanelInteractable`, `QuestGiverInteractable`, `BasicPlayerMovement`, `BasicCameraFollow`, `PlayerFreeLookRig`, `PlayerVisualSwitcher`. Getter phím chỉ đọc để hướng dẫn lấy đúng binding. Sửa focus chung để modal giữ Tab bên trong.

Không cần kéo thêm reference trong Inspector: service bootstrap trước scene, dùng reference camera/HUD/save/UI hiện có. Theme và font dùng asset hiện có; icon bánh răng vẽ bằng UI Toolkit Painter2D, không thêm texture hay package.

## Kiểm tra và giới hạn

- Kết quả Editor: `validation.txt`; runtime TestChar: `runtime-validation.txt`; C# build: `build-final.log`.
- Kết quả cuối: **66 kiểm tra Editor, 26 kiểm tra Play Mode, 175 kiểm tra UXML/USS/assets đều đạt**. Unity compile và build C# **0 lỗi**; build còn 193 cảnh báo sẵn có từ package/imported assets.
- Responsive: 1920×1080, 1600×900, 2560×1440, 1366×768, 1920×1200, 2560×1080. Ở màn thấp, từng nhóm cuộn độc lập; ở baseline nút Thoát hiển thị đầy đủ.
- Fixture kiểm tra repository round trip/corrupt/version/clamp, resolution deduplication, percent, mute, reset/cancel, modal focus, callback không nhân đôi và chống xác nhận exit lặp.
- Play Mode kiểm tra AudioListener, sensitivity/invert/smoothing, minimap, tracker, save feedback, khóa input và return route Menu/gameplay. Các thay đổi preferences dùng storage trong bộ nhớ rồi khôi phục trạng thái trước test.
- Ảnh thực tế: `screenshots/Settings-actual.png`; hướng dẫn: `screenshots/Settings-controls.png`.
- Thay độ phân giải/toàn màn hình thực tế cần kiểm tra trong bản standalone PC; Editor không thay được cửa sổ bản build. Chưa tạo bản build executable ở lượt này.
- Luồng thoát được kiểm tra bằng callback fixture và xác nhận MainMenu nằm trong build settings; không thực hiện load MainMenu trong phiên QA để tránh lưu thêm tiến trình ngoài ý muốn.
- Build C# còn cảnh báo có sẵn của package/imported asset; không tự sửa các package đó trong phạm vi Cài đặt.

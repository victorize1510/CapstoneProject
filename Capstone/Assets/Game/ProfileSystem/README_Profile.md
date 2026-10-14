# Profile / Hồ sơ

## Cach mo

- Nhan `TAB` de mo menu tong, sau do chon `Profile`.
- Nut `QUAY LAI` tro ve menu tong.
- `Escape` hoac `TAB` dong Profile va tra dieu khien ve gameplay.

## Du lieu dang hoat dong

- Player ID cuc bo on dinh.
- Tên, ngày bắt đầu, cấp/EXP, thống kê và thời gian chơi được lưu qua
  PlayerSaveController hiện có; không thay đổi schema save.
- Play Time dung thoi gian khong bi anh huong boi `Time.timeScale` va chi hien ngay,
  gio, phut.
- Current Area lay tu scene hien tai.
- Story Progress lay tu so Main Quest hoan thanh / tong Main Quest trong `QuestManager`.
- Change Name kiem tra ten tu 2 den 20 ky tu.
- Đội thú hiện tại lấy từ IPetHudProvider, có 6 ô theo 2 hàng × 3 cột.
  Bấm một ô chỉ chọn highlight trong Profile, không gọi/thu hoặc đổi pet gameplay.
  Ô trống hiện icon và chữ Trống. Khu huy hiệu cũ đã gỡ khỏi màn hình.

## Achievement

- AchievementManager vẫn giữ metric/huy hiệu và dùng hệ save hiện có.
- Khi chua gan asset dinh nghia, manager dung mot bo achievement fallback de test UI.
- De tao achievement that: `Assets > Create > Capstone > Profile > Achievement Definition`,
  sau do gan danh sach asset vao `AchievementManager > Definitions`.
- Cac metric mac dinh: `creatures_seen`, `species_captured`, `codex_entries`,
  `main_quests_completed`, `bosses_defeated`, `total_battles`.
- Main Quest duoc dong bo tu `QuestManager`. Cac he combat/capture/codex sau nay goi cac ham
  `RecordCreatureSeen`, `RecordSpeciesCaptured`, `SetCodexProgress`, `RecordBossDefeated`
  va `RecordBattle` tren `PlayerProfileRuntimeProvider`.

## Du lieu can noi backend sau

- Level va EXP can noi Player Stats that qua `SetLevelProgress`.
- Creatures Seen, Species Captured, Codex, Bosses Defeated va Total Battles can duoc
  gameplay system goi cac API ghi nhan neu chua co event backend.
- Cac gia tri chua co du lieu hien dau `-`, khong dung so gia.

## Avatar

Tren object `GameMenu`, mo component `Player Profile Runtime Provider`, tang kich thuoc
`Avatar Options` và kéo các sprite avatar vào. Nút đổi ảnh mở danh sách có cuộn
để chọn ảnh; nếu danh sách rỗng, nút được khóa.

## Tao/cap nhat trong scene

Chay menu `Game Tools > GameToolThang > HUD > Create Main Menu`. Tool se them hoac cap
nhat `PlayerProfileRuntimeProvider`, `AchievementManager`, `ProfilePanelController` va
reference cua `GameMenuController` ma khong tao trung object.

Khi co SaveManager/account that, tao provider moi implement `IPlayerProfileProvider`, sau
do gan component do vao `ProfilePanelController > Provider Source`.

## Bố cục mới

```text
ProfilePanelRoot                       # nền phủ toàn canvas
├── ProfileScreen                     # thiết kế 1920×1080
│   ├── HeaderBar                     # HỒ SƠ, mô tả, Quay lại
│   ├── MainProfilePanel
│   │   ├── AvatarSection             # ảnh tròn, camera, đổi ảnh
│   │   ├── BasicInfoSection          # tên, sửa tên, cấp độ, EXP
│   │   └── CurrentTeamSection
│   │       └── TeamPetSlot_01…06      # 2 hàng × 3 ô
│   ├── PlayerInfoPanel               # ID, thời gian, khu vực, ngày bắt đầu
│   └── StatsOverviewPanel
│       └── StatsCard_*               # 2 hàng × 3 thống kê
├── ChangeNameDialogOverlay
└── AvatarPickerOverlay
```

- ProfilePanelController giữ vòng đời và thao tác; ProfilePanelLayout.cs dựng
  bố cục; ProfileIconGraphic vẽ icon vector, không dùng font emoji.
- Root neo `(0,0)` tới `(1,1)`, offsets 0. ProfileScreen nằm giữa, scale đồng đều
  theo `min(canvasWidth/1920, canvasHeight/1080)`, cập nhật khi đổi kích thước.
- Header cao 76, title 38, Back 146×42, đồng bộ Bag/PETS/BOX.
- Nền kem, chữ xanh rừng, sage và gold nhạt; viền 1, góc bo khoảng 12;
  sprite bo góc 9-slice tạo một lần và tái sử dụng. Avatar mask chống răng cưa.
- Nút có hover/pressed/disabled và chống lặp 0,15 giây hiện có.

## Kiểm tra và dựng lại

Game Tools → GameToolThang → HUD:

- **Rebuild Profile In Scene**: dựng riêng Profile, giữ provider và có Undo;
  lưu scene như bình thường. Runtime và Create Main Menu dùng cùng bố cục.
- **Preview Profile Layout**: kiểm tra binding, render 1920×1080 / 1366×768 trong
  scene tạm, không dùng save; ảnh/log tại Temp/ProfileRefresh.
- **Preview BOX Viewport**: nạp UXML/style thật, kiểm tra BOX khớp bốn mép viewport
  mà không chạy controller inventory.
- **Validate Profile Runtime And Interaction**: chạy trong Play Mode để kiểm tra
  Profile đã dựng đủ ở Awake, dựng lại bằng AddComponent runtime, binding dữ liệu
  và các trường hợp tầm nhìn tương tác. Không đổi tên/avatar hay ghi dữ liệu giả.

Các Button và InputField dựng bằng code phải gán `targetGraphic` trực tiếp.
`Selectable.Reset` chỉ tự gán trong Editor; phụ thuộc vào nó khiến runtime dừng
ở AvatarCameraButton và mất các phần thông tin bên dưới.

BOX bỏ lề ngoài trong MenuScreenHeader.uss; vẫn giữ phần đệm nội dung, cuộn và
các thao tác quản lý kho/pet hiện có.

HỒ SƠ dùng `MenuPalette.Background` cho nền ngoài và `MenuPalette.Surface` cho
khung thông tin. Sáu ô đội thú chỉ hiển thị ảnh đầy ô: `AspectRatioFitter` ở chế
độ `EnvelopeParent` cùng `RectMask2D` để cắt ảnh đúng tỷ lệ, không kéo méo. Không
tạo tên, số ô hay dòng chỉ số trong những thẻ này; ô thiếu ảnh dùng icon chân thú.

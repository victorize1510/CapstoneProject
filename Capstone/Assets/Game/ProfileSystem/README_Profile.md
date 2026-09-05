# Profile UI Setup

## Cach mo

- Nhan `TAB` de mo menu tong, sau do chon `Profile`.
- Nut `QUAY LAI` tro ve menu tong.
- `Escape` hoac `TAB` dong Profile va tra dieu khien ve gameplay.

## Du lieu dang hoat dong

- Player ID cuc bo on dinh.
- Ten nguoi choi, ngay bat dau, level, EXP, thong ke va thoi gian choi duoc luu bang
  `PlayerPrefs` cho ban prototype.
- Play Time dung thoi gian khong bi anh huong boi `Time.timeScale` va chi hien ngay,
  gio, phut.
- Current Area lay tu scene hien tai.
- Story Progress lay tu so Main Quest hoan thanh / tong Main Quest trong `QuestManager`.
- Change Name kiem tra ten tu 2 den 20 ky tu.
- Chi huy hieu da mo khoa moi hien tren Profile.

## Achievement

- `AchievementManager` luu metric va huy hieu da mo khoa bang `PlayerPrefs`.
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
`Avatar Options` va keo cac sprite avatar vao. Nut `CHANGE AVATAR` se lan luot chuyen qua
cac sprite hop le. Neu danh sach rong, nut duoc khoa.

## Tao/cap nhat trong scene

Chay menu `Game Tools > GameToolThang > HUD > Create Main Menu`. Tool se them hoac cap
nhat `PlayerProfileRuntimeProvider`, `AchievementManager`, `ProfilePanelController` va
reference cua `GameMenuController` ma khong tao trung object.

Khi co SaveManager/account that, tao provider moi implement `IPlayerProfileProvider`, sau
do gan component do vao `ProfilePanelController > Provider Source`.

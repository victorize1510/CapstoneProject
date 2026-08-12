# Main Menu TAB Setup

Menu nay chi dieu phoi UI da co, khong tao lai Inventory, Quest hoac Map.

## Cach tao trong scene

1. Mo scene can dung, vi du `TestChar`.
2. Chay menu:
   `Game Tools > GameToolThang > HUD > Create Main Menu`
3. Tool se tao hoac cap nhat object `GameMenu`.
4. Tool se tim va gan neu co:
   - `MonsterInventoryController`
   - `InventoryInputController`
   - `MapInputController`
   - `LocalPlayerControlLock`
5. Neu thieu Inventory hoac Map, nut tuong ung se mo panel placeholder trang thay vi goi reference gia.

## Phim dieu khien

- `TAB`: mo/dong menu tong.
- `I`: mo thang Inventory/Quest UI hien co.
- `M`: mo World Map hien co.
- `Escape`: dong UI dang mo.

## Quy tac input

- Khi menu mo: khoa movement/camera cua local player va hien chuot.
- Khi dong menu: khoi phuc lai trang thai player va cursor truoc do.
- Inventory va Map van dung he thong cu, nhung phim mo/dong rieng cua chung duoc menu tong dieu phoi de tranh tranh input.

## Cac muc hien tai

- Inventory: dung `MonsterInventoryController` va `MonsterInventoryAdapter`.
- Quest: dang mo cung UI Inventory vi Quest Journal hien nam chung trong `MonsterInventory.uxml`.
- Map: dung `MapInputController`.
- Profile, Pets, Settings, Codex, Store, Box: tam mo panel placeholder trang co ten tinh nang o tren.

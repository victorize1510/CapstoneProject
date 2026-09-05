# TAB/I Quest + Inventory UI Setup

## Muc dich
Menu TAB/I dung UI Toolkit. Quest Journal va Bag la hai man hinh rieng, dung toan bo vung menu. Inventory dung `MonsterInventoryAdapter` hien co. Quest dung `QuestManager` hien co.

## Can co trong scene
1. `InventoryUI` GameObject
   - `UIDocument`
   - Source Asset: `Assets/ThangTest/Inventory/UI/MonsterInventory.uxml`
   - Panel Settings: dung PanelSettings cua project, hoac tao moi bang `Assets > Create > UI Toolkit > Panel Settings`
2. Tren cung GameObject `InventoryUI`
   - `MonsterInventoryController`
   - `InventoryInputController`
   - `QuestPanelController`
3. Inventory backend
   - Dat `MonsterInventoryAdapter` trong scene, hoac gan cung `InventoryUI`.
   - Neu khong co adapter, UI van hien nhung Bag khong co du lieu.
4. Quest backend
   - Dat `QuestManager` trong scene.
   - `QuestPanelController` se auto find `QuestManager` khi Play.

## Input
- `I` hoac `Tab`: mo/dong menu.
- `Escape`: dong menu.
- Khi dang mo Quest Journal:
  - `Q/E`: doi tab Dang tien hanh, Nhiem vu chinh, Nhiem vu phu, Su kien, Khac.
  - `W/S` hoac mui ten: doi quest dang chon.
  - `Enter`: Track/Untrack quest dang chon.
- Chuot nam tren Bag:
  - `Q/E`: doi category.
  - `W/S` hoac mui ten: doi item.
  - `Enter`: action mac dinh cua item.

## Luu y
- Khong sua package Inventory Framework goc.
- Khong sua demo goc cua package.
- UI khong hard-code hoi mau, bat thu, trang bi. Cac action chi phat event; gameplay system phai bao thanh cong thi item moi bi tru.

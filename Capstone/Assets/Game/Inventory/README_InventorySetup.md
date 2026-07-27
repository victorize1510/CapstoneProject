# Huong dan setup Monster Inventory UI

## Cach nhanh nhat

1. Mo scene can dung inventory.
2. Tren thanh menu Unity chon:

   `Game Tools > Inventory > Create Monster Inventory UI`

3. Tool se tim hoac tao GameObject ten `InventoryUI`.
4. Tool se tu them/gan:
   - `UIDocument`
   - `MonsterInventoryController`
   - `InventoryInputController`
   - `PanelSettings`
   - `MonsterInventory.uxml`

Tool co ho tro Undo, nen co the bam `Ctrl + Z` neu muon quay lai.

## Component can co trong scene

### InventoryUI

GameObject `InventoryUI` nen co cac component:

- `UIDocument`
  - `Panel Settings`: gan `MonsterInventoryPanelSettings`
  - `Source Asset`: gan `MonsterInventory.uxml`

- `MonsterInventoryController`
  - `Document`: gan `UIDocument` tren cung GameObject
  - `Row Template`: gan `MonsterItemRow.uxml`
  - `Adapter`: gan `MonsterInventoryAdapter` dang co trong scene

- `InventoryInputController`
  - `Inventory`: gan `MonsterInventoryController`
  - `Document`: gan `UIDocument`
  - `Player Control Lock`: co the de trong; script se tu tim local player khi play neu co `LocalPlayerControlLock`

## MonsterInventoryAdapter

Scene can co mot `MonsterInventoryAdapter`.

Ban co the dat no tren:

- GameObject rieng, vi du `InventoryBackend`
- Hoac cung GameObject `InventoryUI`
- Hoac mot manager trong scene

Neu tool khong tim thay `MonsterInventoryAdapter`, no se hien warning ro rang va **khong gan reference gia**. Khi do ban can:

1. Tao GameObject moi, vi du `InventoryBackend`.
2. Add Component `MonsterInventoryAdapter`.
3. Chay lai menu `Game Tools > Inventory > Create Monster Inventory UI`.

## Cac asset UI tool se tim

Tool uu tien tim:

- `Assets/Game/Inventory/UI/MonsterInventory.uxml`
- `Assets/Game/Inventory/UI/MonsterItemRow.uxml`
- `Assets/Game/Inventory/UI/MonsterInventoryPanelSettings.asset`

Neu khong co, tool se fallback sang:

- `Assets/ThangTest/Inventory/UI/MonsterInventory.uxml`
- `Assets/ThangTest/Inventory/UI/MonsterItemRow.uxml`
- `Assets/ThangTest/Inventory/UI/MonsterInventoryPanelSettings.asset`

## Luu y

- Tool khong sua player prefab.
- Tool khong sua package Inventory Framework goc.
- Tool khong tao trung `InventoryUI` neu chay nhieu lan.
- Tool chi gan `MonsterInventoryAdapter` neu tim thay adapter that trong scene.
- Cac action nhu Use, Give, Equip, Assign Quick Slot, Drop van can gameplay system dang ky event va goi `request.Complete(true/false)`.

## Kiem tra sau khi setup

1. Bam Play.
2. Nhan `I` hoac `Tab` de mo/tat bag.
3. Neu bag khong hien:
   - Kiem tra `InventoryUI > UIDocument > Source Asset`.
   - Kiem tra `InventoryUI > UIDocument > Panel Settings`.
   - Kiem tra Console co error mau do khong.
4. Neu item khong hien:
   - Kiem tra scene co `MonsterInventoryAdapter`.
   - Neu dung debug item, gan them `InventoryDebugSeeder` vao cung GameObject voi adapter hoac mot GameObject khac va tro reference ve adapter.

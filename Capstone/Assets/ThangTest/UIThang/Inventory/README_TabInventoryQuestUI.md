# TAB/I Quest + Inventory UI Setup

## Muc dich
Menu TAB/I dung UI Toolkit. Quest Journal va Bag la hai man hinh rieng, dung toan bo vung menu. Inventory dung `MonsterInventoryAdapter` hien co. Quest dung `QuestManager` hien co.

## Can co trong scene
1. `InventoryUI` GameObject
   - `UIDocument`
   - Source Asset: `Assets/ThangTest/UIThang/Inventory/UI/MonsterInventory.uxml`
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


## Inventory presentation (2026-10-06)

The live TestChar UIDocument still uses MonsterInventory.uxml, MonsterInventoryController,
MonsterInventoryAdapter and the existing GDS ListBag (starts with 40 physical slots,
then appends slots on demand without a gameplay capacity limit).
No Inspector rebinding or new inventory database is required. Setup and Preview tools
now resolve the relocated UIThang folder.

- Layout: full screen; category sidebar, central grid/toolbar, fixed detail column with
  a large sprite preview and actual Gold wallet. No capacity bar or label.
- Categories: ALL, CaptureBall, Medicine, Food, Material, Equipment, QuestItem, KeyItem.
  ALL is the initial category. Sidebar counts are owned item kinds, consistent with
  the aggregated item grid. The saved capacity field records allocated slots for
  backward compatibility; it is not a gameplay limit.
- Before an item is selected, the detail column contains only
  "Chọn vật phẩm để xem thông tin." No automatic initial selection or fake details.
- Search: name, stable ID and category, ignoring letter case.
- Sort: type, name A-Z / Z-A, quantity and rarity. Selection follows item identity.
- Filter: no filter, usable through a real action, Rare+, droppable. No fictitious
  acquisition timestamp, tag, favorite or currency is displayed.
- Grid: 5-7 square columns based on available logical width; button switches to
  a working list. Cards are pooled across category/search/sort/count updates.
- Use: Dusk Stone keeps the existing Bag -> Pets selection -> Bag confirmation.
  A usable consumable Medicine with HealAmount > 0 opens the existing Heal dialog
  with that exact ItemBase. PetHealService performs the original validation,
  consumption, HP change, save and rollback. Food/key Use requires a real registered
  gameplay handler. Unsupported actions are hidden.
- Quickslot: healing items expose Assign Quick through GameplayHudQuickItemsView;
  its existing two-slot selector and save flow are reused. No item is consumed.
  Other categories, including capture balls, cannot assign through buttons, Enter
  or the Bag action API.
- Drop: the existing quantity/confirmation popup; Quest and Key items are protected.
  Bulk Use is not invented; healing and evolution remain one confirmed action.
- Favorite and Split: omitted. This inventory has no persisted favorite flag or
  transactional split action, and the grid aggregates identical definitions.
- Optional source/effect/flavor blocks disappear when data is absent. Original
  item sprites are reused with their aspect ratio; absent sprites use text fallback.

MenuScreenHeader.uss is shared by Bag, PETS, BOX and Quest: 38px bold Liberation Sans,
76px header, 146x42 Back, common header insets and baseline. Bag/PETS now occupy
the whole viewport; BOX retains its outer inset. BOX's long internal
management caption is simplified to BOX; its header subtitle is hidden. Trainer
Menu now occupies the rightmost 30% of the viewport and overlays the visible HUD
and minimap while keeping gameplay input blocked.
Each occupied PETS party slot exposes a favorite toggle, reflecting the same
saved metadata as the selected pet's star. Toggling a card does not select or
summon that pet and reuses the existing save/rollback transaction. Empty slots
have no favorite button.
PETS hides only the information ScrollView's vertical scroller. The ScrollView,
mouse-wheel input, stats, skill cards and action buttons are retained.

Checks: Tools > ToolCuaThang > UI > Validate Inventory Presentation runs against
isolated temporary item definitions and an adapter, plus the real composed UXML/USS
at 1920x1080, 1600x900 and 2560x1440. It does not load or save player data.
Results are written to Temp/InventoryPresentationValidation.txt. Preview Inventory
Panels uses the same card layout method as runtime and is safe for visual editing.

### Files changed in this pass

Under Assets/ThangTest/UIThang/Inventory/:
- UI/InventoryPanel.uxml and InventoryPanel.uss: new presentation.
- UI/MonsterInventoryController.cs: filters, counts, pooled grid/list and medicine UI routing.
- UI/PetsPanel.uxml, BoxPanel.uxml, QuestPanel.uxml: shared header classes.
- UI/MenuScreenHeader.uss (+ meta): new common header stylesheet.
- Editor/InventoryUiPreviewWindow.cs: correct relocated paths, target resolutions,
  preview sizing shared with runtime.
- Editor/InventoryPresentationValidation.cs (+ meta): new isolated validation tool.
- README_TabInventoryQuestUI.md: this integration report.

Other existing files changed:
- Assets/Game/Inventory/Editor/MonsterInventorySetupTool.cs: resolve actual UI assets.
- Assets/Game/HudSystem/PetHealPanelController.cs: pass the exact selected medicine
  into existing CreatePreview/TryHeal instead of choosing a different default item.

Final checks after the party/menu follow-up: 115 PASS, 0 FAIL, including growth beyond
40 slots, stack reuse, save roundtrip, initial text-only detail, ALL navigation and
hidden-but-scrollable PETS information, favorite indicators on all six party slots,
and fullscreen Bag/PETS at all three resolutions. Runtime C# build: 0 errors (10 existing warnings).
Editor C# build: 0 errors (3 existing warnings). Live TestChar: I opens Bag,
ALL shows saved Dusk Stone x97; the detail prompt is text-only until a click. Dusk
Stone has no quickslot action. PETS shows no scrollbar and mouse-wheel scrolling
reveals the full lower action buttons. Back returns through the existing flow.
Live TestChar also confirms fullscreen Bag/PETS and the 30%-width Trainer Menu
with HUD/minimap visible beneath it. Toggling slot 2's favorite off/on updates that
pet's save without selecting, swapping or summoning it; the original favorite
state was restored before exiting Play Mode.
No items were consumed. Both primary and backup saves were restored byte for byte
from the pre-test copies after Play's autosave updated time metadata. TestChar and
SampleScene files retained their starting hashes. Existing MCP/network warnings
remain unrelated to this UI change.

# Thang Map System Setup

Hệ này nối game của mình vào `AA Map and Minimap System` mà không sửa package gốc.

## File script mới

- `MapMarker.cs`: gắn lên object muốn hiện icon trên minimap/map.
- `MapMarkerManager.cs`: tạo và quản lý `AAMAP.MapIcon`.
- `AAMapRuntimeBinder.cs`: nối `MinimapManager`, `MapManager`, camera và player.
- `MapInputController.cs`: nhấn `M` mở/đóng map lớn, `Escape` đóng map.
- `QuestMapMarkerBridge.cs`: đưa quest đang track lên minimap/map.
- `QuestPanelMapConnector.cs`: nối nút `Show On Map` trong Quest Journal với map lớn.

## Bạn cần làm trong mỗi scene chơi game

1. Kéo prefab của AA vào scene:
   - `Minimap`
   - `Minimap Camera`
   - `Map`
   - `Map Camera`
   - `Map Icon` dùng làm prefab icon

2. Tạo GameObject rỗng tên `MapSystem`.

3. Gắn component vào `MapSystem`:
   - `AAMapRuntimeBinder`
   - `MapMarkerManager`
   - `MapInputController`
   - `QuestMapMarkerBridge` nếu scene có quest
   - `QuestPanelMapConnector` nếu scene có Quest Journal UI

4. Kéo reference:
   - `AAMapRuntimeBinder > Minimap Manager`: object `Minimap`
   - `AAMapRuntimeBinder > Map Manager`: object `Map`
   - `AAMapRuntimeBinder > Minimap Camera`: object `Minimap Camera`
   - `AAMapRuntimeBinder > Map Camera`: object `Map Camera`
   - `AAMapRuntimeBinder > Player Target`: Player trong scene
   - `MapMarkerManager > Map Icon Prefab`: prefab `Map Icon`
   - `MapInputController > Control Lock`: component `LocalPlayerControlLock` nếu muốn khóa player khi mở map

5. Với object muốn hiện icon thủ công:
   - Gắn `MapMarker`.
   - Chọn `Marker Type`: Enemy, NPC, Item, QuestTarget...
   - Gắn texture icon nếu muốn.
   - Bật/tắt `Show On Minimap` và `Show On World Map`.

## Tự động hiện icon

`MapMarkerManager` có thể tự thêm marker runtime cho:

- Player
- PetController
- DummyEnemy

Các marker runtime này không lưu vào prefab/scene, chỉ sinh khi Play.

## Phím

- `M`: mở/đóng map lớn.
- `Escape`: đóng map lớn.

AA MapManager sẽ bị tắt input nội bộ để tránh bắt phím trùng.

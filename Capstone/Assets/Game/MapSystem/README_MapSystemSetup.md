# Huong dan setup Minimap va World Map

Thu muc tich hop moi nam o `Assets/Game/MapSystem`. Package goc `Assets/ThangTest/AA Map and Minimap System` khong bi sua.

## Cach chay setup

1. Mo scene can dung map, vi du `Assets/Scenes/TestChar.unity`.
2. Neu tool khong tu tim thay Player, hay chon object Player trong Hierarchy.
3. Chon menu `Tools > ToolCuaThang > Game Map > Setup Complete Map System`.
4. Bam `Save Scene` neu Unity hoi luu scene.
5. Chon `Tools > ToolCuaThang > Game Map > Validate Setup` de kiem tra lai.

## Hierarchy tool tao/sua

- `Canvas`
  - `MinimapPanel`: mini map goc tren ben phai.
  - `WorldMapPanel`: map lon, mac dinh dong.
    - `Map Mask`: viewport chu nhat mac dinh 1280x720, cat phan map nam ngoai khung.
    - `World Map Overlay`: region, zoom, filter va clear waypoint.
- `Minimap Camera`: camera nhin tu tren xuong theo Player.
- `Map Camera`: camera nhin tu tren xuong cho world map.
- `MapSystem`
  - `MapSystemController`
  - `MinimapController`
  - `WorldMapController`
  - `MapInputController`
  - `MapMarkerManager`
  - `MapIconRegistry`
  - `QuestMapMarkerBridge`
  - `QuestPanelMapConnector`
  - `MapSetupValidator`
  - `Map Icon Containers`

## Dieu khien

- `M`: mo/dong world map.
- `Escape`: dong world map.
- Lan chuot khi world map dang mo: zoom.
- `Numpad +` / phim `+`: zoom in.
- `-` / `Numpad -`: zoom out.
- Keo chuot trai khi world map dang mo: keo ban do.
- Chuot phai tren world map: dat waypoint.
- Nut `Clear Waypoint`: xoa waypoint.

## Zoom world map

`WorldMapController` khong fit toan bo map. Khi mo bang `M`, map se center quanh Player va chi thay mot phan nho:

- `minimumVisiblePercent = 0.05`: zoom gan nhat, thay khoang 5% canh map.
- `initialVisiblePercent = 0.12`: luc vua mo map, thay khoang 12%.
- `maximumVisiblePercent = 0.30`: zoom xa nhat, khong vuot qua 30%.

Percent tinh theo canh dai nhat cua viewport nhin thay, khong phai opacity va khong phai dien tich. Neu map 2000x2000, zoom xa nhat chi thay toi da khoang 600 world units theo canh dai nhat.

Khong chay setup trong Play Mode. Neu map bi trang hoac trong suot, dung `Tools > ToolCuaThang > Game Map > Validate Setup`, sau do chay lai `Setup Complete Map System` o Edit Mode.

## Dang ky icon len map

Gan `MapMarker` hoac `MapIcon` vao object can hien tren map:

- Player: `MapMarkerType.Player`
- Pet: `MapMarkerType.Pet`
- Enemy: `MapMarkerType.Enemy`
- Boss: `MapMarkerType.Boss`
- NPC: `MapMarkerType.NPC`
- Quest: `MapMarkerType.QuestTarget`
- Shop: `MapMarkerType.Shop`
- Fast travel: `MapMarkerType.FastTravel`

`MapMarkerManager` tu scan marker trong scene. Pet dang co `PetController` va enemy co `DummyEnemy` se duoc tao marker runtime tu dong.

## Sprite GUI custom

Setup hien uu tien bo sprite custom trong:

`Assets/Game/MapSystem/Sprites/MapMinimap`

Bo nay gom khung minimap, khung world map, nut zoom/close/center, va icon player, pet, enemy, boss, quest, NPC, shop, item, fast travel, waypoint, co-op player. Neu thieu sprite custom, setup se fallback ve sprite co san cua AA Map package, sau do moi dung placeholder tu tao.

Sau khi thay sprite, chay lai `Tools > ToolCuaThang > Game Map > Setup Complete Map System` de cap nhat reference trong scene.

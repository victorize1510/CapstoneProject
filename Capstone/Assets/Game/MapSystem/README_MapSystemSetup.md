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
- Keo chuot trai khi world map dang mo: keo ban do.
- Chuot phai tren world map: dat waypoint.
- Nut `Clear Waypoint`: xoa waypoint.

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

## Can thay sprite sau nay

Hien setup uu tien sprite co san cua AA Map package. Neu muon ban final dep hon, thay icon texture trong `MapMarkerManager` cho tung loai marker: player, pet, enemy, boss, NPC, quest, shop, fast travel.

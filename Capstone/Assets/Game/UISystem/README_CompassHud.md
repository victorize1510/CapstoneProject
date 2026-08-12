# Compass HUD

Compass HUD la UI rieng, khong dieu khien camera hay player.

## Tao vao scene

Chay menu:

`Game Tools > GameToolThang > HUD > Create Compass HUD`

Tool se:

- Tim hoac tao `Canvas` Screen Space Overlay.
- Tao/cap nhat object `CompassHUD`.
- Gan `CompassHudController`.
- Tu gan `Camera.main` va `Player` neu tim thay.

## Du lieu marker

Compass doc `MapMarker` co san trong project.

Dang hien:

- `Enemy`
- `Boss`
- `QuestAvailable`
- `QuestTarget`
- `Custom` neu `markerId` hoac ten co chu `waypoint`

Neu muon object hien tren compass, gan component `MapMarker` vao object do va chon type phu hop.

## Tuy chinh

Trong `CompassHudController` co the chinh:

- `Visible Angle`: goc nhin cua thanh la ban.
- `Marker Max Distance`: tam hien marker.
- `Anchored Position` va `Size`: vi tri/kich thuoc UI.
- Mau marker enemy, boss, quest, waypoint.

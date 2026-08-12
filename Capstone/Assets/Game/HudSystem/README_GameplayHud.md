# Gameplay HUD Setup

HUD nay dung Unity UI/uGUI de hop voi AA Map and Minimap System hien tai.

## Tao HUD trong scene

1. Mo scene can test.
2. Chay menu `Game Tools > GameToolThang > HUD > Create Gameplay HUD`.
3. Tool se tao/cap nhat object `GameplayHUD`.
4. Bam Play de xem:
   - Minimap goc trai tren.
   - Quest tracker duoi minimap.
   - Pet status goc trai duoi.
   - 6 pet slot doc, phim 1-6.
   - 4 skill slot duoi giua man hinh, phim Z/X/C/V.
   - TAB hint mo menu.

Tool se tu tat object `QuestTrackerHUD` cu neu con nam trong scene, de tranh hien 2 bang quest tracker chong len nhau.

## Du lieu pet that

`GameplayHUD` mac dinh dung `PetCommandHudProvider`.
Provider nay doc truc tiep:

- `PetCommandInput.activePet`
- `PetCommandInput.petSlots[0..5]`

Neu pet can hien level, HP, Energy/Mana, icon va skill, hay them component `PetHudRuntimeStats` vao prefab pet do.
Component nay chi la data source sach cho HUD, sau nay co the thay bang save/account/monster backend that.

UI chi goi:

- `SelectPetSlot(slotIndex)`
- `RequestSkill(skillIndex)`

Pet slot dung phim 1-6. Skill slot dung phim Z/X/C/V va chi nhan input khi gameplay khong bi UI menu/inventory khoa.
Gameplay that tu xu ly ben provider/system rieng. UI khong hard-code logic skill, heal, capture hay combat.

## Quest tracker

`GameplayHudController` tu tim `QuestManager` trong scene va doc quest active/tracked de hien thi.
Neu scene chua co `QuestManager`, vung quest tracker se trong.
HUD moi chi hien 1 quest entry de khong de UI ngoai man hinh bi roi. Quest tracker cu `QuestTrackerHUD` khong can bat song song voi HUD nay.

## Minimap

HUD khong tao lai minimap. Neu scene co `MinimapPanel` cua AA Map, HUD chi dat lai vi tri goc trai tren.
Neu khong co `MinimapPanel`, HUD hien placeholder de bao vi tri minimap.

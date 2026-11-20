# Sau Creature Spawn Zone da duyet

## Trang thai ban giao

- CREATED: 6 Zone prefab va 6 Spawn Table trong `Assets/Game/CreatureSystem/Runtime/SpawnZones/ApprovedSix/`, kem .meta rieng.
- CONFIGURED: Zone -> Table -> catalog OwnedPet hien co; gioi han khu vuc/Zone/Species, ID, Box trigger, effects va weights da gan.
- CONFIGURED/STATIC VERIFIED: Wild Bat01/02 va Dragon01/02 co du lieu combat/capture/factory hien co. Khong doi cac prefab nay. BasicCreatureEnemy duoc bo sung tren clone inactive neu can.
- BLOCKED: 6 Family con lai, KHONG duoc thay bang model-only, prototype skill hay chi so tu bia.
- PLAY VERIFIED: CHUA. Roslyn compile va test quy tac khong thay the Play Mode/Unity import.

Khong tu gan Zone vao DemoScene 1. Khong doi Zone_Bat_Example, SpawnTable_Bat, catalog, scene, starter hay save. Boss Lv28 khong duoc tao va khong nam trong cac table nay.

## Sau prefab va khoang cap

| Prefab | Zone | Species duoc duyet | Hien tai co the duoc chon spawn |
|---|---|---|---|
| Zone_Grass_Easy | 2-6 | chick 2-6 | BLOCKED, chua co loai san sang |
| Zone_Grass_Mid | 4-10 | chick 4-6; cat-electric 4-10; wolf 6-10 | BLOCKED, chua co loai san sang |
| Zone_Grass_Hard | 8-12 | cat-electric 8-10; wolf 8-12; snake-fire 8-12 | BLOCKED, chua co loai san sang |
| Zone_Cave_Entrance | 12-18 | bat 12-18; wolf 12-18 | bat; wolf BLOCKED |
| Zone_Cave_Mid | 14-20 | bat 14-18; snake-fire 14-20; ghost 14-20 | bat; hai loai con lai BLOCKED |
| Zone_Cave_Deep | 18-24 | ghost 18-22; egg-shadow 18-24; Dragon Dusk 18-24 | Dragon Dusk; hai loai con lai BLOCKED |

Ba Zone dong co bi khoa boi du lieu, KHONG phai boi NavMesh. Entry van duoc giu trong table voi ID/range duyet, prefab null va configurationBlocker ro rang. Khong danh dau chung CONFIGURED runtime day du.

Population dem theo loai DU DIEU KIEN, khong dem so form hay entry BLOCKED. Hien cac Zone hang chi co mot loai san sang nen 3-5 slot. Khi bo sung du lieu hop le cho loai con lai va bat dau phien moi, Zone co tu hai loai se dung 3-6 slot. Khong random lai population sau moi lan chet/capture.

## Level va Form

- Khoang roll la giao cua BaseLevel +/- Variance, Min/Max Zone, Min/Max khu vuc me va Min/Max Species. Giao rong thi loai entry va bao loi, khong clamp thanh mot cap gia.
- Sau khi chon Species, roll level trong giao tren; sau do chi chon Form du nguong.
- Dong co chi Form01 weight 100. Hang: Form01 weight75, Form02 weight25 KHI level ca the >=18; duoi18 chi Form01. Form03 weight0/disabled trong ca6table.
- Vi du bat o Cave_Mid chi roll14-18, khong len19/20. Tai level18 co75/25 cho form01/02; khong co nghia25% moi actor o ca Zone la form02.
- Level cao KHONG goi tien hoa. Metadata/form/stage/SkillSet/model do prefab quyet dinh.
- Moi Species weight1 (mac dinh trung tinh, co the chinh table); khong thay doi combat balance.
- Sau prefab moi giu khoang cach player5m, `allowNearPlayerFallback=false`; khong du cho thi retry. Zone_Bat_Example khong bi doi tuy chon fallback cu.
- Du lieu cu thieu cac field moi: gioi han0, `formEligibilityUsesIndividualLevel=false`, `allowPartialConfiguration=false`, `requireSupportedTypes=false`. Zone_Bat_Example giu cach tinh va cach xet BaseLevel cu.

## BLOCKED chi tiet

Da doc Family Definition va source prefab cua ca3form cho6Family duoi day. Ca18form chua du dieu kien tao WildEnemy hoan chinh:

| Family | Source Form01 | Du lieu con thieu / xung dot |
|---|---|---|
| chick | Assets/Prefabs/Trung/Pets/Chim Con.prefab | Chua co StatProfile/capture rate, SpeciesSkillSet/learnset. Source typing Wind=4 ngoai9he, khac dinh huong Normal/Flying; khong tu ghi de |
| cat-electric | Assets/Prefabs/Trung/Pets/Mèo Điện.prefab | Thieu StatProfile/capture rate, SkillSet/learnset. Electric hien co phu hop, giu nguyen |
| wolf | Assets/Prefabs/Trung/Pets/Sói Tí Nị.prefab | Thieu StatProfile/capture rate, SkillSet/learnset. Source Form01 dang Dark, khac dinh huong Normal; khong tu ghi de |
| snake-fire | Assets/Prefabs/Trung/Pets/Rắn Lửa.prefab | Thieu StatProfile/capture rate, SkillSet/learnset. Source Form01 Fire/Dark la song he hop le, khac dinh huong Fire; khong tu bo Dark |
| ghost | Assets/Prefabs/Trung/Pets/Ma Nhỏ.prefab | Thieu StatProfile/capture rate, SkillSet/learnset; Dark hien co duoc giu |
| egg-shadow | Assets/Prefabs/Trung/Pets/Trứng Ma Quái.prefab | Thieu StatProfile/capture rate, SkillSet/learnset; Form01 typing Unknown=0 chua du lieu hop le |

Source Form01 co PetController/Stats legacy, `usePrototypeSkillsWhenEmpty=1`, SkillSet null, DefinitionId trong va form `base`; khong coi4skill prototype la learnset da duyet. Source Form02/03 chu yeu model/Animator, chua co stats/metadata runtime.

Con thieu o6Family: runtime AnimationProfile/CreatureVisualBinding, WildEnemy combat/capture, Owned prefab va factory mapping tuong ung. Animation clip/controller goc da co, nhung khong tu doan state/timing/socket de lap mapping. Form02/03 chua co typing metadata; cac huong "co the" o prompt chua duoc ghi thanh typing moi.

Chua tao runtime shell gia cho6Family, chua copy skill Bat, chua sua Source Prefab/Family. Chi khi co BST/capture config va skill/learnset duoc duyet moi co the hoan thien cac prefab va gan vao nhung o null da chuan bi. Khong can doi ID/table/zone schema de bo sung sau nay.

## Bay buoc su dung va kiem thu

1. Dung Play. Mo scene can thu. Keo **Zone_Cave_Entrance.prefab** tu ApprovedSix vao scene de thu Doi truoc; khong bat dau bang Zone Grass dang BLOCKED.
2. Dat Box len san co NavMesh; chinh Center/Size/Yaw, giu IsTrigger. Zone oorigin trong prefab de nguoi dung dat vi tri; khong tu dua vao scene. Box dungY, khong parent tao shear.
3. Bam **Kiem tra cau hinh Zone**. Doc ro danh sach BLOCKED. Bat01/02 duoc validate typing9he, StatProfile/capture rate, Combat/Presentation/Catalog/loadout va factory. Loi prefab/catalog duoc bao rieng.
4. Bam **Kiem tra coverage NavMesh (khong bake)**. Moi AgentType duoc lay9mau, bao do cao/ngoaiBox. 0/9: kiem Surface da bat, AgentType va do cao Box. Coverage la mau thu, khong bao dam du vi tri/collider; tool KHONG bake hay sua Surface/Data.
5. Play: xem Slot, TargetPopulation, EligibleSpeciesCount, LastStatus va LastPlacementReport. Nhan du3-5 con Bat, khong vuot cap12-18. Neu khong spawn, doc bo dem NavMesh/height/Box/edge/player/actor/obstacle; ten vat can duoc hien khi tim thay. Thu Boxresize/yaw va vat can.
6. Ha ve0HP: Downed van chiem slot. Chi sau Deadketthuc/modelbienmat moi dem5s. Capture thanhcong: dung ca the giuID/genes/skills va luong save hien co; replacement coIDmoi. Capture thatbai khong giai phongslot. Chi dung save thu rieng khi QA.
7. StopPlay, thu Cave_Mid va Cave_Deep; doi chieu range Species va Form02>=18. Khong the PLAY VERIFY cac Family BLOCKED bang viec bat checkbox. Khi bo sung du lieu du, thu lai so luong3-6 va itnhatmotca the/loai neu du slot/vi tri.

Tinh nang duoc giu: EncounterSpawnPoint lam owner duy nhat moi slot; khong co death/capture/respawn manager thu hai. Actor da bat khong bi parent vao Zone, khong bi huy khi xoa Zone. Cac kiem thu save/physics/capture/multiZone thuc te van can Play Mode.

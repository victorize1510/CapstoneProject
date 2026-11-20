# Creature Spawn Zone

## Bat dau voi mot Zone

1. Dung Play. Mo `Assets/Scenes/DemoScene 1.unity`.
2. Keo `Assets/Game/CreatureSystem/Runtime/SpawnZones/Zone_Bat_Example.prefab` vao scene. Chua co Zone nao duoc tu dong gan vao scene.
3. Zone mau o `(48.7, -95.44241, 44.3)`, Box `18 x 6 x 18`, Center Y `1.5`. Co the doi vi tri, size, scale va yaw. Khong nghieng Box theo X/Z; khong dung parent tao shear.
4. Bao dam NavMesh tai khu vuc nay da bake va Surface dang bat. Zone khong tu bake/bat NavMesh. Khong can Rigidbody: Box trigger duoc dung cho phep thu hinh hoc, khong dung OnTriggerEnter.
5. Bam **Kiem tra cau hinh Zone** trong Inspector. Neu hop le, Play va xem `Slot dang giu / muc tieu`, `So loai du dieu kien`, `Tong da spawn`, `LastStatus`.

Hoac tao GameObject rong, Add Component `CreatureEncounterZone`. Script tu yeu cau BoxCollider; kiem IsTrigger va gan Zone Bounds. Zone ID phai de phan biet cac Zone, khong duoc dung de thay PetId/EncounterId.

## Inspector

| Field | Mau | Y nghia |
|---|---|---|
| Zone ID / Name | `bat-demo-zone-01` / Bat Demo Zone | Dinh danh quan ly Zone, khong doi save |
| Base Level / Level Variance | 5 / 2 | Moi ca the Lv3-7; gioi han cap game duoc giu nguyen |
| Respawn Delay | 5 s | Dem sau khi model chet bien mat, hoac capture thanh cong duoc owner tach ra |
| Minimum Spawn Separation | 2.5 m | Khoang cach XZ voi enemy dang ton tai va pet dang tham chien, ke ca encounter ngoai Zone |
| Minimum Player Spawn Distance | 5 m | Uu tien xa player |
| Allow Near Player Fallback | true | Chi bo dieu kien khoang cach player o 1/3 so lan thu cuoi; KHONG bo NavMesh, collider, ranh gioi hay separation |
| Position Attempts | 24 | Gioi han moi lan; that bai thu lai sau 1 s |
| NavMesh Sample Distance | 2 m | Tang chieu cao Box KHONG tu tang ban kinh sample; dat center Box gan san can spawn |
| Spawn Table | SpawnTable_Bat | Du lieu dung chung nhieu Zone |
| Owned Pet Catalog | CreatureRuntimeCatalog | Nam trong table; dung xac minh duong load sau capture, KHONG spawn prefab Owned |
| Form Level Overrides | rong | Ghi de moc cho tung DefinitionId + FormId chi trong Zone nay |
| Player | tuy chon | De trong se tim BasicPlayerMovement active; co the gan dung player root |
| Spawning Enabled / component enabled | true | Tat chi dung sinh moi; actor dang co van duoc theo doi chet/capture |

Neu co Spawn Table, danh sach Species va Owned Pet Catalog truc tiep tren Zone khong duoc dung. Khong co table thi dien hai muc nay tren Zone. Thay doi table/base level/population khi Edit Mode; dung va chay lai Play de ap dung toan bo cau hinh.

## Mot loai, ba dang

Mo `Assets/Game/CreatureSystem/Runtime/SpawnZones/SpawnTable_Bat.asset`:

- Species `definitionId = bat`, weight 1, Enabled.
- Form `01`, weight 70: `Assets/Game/CreatureSystem/Runtime/DemoSceneOne/WildEnemy_Bat01.prefab`.
- Form `02`, weight 25: `Assets/Game/CreatureSystem/Runtime/Bats/Wild/Wild_Bat02.prefab`.
- Form `03`, weight 5: `Assets/Game/CreatureSystem/Runtime/Bats/Wild/Wild_Bat03.prefab`.
- Minimum Zone Level = 0: mac dinh stage 0/1/2 tuong ung Zone Lv1/18/36. Co the dat so duong de override tren table, hoac dien Form Level Overrides tren Zone.

Ba form van la MOT loai, do dem theo DefinitionId. Zone chon ngau nhien 3-5 slot MOT LAN trong phien, khong roll lai so slot sau moi lan chet.

Bat02/03 hien co prefab Wild day du stats/combat/capture nhung CHUA co BasicCreatureEnemy serialize san. Zone them BasicCreatureEnemy vao BAN CLONE khi con inactive, giu SkillSet, model, profile va sockets goc. Khong gan model-only. Neu can prefab WildEnemy rieng de tai su dung ngoai Zone, tao Prefab Variant tu Wild_Bat02/03, them BasicCreatureEnemy, giu role Wild va capture adapter; khong thay prefab OwnedPet.

- Zone Lv5: chi form01, ca the Lv3-7.
- Zone Lv18: form01/02, ca the Lv16-20.
- Zone Lv32: form01/02, ca the Lv30-34, KHONG tu tien hoa form01.
- Zone Lv36: ca ba form, ca the Lv34-38.

Form va cap duoc chon DOC LAP; skill giu loadout prefab/learnset hien co, Zone khong tu trang bi skill moi theo level.

## Hai loai, nhieu dang

Tao table moi qua Create > Game > Creatures > Spawn Table (hoac duplicate table mau); gan catalog hien co.

1. Entry `bat` nhu tren, Species Weight 3.
2. Entry thu hai dung DefinitionId THUC cua loai da hoan thien, vi du `Dragon Dusk`, Species Weight 1. Gan cac prefab Wild day du dung form 01/02/03, weight va moc cap rieng. Khong gan Enemy khong bat duoc, Visual hoac OwnedPet.

Khong co asset/family thu hai nao duoc tao trong task nay. Zone chi cho form co profile bat duoc, Animator/SkillSet/binding hop le va Owned prefab khop form/stage/SkillSet trong catalog.

Zone co tu hai loai DU DIEU KIEN se chon 3-6 slot. Khi so loai khong vuot so slot, bo chon uu tien cac loai chua hien dien; sau do chon theo Species Weight, roi Form Weight. Weight 0/Enabled false loai entry khoi bo chon. Form bi khoa boi cap khong duoc tinh nhu mot loai khac. Vi tri khong hop le khong bi ep spawn de du so luong.

## Lifecycle va du lieu

- Moi slot co MOT EncounterSpawnPoint; Zone khong dang ky them Defeated hoac tao he death/capture khac.
- Downed van giu slot. Chet: het cua so bat -> Dead -> het animation -> an/destroy actor -> dem Respawn Delay -> chon loai/form/vi tri/cap moi.
- Capture: coordinator commit hien co -> IsWild false -> owner tach actor -> dem Respawn Delay. Actor duoc nhan KHONG bi an, huy hay parent vao Zone.
- Actor moi duoc gan ID rieng va khoi tao genetics mot lan trong parent inactive, truoc Awake/cooldown. EncounterLevel nhan cap Zone mot lan; Start khong roll lai cap 5. Khong goi evolution.
- Actor nam ngoai hierarchy Zone/lifecycle. Xoa Zone chi don clone con Wild khong CaptureLocked/StagedForSave; ca the da nhan duoc giu nguyen. Capture dang giao dich duoc de cho coordinator hien co ket thuc, khong cuong ep huy.
- Chi them nhanh Zone cho EncounterSpawnPoint. Owner cu khong co ZoneOwner van dung duong cu va home cu. Khong doi Party/BOX/catalog/save schema.
- Khong co persistence Zone qua reload scene. Zone la nguon encounter moi; khong tu ghi save nguoi choi.

## Checklist Play Mode (chua xac minh boi compiler)

1. Chi mot Zone: ghi TargetPopulation; cho 30 s, du 3-5 con thi khong vuot muc tieu. Thu 2 loai hop le: 3-6 va du moi loai neu du slot/vi tri.
2. Thu base 5, 18, 32, 36; doi chieu form/stage/SkillSet va level range; form01 Lv32 van form01. Moi clone co PersistentId/EncounterId khac nhau.
3. Danh mot con ve 0 HP: Downed van chiem slot, khong sinh som. Cho Dead va model bien mat, sau 5 s moi co replacement.
4. Bat thanh cong: dung ca the do chuyen Party/BOX, giu ID/genes/level/skill/HP theo coordinator; replacement ID moi, cooldown moi. Bat that bai khong giai phong slot. Dung save thu tach biet; khong reset save that.
5. Tat Surface/chan Zone bang collider/lam Box qua nho: khong sinh chong, LastStatus bao cho vi tri; mo lai thi retry. Thu Box xoay Y, scale khac nhau va player dung gan; fallback khong bo collision/separation.
6. Tat Spawning Enabled: khong sinh moi, actor cu van ket thuc chet/capture. Bat lai thi tiep tuc. Xoa Zone sau capture: pet da nhan con nguyen. Khong doi table/script trong Play khi kiem persistence.

Khong tu thay encounter hien tai trong DemoScene 1; Zone mau chi la prefab de keo vao khi can thu.

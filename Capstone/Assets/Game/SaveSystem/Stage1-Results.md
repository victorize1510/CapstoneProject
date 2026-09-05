# Dot 1: Nen du lieu va Save

## Pham vi

Mo rong he thong PlayerSaveController hien co, khong tao he save song song.
Khong thay doi UI, gameplay, scene, prefab, animation, sprite hoac package.
Prompt da doi chieu nam trong Stage1-Prompt.md.

## Dinh dang va bao ve du lieu

- PlayerSaveData version 6 gom quest, inventory, currency, pets, profile, achievements.
- Pet roster luu ID instance, ID definition, thu tu Party/Box, active pet,
  nickname/favorite, stats, skill loadout, evolution, resource investments va release tombstones.
- hasSkillLoadout phan biet loadout khong co trong save cu voi loadout co y de trong.
- Item definition co stableId va legacyIds; skill co skillId va legacySkillIds.
  ID hop le cu khong bi doi hang loat.
- File chinh: Saves/player_<slot>.json; ban du phong: .json.bak.
- Ghi file .tmp, flush xuong dia, roi thay file bang thao tac atomic.
  Neu he thong file khong ho tro thay atomic, save bao loi, khong xoa file chinh truoc.
- JSON duoc kiem tra truoc khi ghi: version, ID trung, tham chieu Party/Box,
  capacity, skill slots va cau truc co ban. Tu choi JSON co key trung.
- Primary hong thi thu backup. Lan ghi sau khi recovery khong de primary hong
  ghi de backup tot. Neu ca hai hong, chan save cho den khi recovery/reset ro rang.
- Save co version moi hon game khong bi downgrade hoac ghi de.
- Load that bai/nguon du lieu thieu thi chan autosave va SaveNow, bao ly do.
- Khong cho SaveNow ghi de slot cu truoc khi LoadNow thanh cong.
- Phan du lieu vang mat/null khong dong nghia reset subsystem. Dung structured JSON
  de giu null vi JsonUtility tu materialize class null thanh object mac dinh.

## Migration

- Doc version 1 den 5, dua ve version 6 trong bo nho; ID duoc giu nguyen.
- Voi save cu, equippedSkillIds co mat moi duoc coi la loadout da luu.
- Truoc lan ghi version 6 dau tien, sao nguyen byte ban cu hop le sang
  player_<slot>.json.pre-v6.bak. Khong ghi de archive nay o nhung lan sau.
- Save Quest rieng cu duoc migrate vao save chung; file Quest cu khong bi xoa.
- QuestSaveController uy quyen Save/Load cho PlayerSaveController khi cung manager/slot,
  tranh hai luong autosave doc lap cho cung Quest.
- Item prototype chua co asset van duoc khoi phuc tu metadata cu, khong tu y loai bo.

## Kiem thu da chay

Unity 6000.3.16f1 batch compile thanh cong. Bo test batch: **34 passed, 0 failed**.
Lan dau 28/31, phat hien loi null serialization; da sua va chay lai toan bo.

Bo test tao scene rong trong bo nho va thu muc temp GUID rieng, khong luu scene,
khong doc/ghi save that cua nguoi choi.

- Save chua ton tai; snapshot day du save-load-sua-save-load.
- Migration version 1..5 va archive giu nguyen byte.
- Thieu section, thieu nested field va customization null.
- Primary hong, backup tot; ca hai hong; future version; JSON trung key.
- Party/Box ID trung, reference thieu, active pet ngoai Party.
- Inventory restore lap lai, doi ten asset nhung giu ID, alias cu, prototype,
  capacity khong du thi giu nguyen inventory hien tai.
- Skill 2/4 slot, o trong giua, loadout rong, alias, skill khong ton tai.
- Quest claimed rewards khong bi reset/replay khi restore; Quest thieu definition
  khong xoa state cu; migration Quest rieng.
- Controller chan save khi chua load/du lieu hong/nguon du lieu thieu.
- Party/Box + nickname + favorite + health/level qua nhieu lan save/load.
- Thieu pet prefab catalog thi chan load truoc khi Inventory bi thay doi.

Entry point: Capstone.Game.SaveSystem.Editor.SaveSystemValidation.RunBatch.
Chi chay bang Unity -batchmode -executeMethod; tham so -saveValidationReport chon noi ghi JSON.
Fixtures v1..5 la du lieu test dai dien, khong phai toan bo save lich su cua nguoi choi.

## File thay doi so voi ban sao truoc Dot 1

- Game/SaveSystem: PlayerSaveData.cs, PlayerSaveController.cs, PlayerJsonFileSaveStore.cs.
- Game/HudSystem: PetHudData.cs, PetHudRuntimeStats.cs, PetPrefabCatalog.cs.
- Game/QuestSystem: QuestManager.cs, Save/QuestJsonFileSaveStore.cs, Save/QuestSaveController.cs.
- ThangTest/Inventory: MonsterItemDefinition.cs, InventoryItemSnapshot.cs, MonsterInventoryAdapter.cs.
- Moi: AtomicSaveFile.cs, PlayerSaveMigration.cs, Editor/SaveSystemValidation.cs,
  Stage1-Prompt.md, Stage1-Results.md va metadata Unity tuong ung.

## Gioi han va viec can du lieu that

- Item asset cu chua gan stableId van fallback ve ten asset. Skill cu chua gan skillId
  van nhan dien theo ten cu. Can gan ID co dinh va legacy alias truoc khi doi ten noi dung.
  Dot nay khong tu y sua asset/prefab de gan ID hang loat.
- Pet trong scene chua gan ID ro rang van dung co che fallback cu. Can gan persistent ID
  truoc khi doi ten/thu tu scene; pet bat moi da co GUID tu capture coordinator.
- Pet da luu nhung khong con trong scene can PetPrefabCatalog day du de tao lai.
  Test da xac nhan chan mat du lieu khi catalog thieu, chua kiem thu prefab that duoc spawn.
- Cac test tren la batch/editor, chua thay the Play Mode tren scene gameplay va build player.
  Animation/summon, doi scene va quit/pause autosave can smoke-test khi mo gameplay.
- Thay doi truc tiep public petSlots/activePet ngoai provider khong co event rieng;
  code goi truc tiep can RequestSave sau khi doi. Khong sua PetCommandInput o Dot 1.
- Khong cam ket rollback toan bo runtime neu mot event listener ben ngoai nem exception
  giua luc apply. Preflight chan cac loi du lieu da biet va file goc van duoc bao ve.
- Batch log co loi authorization tu plugin Unity MCP hien co, khong phai loi C# save.
  Khong sua cau hinh/plugin trong dot nay.

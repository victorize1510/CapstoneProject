# DemoScene 1: starter, Shop và vòng đời quái

Phạm vi là `Capstone/Assets/Scenes/DemoScene 1.unity`. DemoScene gốc, DefaultShopCatalog, prefab/model/controller gốc và save chơi thật không bị thay bằng dữ liệu QA.

## Chơi và nhận gói khởi đầu

Mở DemoScene 1 rồi Play. `DemoSceneOneSaveIsolation` dùng profile phát triển riêng trên E; `PlayerSaveController` tải xong mới gọi `DemoSceneOneStarterGrant`.

- Dạng 01: cấp 5; dạng 02: cấp 18; dạng 03: cấp 36. Tên và icon lấy từ prefab/catalog hiện có.
- Nhận thêm đúng 5 `capture-ball-basic-01` trong cùng lần ghi player save.
- Receipt tùy chọn `appliedStarterGrants: ["demoscene1.starter.v1"]` ngăn cấp lại sau khi dùng bóng, mở menu, đổi scene hoặc tải lại. Save vẫn là v14; save cũ thiếu trường này được hỗ trợ.
- Preview cũ chỉ được nhận diện bằng ID starter gốc hoặc tiền tố `preview-dragon-2-` / `preview-dragon-3-`, đúng dạng và không có thông tin bắt. Không dò theo tên hiển thị.
- Chỉ tăng cấp thấp hơn mốc yêu cầu; giữ IV/EV, tính khí, nickname, EXP đã kiếm và loadout. Hồi đầy HP cho các starter được nhận diện đúng một lần khi receipt được ghi thành công.
- Thiếu dạng thì tạo từ `DemoDragonCatalog`, điền ô Party trống trước, rồi BOX. Không thay thế cá thể đang có. Thiếu chỗ hoặc ghi file thất bại thì hoàn tác cả vật phẩm, cấp/HP, receipt và các cá thể vừa tạo.

Profile phát triển: `E:/UnityWork/CapstoneProjectData/PlayProfiles/DemoScene1/`. Bản release dùng thư mục DemoScene1 trong `Application.persistentDataPath`. QA luôn chuyển sang save tạm dưới artifact root trước Start, không chạy migration trên profile thật.

## Danh sách vật phẩm và Shop

NPC Shop có `ShopSource` trỏ riêng tới `Assets/Game/CreatureSystem/Runtime/DemoSceneOne/Shop.asset`.

| ID | Công dụng hiện có | Giá vàng |
|---|---|---:|
| healing-apple | Hồi tối đa 30 HP | 5 |
| Potion | Hồi tối đa 100 HP | 20 |
| mana-potion | Hồi tối đa 30 MP cho thú dùng MP | 15 |
| leaf-berry | 50 EXP mỗi quả khi tăng cấp từ tab Phát triển | 8 |
| capture-ball-basic-01 | Bóng bắt cơ bản x1 | 10 |
| capture-ball-rare-01 | Bóng hiếm, hệ số x1,5 | 30 |
| capture-ball-ultra-01 | Bóng cao cấp, hệ số x2 | 60 |
| dusk-stone | Tiến hóa theo rule hiện có | 100 |

Cả tám món có Stock = -1 (không giới hạn) và MaxPerPurchase = 99. Mua qua `ShopPurchaseService` hiện có, giữ kiểm ví/túi, chống nhấn lặp và cập nhật số lượng sở hữu. Bóng hiếm và cao cấp dùng cùng công thức thu phục thời gian thực với bóng cơ bản. Thuốc HP thường dùng cho thú còn sống.

Tiến hóa là `LevelOrItem`: đường cấp yêu cầu 18/36; Dusk Stone được phép tiến hóa sớm. Không đổi rule, ID hay avatar theo dạng.

## Choáng, Die, khói và sinh lại

`EncounterSpawnPoint` nằm trên object home riêng, không nằm trên prefab owned pet. Inspector gồm:

- `initialActor`: cá thể wild/enemy ban đầu của điểm gặp.
- `encounterPrefab`: prefab wild/enemy nguồn, không dùng prefab owned pet hoặc clone đã bị bắt.
- `openingEncounter`: điểm mở đầu luôn cấp 5; điểm khác dùng phân bố cấp 3–7 sẵn có.
- `respawnDelay`: 5 giây sau khi con cũ đã được tách/bỏ đi.
- `smokePrefab`: `DeathSmoke.prefab`, bản URP dùng pool từ CFXR3 Hit Misc F Smoke. Đã bỏ script demo, tự destroy và camera shake.
- `downedStarsPrefab`: ba sao fallback có sẵn của Capstone.
- `settings`: sao chép tham số AI đánh thường, phạm vi đuổi và territory hiện có cho cá thể mới.

Luồng: HP về 0 → 15 giây choáng và cho phép bắt → hết cửa sổ mới chốt chết/thưởng → Die → khói → ẩn thân sau khoảng 0,2 giây → bỏ cá thể chết → chờ 5 giây → tạo cá thể mới ở vị trí home an toàn. Nếu home bị chặn hoặc thiếu NavMesh hợp lệ, thử lại sau 1 giây.

`CreatureAction.Stun` được thêm cuối enum, không đổi chỉ số các action đã serialize. Có Stun thật thì gán trong `CreatureAnimationProfile.actions`; chưa có thì dùng Idle. Không dùng state Downed cũ vốn lấy từ clip Die. Hit/Death hiện có vẫn dùng Take Damage/Die theo profile.

Bắt thành công tách tham chiếu khỏi home sau commit save, giữ nguyên thú owned và HP theo quy tắc tối thiểu 25%. Không giết, ẩn hoặc tái sử dụng con vừa bắt. Home cũng chờ 5 giây rồi tạo cá thể wild mới với ID/di truyền/HP và trạng thái combat mới. Training dummy không dùng vòng đời này. Owner ẩn hoặc scene unload sẽ ngừng hẹn sinh và trả hiệu ứng về pool.

## Công cụ và bằng chứng

Các lệnh đều chạy rõ ràng qua `Tools → Capstone → PvE`, không tự chạy khi import:

3. Apply starter Shop and respawn (DemoScene 1): authoring lặp lại an toàn; chỉ lưu scene đã chỉ định và asset riêng.
4. Verify starter Shop and respawn (isolated saves): kiểm luồng chính bằng callback thật, lưu/tải thật và file-lock thật.
5. Verify full BOX rollback and scene unload: kiểm thiếu dạng, Party/BOX đầy, tạo cá thể rồi rollback, ba dạng rồng và dọn khi unload.

Báo cáo, danh sách file, save thử và ảnh engine: `E:/UnityWork/CapstoneProjectData/Artifacts/demoscene1-starter-shop-respawn-2026-11-16/`. Xem REPORT.md để biết kiểm nào đã thực chạy và giới hạn; không coi ảnh thiết kế hoặc compile là bằng chứng thao tác chuột, standalone hay Profiler.

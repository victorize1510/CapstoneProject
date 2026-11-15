# Rồng runtime trong DemoScene — 06/11/2026

## Nguồn đang dùng

`Assets/Scenes/DemoScene.unity` dùng:

- Actor rồng khởi đầu: `Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon01.prefab`.
- `PlayerSaveController → Pet Prefab Catalog`: `Runtime/Dragons/DemoDragonCatalog.asset`.
- Catalog ánh xạ `Dragon Dusk + 01/02/03` sang ba root OwnedPet tương ứng; giữ alias save cũ.
- `Visual/` và `Animation/` trong cùng thư mục runtime là model-only và semantic profile. Root model chuẩn hóa scale 1, root cá thể dùng scale 0,7 đã có của rồng khởi đầu DemoScene; tránh nhân scale hai lần và đổi kích thước khi nạp dạng đã tiến hóa.
- `Wild/`: ba prefab có adapter bắt thật, để designer gắn vào encounter hiện có khi cần. Lượt này không đặt thêm encounter/thú hoang vào scene.

Các asset ở `Pilot/` vẫn là bản thử. Runtime mới không phụ thuộc thư mục Pilot, không nhân bản FBX/texture. Family rồng tham chiếu visual/profile/Pet/Wild runtime mới; Enemy và AI chưa triển khai thêm.

## Nội dung được giữ

Actor khởi đầu giữ tên GameObject, parent, vị trí/góc/scale đã đặt, persistent ID, metadata và các cấu hình điều khiển. Hai model rồng 02/03 đang tắt sẵn trong DemoScene vẫn nguyên trạng; không tự biến chúng thành cá thể trong đội.

Đội mới vẫn theo cờ `Seed Dragon Preview Party On Fresh Save` sẵn có của DemoScene. Không bật/tắt cờ đó hoặc cấp thêm so với trước. Khi cờ hoạt động, hệ lấy đúng root dạng 02/03 trực tiếp từ catalog; catalog legacy trả root dạng 01 vẫn được khởi tạo theo luồng cũ. Không đổi luật tiến hóa, công thức chỉ số, ID skill hoặc save schema.

Script `PetEvolutionTestButton` chỉ được bỏ khỏi actor thay thế trong DemoScene. Source Trung vẫn giữ nguyên. Script test đó dùng T để bỏ qua điều kiện/save, nên không thuộc actor gameplay mới và không đụng vật phẩm nhanh T nữa.

Catalog `Assets/Game/HudSystem/Prefabs/DragonEvolutionCatalog.asset` cũ không bị đổi, các scene khác không được lưu lại. Unity từng serialize thêm một vài trường ngoài phạm vi khi lưu DemoScene; các block đó, gồm NavMeshData, đã được phục hồi chính xác từ baseline.

## Chỉnh về sau

- Avatar: Family → đúng Form → Avatar, rồi Bake lightweight avatar catalog.
- Animation: `Runtime/Dragons/Animation/Dragon01/02/03.asset`; ánh xạ action logic sang state thật, không đổi tên/ID clip hay skill đã lưu.
- Skill: bộ `Game/Skills/Data/Species/Dragon01/02/03.asset` và định nghĩa chung đang có. VFX/SFX vẫn gắn qua presentation/override hiện có.
- Portrait lớn vẫn do người làm art thêm; không lấy avatar slot làm hero.
- Wild chưa có AI/spawn mới. Enemy/NPC AI nằm ngoài lượt triển khai.

## Kiểm thực tế

Đã chạy 96 kiểm tra Editor Play Mode trong DemoScene, save mới trên E: nguồn catalog/runtime, đúng ba dạng khởi tạo, state Animator cả ba dạng, summon/switch qua Party và SummonDirector, dùng skill/cooldown, recall, tiến hóa qua cinematic, ẩn HUD, giữ ID/tên/loadout/IV/EV/nature/socket; save/load cá thể dạng 03 bị thiếu, load lặp không nhân đôi và giữ scale.

Đây là callback/API thật trong engine, không phải thao tác chuột/phím Windows. Không chạy standalone hoặc Profiler. Không tạo screenshot QA trong lượt này. Không ghi vào save thật, không commit/push.

Báo cáo và mã kiểm tái lập:
`E:/UnityWork/CapstoneProjectData/Artifacts/dragon-demoscene-runtime-2026-11-06/REPORT.md`.

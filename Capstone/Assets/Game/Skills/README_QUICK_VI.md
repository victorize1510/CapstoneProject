# Rồng, skill, VFX và âm thanh — hướng dẫn nhanh 29/10/2026

> **Hướng dẫn hiện hành 09/11/2026:** [Pet-Skills-VFX-Audio-Guide-2026-11-09.md](../../../../Docs/Pet-Skills-VFX-Audio-Guide-2026-11-09.md).
> Nội dung bên dưới là bản lịch sử. Các đoạn nói prefab legacy/Pilot là nguồn chính, profile VFX trống, Impact chưa nối combat hoặc Timed là preset rồng không còn mô tả hiện trạng. Runtime hiện dùng CreatureSystem/Runtime, normalized animation, combat executor thật, buff/bleed và số damage. Xem hướng dẫn mới trước khi chỉnh.

Các đường dẫn dưới đây nằm trong cửa sổ Project của Unity, bắt đầu bằng Assets/.

## Ba mục Cẩm nang

| Số Cẩm nang | Tên thật trong project | Prefab gameplay | Bộ skill |
|---|---|---|---|
| 01 | Dragon Dusk | Game/HudSystem/Prefabs/Dragon01Gameplay.prefab | Game/Skills/Data/Species/Dragon01.asset |
| 02 | Dragon Darkness (Dragon dark) | Game/HudSystem/Prefabs/Dragon03Gameplay.prefab | Game/Skills/Data/Species/Dragon03.asset |
| 03 | Dragon Nightfall (Dragon night) | Game/HudSystem/Prefabs/Dragon02Gameplay.prefab | Game/Skills/Data/Species/Dragon02.asset |

Số Cẩm nang độc lập số dạng tiến hóa/tên file. Vẫn tiến hóa Dusk → Nightfall → Darkness. Sửa danh mục tại **Game/CodexSystem/Resources/PetCodex/PetCodexCatalog.asset**; menu **Game Tools → GameToolThang → Pets → Create or Select Dragon Codex** chọn nhanh asset này.

Avatar hiện tra theo **CreatureSystem/Data/Families → đúng Form → Avatar**, sau đó Bake lightweight avatar catalog; không còn lấy cố định ảnh dạng 01. Prefab ngoài thư viện vẫn giữ Icon legacy. Ảnh lớn Cẩm nang gán riêng vào Portrait, không phóng avatar thành hero. Chưa tạo ảnh/model mới. Phần mô tả đang lấy metadata có sẵn của prefab (các dạng sau còn sao chép mô tả Dusk), cần nội dung riêng khi bạn hoàn thiện dữ liệu loài.

Provider runtime đọc danh mục đã soạn và nhận biết dạng đang sở hữu từ đội/Kho thật. Không lấy danh sách đội/Kho làm catalog. Loài chưa sở hữu vẫn che tên/hệ/ảnh theo quy tắc unknown; tìm bằng số vẫn được. Mở lại Cẩm nang sẽ cập nhật dữ liệu. Cẩm nang hiện lưu lịch sử và favorite theo dạng bằng các trường tùy chọn trong profile save v13, tương thích file cũ thiếu trường. Favorite không phụ thuộc favorite cá thể; ghi lỗi được rollback. Favorite cá thể Pet/BOX không bị đổi.

## Thêm / sửa skill

1. Dừng Play. Mở **Game/Skills/Data/Definitions/**: hiện có `Skill_bite`, `Skill_blast`, `Skill_projectile`, `Skill_wing_strike`.
2. Sửa **Display Name / Description / Icon / Cooldown Seconds**. Icon phải là Sprite; không đổi **Skill Id** của skill đã nằm trong save.
3. Skill mới: chuột phải → **Create → Capstone → Pet Skills → Skill**; thêm vào **Game/Skills/Resources/PetSkills/PetSkillCatalog.asset → Skills**.
4. Mở bộ loài ở bảng trên → thêm vào **Learnset**, đặt **Learn At Level**. **Default Loadout** chỉ là bộ khởi tạo cho thú mới, không ghi đè trang bị save cũ.
5. Prefab thú dùng **Pet Hud Runtime Stats → Species Skill Set**. Trong game, gán vào bốn ô qua tab Kỹ năng hiện có.

## Gắn VFX / âm thanh skill

1. Mở **Game/Skills/Data/Presentation/Presentation_<tên skill>.asset**. Bốn profile đã có nhưng hiện chưa gán `Effect Prefab` / `Sound`.
2. **Cast** = tụ chiêu; **Release** = phóng chiêu; **Impact** = trúng đích. Mỗi nhóm nhận prefab hiệu ứng và AudioClip riêng.
3. VFX Graph phải đặt trong **prefab có component Visual Effect**, rồi kéo prefab vào **Effect Prefab**; ParticleSystem cũng được. Kéo `.wav`/`.ogg` vào **Sound**.
4. **Release Mode = Timed**: đặt **Release Delay Seconds** tính từ lúc animation bắt đầu. Hoặc dùng Animation Event `OnPetSkillRelease` theo hướng dẫn đầy đủ.
5. Điền **Socket** (ví dụ `Mouth`), rồi trên prefab thú thêm **Pet Skill Presentation Player → Sockets** với key trùng và Anchor là xương/Transform thật. Thiếu socket sẽ phát tại root.
6. Chỉnh Scale/Offset, Volume/Pitch, Lifetime; gán Mixer Group SFX nếu có. **Skill Definition → Presentation** trỏ profile chung; **Species Learnset → Presentation Override** dùng khi một loài cần hiệu ứng riêng.

**Giới hạn thực tế:** Cast/Release đã có code phát. Impact chưa được combat/va chạm nào trong project gọi `NotifyImpact`; chỉ gán asset chưa làm nó tự phát hoặc tạo sát thương/đạn. Hook va chạm cần dùng token từ `SkillReleased`, rồi gọi `NotifyImpact(token, vị trí, pháp tuyến)`.

## Hiệu ứng / âm thanh chung của pet

- VFX gọi/thu đã có: **Game/PetSummon/PetVFX_Default.asset**, với Spawn Prefab / Recall Prefab; component sử dụng là **Pet Summon Release Effect → Vfx Profile**.
- Profile gọi/thu hiện **không có ô AudioClip**. Chưa có profile/hook âm thanh tập trung cho gọi/thu, tiếng kêu, trúng đòn, chết hay bước chân trong hệ Pet đang đọc. Không nhầm chúng với Sound của skill.
- Hướng dẫn đầy đủ: **Game/Skills/README_VI.md**. Kiểm thư viện: **Tools → Capstone → Pet Skills → Validate Library**.

## Cập nhật 05/11: DemoScene, badge và tiến hóa

- Xác nhận từ Companion/BAG dùng controller cinematic của DemoScene; dịch vụ đổi dữ liệu/model tại nhịp che trắng, rồi trả pet về scene/parent cũ. HUB/camera/input phục hồi khi kết thúc hoặc hủy; hủy sau commit không hoàn tác dữ liệu đã lưu.
- ElementUi là danh mục trình bày 9 hệ R2. Các enum mới chỉ nối thêm; không chuyển Wind thành Flying hoặc Unknown thành Normal. Dữ liệu legacy ngoài 9 hệ hiện Chưa phân loại cho đến khi tác giả duyệt.
- Skill Definition → Optional UI classification cấu hình nhãn Vật lý/Phép/không gây sát thương và các mô tả hiệu ứng. Chưa cấu hình thì UI nói thiếu dữ liệu. Đây không phải hệ gây sát thương/buff mới.
- Cơ hội bắt: CaptureOpportunitySignal chỉ nhận BeginOpportunity/EndOpportunity hoặc SetOpportunity từ sự kiện combat thật trên WildPetCaptureTarget. HUD đọc tín hiệu của đúng mục tiêu đang khóa; mặc định tắt. Chưa có luật combat phát tín hiệu thì không hiện cảnh báo; không suy ra từ HP hoặc xác suất.
- Xem Docs/Creature-Runtime-Pilot.md về bộ rồng thử; các root đó chưa thay catalog/spawn chính.

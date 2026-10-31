# Rồng, skill, VFX và âm thanh — hướng dẫn nhanh 29/10/2026

Các đường dẫn dưới đây nằm trong cửa sổ Project của Unity, bắt đầu bằng Assets/.

## Ba mục Cẩm nang

| Số Cẩm nang | Tên thật trong project | Prefab gameplay | Bộ skill |
|---|---|---|---|
| 01 | Dragon Dusk | Game/HudSystem/Prefabs/Dragon01Gameplay.prefab | Game/Skills/Data/Species/Dragon01.asset |
| 02 | Dragon Darkness (Dragon dark) | Game/HudSystem/Prefabs/Dragon03Gameplay.prefab | Game/Skills/Data/Species/Dragon03.asset |
| 03 | Dragon Nightfall (Dragon night) | Game/HudSystem/Prefabs/Dragon02Gameplay.prefab | Game/Skills/Data/Species/Dragon02.asset |

Số Cẩm nang độc lập số dạng tiến hóa/tên file. Vẫn tiến hóa Dusk → Nightfall → Darkness. Sửa danh mục tại **Game/CodexSystem/Resources/PetCodex/PetCodexCatalog.asset**; menu **Game Tools → GameToolThang → Pets → Create or Select Dragon Codex** chọn nhanh asset này.

Ba prefab hiện dùng chung icon cũ. Avatar ô thú lấy từ **Pet Hud Runtime Stats → Icon**; ảnh riêng Cẩm nang có thể gán vào **Portrait** của từng dòng catalog. Chưa tạo ảnh/model mới. Phần mô tả đang lấy metadata có sẵn của prefab (các dạng sau còn sao chép mô tả Dusk), cần nội dung riêng khi bạn hoàn thiện dữ liệu loài.

Provider runtime đọc danh mục đã soạn và nhận biết dạng đang sở hữu từ đội/Kho thật. Không lấy danh sách đội/Kho làm catalog. Loài chưa sở hữu vẫn che tên/hệ/ảnh theo quy tắc unknown; tìm bằng số vẫn được. Mở lại Cẩm nang sẽ cập nhật dữ liệu. Chưa có lịch sử khám phá hoặc favorite Cẩm nang lưu riêng; nút favorite theo loài còn khóa. Favorite cá thể Pet/BOX không bị đổi.

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

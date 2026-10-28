# Hướng dẫn kho kỹ năng thú — 22/10/2026

## 1. Hệ thống nằm ở đâu?

Trong cửa sổ **Project**, mở `Assets/Game/Skills/`:

| Thư mục | Nội dung |
|---|---|
| `Data/Definitions` | Mỗi file là một skill dùng chung: tên, icon, cooldown, animation mặc định |
| `Data/Presentation` | VFX/âm thanh lúc vận chiêu, phóng chiêu, trúng đích |
| `Data/Species` | Mỗi giống loài/dạng thú có bộ skill được học và cấp mở khóa |
| `Resources/PetSkills/PetSkillCatalog.asset` | Danh mục tra cứu chung và danh sách giống loài tự kết nối |

**Skill dùng chung → bộ học của loài → tiến trình/bộ trang bị riêng của từng con.**

Đã kết nối ba prefab `Assets/Game/HudSystem/Prefabs/Dragon01Gameplay.prefab`, `Dragon02Gameplay.prefab`, `Dragon03Gameplay.prefab`. Các dạng Dragon Dusk/Nightfall/Darkness cùng tham chiếu bốn skill gốc. Vẫn giữ `bite`, `blast`, `projectile`, `wing strike` để save cũ nhận đúng. Chưa tự gán VFX/âm thanh nghệ thuật vào các profile trống.

Menu công cụ: **Tools → Capstone → Pet Skills**:

1. **Create or Connect Existing Dragon Library**: kết nối ba prefab gốc; chạy lại không ghi đè định nghĩa/profile đã tạo hoặc thiết kế lại bộ học đã có.
2. **Validate Library**: kiểm ID/alias trùng, thiếu tham chiếu, skill ngoài catalog, default loadout sai.
3. **Open Guide**: mở tài liệu này.

Luôn dừng Play Mode trước khi sửa asset. Không dùng lệnh tạo thư viện để reset save.

## 2. Đổi icon/tên/cooldown cho skill đang có

1. Mở `Data/Definitions/Skill_bite.asset` (hoặc skill khác).
2. Sửa **Display Name**, **Description**, **Element**, **Cooldown Seconds** nếu cần.
3. Với icon PNG: chọn ảnh → Inspector → **Texture Type: Sprite (2D and UI)** → **Apply**.
4. Kéo Sprite vào trường **Icon** của Skill asset.
5. Không đổi **Skill Id** chỉ để đổi tên hiển thị.
6. Play lại. HUD và trang Kỹ năng đọc cùng định nghĩa. Không phải gán icon vào hai danh sách Skills/Learned Skills nữa khi loài đã kết nối thư viện.

Những bản copy cũ trong component được giữ để tương thích dữ liệu; đối với skill đã có trong bộ loài, định nghĩa chung là nguồn hiển thị/timing/cooldown hiện hành.

Ba bộ rồng chuyển từ dữ liệu cũ đang bật **Override Animation** để giữ nguyên state/timing của từng prefab. Tắt ô này nếu muốn loài lấy animation mặc định từ Skill asset; icon/cooldown/presentation chung vẫn được dùng bình thường.

## 3. Tạo một skill mới

Ví dụ tạo `fireball`:

1. Chuột phải tại `Data/Definitions` → **Create → Capstone → Pet Skills → Skill**.
2. Đặt tên file `Skill_fireball`; **Skill Id = fireball**, **Display Name = Cầu lửa**.
3. Gắn icon, mô tả, hệ, cooldown.
4. Điền **Animator States** bằng TÊN STATE trong Animator Controller, không chỉ tên file clip. Có thể khai báo nhiều tên dự phòng theo thứ tự.
5. **Animation Duration**: thời lượng state phát. **Animation Fade**: thời gian chuyển. **Windup Seconds**: thời gian chờ TRƯỚC khi state bắt đầu. **Recovery Seconds**: phần hồi động tác.
6. Chuột phải tại `Data/Presentation` → **Create → Capstone → Pet Skills → Presentation**. Tạo `Presentation_fireball` và kéo vào **Presentation** của skill.
7. Mở `Resources/PetSkills/PetSkillCatalog.asset`, thêm Skill asset vào **Skills**.
8. Thêm skill vào **Learnset** của loài được phép học; chỉ thêm vào catalog chưa làm mọi loài học được.
9. Chạy **Validate Library**.

`criticalRateBonus` / `criticalDamageBonus` được giữ theo hợp đồng dữ liệu cũ; thư viện này không bổ sung công thức sát thương mới. Tạo định nghĩa/VFX không tự biến một animation thành viên đạn gây damage.

### Tra cứu từ code

```csharp
using Capstone.Game.Skills;
using UnityEngine;

var catalog = Resources.Load<PetSkillCatalog>("PetSkills/PetSkillCatalog");
if (catalog != null && catalog.TryGet("projectile", out var definition)) {
    // Đọc tên/icon/presentation dùng chung. Cache catalog trong component gọi thường xuyên.
    Debug.Log(definition.displayName);
}
```

Lúc chiến đấu vẫn gọi `PetHudRuntimeStats.RequestSkill(slotIndex)` cho ô ĐANG TRANG BỊ, không bỏ qua luồng kiểm thú đang hoạt động/input/cooldown để gọi thẳng VFX. Chỉ tra thấy skill trong catalog không có nghĩa mọi loài/cá thể được phép sử dụng nó.

## 4. Cho từng loài học skill

1. Tạo **Create → Capstone → Pet Skills → Species Skill Set** tại `Data/Species`.
2. **Species Id**: tên loài chính xác, khớp `Pet Collection Metadata → Species` nếu muốn tự kết nối các instance trong scene.
3. **Catalog**: kéo `PetSkillCatalog.asset` vào.
4. Trong **Learnset**, thêm mỗi skill MỘT lần:
   - **Skill**: kéo Skill asset.
   - **Learn At Level**: cấp được học. Ví dụ Cắn cấp 1, Cầu lửa cấp 5.
   - **Override Animation**: bật khi cơ thể/Animator loài này dùng tên state hoặc timing khác. Điền đủ timing riêng, giá trị 0 là có chủ ý.
   - **Presentation Override**: tùy chọn VFX/âm thanh biến thể của loài; bỏ trống sẽ dùng profile chung của skill.
5. **Default Loadout**: thứ tự ô skill khởi đầu cho một thú mới. Các skill phải có trong Learnset và đủ cấp mới được điền. Số ô vẫn do **Equipped Skill Slot Count** của thú quy định, từ 2 đến 4.
6. Gán asset vào **Pet Hud Runtime Stats → Species Skill Set** trên prefab thú.
7. Với instance trong scene chưa gán trực tiếp: thêm bộ loài vào **Species Sets** của catalog chung; `Auto Resolve Species Skill Set` sẽ tìm theo Metadata.Species khi Awake.
8. Nếu tạo prefab thú MỚI từ bản copy: menu dấu ba chấm của **Pet Hud Runtime Stats** → **Apply Species Defaults (New Pet Authoring Only)**, rồi lưu prefab. Lệnh này xóa bộ skill/progress khởi tạo trên prefab để lấy mặc định loài, không dành cho thú đã chơi.

Sửa Default Loadout không ghi đè bộ trang bị của save cũ. Tiến trình mở khóa của từng con được giữ. Thêm skill/cập nhật learnset lúc Editor đang dừng rồi Play lại để instance nạp bộ học mới.

## 5. Gắn VFX và âm thanh

Mở Presentation asset. Có ba nhóm **Cast / Release / Impact**:

| Nhóm | Khi nào phát? | Ví dụ |
|---|---|---|
| Cast | Yêu cầu hợp lệ và animation được chấp nhận; bắt đầu cả phần chờ windup | Tụ năng lượng |
| Release | Đúng mốc thời gian hoặc Animation Event | Tia/chớp/phóng năng lượng |
| Impact | Code va chạm thật gọi API kèm mã cast hợp lệ | Nổ tại vị trí trúng |

Trong từng nhóm:

- **Effect Prefab**: kéo prefab chứa `ParticleSystem` và/hoặc `Visual Effect` của VFX Graph.
- **Sound**: kéo AudioClip. Có thể chỉ có VFX hoặc chỉ có âm thanh.
- **Socket**: khóa điểm phát như `Mouth`, `Body`, `Ground`.
- **Follow Socket**: bật để hiệu ứng bám theo điểm phát; bỏ để giữ ở vị trí đã sinh.
- **Local Offset / Local Euler Angles / Scale**: căn vị trí, hướng, kích thước mà không sửa prefab chung.
- **Volume / Pitch**: âm lượng/cao độ.
- **Lifetime Seconds**: giới hạn 0.05–30 giây, hết thì dọn/trả pool. Chừa thời gian cho âm thanh và đuôi hạt; âm thanh dài hơn lifetime sẽ bị cắt.

Profile còn có **Mixer Group**, **Spatial Blend** (1: âm thanh thế giới 3D; 0: 2D), khoảng nghe Min/Max Distance. Chọn mixer SFX hiện có nếu game đã dùng. Âm thanh click UI vẫn thuộc hệ UI riêng.

Không gắn vào đây prefab có script gameplay tự gây damage, tự Destroy root hoặc tự đổi parent; bộ này sở hữu vòng đời hiệu ứng và tái sử dụng chúng. Nếu prefab đã có AudioSource phát tự động, tránh thêm cùng tiếng ở Sound khiến phát hai lần.

### Socket trên prefab thú

1. Mở prefab thú, tìm component **Pet Skill Presentation Player** cạnh Pet Hud Runtime Stats.
2. Trong **Sockets**, đặt khóa `Mouth`, kéo Transform/xương miệng thật vào **Anchor**.
3. `Body` và `Ground` có thể dùng Transform con được căn đúng trên cơ thể.
4. Khóa phải khớp với profile. Nếu thiếu anchor/khóa, hệ thống dùng root thú; điều này không có nghĩa vị trí miệng đã được căn đúng.
5. Với model tiến hóa: các socket trỏ xương model cũ cần được cấu hình lại theo model mới. Relay animation tự gắn lại khi animator được thay; socket do người tạo asset quản lý.

### Hai cách đặt thời điểm phóng

**Timed**: đặt Release Mode = Timed. `Release Delay Seconds` tính từ lúc animation STATE bắt đầu; hệ thống cộng riêng Windup Seconds. Ví dụ windup 0.1s + release delay 0.25s → phát ở khoảng 0.35s sau yêu cầu.

**Animation Event**: đặt Release Mode = AnimationEvent. Thêm Animation Event vào đúng frame phóng của clip, chọn hàm **OnPetSkillRelease**. `PetSkillAnimationRelay` phải cùng GameObject với Animator; player sẽ gắn relay lúc runtime, kể cả animator của model tiến hóa. Có thể thêm relay sẵn khi authoring để chọn hàm trong giao diện. Không cấu hình cả Timed và event cho cùng thời điểm.

Nếu clip không phát event, hệ thống không tự đoán phóng chiêu. Một mốc release chỉ được nhận một lần trong mỗi cast.

### Nối va chạm thật (dành cho lập trình viên)

`PetSkillPresentationPlayer.SkillReleased` cung cấp `(castId, skillId)` khi ra chiêu. Bộ projectile/combat giữ `castId`, khi trúng đích gọi:

```csharp
presentation.NotifyImpact(castId, hit.point, hit.normal);
```

Impact chỉ nhận sau release, trước hết Impact Window và tối đa một lần cho mỗi cast. Cast đã hủy không được phát lại. Không gọi Impact lúc vừa bấm nút hoặc tự giả định mục tiêu đã trúng. Projectile nhiều hit/AOE cần thiết kế hợp đồng hit riêng trước khi dùng; phiên bản này là một impact cho mỗi cast.

**Chưa có trong phần này:** hệ đạn mới, tìm đường viên đạn, hitbox, sát thương skill mới hoặc âm thanh/VFX nghệ thuật hoàn chỉnh. Profile là chỗ nối asset và trình diễn, gameplay đang có được giữ.

## 6. Kiểm nhanh sau khi gắn asset

1. Chạy Validate Library khi dừng Play.
2. Play với save thử nghiệm tách biệt. Chọn/triệu hồi đúng thú và dùng skill.
3. Kiểm icon trong HUD và tab Kỹ năng; trang bị skill vẫn preview rồi xác nhận.
4. Nghe/nhìn Cast–Release khớp động tác; kiểm góc nhìn camera gameplay.
5. Bấm lại trong cooldown: không phát thêm hiệu ứng/âm thanh.
6. Thu hồi hoặc tắt thú trong lúc vận chiêu: không còn hiệu ứng đang bám hoặc âm thanh treo.
7. Hai con dùng chung một Skill asset: cooldown/progress không ảnh hưởng lẫn nhau.
8. Với Impact, chỉ báo đạt sau khi kiểm luồng va chạm thật; fixture gọi API không chứng minh hệ đạn của game đã chạy.

## 7. Save và lỗi thường gặp

- **Không đổi ID đã phát hành.** Nếu bắt buộc đổi, thêm ID cũ vào **Legacy Ids** và kiểm save trước khi giao. Không trùng ID/alias giữa hai skill.
- Save vẫn dùng `PetRuntimeStatsSaveData` / `PetSkillProgressSaveData` cũ. Không lưu prefab, texture, âm thanh hoặc cooldown đang chạy vào save.
- Một skill hiện trong Catalog nhưng chưa có trong Learnset: loài đó chưa học được.
- Skill chưa xuất hiện: kiểm Learn At Level, progress mở khóa và bộ loài được gán đúng.
- Đổi default nhưng save không đổi: đúng chủ ý, save giữ lựa chọn của người chơi.
- Không VFX/âm thanh: profile/cue có asset chưa, Animator có nhận state không, thú đã triệu hồi chưa, cooldown/input có chặn không?
- Phát ở chân: thiếu Mouth anchor hoặc sai khóa socket.
- Hồng/mất texture: kiểm shader/material tương thích URP của asset VFX.
- Không nhận event: kiểm clip, tên hàm, relay cạnh Animator, Release Mode và frame event nằm trong thời lượng animation.

Asset runtime/source và tài liệu này ở project. Log, test save, capture, profiler và build thử phải ở `E:/UnityWork/CapstoneProjectData/Artifacts/pet-skill-library-2026-10-22/`, ngoài Git. Không commit/push tự động.

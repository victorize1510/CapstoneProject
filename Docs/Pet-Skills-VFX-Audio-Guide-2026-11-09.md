# Hướng dẫn hiện hành: pet, skill, animation, VFX và âm thanh

Ngày 09/11/2026. Tổng hợp hệ đang có sau các lượt 06–08/11, đối chiếu lại source. Các đường dẫn `Assets/...` bên dưới dùng trong cửa sổ Project của Unity; project nằm ở `D:/Download/ProjectVTC/CapstoneProject/Capstone/`.

Ưu tiên tài liệu này khi hướng dẫn tháng 10 hoặc báo cáo các đợt trước nói khác. Ví dụ thông tin cũ “Impact chưa nối combat”, “skill cần mục tiêu khóa” và cooldown Cắn 1,2 giây đã được thay thế. Lượt viết hướng dẫn này không chạy Play, không thay dữ liệu gameplay/save.

## 1. Các phần đã làm và hiện trạng

| Phần | Có thể dùng hiện tại | Giới hạn cần nhớ |
|---|---|---|
| Thư viện thú | Family cha chứa các Form; avatar theo đúng dạng | Có dữ liệu authoring không đồng nghĩa đủ prefab/stat/animation để spawn |
| Rồng | Dusk 01 → Nightfall 02 → Darkness 03, bộ OwnedPet/Wild/Visual/Animation runtime | Dùng Runtime/Dragons, không dùng Pilot hoặc prefab gameplay legacy để mở rộng mới |
| Dơi | Ba form, OwnedPet/Wild và Enemy đánh thường | Enemy chỉ đánh pet đang triệu hồi; chưa có AI skill/NPC đầy đủ |
| Skill data | Catalog chung → bộ học theo loài/form → loadout từng cá thể | Bốn ô trang bị khác kho skill; thêm vào catalog chưa làm mọi thú học được |
| Combat | Đánh thường theo ATK; melee, projectile, targeted một hit, self buff; bleed ba nhịp | Chưa có AoE, đạn xuyên nhiều địch, lốc nhiều tick hoặc hệ status tổng quát |
| Trình diễn | Cast/Release/Impact, socket, audio, FX, số damage, crit, recoil | Gán VFX không tự tạo luật damage hoặc debuff |
| Tiến hóa | Giữ cá thể/progress/loadout; đổi model/avatar/profile theo form, HUB ẩn trong cinematic | Form mới phải có learnset và animation mapping của các skill cần giữ |
| HUB | 1–6 chọn/gọi pet, Backspace thu, Z/X/C/V, P, G, R/T | Bốn ô skill mặc định chỉ hiện khi pet sẵn sàng; không phải lỗi thiếu UI |
| Kiểm thử | Dummy, engine assertions, save thử trên E, ảnh runtime | Không có chứng nhận standalone hoặc Profiler chuyên sâu cho lượt combat 08/11 |

Thư viện nền có 11 Family/33 dạng. Hiện sáu dạng Rồng/Dơi có tích hợp runtime theo các vai trò kể trên. Không xem 27 dạng còn lại là đã hoàn thiện chỉ vì thấy chúng trong Family hoặc bảng kiểm kê.

## 2. Hiểu hệ dữ liệu để gắn đúng chỗ

**Skill dùng chung → learnset của loài/form → skill học/trang bị của từng cá thể.** Animation và hình/âm có thể khác nhau theo form dù dùng cùng skill ID.

| Asset/component | Chức năng | Nơi chính |
|---|---|---|
| `PetSkillDefinition` | Tên, icon, hệ, cooldown; trỏ Combat và Presentation | `Assets/Game/Skills/Data/Definitions/` |
| `PetSkillCatalog` | Danh mục skill và auto-binding species | `Assets/Game/Skills/Resources/PetSkills/PetSkillCatalog.asset` |
| `PetSpeciesSkillSet` | Learnset, cấp học, default loadout, override trình diễn | `Assets/Game/Skills/Data/Species/Dragon01/02/03.asset` |
| `PetSkillCombatProfile` | Delivery, power, range, đạn, buff, bleed, bước lùi | Mở qua ô Combat của definition; bốn combat rồng hiện vẫn ở `Assets/Game/CreatureSystem/Runtime/Bats/Combat/` để giữ GUID: Combat_bite.asset, Combat_blast.asset, Combat_projectile.asset, Combat_wing_strike.asset |
| `PetSkillPresentationProfile` | Mốc animation, VFX/Sound của ba giai đoạn | `Assets/Game/Skills/Data/Presentation/` |
| `CreatureAnimationProfile` | skillId → action logic → state thật | `Assets/Game/CreatureSystem/Runtime/Dragons/Animation/Dragon01/02/03.asset` |
| `CreatureVisualBinding` | Profile animation, skill set và socket của model | Component trên GameObject Animator trong Visual prefab |
| `PetHudRuntimeStats` | HP, bộ học/trang bị/cooldown cá thể; API gọi skill | Component trên root OwnedPet |

Bộ Dơi riêng nằm tại `Assets/Game/CreatureSystem/Runtime/Bats/Skills/`, gồm `Bat01/02/03.asset` và các definition/presentation riêng. Không sửa nhầm Cắn của Dơi khi muốn sửa Cắn của Rồng.

## 3. Sửa nhanh một skill đang có

1. Dừng Play. Tìm `Skill_bite`, `Skill_blast`, `Skill_projectile` hoặc `Skill_wing_strike` trong Definitions.
2. Sửa **Display Name, Description, Icon, Element, Cooldown Seconds**. Icon kéo vào phải là Sprite.
3. Sửa damage trong asset được gắn ở **Combat**; sửa VFX/âm thanh ở asset **Presentation**.
4. Nếu loài đang dùng **Presentation Override** trong Learnset, sửa override đó; thay profile chung sẽ không thay trình diễn của loài đã override.
5. Play lại để actor nạp dữ liệu, mở Thú đồng hành → Kỹ năng để kiểm và trang bị bằng luồng preview/xác nhận.

**Không đổi Skill Id chỉ để đổi tên hiển thị.** Không đổi ID `wing strike` thành `wing_strike`: dấu cách trong ID hiện có là dữ liệu thật. Đổi tên file asset không phải đổi ID. Chỉnh definition/profile chung tác động mọi loài đang cùng tham chiếu asset đó.

### Thông số rồng hiện tại

| Phím mặc định | Tên / ID | Công dụng | Hồi chiêu |
|---|---|---|---:|
| Đánh thường | basic | 100% ATK; không cộng cứng 20 khi có stats | Theo nhịp/SPD đang có |
| Z | Cắn / `bite` | 120% ATK + 10% ATK mỗi giây × 3 nhịp bleed | 2 s |
| X | Gầm cường hóa / `blast` | +20% ATK/SP.ATK trong 6 s; không stack | 10 s |
| C | Đạn năng lượng / `projectile` | 160% SP.ATK, một đạn/một địch | 3 s |
| V | Chém gió / `wing strike` | 180% ATK, một lưỡi gió/một địch | 4,2 s |

`Power = 1.2` nghĩa là 120%, **không nhập 120**. `Offense Buff Percent = 20` nghĩa là +20%; `Bleed Power = 0.1` nghĩa là mỗi nhịp 10% ATK vật lý, không phải 10% HP địch.

Physical dùng ATK/DEF, Magical dùng SP.ATK/SP.DEF. Công thức giảm damage giữ `raw × 100/(100 + phòng thủ)`. CRIT giữ theo stats cá thể. Số nổi là HP thực mất sau phòng thủ và giới hạn HP còn lại, không phải raw damage. Element hoặc nhãn UI không tự đổi công thức, gây cháy/đóng băng hay thêm tương khắc.

`Optional UI Classification` chỉ là nhãn/mô tả. Chọn Buff/Dot/Slow ở đó không tạo hiệu ứng gameplay. Các trường bonus crit/skill level còn có trong dữ liệu cũng không nên coi là một công thức tăng damage riêng đã được executor hiện hành áp dụng.

## 4. Cho một loài/form học và dùng skill

1. Mở Species Skill Set của form cần chỉnh.
2. **Catalog** trỏ catalog chung. Thêm một phần tử **Learnset → Skill**, đặt **Learn At Level**.
3. Mỗi skill chỉ xuất hiện một lần trong Learnset. Skill phải có trong `Catalog → Skills`.
4. **Default Loadout** là thứ tự ô khởi đầu cho thú mới; tối đa theo Equipped Skill Slot Count, hiện dùng bốn ô. Các skill phải có trong Learnset và đủ cấp.
5. Trên **Visual prefab → Animator object → Creature Visual Binding**, đặt **Skill Set** đúng form. Root OwnedPet có **Pet Hud Runtime Stats → Species Skill Set** tương ứng. Binding của model có thể thay lại bộ này khi model được bind; đừng sửa một chỗ rồi để chỗ còn lại trỏ bộ cũ.
6. Thêm mapping animation cho skill mới theo mục 6; thêm presentation theo mục 5.
7. Nếu cần auto-resolve cho actor legacy, thêm bộ loài vào `Catalog → Species Sets`, khớp chính xác `PetCollectionMetadata.Species`. Family ID, tên hiển thị và Species không được tự coi là cùng một trường. Ưu tiên binding rõ ràng của prefab runtime.

**Save cũ:** đổi Default Loadout không ghi đè bốn ô đã lưu. Dùng UI đổi skill để thay trang bị. Bộ học giữ tiến trình đã có, nên không dùng reset để chữa một cá thể đã chơi. Menu component **Apply Species Defaults (New Pet Authoring Only)** chỉ dành cho authoring prefab thú mới; nó xóa dữ liệu khởi tạo skill/progress trên đối tượng đang chọn.

**Tiến hóa:** loadout giữ theo skill ID, không tự trở về bộ mặc định. Form 02/03 cần tiếp tục có những ID đó trong Learnset và animation profile; nếu không thì dữ liệu có thể còn nhưng không đủ mapping để thi triển. Skill mới của form sau được quản lý qua learnset/cấp học, không tự thay ô cũ.

## 5. Gắn VFX và âm thanh

Mở Presentation profile đang thực sự được loài dùng. Có thể giữ VFX hiện tại và chỉ thay Sound, hoặc ngược lại.

| Nhóm | Thời điểm | Ví dụ | Follow Socket |
|---|---|---|---|
| **Cast** | Cast được chấp nhận, có thể bắt đầu từ bước lùi | Tụ năng lượng | Thường bật |
| **Release** | Mốc phát đòn của animation | Chớp miệng, vòng gầm, tiếng vung | Tùy hiệu ứng |
| **Impact** | Va chạm được executor xác nhận | Nổ/trúng đích; surface cue khi đạn chạm tường | Thường tắt |

Trong từng nhóm:

- **Effect Prefab:** kéo prefab chứa ParticleSystem hoặc Visual Effect. VFX Graph cần đặt trong GameObject có component **Visual Effect**, lưu thành prefab rồi kéo prefab, không kéo graph asset trực tiếp.
- **Sound:** kéo AudioClip WAV/OGG đã import. Có thể để Effect Prefab trống và chỉ phát Sound.
- **Socket:** khóa điểm phát; điền `Mouth`, `Body`, `Ground` hoặc khóa tự tạo có anchor thật.
- **Follow Socket:** hiệu ứng di chuyển theo anchor. Impact có world position do collision cung cấp nên không cần bám thú.
- **Actor Space:** vị trí có thể theo socket nhưng hướng/offset lấy theo trục actor; hữu ích khi xương hàm bị xoay khác trục. Tắt để dùng trục socket.
- **Local Offset / Local Euler Angles / Scale:** căn riêng cho cue, không kéo méo model để chữa VFX.
- **Volume / Pitch:** bắt đầu khoảng 0,2–0,4 và Pitch 1 rồi nghe tại camera gameplay. Runtime có biến thiên pitch nhẹ 0,95–1,05 bằng RNG trình diễn riêng.
- **Lifetime Seconds:** phải đủ đuôi hạt và `độ dài âm / pitch`; hết cue sẽ bị dọn, audio có thể bị cắt nếu lifetime quá ngắn.
- **Mixer Group:** gán nhóm SFX hiện có nếu dùng mixer. **Spatial Blend = 1** là âm thanh thế giới; 0 là 2D. Min/Max Distance quyết định khoảng nghe.

**Phân biệt đạn thật và chớp miệng:** hình bay theo đạn đặt ở **Combat → Projectile Visual**. Release chỉ nên là flash/tiếng phóng. Gắn cùng viên đạn vào cả hai nơi sẽ tạo thêm một hình không có simulation hoặc khiến tưởng bị bắn hai lần. Aura buff đặt ở **Combat → Buff Visual**, tồn tại theo Buff Duration; vòng gầm một lần đặt ở Release.

Prefab VFX nên chỉ chứa hình/âm. Không dùng prefab demo tự gây damage, đổi Time.timeScale, tự Destroy root hoặc tự chạy camera shake: executor/pool đang sở hữu các việc đó. Tránh AudioSource Play On Awake phát trùng với ô Sound. Preset projectile/buff hiện dùng ParticleSystem/Trail; Graph ở các cue đã có Reinit/Stop, còn Graph custom làm projectile/buff cần kiểm lại vòng đời pooling.

Asset hiệu ứng mới: `Assets/Game/Skills/Data/CombatFeel/` có BiteRelease/BiteImpact, EnergyCharge/EnergyOrb/EnergyImpact, WindBlade/WindImpact, RoarRelease/RoarAura và BleedTick.

### Socket đúng để không bắn từ chân

1. Mở **Visual_Dragon01/02/03.prefab**, chọn object chứa Animator và CreatureVisualBinding.
2. Trong **Sockets**, `Mouth` trỏ xương hàm/Transform miệng, `Body` trỏ thân, `Ground` trỏ điểm nền.
3. Nếu hướng xương không hợp, tạo Transform con `Socket_Mouth` để căn local position/rotation; gán Transform này làm Anchor.
4. Combat có **Release Socket** riêng; Presentation mỗi cue cũng có **Socket**. Cả hai phải đúng theo mục đích.
5. Kiểm cả ba form vì rig/scale khác nhau. Đặt điểm nhả đạn ở trước miệng và trên mặt nền; đạn tạo chồng đất/vật cản sẽ bị chặn thật.

Socket của visual được copy sang PetSkillPresentationPlayer khi bind model/tiến hóa. Sửa danh sách runtime tạm trên actor sẽ không bền khi rebind. Thiếu key/anchor thì code fallback về root; đó là lý do phổ biến hiệu ứng phát ở chân.

### Cùng skill/animation nhưng khác VFX theo loài

Ví dụ giữ skill `projectile` chung:

1. Duplicate Presentation chung thành `Presentation_projectile_dragon` và `Presentation_projectile_bat`.
2. Đổi Effect Prefab, Sound, socket, màu/scale và timing trên từng profile.
3. Trong Learnset của mỗi loài/form, phần tử skill đó → **Presentation Override** kéo đúng profile.
4. Giữ chung PetSkillDefinition nếu tên/cooldown/power/luật đánh là chung.

Khác ATK/SP.ATK tự tạo khác damage thực. Nếu cần **hệ số Combat khác riêng theo loài** hoặc cooldown khác riêng, hiện chưa có Combat/Cooldown Override trong learnset: tạo definition với ID mới cho biến thể, hoặc yêu cầu mở rộng code. Không tạo hai definition trùng ID trong catalog.

## 6. Gắn animation và canh nhịp

Không cần đổi tên toàn bộ file animation thành Bite/Run. Dùng tên hành động logic để ánh xạ:

`skillId projectile → action Projectile → state thật Base Layer.Projectile Attack`.

1. Trên Visual, Animator trỏ controller runtime phù hợp rig. Không gắn clip Generic của loài khác chỉ vì cùng tên hành động.
2. Mở CreatureAnimationProfile của form: **Actions** nối action sang tên **state thật** trong Animator; **Skills** nối ID sang action đó. Tên clip và tên state có thể khác nhau.
3. Skill mới muốn dùng lại động tác phóng đạn: thêm mapping `fireball → Projectile`; không nhất thiết thêm action enum hoặc đổi state.
4. Với prefab production có CreatureAnimationProfile, mapping này được ưu tiên. Chỉ sửa **Learnset → Animator States** sẽ không thay được state do driver đang resolve; Override Animation vẫn có các thông số duration/fade và phục vụ actor legacy không có semantic profile.
5. State không tồn tại/mapping thiếu thì sửa dữ liệu thật; không gọi thẳng TryPlay để bỏ qua lỗi/gate.

### Chế độ hiện hành: Normalized Animation

Trong Presentation chọn **Release Mode = Normalized Animation**:

- **Release Normalized = 0.36**: phát ở khoảng 36% chuyển động, không phải 0,36 giây.
- **Chain Normalized = 0.85**: sau 85% có thể blend sang động tác tiếp. Luôn đặt sau mốc release.
- **Playback Speed = 1** giữ tốc độ gốc; Gầm hiện 1,25. Controller runtime cần float **CombatSpeed**, và state dùng tham số đó làm Speed Multiplier. Các controller rồng đã gắn; controller mới thiếu tham số sẽ không tự có chức năng này chỉ vì điền 1,25 trên profile.
- **Animation Duration** ở dữ liệu nên theo clip thật; dùng để ước tính cửa buffer. Driver có watchdog khoảng 5 giây, nên chiêu vận quá lâu cần mở rộng code có chủ đích.
- Bộ đệm nhận một lệnh hợp lệ trong cửa cuối recovery, lệnh mới nhất thay lệnh cũ; không tự tạo combo hoặc auto spam.

Mốc rồng: Cắn 0,36; Gầm 0,43; Đạn 0,30; Chém gió 0,45. Chain lần lượt 0,85/0,85/0,85/0,90. Gầm hiện dùng **Blast Attack + âm/VFX gầm**, không có clip Roar mới. Z vẫn là Cắn, không phải Cào.

**Timed / Animation Event** còn giữ để tương thích. Khi dùng AnimationEvent, hàm relay là `OnPetSkillRelease`, relay ở cùng object Animator. Không thêm event release thứ hai cho bộ rồng Normalized hiện tại. Đừng sửa Release Delay Seconds để mong đổi mốc normalized: trường đó phục vụ đường Timed.

## 7. Ví dụ tạo một skill mới: Cầu lửa

Đây là hướng dẫn authoring, chưa tạo asset Cầu lửa trong lượt này.

1. Duplicate Skill_projectile thành Skill_fireball. Đặt **Skill Id = fireball**, Display Name = Cầu lửa; xóa alias `projectile` đã copy, trừ khi đang làm migration có chủ đích.
2. Duplicate Combat và Presentation thành hai asset riêng rồi gán lại vào definition mới. Duplicate definition đơn thuần vẫn tham chiếu profile cũ.
3. Combat: **Delivery = Projectile**, Damage Kind = Magical, Power = 1.6, Range = 14, Projectile Speed = 12, radius khoảng 0,18, Lifetime = 2; gắn hình đạn ở Projectile Visual, Release Socket = Mouth. Đây là giá trị ví dụ để bắt đầu, không tự gây cháy.
4. Presentation: Normalized Animation; mốc nhả theo clip, ví dụ 0,30 nếu dùng cùng clip rồng. Cast = tụ; Release = flash/tiếng bắn; Impact = nổ/tiếng trúng. Gắn hiệu ứng màu lửa do bạn chọn.
5. Thêm definition vào **PetSkillCatalog → Skills**.
6. Thêm vào Learnset của các form được học, đặt cấp học. Thêm vào Default Loadout chỉ nếu muốn thú mới bắt đầu với skill đó.
7. CreatureAnimationProfile của mỗi form thêm **Skills: fireball → Projectile**; action Projectile đã trỏ state phóng đạn của form.
8. Chạy Validate Library. Với save thử, vào Kỹ năng → Đổi kỹ năng → ô cần đổi → Cầu lửa → xác nhận. Không sửa save thật hoặc cấp skill vào mọi loài để thử.

Với đòn cận chiến chọn Melee, canh range/half-angle; Targeted là một hit trực tiếp khi release nếu mục tiêu hợp lệ, không phải AoE. SelfBuff hiện chỉ có buff ATK/SP.ATK phần trăm, làm mới thời gian. Muốn heal/shield/slow/stun, nhiều hit hoặc giữ nút charge thì cần code tương ứng, không chỉ chọn nhãn hoặc kéo VFX khác.

## 8. Tiếng/hiệu ứng chung của thú và số damage

| Muốn đổi | Sửa ở đâu |
|---|---|
| Tụ/phóng/trúng của từng skill | Presentation của skill hoặc Presentation Override |
| Hình đạn bay / aura buff | Combat → Projectile Visual / Buff Visual |
| FX/âm đánh thường và nhịp bleed | `Assets/Game/CreatureSystem/Resources/CombatFeelSettings.asset` → Basic Impact/Sound, Bleed Impact/Sound |
| Độ rung camera / hitstop crit | Cùng asset → Camera Amplitude/Duration, Critical Visual Pause; đặt 0 để tắt |
| Font/cỡ/màu/quỹ đạo số damage | `Assets/Game/HudSystem/FloatingCombatText.cs`; hiện chưa có bộ Inspector style riêng |
| FX gọi/thu, màu, tốc độ, độ tan/rút | `Assets/Game/PetSummon/PetVFX_Default.asset`, component PetSummonReleaseEffect → Vfx Profile |
| Âm gọi/thu/idle/bước chân/chết chung | Chưa có một Pet Audio Profile/hook tập trung đủ các sự kiện này; cần nối thêm code/animation event rõ ràng |

PetVFXProfile gọi/thu nhận **PetSummonVfxInstance**, không phải ô nhận mọi prefab ParticleSystem tùy ý; giữ contract prefab đó. Skill Sound không thay thế tiếng chung cho toàn vòng đời pet.

Số damage hiện tự xuất hiện từ damage đã commit: physical sáng ấm, magical xanh lạnh, crit lớn màu champagne, bleed nhỏ đỏ. Không cần gắn một Canvas/Text lên từng địch. Chỉ phát animation hoặc VFX không làm xuất hiện số. Recoil không đẩy collider; pause crit chỉ giữ Animator cục bộ, không đổi Time.timeScale. Camera shake cần PlayerFreeLookRig/Cinemachine tương thích; scene thiếu rig không bị tự tạo camera mới.

## 9. Prefab, form, avatar, tiến hóa và mang sang scene khác

### Dùng đúng lớp prefab

- **Source của Trung:** giữ làm nguồn mesh/rig/material/clip; không gắn cả PetController và Enemy AI lên đây.
- **Visual:** hình + Animator + CreatureVisualBinding/socket, không sở hữu AI/save/health.
- **OwnedPet:** root thú đồng hành, controller/metadata/stats/visual.
- **Wild:** root thú hoang có capture adapter; không tự có AI tấn công.
- **Enemy:** root combat/AI enemy. Dơi có BasicCreatureEnemy; Rồng runtime production hiện không có thư mục Enemy riêng, các enemy trong Pilot không được coi là bản enemy production mới.

Đường dẫn chính: `Assets/Game/CreatureSystem/Runtime/Dragons/` và `Runtime/Bats/`. Source-only 27 dạng còn lại cần bộ runtime/profile/collider/data riêng trước khi đưa vào vòng chơi. Không cần sửa FBX/texture để tạo mỗi vai trò.

### Family và hình theo form

Mở `Assets/Game/CreatureSystem/Data/Families/Family_Dragon_Dusk.asset`: Family cha là dòng Rồng, ba Forms 01/02/03, không phải lấy con 01 làm cha chứa hai con kia.

- Avatar nhỏ: gắn **Forms → đúng Form → Avatar**, rồi **Tools → Capstone → Creatures → 2. Bake lightweight avatar catalog**. Runtime đọc bản nhẹ theo DefinitionId + FormId, không nạp cả thư viện model để hiện một avatar.
- Portrait 2D lớn của Companion: hiện đọc **`Assets/ThangTest/UIThang/GUIArt/Resources/UI/Companion/Portraits.asset`**, phần tử Definition Id/Form Id → **Large Illustration**. Avatar Crop là tùy chọn crop slot riêng. **Family.HeroPortrait hiện là trường authoring, chưa phải nguồn mà Companion đang đọc**; chỉ gắn vào đó chưa làm hình lớn hiện trong panel.
- Dòng Rồng giữ DefinitionId **Dragon Dusk** qua các form 01=Dusk, 02=Nightfall, 03=Darkness. Không đổi ID theo tên hiển thị hoặc dùng số Cẩm nang thay FormId.
- Stat hiện tra profile trong `Assets/Resources/PetStats/` theo loài/dạng. Chỉnh đúng DragonDusk/DragonDuskWinged/DragonDuskElder hoặc Bat01/02/03. Chỉ thay tham chiếu Stat Profile trong Family không tự thay cơ chế lookup/runtime prefab.
- Family ghi nhận tham chiếu authoring; sửa Skill Set/Visual trong Family không tự viết lại mọi prefab đã sinh. Kiểm binding trên Visual và OwnedPet thật.

### Scene khác

1. Dùng player/Party/Input/HUB/EventSystem hiện có; tránh thêm chồng EventSystem hoặc bộ input thứ hai. Không chỉ copy Canvas ảnh của HUB.
2. Dùng OwnedPet runtime phù hợp và nối vào PetCommandInput/roster theo luồng của scene.
3. `PlayerSaveController → Pet Prefab Catalog`: catalog mở rộng hiện dùng **`Assets/Game/CreatureSystem/Runtime/CreatureRuntimeCatalog.asset`**, có mapping Rồng 01/02/03 và Dơi 01/02/03. Catalog save trỏ OwnedPet, không trỏ Wild/Visual/source.
4. Kiểm mặt nền, collider, layer, NavMesh và kích thước actor. Enemy có fallback tránh vật cản nhưng không phải một hệ tìm đường vòng thay NavMesh.
5. Giữ service tiến hóa và EvolutionPresentationScope hiện có để HUB/input/camera được ẩn/phục hồi đúng; không để lại PetEvolutionTestButton bypass bằng T trên actor gameplay.
6. Kiểm save load form sau tiến hóa và avatar đúng dạng. Mang sang scene khác vẫn phải QA scene đó; chưa có kết luận mọi scene đều đã kiểm.

## 10. Dùng trong game và kiểm nhanh

1. Vào DemoScene bằng save thử riêng. Phím **1–6** hoặc radial **P** chọn/gọi thú; **Backspace** thu.
2. Bốn ô Z/X/C/V chỉ hiện khi pet đã sẵn sàng; ẩn ngay khi recall bắt đầu. GameplayHUD → **Skill Bar Display Mode** có WhilePetSummoned/AlwaysVisible; dùng WhilePetSummoned theo cấu hình đã chốt.
3. Chọn skill từ Companion theo luồng **Đổi kỹ năng → chọn ô → chọn skill → xem trước → xác nhận**. Chỉ xem/hover không tự đổi trang bị.
4. Thử trên **TrainingDummy_Bat_Demo** gần quầy chợ: 5.000 HP, tự hồi sau chết, không EXP/EV/quest, không bắt được. Enemy thật khác dummy: Dơi enemy nhắm pet đã gọi và có thưởng theo cấu hình.
5. Skill ưu tiên mục tiêu khóa hợp lệ, mục tiêu đang đánh, địch gần có đường nhìn rồi hướng pet. Không có mục tiêu vẫn có animation/VFX và cooldown; đánh hụt không có số damage. Đạn trúng địch đầu tiên, tường chặn.
6. Nhìn Cast → Release → Impact khớp động tác; nhìn/nghe từ camera chơi, không chỉ Scene view. Thử lại cả ba form, kể cả sau tiến hóa.
7. G thử với thú hoang bắt được và bóng thật: tap ném qua service, giữ mở bảng chọn rồi thả đóng, không tự ném sau hold. BAG vẫn có luồng chọn bóng → Bắt thú. Không tự cấp bóng vào save thật.

R/T dùng item nhanh được gán qua BAG. Shift+G giữ chức năng đổi visual player khi HUD chiếm G. Cảnh báo “Cơ hội bắt” cần flag combat thật, không tự suy ra từ HP/xác suất. Không bật checkbox ngẫu nhiên chỉ để làm cảnh báo hiện.

Các kiểm tra của lượt combat 08/11: 314 ca ma trận và 52 ca bổ sung, engine 1920×1080/1366×768; có phím/chuột thật và Backspace. Không đổi kết quả đó thành chứng nhận cho asset mới bạn vừa gắn. Mỗi thay đổi art/timing cần chạy lại ca liên quan; chưa kiểm standalone/Profiler chuyên sâu, physical hold P/G dài hoặc nghe/duyệt âm trên thiết bị của bạn.

## 11. Lỗi thường gặp

| Biểu hiện | Kiểm theo thứ tự |
|---|---|
| Không có skill trong kho | Catalog có definition; learnset có skill; đủ cấp/unlock; binding visual/root đúng set; save cũ có progress gì |
| Skill có ảnh nhưng bấm không chạy | Pet đã gọi/ready; không có modal/gate; cooldown; Combat có gán; AnimationProfile có skillId/action/state thật |
| Không thấy VFX mới | Species Presentation Override đang che profile chung; đúng effect prefab; lifetime/scale/URP shader; không đang sửa asset legacy |
| Skill bắn từ chân hoặc lập tức trúng đất | Key/anchor Release Socket và cue Socket; Mouth đặt ở trước miệng, không chồng sàn/vật cản |
| Animation chạy nhưng damage 0 | Gán Combat; Delivery/range/LOS/cone/va chạm; mục tiêu sống/được đánh/không CaptureLocked; VFX không phải hitbox |
| Đạn nhìn như bắn hai lần | Có đặt viên đạn ở cả Release và Projectile Visual không; prefab có script demo tự bắn không |
| Âm không có/bị cắt | Sound đúng AudioClip; Mixer mute/volume; Spatial Blend/khoảng cách; AudioListener; lifetime/pitch; Play On Awake hoặc audio trùng |
| Thay state ở learnset không tác dụng | Production ưu tiên CreatureAnimationProfile; kiểm Skills → action → state của đúng form |
| Thay default loadout nhưng save vẫn cũ | Chủ ý giữ save; đổi bằng UI hoặc thử thú mới trên save thử, không reset cá thể thật |
| Sau tiến hóa mất chức năng skill | Form mới thiếu learnset/mapping/presentation/socket của ID đang trang bị |
| Avatar luôn giống form 01 | Gắn Avatar từng form + Bake; kiểm DefinitionId/FormId; không trỏ catalog save luôn về prefab 01 |
| Gắn Hero Portrait mà hình lớn chưa hiện | Companion hiện dùng Portraits.asset → Large Illustration, không đọc Family.HeroPortrait |
| Chỉnh xong lại về mặc định | Đã chạy menu preset/rebuild; hoặc sửa trong Play thay vì source asset/prefab |

## 12. Khi nào cần sửa C#?

**Không cần** khi chỉ thay icon/tên/cooldown/power, loài được học, state có sẵn, ba cue VFX/Sound, socket, loại delivery đã có, buff ATK/SP.ATK hoặc bleed đúng contract hiện tại.

**Cần** cho cơ chế mới: AoE/nhiều tick/nhiều mục tiêu, đạn xuyên/bật nảy/homing đặc biệt, combo/charge, status/buff khác, AI dùng skill, audio vòng đời chung, skin theo cá thể, hoặc power/cooldown override riêng từng loài mà vẫn chung một skill ID.

Các script quan trọng để lập trình viên mở:

- `Assets/Game/HudSystem/PetHudRuntimeStats.cs`: request, learnset/loadout/cooldown, bind form.
- `Assets/Game/Skills/PetCombatActionDriver.cs`: một động tác, buffer, normalized release, cancel/gate.
- `Assets/Game/Skills/PetSkillCombatExecutor.cs`: targeting, bước lùi, đạn/va chạm, hit/buff/bleed.
- `Assets/Game/Skills/PetSkillPresentationPlayer.cs`: cue/socket/audio, token; không tự quyết định HP.
- `Assets/Game/CreatureSystem/CreatureAnimationDriver.cs`: mapping semantic action/state.
- `Assets/Game/CreatureSystem/CreatureCombatant.cs`, `CombatDamage.cs`, `CombatBleed.cs`: offense/buff, kết quả damage và vết thương.
- `Assets/Scripts/Thắng/PetSystem/DummyEnemy.cs`: commit HP/defeat, báo damage; PetController điều phối pet/đánh thường hiện có.
- `Assets/Game/HudSystem/FloatingCombatText.cs`, `Assets/Game/CreatureSystem/CombatFeedback.cs`: số nổi và phản hồi hình/âm/camera.

Gọi skill đang trang bị từ code bằng API có gate, ví dụ `pet.GetComponent<PetHudRuntimeStats>().RequestSkill(0)` cho ô 1; caller vẫn phải là luồng input được cho phép. `CreatureAnimationDriver.TryPlay(...)` chỉ phát hình, không gây damage; không gọi thẳng NotifyImpact hoặc tự trừ máu từ ParticleSystem để vá skill.

## 13. Công cụ và tài liệu liên quan

- **Tools → Capstone → Pet Skills → 2. Validate Library**: kiểm catalog/learnset/ID/alias/default loadout. Validator này còn kiểm một số prefab legacy; pass không chứng minh collider/socket/timing của mọi prefab runtime mới đã đúng.
- **Tools → Capstone → Creatures → 2. Bake lightweight avatar catalog**: cập nhật avatar.
- **Tools → Capstone → Creatures → 3. Validate library and write report on E**: rà dữ liệu thư viện, không tự hoàn thiện loài còn thiếu.
- **Tools → Capstone → Creatures → 14. Apply combat feel and skill balance**: áp lại preset đã giao; **đã chạy rồi**, không cần bấm để sử dụng. Có thể ghi đè tuning vừa sửa.
- Menu 4/5 là Pilot, 7/9/12/13 là các bước setup/rebuild theo đợt; không bấm hàng loạt để “sửa lỗi”, đặc biệt sau khi đã tune art/skill/prefab riêng. Giữ backup trước khi chủ động rebuild.

Chi tiết kỹ thuật/bằng chứng: `Docs/Combat-Feel-2026-11-08.md`, `Combat-Practice-Freecast-2026-11-07.md`, `Dragon-DemoScene-Runtime.md`, `Bat-Combat-Enemy-Setup.md`, `HUB-R11-Setup.md`, `CaptureSystem-Setup.md`. Báo cáo cũ ghi hiện trạng tại thời điểm đó, không phải luôn là hành vi mới nhất.

QA/save thử/capture/log/build đặt dưới `E:/UnityWork/CapstoneProjectData/Artifacts/<task>/`, không vào Assets/Git; giữ tối đa 10 rolling frame và dọn đúng file task tạo. Giữ asset runtime và `.meta` của chúng trong project, bảo toàn ảnh thiết kế/source/save/backup. Nếu Unity treo/crash thì dừng, không tự restart hoặc chạy thêm lượt QA.

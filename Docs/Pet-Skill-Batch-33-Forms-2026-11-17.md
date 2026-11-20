# 11 Family / 33 dạng: kiểm kê và learnset đề xuất

Nguồn: Family hiện có, `Assets/Prefabs/Trung/Pets`, `Icons`, `Skills`, catalog skill và catalog Cẩm nang. Đây là bảng authoring; chưa phải xác nhận 33 pet chơi được. Hướng dẫn runtime vẫn ưu tiên `Docs/Pet-Skills-VFX-Audio-Guide-2026-11-09.md`.

## Kết quả kiểm kê

- Có **11 Family, mỗi Family đủ ba formId 01/02/03**, trỏ đến 33 prefab nguồn khác nhau.
- **Rồng bóng tối và Dơi** có sáu bộ skill cùng prefab runtime và animation profile. Chín Family còn lại chưa có skillSet/prefab runtime trong Family.
- Có **7 Skill Definition** thật trong catalog: `bite`, `blast`, `projectile`, **`wing strike`** (có dấu cách), `bite.normal`, `orb.dark`, `pulse.dark`.
- Trung cung cấp **30 prefab VFX và 30 PNG icon**, không phải 30 Skill Definition có damage/cooldown/animation hoàn chỉnh.
- Không sao bốn skill prototype từ model nguồn sang các loài: một số model có cùng bốn fallback do setup thử trước đây.
- Có 11 Family không có nghĩa 11 Family đã được duyệt tiến hóa. Hai Family Rồng/Dơi có cờ reviewed; chín Family còn lại chưa duyệt. Đặc biệt thứ tự Trứng dưới đây là dữ liệu đang có, cần đối chiếu hình/model trước khi chốt thiết kế.

## Tên, dạng và số Cẩm nang

Số trong bảng là **đề xuất đăng ký**, chưa ghi vào catalog runtime. Tên đẹp có thể đổi ở lớp hiển thị; không đổi `definitionId`, formId, skillId, GUID hoặc ID lịch sử/favorite đã lưu.

| Số đề xuất | Family ID giữ nguyên | Dạng 01 → 02 → 03 | Tình trạng |
|---|---|---|---|
| 001–003 | Dragon Dusk | Rồng Chạng Vạng → Rồng Đêm Tối → Rồng Bóng Tối | Có runtime; Family đang ghi tên cuối là **Dragon Bóng Tối** |
| 004–006 | bat | Dơi Nhỏ → Dơi Ma Cà Rồng → Dơi Chúa Tể | Có runtime |
| 007–009 | cat-electric | Mèo Điện → Mèo Tia Chớp → Mèo Sấm Sét | Có nguồn, chưa có runtime |
| 010–012 | chick | Chim Con → Gà Lôi → Gà Gió Lốc | Có nguồn, chưa có runtime |
| 013–015 | dragon-fire | Khủng Long Lửa → Rồng Lửa Bay → Rồng Lửa Địa Ngục | Có nguồn, chưa có runtime |
| 016–018 | egg-shadow | Trứng Ma Quái → Quái Trứng Quỷ → Quái Trứng Lực Sĩ | Có nguồn; cần duyệt thứ tự; nguồn viết “Lực SĨ” |
| 019–021 | ghost | Ma Nhỏ → Ma Mặt Nạ → Ma Nguyền Rủa | Có nguồn, chưa có runtime |
| 022–024 | shell | Ốc Nhà → Ốc Gai → Ốc Mượn Hồn Vua | Có nguồn, chưa có runtime |
| 025–027 | snake-fire | Rắn Lửa → Rắn Bùng Cháy → Rắn 3 Đầu | Có nguồn, chưa có runtime |
| 028–030 | sun-blossom | Hướng Dương Nảy Mầm → Hướng Dương Tiên → Hướng Dương Nữ Hoàng | Có nguồn, chưa có runtime |
| 031–033 | wolf | Sói Tí Nị → Sói Bóng Tối → Người Sói Quỷ | Có nguồn, chưa có runtime |

**Cẩm nang hiện chưa đúng bảng này:** catalog Resources chỉ có ba rồng, đang là Dusk #1, Darkness #2, Nightfall #3. Theo thứ tự tiến hóa trong Family thì Nightfall phải đứng trước Darkness. Không đổi ID `dragon-nightfall`/`dragon-darkness` để sửa số. Chưa tự thêm 30 entry rỗng: provider cần metadata đúng và phải giữ việc che loài chưa khám phá.

Metadata `Species` của prefab runtime Rồng vẫn là `Dragon Dusk` / `Dragon Nightfall` / `Dragon Darkness`, đồng thời đang khớp `speciesId` của các skill set. Vì vậy sửa tên Việt trong Family chưa tự làm mọi màn Việt hóa. Khi triển khai tên hiển thị phải tách nhãn tiếng Việt khỏi khóa lookup/save; không đổi riêng trường Species rồi làm mất auto-bind hoặc tiến trình.

`formId 01/02/03` là dạng, khác với level. Mốc Rồng 18/36 đã được giao trước đây; không dùng hai mốc đó để tự cấu hình tiến hóa tất cả loài. Các số 18/36 trong learnset đề xuất dưới đây là **cấp học skill**, không phải rule tiến hóa đã được duyệt.

## Luồng dữ liệu và ID

`Skill Definition dùng chung → Learnset của loài/dạng → tiến trình/ô trang bị/cooldown của từng cá thể`.

- `Skill_bite.asset` là tên file. `skillId = bite` là khóa gameplay/save. `displayName = Cắn` là chữ người chơi thấy.
- Không đổi `bite` thành `Cắn` trong ID. Đổi tên hiển thị không đổi ID. Với skill mới, dùng ID ổn định như `electric_orb` và tên hiển thị “Quả cầu điện”. Đây là ID dự thảo, chưa có asset runtime.
- **`wing strike` là ID đã có dấu cách**, không sửa thành `wing_strike`; dấu gạch dưới chỉ đang có trong tên file.
- Cùng skill, cùng cơ chế, khác màu VFX/tiếng giữa các loài: một definition; dùng `Presentation Override` ở learnset. Chỉ tạo skill khác khi cơ chế/thông số thật sự khác.
- `orb.dark` đã là đạn tối dùng chung. “Quả cầu bóng tối” của Trung nên là ứng viên trình diễn cho skill này, không tạo thêm ID chỉ để đổi tranh/VFX.
- Default Loadout chỉ dành cho cá thể mới. Không chạy reset để ép loadout lên thú đã lưu.

## Learnset đề xuất

Ký hiệu `@12` = học ở cấp 12. Các hàng mới chỉ ở JSON authoring, `approved: false`; chưa tạo skill giả hoặc tự đưa vào HUD.

| Family | Learnset | Hai ô mặc định cho thú mới |
|---|---|---|
| Rồng bóng tối | Giữ nguyên cả ba asset: `bite@1`, `blast@1`, `projectile@1`, `wing strike@1` | Giữ bốn ô hiện có |
| Dơi | Giữ ba asset: `bite.normal@1`, `orb.dark@1`, `pulse.dark@12` | `bite.normal`, `orb.dark` |
| Mèo điện | `bite.normal@1`, `electric_orb@1`, `electric_spark@8`, `lightning@18`, `thunder@36` | Cắn thường, Quả cầu điện |
| Chim | `peck@1`, `wind_cut@1`, `tornado@18` | Mổ, Chém gió |
| Rồng lửa | `bite.normal@1`, `fireball@1`, `heat_up@12`, `flamethrower@18`, `dragon_breath@36` | Cắn thường, Quả cầu lửa |
| Trứng bóng tối | `bite.normal@1`, `orb.dark@1`, `protect@12`, `dark_spirit@18`, `black_hole@36` | Cắn thường, Cầu tối |
| Ma | `orb.dark@1`, `dark_spirit@1`, `dark_bats@12`, `pulse.dark@18`, `black_hole@36` | Cầu tối, Linh hồn |
| Ốc | `bite.normal@1`, `water_bubble@1`, `defense_curl@12`, `water_vortex@18`, `water_cannon@36` | Cắn thường, Bong bóng |
| Rắn lửa | `bite.normal@1`, `fireball@1`, `heat_up@12`, `fire_shield@18`, `flamethrower@36` | Cắn thường, Quả cầu lửa |
| Hướng dương | `energy_ball@1`, `seed_barrage@1`, `absorb@12`, `poison_burst@18`, `leaf_storm@36` | Bóng năng lượng, Đạn hạt |
| Sói | `bite.normal@1`, `orb.dark@1`, `blast@12`, `dark_spirit@18`, `pulse.dark@36` | Cắn thường, Cầu tối |

Đây là lựa chọn nội dung ban đầu để duyệt, chưa là cân bằng damage/cooldown. Dùng `bite.normal` tránh tự mang chảy máu của `bite` rồng sang mọi loài. `blast` ở Sói là buff ATK/SP.ATK hiện có, cần mapping động tác gầm của Sói trước khi nối runtime.

**Giới hạn quan trọng:** một Species Skill Set chung chỉ có điều kiện cấp. Dạng 01 ở level 36 cũng có thể học skill @36. Muốn bắt buộc dạng 03 mới học được thì phải tách set theo dạng hoặc thiết kế thêm điều kiện form. Không thể bảo đảm khóa theo tiến hóa chỉ bằng Learn At Level.

Giữ Rồng và Dơi đã tách; thêm chín bộ dùng chung sẽ thành **15 asset cho 11 Family**, không phải 11 asset tổng cộng. Chưa tự tạo ba model hoặc ba bộ skill giống nhau cho từng loài.

## Những VFX có thể nối và những cơ chế còn thiếu

Đường combat hiện có hỗ trợ melee, projectile, targeted một hit, buff ATK/SP.ATK, bleed và loạt gió riêng. Có thể dùng làm nền cho Mổ, Quả cầu điện/lửa/nước, Tia sét, Bóng năng lượng sau khi chốt thông số và animation.

Không suy ra AoE/DoT/khống chế từ hình VFX. Khiên, DEF buff, hút máu, hồi máu, poison, thời tiết mưa, tia liên tục, hố đen hút và lốc xoáy nhiều hit cần executor hoặc thiết kế cơ chế bổ sung. Không dùng bleed để giả poison, không dùng buff ATK để giả khiên.

**12/30 prefab VFX có MonoBehaviour, trong đó 10 có script VFX thư viện.** `csParticleMove` tự dịch chuyển, `csDestroyEffect` nghe Z/X/C rồi Destroy, `CFX_AutoDestructShuriken` tự hủy; một số dùng `CFXR_Effect` có quản lý vòng đời riêng. Không gắn nguyên bản các prefab có hành vi này vào Effect Prefab/Projectile Visual của pool. Hai prefab Chùm Sao Rồng/Chùm Tia Laser chỉ thấy component URP `UniversalAdditionalLightData`; component ánh sáng này không phải script demo cần xóa.

Giữ source của Trung. Khi làm hình ảnh, tạo variant runtime riêng, bỏ script demo/chuyển động/hủy do thư viện và dùng vòng đời của executor/pool. 18 prefab còn lại không có MonoBehaviour vẫn cần kiểm particle stopAction, looping, shader, scale, culling và âm thanh nhúng trong Unity; chưa thể gọi là đã duyệt an toàn.

Các cặp tên cần ánh xạ thủ công: `Bảo Vê` ↔ icon `Bảo Vệ`; `Cơn Lốc Lá` ↔ `Bão Lá`; `Chém Gió` ↔ `Gió cắt`; `Hơi Thở Rồng` ↔ `Hơi Thở Của Rồng`; `Điện Cầu` ↔ `Quả Cầu Điện`; `Độc Nổ` ↔ `Nổ Độc`. `Bong Bóng Nổ` chưa có icon trùng tên, `Tia Laser`/`Chùm Tia Laser` có hai ứng viên. Không đổi tên source để khớp tự động.

## Dùng tool mới

File bảng: `Assets/Game/Skills/Data/Authoring/PetSkillBatch.json`. File kiểm kê: `FamilyFormReview.json`. Hai file này là dữ liệu Editor, không nằm trong Resources và không được runtime tự đọc.

Menu **Tools → Capstone → Pet Skills → Batch**:

1. **Open authoring table**: mở JSON, không tạo asset.
2. **Validate and preview**: đọc toàn thư viện, kiểm ID/alias, catalog, reference, default/learn level, gán Family/prefab và báo kế thừa giữa các dạng; không ghi asset. Báo cáo trên E.
3. **Apply approved additions**: chỉ xử lý `approved: true`. Tiền kiểm toàn bộ trước khi ghi. Sau apply dựng lại kế hoạch để kiểm còn thay đổi hay không.
4. **Verify merge safety**: kiểm trong bộ nhớ việc giữ level/animation/presentation/default đã chỉnh tay và merge lần hai không đổi. Không truy cập save.

Trạng thái bảng giao ban đầu: bảy skill và sáu set hiện có được nhận diện để giữ nguyên; các skill/set đề xuất chưa duyệt. **0 lỗi cấu trúc không có nghĩa 33 pet đã hoàn thiện.** Các dòng chờ duyệt và các prefab thiếu xuất riêng trong report.

Để duyệt một skill mới: điền `displayName`, `description`, `element` (tên enum thuộc chín hệ), `cooldown`, `icon`, `combat` trỏ đến Combat Profile thực; `presentation` có thể trống. `candidateVfx` chỉ là đường dẫn tham khảo, tool tuyệt đối không tự gán. Sau đó mới bật `approved`. Cooldown 0 ở dòng chưa duyệt hiện chỉ là trường chưa cấu hình, không phải đề xuất bỏ hồi chiêu.

Để duyệt một bộ mới: điền learnset/default bằng ID đã có hoặc skill mới đã duyệt, rồi bật `approved`. Hai default phải đã học ở `newPetLevel`; tối đa bốn ô. Tool tạo set mới hoặc chỉ thêm entry còn thiếu vào set đã có. Entry cũ giữ nguyên level/override. Default của set đã tồn tại không bị tự lấp/reset.

Binding chỉ vào trường `speciesSkillSet` của Stats và `skillSet` của VisualBinding còn trống, trên các prefab runtime **đã có trong Family**. Binding khác đã có sẽ báo xung đột. Không đổi model nguồn, không tạo prefab runtime thiếu, không gọi `ConfigureSkillSet(false)`, không sửa tiến trình cá thể hoặc các mảng skill legacy.

Không dùng menu cũ **Create or Connect Existing Dragon Library** để áp lại bảng. Lượt kiểm cũ `PetSkillLibraryVerification.RunEditor` vẫn có kỳ vọng lịch sử 4 skill/3 set; dùng menu Batch mới cho thư viện mở rộng, không xem kỳ vọng cũ đó là lỗi dữ liệu hiện tại.

## Gắn tay theo từng lớp

1. **Skill Definition**: ID ổn định; tên/icon/hệ/cooldown; Combat Profile quyết định damage/delivery. Presentation chỉ trình diễn, không tự gây damage.
2. **Presentation**: Cast = lấy đà; Release = phát đòn; Impact = va chạm thật. Cue chứa Effect Prefab, Sound, Socket, Offset, Scale, Volume. Projectile bay đặt ở `Combat.projectileVisual`; đừng đặt thêm một đạn ở Release.
3. **Species Skill Set**: add definition vào Learnset, chọn cấp. Muốn cùng skill khác hiệu ứng cho loài này thì chọn Presentation Override. Override Animation chỉ khi state/timing khác thật.
4. **CreatureAnimationProfile**: ánh xạ chính xác `skillId → CreatureAction → state Animator`. Ví dụ `bite → Bite → Bite Attack`. Không đổi ID thành tên state. Với prefab production, profile này có thể là nguồn state ưu tiên hơn state ghi trên learnset.
5. **Prefab gameplay**: root Stats và VisualBinding phải trỏ cùng set. Animator/controller/profile phải thuộc đúng dạng; giữ controller nguồn của Trung. Socket Mouth/Body/Ground cần anchor thật trên rig; không đoán transform theo tên loài.
6. **Test trực tiếp**: dùng encounter/save thử tách biệt và bản runtime riêng. Clone set/presentation để thử trước khi gán vào prefab test; nếu chỉ đổi trong Play thì thoát Play sẽ mất chỉnh Inspector. Dùng skill qua luồng HUD/controller thực để kiểm release/cooldown/damage, không chỉ gọi ParticleSystem.Play rồi kết luận skill chạy đúng. Không chạy trên save thật để reset loadout.

## Việc còn lại và giới hạn kiểm chứng

- Chốt thứ tự/model chín Family chưa reviewed, tên hiển thị cuối Rồng và chữ “Lực Sĩ”. Chưa sửa những asset đó trong lượt lập bảng này.
- Duyệt cơ chế/cooldown/power từng skill mới; tạo Combat Profile thật, mapping animation theo rig, đặt socket và chọn variant VFX/audio đã làm sạch.
- Tạo/kiểm prefab gameplay cho 27 dạng còn thiếu là công việc riêng; tool không tự chuyển model nguồn có script prototype thành pet/enemy runtime.
- Đăng ký Cẩm nang đủ dạng với metadata/provider/discovery thật; sửa số hiển thị mà giữ ID. Chưa sửa panel hoặc catalog Cẩm nang trong lượt này.
- Đã kiểm compile C# bằng compiler Unity theo response files của project và kiểm cấu trúc bảng bằng script. Unity Editor không đang chạy lúc kiểm, nên chưa import/menu-test trong engine, chưa apply hai lần trên asset thật, chưa Play/HUD/cooldown hai cá thể/VFX/audio/save. Không có screenshot runtime hoặc kết quả Profiler mới.

QA của lượt này ở `E:/UnityWork/CapstoneProjectData/Artifacts/pet-skill-batch-2026-11-17/`. Không commit/push.

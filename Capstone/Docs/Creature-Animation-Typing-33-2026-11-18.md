# Animation, typing và tên hiển thị — 33 dạng

Đã kiểm kê 33/33 dạng. 27 bộ Controller/Profile/Visual mới dùng motion thật cùng rig; 6 bộ runtime Rồng/Dơi giữ nguyên. Không tạo Skill/Stats giả. Chưa chạy Unity import, Animator.HasState hoặc Play Mode.

## Bảng đủ 33 dạng

Clips là số motion duy nhất được controller runtime tham chiếu. Danh sách FBX nguồn đầy đủ ở audit JSON trên E. Không đổi DefinitionId/FormId.

| Family | Form | Model nguồn | Rig | Clips | Controller | Profile | Skill Mapping | Typing mục tiêu | Display Name | Status |
|---|---|---|---|---:|---|---|---|---|---|---|
| dragon-fire | 01 | Khủng Long Lửa.prefab | Generic | 8 | dragon-fire_01.controller | Animation_dragon-fire_01.asset | Thiếu SkillSet | Lửa | Khủng Long Lửa | CREATED; STATIC |
| dragon-fire | 02 | Rồng Lửa Bay.prefab | Generic | 7 | dragon-fire_02.controller | Animation_dragon-fire_02.asset | Thiếu SkillSet | Lửa/Rồng | Rồng Lửa Bay | CREATED; STATIC |
| dragon-fire | 03 | Rồng Lửa Địa Ngục.prefab | Generic | 10 | dragon-fire_03.controller | Animation_dragon-fire_03.asset | Thiếu SkillSet | Lửa/Rồng | Rồng Lửa Địa Ngục | CREATED; STATIC |
| chick | 01 | Chim Con.prefab | Generic | 6 | chick_01.controller | Animation_chick_01.asset | Thiếu SkillSet | Thường/Bay **CONFLICT** | Chim Con | CREATED; STATIC |
| chick | 02 | Gà Lôi.prefab | Generic | 7 | chick_02.controller | Animation_chick_02.asset | Thiếu SkillSet | Thường/Bay | Gà Lôi | CREATED; STATIC |
| chick | 03 | Gà Gió Lốc.prefab | Generic | 7 | chick_03.controller | Animation_chick_03.asset | Thiếu SkillSet | Bay | Gà Gió Lốc | CREATED; STATIC |
| cat-electric | 01 | Mèo Điện.prefab | Generic | 8 | cat-electric_01.controller | Animation_cat-electric_01.asset | Thiếu SkillSet | Điện | Mèo Điện | CREATED; STATIC |
| cat-electric | 02 | Mèo Tia Chớp.prefab | Generic | 8 | cat-electric_02.controller | Animation_cat-electric_02.asset | Thiếu SkillSet | Điện | Mèo Tia Chớp | CREATED; STATIC |
| cat-electric | 03 | Mèo Sấm Sét.prefab | Generic | 8 | cat-electric_03.controller | Animation_cat-electric_03.asset | Thiếu SkillSet | Điện | Mèo Sấm Sét | CREATED; STATIC |
| bat | 01 | Dơi Nhỏ.prefab | Generic | 7 | Bat01.controller | Bat01.asset | Giữ mapping thật | Tối/Bay **CONFLICT** | Dơi Nhỏ | KEEP; STATIC |
| bat | 02 | Dơi Ma Cà Rồng.prefab | Generic | 7 | Bat02.controller | Bat02.asset | Giữ mapping thật | Tối/Bay | Dơi Ma Cà Rồng | KEEP; STATIC |
| bat | 03 | Dơi Chúa Tể.prefab | Generic | 7 | Bat03.controller | Bat03.asset | Giữ mapping thật | Tối/Bay | Dơi Chúa Tể | KEEP; STATIC |
| ghost | 01 | Ma Nhỏ.prefab | Generic | 6 | ghost_01.controller | Animation_ghost_01.asset | Thiếu SkillSet | Tối | Ma Nhỏ | CREATED; STATIC |
| ghost | 02 | Ma Mặt Nạ.prefab | Generic | 8 | ghost_02.controller | Animation_ghost_02.asset | Thiếu SkillSet | Tối | Ma Mặt Nạ | CREATED; STATIC |
| ghost | 03 | Ma Nguyền Rủa.prefab | Generic | 8 | ghost_03.controller | Animation_ghost_03.asset | Thiếu SkillSet | Tối | Ma Nguyền Rủa | CREATED; STATIC |
| egg-shadow | 01 | Trứng Ma Quái.prefab | Generic | 6 | egg-shadow_01.controller | Animation_egg-shadow_01.asset | Thiếu SkillSet | Tối | Trứng Ma Quái | CREATED; STATIC |
| egg-shadow | 02 | Quái Trứng Quỷ.prefab | Generic | 7 | egg-shadow_02.controller | Animation_egg-shadow_02.asset | Thiếu SkillSet | Tối | Quái Trứng Quỷ | CREATED; STATIC |
| egg-shadow | 03 | Quái Trứng Lực SĨ.prefab | Generic | 8 | egg-shadow_03.controller | Animation_egg-shadow_03.asset | Thiếu SkillSet | Tối/Giác Đấu | Quái Trứng Lực SĨ | CREATED; STATIC |
| Dragon Dusk | 01 | Rồng Chạng Vạng.prefab | Generic | 17 | Dragon_Dusk_PetRuntime.controller | Dragon01.asset | Giữ mapping thật | Rồng/Tối | Rồng Chạng Vạng | KEEP; STATIC |
| Dragon Dusk | 02 | Rồng Đêm Tối.prefab | Generic | 17 | Dragon02.controller | Dragon02.asset | Giữ mapping thật | Rồng/Tối | Rồng Đêm Tối | KEEP; STATIC |
| Dragon Dusk | 03 | Rồng Bóng Tối.prefab | Generic | 17 | Dragon03.controller | Dragon03.asset | Giữ mapping thật | Rồng/Tối | Rồng Bóng Tối | KEEP; STATIC |
| snake-fire | 01 | Rắn Lửa.prefab | Generic | 8 | snake-fire_01.controller | Animation_snake-fire_01.asset | Thiếu SkillSet | Lửa **CONFLICT** | Rắn Lửa | CREATED; STATIC |
| snake-fire | 02 | Rắn Bùng Cháy.prefab | Generic | 8 | snake-fire_02.controller | Animation_snake-fire_02.asset | Thiếu SkillSet | Lửa | Rắn Bùng Cháy | CREATED; STATIC |
| snake-fire | 03 | Rắn 3 Đầu.prefab | Generic | 7 | snake-fire_03.controller | Animation_snake-fire_03.asset | Thiếu SkillSet | Lửa/Rồng | Rắn 3 Đầu | CREATED; STATIC |
| shell | 01 | Ốc Nhà.prefab | Generic | 8 | shell_01.controller | Animation_shell_01.asset | Thiếu SkillSet | Nước | Ốc Nhà | CREATED; STATIC |
| shell | 02 | Ốc Gai.prefab | Generic | 7 | shell_02.controller | Animation_shell_02.asset | Thiếu SkillSet | Nước | Ốc Gai | CREATED; STATIC |
| shell | 03 | Ốc Mượn Hồn Vua.prefab | Generic | 7 | shell_03.controller | Animation_shell_03.asset | Thiếu SkillSet | Nước | Ốc Mượn Hồn Vua | CREATED; STATIC |
| sun-blossom | 01 | Hướng Dương Nảy Mầm.prefab | Generic | 8 | sun-blossom_01.controller | Animation_sun-blossom_01.asset | Thiếu SkillSet | Cỏ | Hướng Dương Nảy Mầm | CREATED; STATIC |
| sun-blossom | 02 | Hướng Dương Tiên.prefab | Generic | 8 | sun-blossom_02.controller | Animation_sun-blossom_02.asset | Thiếu SkillSet | Cỏ | Hướng Dương Tiên | CREATED; STATIC |
| sun-blossom | 03 | Hướng Dương Nữ Hoàng.prefab | Generic | 8 | sun-blossom_03.controller | Animation_sun-blossom_03.asset | Thiếu SkillSet | Cỏ | Hướng Dương Nữ Hoàng | CREATED; STATIC |
| wolf | 01 | Sói Tí Nị.prefab | Generic | 8 | wolf_01.controller | Animation_wolf_01.asset | Thiếu SkillSet | Thường **CONFLICT** | Sói Tí Nị | CREATED; STATIC |
| wolf | 02 | Sói Bóng Tối.prefab | Generic | 8 | wolf_02.controller | Animation_wolf_02.asset | Thiếu SkillSet | Thường/Tối | Sói Bóng Tối | CREATED; STATIC |
| wolf | 03 | Người Sói Quỷ.prefab | Generic | 8 | wolf_03.controller | Animation_wolf_03.asset | Thiếu SkillSet | Tối/Giác Đấu | Người Sói Quỷ | CREATED; STATIC |

## Thay đổi

- Asset mới: Assets/Game/CreatureSystem/Runtime/AnimationReady/<Family>/Animation/ và Visual/: 27 controller + 27 profile + 27 Visual, kèm meta. Visual chỉ chứa model/rig/Animator và CreatureVisualBinding; không Stats, AI, Capture hay Skill giả.
- Controller dùng state có motion thật, Idle mặc định, không tự nối chuỗi demo/AnyState. Locomotion chọn In Place cùng rig; không retarget khác loài, không sửa FBX hoặc controller vendor.
- 32 prefab nguồn đổi tham chiếu controller và tắt Apply Root Motion. Source Rồng01 đã đúng nên giữ nguyên. Sáu bộ runtime Rồng/Dơi và các role prefab của chúng giữ nguyên.
- Family Definition thêm typingStatus, primaryElement, secondaryElement và TryGetTyping. Conflict không tự áp dụng. 27 form được gán Visual/Profile mới; SkillSet/StatProfile thiếu vẫn để trống.
- Metadata nguồn Trứng Ma Quái từ Unknown được bổ sung Tối. Metadata đã có typing khác mục tiêu giữ nguyên.
- CreatureDisplayNames chỉ xử lý trình bày. Stats.DisplayName, Metadata.SpeciesDisplayName, EvolutionRule.TargetDisplayName và nhãn Party/BOX/popup/Cẩm nang dùng tên Việt. HUD/nameplate/capture lấy tên qua Stats/ResolveDisplayName. Species raw, nickname và khóa save/catalog không bị đổi.

## Bốn conflict giữ nguyên

| Form | Dữ liệu cũ | Mục tiêu | Xử lý |
|---|---|---|---|
| chick/01 | Wind enum4, ngoài9hệ | Thường/Bay | CONFLICT; không thay source/save |
| bat/01 | Source prototype Tối; runtime Tối/Bay | Tối/Bay | Giữ runtime đúng; source prototype chờ duyệt |
| snake-fire/01 | Lửa/Tối | Lửa | CONFLICT; không tự bỏ hệ phụ |
| wolf/01 | Tối | Thường | CONFLICT; không ghi đè |

29 dạng CONFIGURED typing thiết kế, 4 CONFLICT. Không thêm Ghost, không đổi enum/tương khắc hay typing trong save. PetStatProfile không có typing nên không thêm nguồn hệ thứ hai. Evolution template của các loài chưa runtime có targetID trống: không đoán đích theo thứ tự.

## Giới hạn

- 33 dạng có motion tham chiếu thật cho Idle/Move/Attack/Hit/Death ở mức static; 0 dạng thiếu rig/motion cốt lõi trong graph. Skinning, curve binding và playback cần Unity.
- 30 controller chưa có state Downed riêng. Giữ fallback hiện có; không thay bằng Die hoặc tạo clip đứng giả. Ba state Downed Rồng cũng không được coi là bằng chứng clip gục chuyên dụng.
- Spawn chỉ map khi có clip thật; Recall giữ VFX nếu hệ đang dùng VFX. Không tạo clip giả để đủ semantic actions.
- 27 dạng ngoài Rồng/Dơi vẫn thiếu SkillSet/learnset và runtime combat/capture đầy đủ. Skill mapping trống là chủ đích, không tự mở khóa các Zone BLOCKED.
- Sáu dạng đang hoạt động giữ Skill ID, release timing, socket, VFX/audio, damage và cooldown. Không sửa driver, PetController, AI rotation hay capture coordinator.
- Meta của one-shot được chọn mới không có loopTime=1; clip.isLooping thực vẫn cần Editor verifier. Compile không chứng minh playback đúng.

## Kiểm Unity

1. Dừng Play, để Unity import/compile. Không chạy setup reset library hoặc reset save.
2. Chọn Tools > Capstone > Creatures > Verify animation library (read-only). Tool mở Preview Scene riêng, kiểm Avatar Generic, Animator.HasState, curve paths, one-shot loop và factory ba form Rồng. Không sửa scene/prefab/save; CSV ra E qua QaArtifactPaths.Root.
3. Nếu import/binding lỗi, giữ source và đọc CSV; không ép Humanoid.
4. Dùng encounter/save thử riêng: Idle khi đứng, Move khi đi, Attack/Skill một lần mỗi lệnh, Hit không cắt Dead, Downed không tự thoát, hết Dead mới mất model/respawn.
5. Hồi quy Rồng/Dơi: Z/X/C/V, release normalized time, thu/gọi/capture/tiến hóa; kiểm tên Việt, nickname và lookup tiếng Anh.
6. Load save thử cũ kiểm ba form Rồng. Catalog/factory và ID được kiểm hash; chưa chạy load/save thật trong task.

QA: E:/UnityWork/CapstoneProjectData/Artifacts/creature-animation-types-2026-11-18/. Không có capture/build/Profiler, không commit/push.

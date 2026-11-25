# Hướng dẫn gắn kỹ năng trong Unity

Đối chiếu source ngày 21/11/2026. Project D:/Download/ProjectVTC/CapstoneProject/Capstone. Dừng Play trước khi chỉnh asset. Đường dẫn Assets/... dùng trong cửa sổ Project.

## 1. Luồng dữ liệu và phạm vi thay đổi

PetSkillCatalog → PetSpeciesSkillSet của Form → PetHudRuntimeStats của cá thể → PetCombatActionDriver → PetSkillCombatExecutor. PetSkillPresentationPlayer đọc Cast/Release/Impact để phát hình và tiếng. Damage ghi nhận HP thực mất rồi mới phát CombatDamageEvents. Tiến trình và cooldown thuộc cá thể.

| Thao tác Inspector | Phạm vi tác động |
|---|---|
| Sửa Skill Definition, Combat hoặc Presentation dùng chung | Tất cả loài/Form đang tham chiếu asset đó |
| Sửa Learnset của Species_wolf_01 | Những cá thể dùng set Form01 |
| Gán Presentation Override riêng trong một dòng Learnset | Dòng skill của Form đó, nếu profile override được dùng riêng |
| Override Animation trong Learnset | Animation/timing của dòng skill đó |
| Chỉnh StatProfile | Toàn bộ cá thể Owned/Wild có cùng DefinitionId/FormId |
| Chỉnh một instance trong Play | Chỉ phiên thử, thường bị hoàn nguyên khi dừng |

## 2. Skill Definition

Mở Assets/Game/Skills/Resources/PetSkills/PetSkillCatalog.asset. Danh sách Skills chứa các asset thực, bao gồm Data/Definitions, Data/B1, Data/V2 và Runtime/Bats/Skills. Bấm asset trong Catalog để tránh mở nhầm bản trùng tên.

| Trường | Cách dùng |
|---|---|
| Skill Id | Khóa lưu trong save. Giữ ID hiện có, kể cả ID legacy có dấu cách như wing strike |
| Legacy Ids | Alias để resolve ID cũ khi thật sự cần migration. Kiểm tra alias không trùng skill khác trước khi đổi ID |
| Display Name / Description | Tên và mô tả UI chung |
| Element | Hệ của đòn. Các ID đã canonical có thể được SkillElementAssignments.Resolve ánh xạ lại. Kiểm bảng hệ canonical khi đổi |
| Icon | Sprite của kỹ năng. PNG cần Texture Type = Sprite (2D and UI) |
| Cooldown Seconds | Thời gian hồi chiêu gameplay |
| Mana Cost | Trường tương thích dữ liệu cũ. Runtime bỏ MP và ép chi phí về 0 |
| Critical Rate Bonus / Critical Damage Bonus | Bonus theo skill đã trang bị, được cộng vào snapshot stats |
| Animator States / Animation Duration / Animation Fade | State mặc định, thời lượng và blend. Dòng Learnset có thể override |
| Windup Seconds / Recovery Seconds | Chuẩn bị/hồi động tác của luồng tương ứng. Đừng thêm thời gian rỗng nếu clip đã có lấy đà |
| Presentation | Profile VFX/audio/timing mặc định |
| Combat | Asset PetSkillCombatProfile thực thi damage |
| Ui Metadata | Mô tả bổ sung. Điền hiệu ứng ở đây không tự tạo gameplay |

Skill mới tạo bằng Create → Capstone → Pet Skills → Skill, Combat hoặc Presentation. Đăng ký Skills và Species Sets trong Catalog hiện có. Không dùng Create or Connect Existing Dragon Library để reset.

## 3. Combat Profile và công thức thật

| Trường | Ý nghĩa |
|---|---|
| Damage Kind | Physical dùng ATK/DEF. Magical dùng SP.ATK/SP.DEF |
| Power | Hệ số nhân tấn công trước phòng thủ |
| Delivery | Melee, Projectile, Targeted, SelfBuff |
| Range | Tầm xác thực mục tiêu, tính theo điểm combat hiện có |
| Projectile Speed / Projectile Radius / Lifetime | Tốc độ, bán kính va chạm và tuổi thọ đạn |
| Projectile Visual / Visual Scale | Prefab hình đạn và kích thước trình diễn |
| Release Socket | Khóa socket, thường Mouth hoặc Body |
| Auto Acquire Target | Cho tìm mục tiêu hợp lệ nếu chưa khóa |
| Melee Half Angle | Nửa góc vùng đánh gần |
| Offense Buff Percent / Buff Duration / Buff Visual | Buff ATK và SP.ATK tạm thời, làm mới thời gian |
| Bleed Power | Hệ số ATK cho ba nhịp bleed ở giây 1/2/3, không crit thêm |
| Retreat Distance / Retreat Speed / Preferred Distance | Lùi lấy khoảng cách cho đòn xa |
| Weighted Wind Volley / Burst End Normalized | Loạt gió 2–5 phát theo trọng số 10/40/40/10. Power là ngân sách tổng |

CreatureCombatRules.Offense lấy ATK hoặc SP.ATK × Power × buff, rồi crit nhân 1 + CriticalDamage/100 nếu roll thành công. Mitigate nhân 100/(100 + DEF hoặc SP.DEF). Sau đó nhân tương khắc và thưởng cùng hệ STAB ×1,5. HP mất bị giới hạn bởi HP còn lại. Bleed bỏ STAB/tương khắc.

Ví dụ ATK=80, Power=1,2, DEF=60, chưa buff/crit: raw=96, sau phòng thủ=60. Cùng hệ ×1,5 và khắc đơn ×2 cho 180 HP. Nếu chỉ còn 70 HP, số damage hiển thị là 70. Một loài có thể dùng cả Physical lẫn Magical.

Bảng hệ hiện tại. Hàng là hệ đòn, cột là hệ mục tiêu. Mục tiêu hai hệ khác nhau nhân hai ô, trùng hai hệ chỉ tính một lần.

| Đòn / Đích | Lửa | Nước | Cỏ | Điện | Giác đấu | Bay | Thường | Tối | Rồng |
|---|---|---|---|---|---|---|---|---|---|
| Lửa | 1 | ½ | 2 | 1 | 1 | 1 | 1 | 2 | ½ |
| Nước | 2 | 1 | ½ | 1 | 1 | 1 | 1 | 1 | 1 |
| Cỏ | ½ | 2 | 1 | 1 | 1 | ½ | 1 | 1 | 1 |
| Điện | 1 | 2 | ½ | 1 | 1 | 2 | 1 | 1 | 1 |
| Giác đấu | 1 | 1 | 1 | 1 | 1 | ½ | 2 | 2 | 1 |
| Bay | 1 | 1 | 2 | ½ | 2 | 1 | 1 | ½ | 1 |
| Thường | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| Tối | ½ | 1 | 1 | 1 | ½ | 2 | 1 | 1 | 1 |
| Rồng | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 2 |

Nguồn Assets/Game/Elements/Resources/Combat/ElementMatchups.asset. Hai hệ nguồn đều có thể nhận STAB, chỉ nhân ×1,5 một lần.

## 4. Icon, animation, VFX và audio

1. Mở Skill → Icon và kéo Sprite vào.
2. Bấm Presentation. Cast → Effect Prefab/Sound là lấy đà, Release → Effect Prefab/Sound là phát đòn, Impact → Effect Prefab/Sound là va chạm thật.
3. Socket là chuỗi khóa trong Creature Visual Binding → Sockets trên Visual prefab. Mở Mouth/Head/Body/Ground và kiểm đúng Transform xương của Form. Khóa chưa có sẽ fallback về gốc actor, dễ khiến VFX lệch. Chỉ dùng khóa đã gán.
4. Follow Socket cho hiệu ứng đi theo xương. Actor Space, Local Offset, Local Euler Angles, Scale, Volume, Pitch, Lifetime Seconds điều khiển vị trí/hình/âm.
5. Mixer Group, Spatial Blend, Min Distance, Max Distance đặt ở Presentation. Skill trong thế giới thường dùng 3D; UI dùng đường audio UI riêng.
6. Release Mode có Timed, Animation Event, Normalized Animation. Luồng action phối hợp hiện tại dùng normalizedTime thực, Release Normalized và Chain Normalized. Animation Event phải có receiver tương ứng, không thêm callback tạo damage thứ hai. Timed dùng Release Delay Seconds ở luồng trình diễn tương thích. Luôn kiểm driver thực đang dùng khi đổi mode.
7. Override Animation trong Learnset chỉ bật khi Form dùng state hoặc timing khác. Animator States là tên state thật, không phải tên file FBX. CreatureAnimationProfile.skills còn phải map đúng Skill Id, vì luồng runtime kiểm cả profile của Visual.

Ví dụ cùng bite.normal cho Sói 01 và Sói 02: giữ cùng Skill asset, Icon và Combat. Duplicate Presentation đang dùng của Sói01 thành Presentation_wolf02_bite, thay Cast/Release/Impact Sound và gán Learnset Sói02 → Presentation Override. Sói01 giữ profile cũ. Đặt Override Animation và Animator States riêng khi Animator Sói02 khác. Nếu cả hai cùng tham chiếu bản override thì sửa nó vẫn ảnh hưởng cả hai.

## 5. Learnset Sói 01/02/03

| Form | SkillSet |
|---|---|
| wolf/01 Sói Tí Nị | Assets/Game/CreatureSystem/Runtime/Expansion/wolf/Skills/Species_wolf_01.asset |
| wolf/02 Sói Bóng Tối | Assets/Game/CreatureSystem/Runtime/Expansion/wolf/Skills/Species_wolf_02.asset |
| wolf/03 Người Sói Quỷ | Assets/Game/CreatureSystem/Runtime/Expansion/wolf/Skills/Species_wolf_03.asset |

Visual nằm tại Assets/Game/CreatureSystem/Runtime/AnimationReady/wolf/Visual. Animator/Profile nằm trong Animation cùng Family. Runtime OwnedPet/Wild nằm tại Expansion/wolf/OwnedPet và Expansion/wolf/Wild.

1. Mở đúng Species asset, mở Learnset, bấm +, gán Skill asset và Learn At Level.
2. Bật Override Animation khi cần. Các state Sói đang có gồm Bite Attack, Cast Spell, Projectile Attack, Idle, Walk Forward In Place và Run Forward In Place. Chọn state theo hành động thật của skill và mapping trong Animation_wolf_0X.asset.
3. Gán Presentation Override riêng nếu cần hình/âm hoặc thời điểm phát khác.
4. Default Loadout chứa tối đa bốn skill và chỉ áp cho cá thể mới. Skill chỉ được trang bị nếu cấp cá thể đạt Learn At Level. Initialize Fresh Wild Loadout cho Wild mới nhận loadout theo level, không áp lại khi bắt hay load.
5. Trên prefab OwnedPet/Wild, Pet Hud Runtime Stats → Species Skill Set phải trỏ đúng set. Visual → Creature Visual Binding → Skill Set cũng phải khớp vì visual mới có thể bind lại set.
6. Kiểm ở Lv dưới mốc và đúng mốc qua PetLevelUpService. Tab Kỹ năng phân biệt kho đã học và bốn ô trang bị. Học không đồng nghĩa tự thay skill đang dùng.
7. Các set Sói hiện có các mốc 1/12/18/36. Ví dụ thiết kế cần học bite.normal ở Lv5 thì tự đổi dòng đó trên những Form cần áp dụng. Giữ ít nhất một đòn dùng được cho cá thể bắt đầu Lv1. Đây là ví dụ thao tác, tài liệu không đổi các mốc đang có.
8. Form02 muốn giữ chiêu Lv5 của Form01 thì giữ cùng ID trong Learnset và map animation của Form02. Form03 thêm chiêu ở Lv36 bằng một dòng nữa, đăng ký nó trong Catalog và profile animation nếu là ID mới.
9. Tiến hóa dùng ConfigureSkillSet(..., true), giữ tiến trình, loadout và cooldown theo ID. Mỗi skill giữ lại cần có entry/presentation/action hợp lệ ở Form đích để dùng được.
10. Save lưu ID đã học và ID trang bị. Giữ ID và Legacy Ids hợp lệ. Kiểm save QA trước khi thay Learn At Level hoặc xóa dòng vì danh sách học có lọc RequiredPetLevel. Thay Default Loadout không reset bộ đã lưu.

Sói hiện dùng 3 SkillSet do animation và presentation của từng dạng được authoring riêng. Một loài có thể dùng 1 set nếu cả ba form cùng cơ chế/mapping. Gán set qua cả runtime stats và VisualBinding hiện hành, không đổi Species Id tùy tiện. Cấp học theo hệ chính/phụ hiện là Learn At Level riêng cho từng set, chưa có phép tự cộng cấp hệ phụ.

## 6. Base Stats và cá thể

Assets/Resources/PetStats/wolf_01.asset, wolf_02.asset, wolf_03.asset. Trường Hp, Attack, Defense, Magic Attack, Magic Defense, Speed là chỉ số gốc, không phải số hiện ở Lv hiện tại.

PetStatCalculator tính core = floor((2×Base + IV + floor(EV/4))×Level/100). HP=(core+Level+10)×4. Năm chỉ số khác=floor((core+5)×Nature). IV 0–31, EV mỗi stat 0–252, tổng 510. PetNature tăng 10% một chỉ số, giảm 10% chỉ số khác, HP trung tính. Cấp tối đa 100.

SPD tác động cả tốc độ di chuyển và nhịp đánh thường: MovementScale=clamp(sqrt(SPD/50),0,75,1,35); AttackIntervalScale=clamp(1/MovementScale,0,8,1,25). Cooldown skill vẫn theo Skill Definition.

Sửa Base Stats ảnh hưởng các cá thể cùng loài/form khi calculator đọc lại, kể cả load save. Dữ liệu genetics và HP thực được đồng bộ qua các luồng hiện có. Kiểm lại trên save QA. Power nhân ATK/SP.ATK đã tính từ Base/Level/IV/EV/Nature.

## 7. Âm thanh

Pet cry, skill và UI có hướng gắn riêng. Danh sách file, duration, import settings, references và bảng 33 Form được xuất ở báo cáo âm thanh của lượt audit trên ổ E.

Pet cry hiện chưa có trường Cry riêng trên Family, CreatureVisualBinding hoặc OwnedPet/Wild, và chưa tìm thấy hệ phát cry theo summon/hit/evolution. Vì thế chưa có chỗ gắn cry độc lập trong Inspector hiện hành. Có thể dùng tiếng gầm trong Skill Presentation nếu muốn phát khi tung chiêu cụ thể. Muốn cry theo vòng đời cần bổ sung profile và hook sự kiện sau khi được duyệt.

Skill dùng Sound trong Cast, Release và Impact của PetSkillPresentationProfile. Ví dụ orb.dark ở Assets/Game/CreatureSystem/Runtime/Bats/Skills/Skill_orb.dark.asset, bấm Presentation. Để âm thanh riêng cho Form, duplicate profile rồi gán Presentation Override ở dòng Learnset của Form đó.

### Pet cry

Nguồn: `Assets/Sounds/Trung/SFX/Pet Cry/`, có 33 file. Có thể đề xuất ghép đủ 33 Form theo tên; bốn tên cần nghe và duyệt lại: Khủng Long Lửa ↔ Rồng Trẻ Con, Rồng Bóng Tối ↔ Rồng Ác Mộng, Người Sói Quỷ ↔ Ma Sói, Rắn 3 Đầu ↔ Rắn Ba Đầu. Đây chỉ là ánh xạ đề xuất, chưa phải âm thanh đã gắn hoặc nghe kiểm chứng. Owned và Wild đều chưa có trường cry theo vòng đời riêng. Không thêm Animation Event gọi hàm cry chưa tồn tại.

### Skill: thao tác cụ thể

Nguồn: `Assets/Sounds/Trung/SFX/Skills/`, có 29 file. Ví dụ `Quả Cầu bóng Tối.mp3` phù hợp để thử ở **Release → Sound** của `orb.dark`; Cast là lấy đà, Impact là trúng đòn, nên không kéo cùng file vào cả ba nếu không muốn nghe lặp. Trước tiên duplicate Presentation nếu chỉ đổi tiếng cho một Form, gán bản riêng vào **Learnset → Presentation Override**, rồi chỉnh **Volume**, **Pitch**, **Mixer Group**, **Spatial Blend**, **Min Distance**, **Max Distance**. Không cần đổi Skill Id hoặc Combat.

27 Skill trong Catalog hiện đều có ít nhất một Sound ở Presentation mặc định; đó là bộ tiếng cũ. Các Presentation Override theo Form có thể khác, nên luôn mở profile thực được dòng Learnset sử dụng. Chưa tìm thấy tham chiếu GUID trực tiếp tới 64 SFX mới trong YAML/code đã quét; chưa loại trừ cách nạp động từ bên ngoài phạm vi đọc. Tài liệu này không gán thêm tiếng.

### UI hover/click

Nguồn: `Assets/Sounds/Trung/SFX/Hover And Click/ButtonClickSound.mp3` và `HoverButtonSound.wav`. Chưa thấy binder hover/click dùng chung trên các panel runtime đã đọc. Có demo `Assets/Scripts/Thang/ThangTest/UIThang/GDS/Examples/01-Beginner/05-AddSounds/UITK/AddSounds_Sfx.cs`, nhưng các trường **Fail/Pick/Place** nghe sự kiện inventory, không phải hook hover/click của HUD.

Đề xuất ít thay đổi cho uGUI: AudioSource dùng chung, **Play On Awake** tắt, **Spatial Blend** = 0; Button → On Click gọi **AudioSource.PlayOneShot(AudioClip)** với ButtonClickSound; Event Trigger → Pointer Enter gọi cùng AudioSource với HoverButtonSound. Phải giới hạn tiếng hover khi nhiều nút liên tiếp. UI Toolkit không có On Click Inspector giống uGUI, cần một binder dùng chung nhận PointerEnter/Click/Submit trước khi tự gắn qua cấu hình. Chưa tạo binder, AudioSource hay sửa panel trong lượt này. Không gán tiếng UI vào Cast/Release/Impact của pet.

### Import và kiểm nghe

Hiện các file mới để **Decompress On Load**, **PCM**, **Preload Audio Data** tắt và **Force To Mono** tắt. Với SFX rất ngắn, nên thử preload và cân nhắc mono cho âm 3D; tiếng UI stereo có thể giữ để bảo toàn thiết kế. Music dài nên đánh giá Streaming/Compressed In Memory và bộ nhớ trước khi đổi. Đây là đề xuất, chưa chỉnh import. Thời lượng trong báo cáo static tính từ frame MP3/RIFF; AudioClip.length thực sau import cần chạy menu Editor audit.

Bảng đầy đủ: [báo cáo A–F](E:/UnityWork/CapstoneProjectData/Artifacts/animation-return-skill-audit-2026-11-21/report.md), [33 tiếng pet](E:/UnityWork/CapstoneProjectData/Artifacts/animation-return-skill-audit-2026-11-21/pet-cry-33-static.csv), [29 tiếng skill](E:/UnityWork/CapstoneProjectData/Artifacts/animation-return-skill-audit-2026-11-21/skill-sound-proposals.csv), [toàn bộ file âm thanh](E:/UnityWork/CapstoneProjectData/Artifacts/animation-return-skill-audit-2026-11-21/audio-files-static.csv).

## 8. Lỗi thường gặp

- Icon kéo không vào: kiểm Sprite import.
- Có VFX nhưng không gây damage: kiểm Combat, executor, range, mục tiêu và va chạm thật.
- Đổi Presentation mặc định nhưng âm cũ còn: kiểm Presentation Override của Form.
- Skill ở Catalog nhưng pet chưa học: kiểm entry Learnset, Learn At Level và set thực đang bind.
- Animation khác tên clip: dùng tên Animator State đúng rig và profile.skills đúng ID.
- Effect Prefab chứa demo script tự damage/Destroy/reparent có thể phá pool. Dùng variant trình diễn đã kiểm, giữ gameplay ở executor.
- Đổi asset dùng chung ảnh hưởng nhiều loài. Duplicate Presentation/override khi chỉ muốn khác hình/âm.
- Dùng Apply Species Defaults (New Pet Authoring Only) với dữ liệu cá thể cũ có thể làm sai kỳ vọng loadout. Thử trên prefab mới và save QA riêng.
- Dùng ID mới để đổi tên chiêu khiến save không resolve được. Đổi Display Name cho nhu cầu đổi chữ.

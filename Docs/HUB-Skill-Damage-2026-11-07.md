# HUB và sát thương bốn kỹ năng — 07/11/2026

## Đã sửa

- `Capstone/Assets/Game/HudSystem/GameplayHudController.cs`: chế độ chỉ hiện khi triệu hồi kiểm pet đang hoạt động, nhận lệnh được, đã hiện model và không nằm trong chuỗi summon/recall. Không có pet thì ẩn cả nhóm bốn ô; thu hồi bắt đầu thì ẩn ngay. Giữ tùy chọn Inspector `AlwaysVisible` cho authoring.
- `Capstone/Assets/Scenes/DemoScene.unity`: đổi cấu hình đang lưu từ `AlwaysVisible` sang `WhilePetSummoned`. Đây là nguyên nhân scene vẫn hiện bốn ô dù mặc định trong C# đã đúng. Scene được lưu bằng Unity, kèm một số trường mặc định do serializer ghi lại; không đổi gameplay hoặc vị trí đối tượng.
- Skill thiếu sprite dùng lại glyph trung tính `UI/Latest20261020/Masks/skills`, tránh chữ tên tiếng Anh xuống dòng chồng cooldown. Sprite đã có giữ nguyên. Tooltip vẫn có tên kỹ năng.
- Không sửa combat profile, animation source, chỉ số, skill ID hoặc save trong lượt này. Hai script probe được lưu ngoài Git để tái hiện kiểm chứng, rồi gỡ khỏi `Assets`.

## Kết quả chạy thật

Unity 6000.3.16f1, DemoScene, save thử độc lập trên E. Hoàn thành **177 assertion**, không có assertion lỗi. Đây là kiểm tự động trong Editor Play Mode qua hit-test EventSystem và callback nút thật; không phải kiểm bằng chuột/phím vật lý hoặc standalone.

- Triệu hồi và thu hồi qua `PetSummonDirector` thật: ẩn trước/trong summon, hiện khi nhận lệnh được, ẩn khi bắt đầu recall, tiếp tục ẩn sau recall.
- Mỗi prefab OwnedPet_Dragon01/02/03 dùng bốn kỹ năng thật: 12 lần cast. Kiểm snapshot ATK/SP.ATK, power, crit thực phát sinh, DEF/SP.DEF, không gây damage trước release, có trừ HP, đúng một release/hit, có cooldown, không hit lặp trong khoảng quan sát sau đó.
- Đối tượng đích là Wild_Bat02 tạm cấp 50 có 50.000 HP để tránh chết giữa bài kiểm. DEF 68 và SP.DEF 65 khác nhau, nên bài kiểm phân biệt được hai nhánh giảm sát thương. Fixture chỉ tồn tại trong Play Mode. Dữ liệu và roster dùng bản thử, không ghi đè save người chơi.
- Chụp Game view 1920×1080 và 1366×768; kiểm trạng thái summoned/recall-lock/withdrawn ở từng kích thước. Ba ảnh HUB cuối và ba board animation nằm ngoài Git.
- Animation của cả ba dạng được resolve từ binding hiện hành đến state và AnimationClip thật, sample sáu pose/clip. Board mỗi dạng có bốn hàng: Cắn / Bộc phá / Đạn năng lượng / Đánh cánh; cột lần lượt 0%, 18%, 36%, 54%, 72%, 94%.
- Sau khi gỡ probe khỏi Assets: Tundra compile thành công 5,29 s; Mono reload thành công 7,339 s, không có lỗi C# trong lượt compile cuối. Scene sau QA trùng SHA256 với bản native-save trước khi chạy, không lưu fixture vào scene.

Bằng chứng: `E:/UnityWork/CapstoneProjectData/Artifacts/hud-skill-damage-2026-11-07/`, gồm `checks.txt`, `damage.csv`, `animation-clips.csv`, `REPORT.md` và ảnh cuối.

Ảnh runtime: [đã triệu hồi 1920](E:/UnityWork/CapstoneProjectData/Artifacts/hud-skill-damage-2026-11-07/summoned-1920.png), [đã triệu hồi 1366](E:/UnityWork/CapstoneProjectData/Artifacts/hud-skill-damage-2026-11-07/summoned-1366.png), [đã thu hồi](E:/UnityWork/CapstoneProjectData/Artifacts/hud-skill-damage-2026-11-07/recalled-final.png). Gỡ bốn file probe/.meta tạm khỏi project, dọn dữ liệu trung gian có ledger. Giữ sáu ảnh hữu ích ngoài Git khoảng 6,4 MB; toàn thư mục khoảng 14 MB gồm backup nguồn. Không tạo build hoặc recorder chạy nền.

## Bốn skill đang gây damage thế nào

| Skill | Animation thật | Cách đánh hiện tại | Raw damage | Tầm / hồi chiêu |
|---|---|---|---|---|
| Cắn (`bite`) | Bite Attack, 0,833 s | Melee, một mục tiêu khóa | ATK × 1,00 | 3 / 1,2 s |
| Bộc phá (`blast`) | Blast Attack, 1,500 s | Targeted, một hit vào mục tiêu khóa | SP.ATK × 1,15 | 10 / 3,8 s |
| Đạn năng lượng (`projectile`) | Projectile Attack, 0,833 s | Đạn bay, sphere cast đến mục tiêu khóa | SP.ATK × 1,00 | 14 / 3,0 s |
| Đánh cánh (`wing strike`) | Wing Attack, 0,667 s | Melee, một mục tiêu khóa | ATK × 1,20 | 3,5 / 4,2 s |

Raw damage được nhân `1 + CRIT DMG / 100` nếu lần roll crit của cast thành công. Sau đó:

`HP mất = raw × 100 / (100 + phòng thủ)`

Physical dùng DEF; Magical dùng SP.DEF. Tên hệ trên UI không tự chọn loại sát thương. Không cộng thêm damage đánh thường vào skill. Sai số tối đa giữa số đo HP và kỳ vọng của 12 lần cast nằm trong 0,03 HP, có sai số float khi trừ khỏi HP thử 50.000. Các số trong CSV là giá trị runtime của fixture, không phải damage cố định hoặc số đề nghị cân bằng game.

Đạn hiện bay tốc độ 9, bán kính sphere cast 0,18, tồn tại tối đa 3 s. Một kẻ địch khác có thể chặn đạn nhưng hiện chỉ mục tiêu đã khóa mới nhận damage. Không có cơ chế tự gây AoE, damage nhiều tick, hiệu ứng hệ hoặc sát thương từ hạt VFX.

## Phán đoán từ chuyển động và phương án đề xuất

Phần dưới là **đề xuất, chưa triển khai thay đổi luật đánh**. Animation thể hiện động tác; không đủ để tự quyết định bán kính, số mục tiêu, power hoặc số tick.

1. **Cắn:** chuyển động rụt đầu rồi lao hàm về trước. Giữ một hit vật lý cự ly gần; canh hit ở lúc hàm tiếp xúc. Nếu bổ sung quét, dùng nón ngắn trước miệng và mỗi mục tiêu chỉ nhận một hit/cast, kiểm vật cản.
2. **Bộc phá:** động tác chuẩn bị dài hơn rồi hướng miệng về trước. Phù hợp một xung bùng nổ hoặc nón phép ngắn. Giữ một hit hiện tại trước; chỉ mở nhiều mục tiêu sau khi chốt bán kính/góc. Không tự gọi đây là lốc xoáy.
3. **Đạn năng lượng:** động tác nhả đạn bằng miệng. Giữ damage tại va chạm thật, tạo visual bay riêng bám đạn từ socket Mouth; chốt chính sách 'địch đầu tiên trúng' hay 'chỉ mục tiêu khóa'. Không dùng VFX nổ đứng yên làm cảm giác đạn bay. Lifetime/range phải hữu hạn, vật cản chặn.
4. **Đánh cánh:** chuyển động cánh/thân, không phải một vòng xoay kéo dài. Hợp với quạt gió hình nón, một xung; có thể giữ ATK để đúng cách đánh vật lý hiện tại. Không nên cho nhiều tick chỉ vì VFX gió có hình xoáy.
5. **Cào:** ba dạng rồng này không gắn clip Claw/Slash trong bốn skill. Cần chọn clip phù hợp cho đúng bộ xương hoặc làm animation bổ sung, rồi cấu hình vùng quét theo tay. Các family khác có clip Slash/Spin nhưng không thể lấy gắn thẳng lên rig rồng. Chưa tự đổi Bite thành Cào.
6. **Lốc xoáy thật:** nên là skill/profile riêng với vùng tồn tại hoặc vùng di chuyển, bán kính, thời gian, chu kỳ tick và giới hạn mục tiêu rõ ràng. Chia tổng power theo tick (ví dụ tổng 1,2 qua bốn tick là 0,3/tick, không phải 1,2 mỗi tick). Roll crit một lần/cast nếu chọn chính sách này; kiểm mục tiêu ở trong vùng mỗi tick, không xuyên tường tùy luật đã chốt; hủy khi thu pet/chết/đổi scene. Không thêm hất tung, kéo hay status khi chưa được yêu cầu.

### Thứ tự nên làm tiếp

1. Căn duration/release với clip thật theo từng form. Hiện species override duration lần lượt **0,80 / 1,00 / 0,95 / 1,00 s**, còn clip thật **0,833 / 1,500 / 0,833 / 0,667 s**; cả bốn đang release sau **0,25 s** tính từ animationStart. Bộc phá có nguy cơ kết thúc hoạt ảnh sớm, Wing giữ trạng thái lâu hơn clip. Cần dùng frame tiếp xúc/nhả đạn, không chọn lại thời điểm bằng tên skill. Sáu pose dùng để phân loại, chưa đủ xác nhận frame hit chính xác.
2. Tận dụng `CreatureAnimationProfile` (skillId → CreatureAction → Animator state), `PetSpeciesSkillSet` và override presentation từng loài. Giữ tên FBX/controller gốc; code gọi action chung. Đã có Bite/Blast/Projectile/WingStrike. Thêm action mới ở cuối enum nếu cần, không đổi thứ tự giá trị serialized cũ.
3. Mở rộng `PetSkillCombatProfile`/executor bằng delivery có cấu hình cho cone/sweep/area-ticks, giữ cách đánh cũ hoạt động. Không đặt công thức damage trong animation hoặc particle. `PetSkillPresentationPlayer` vẫn chỉ phụ trách animation/VFX/SFX, damage do executor xác nhận.
4. Với đòn nhiều mục tiêu/tick, tách idempotency của cast và từng hit. Hiện `NotifyImpact` tiêu thụ token sau một hit; chỉ thêm vòng lặp tick mà không thay contract này sẽ chỉ trúng lần đầu. Dùng khóa cast + target + tick, một commit/hit hợp lệ, reward hạ gục đúng một lần. Cache buffer, pool VFX, giới hạn duration và số vùng; đo trước khi tối ưu sâu.
5. QA riêng: mục tiêu chạy khỏi vùng, chết/khóa bắt trước hit, vật cản/địch chắn đạn, nhiều collider trên một địch, spam, hủy cast, recall/scene unload, damage tổng nhiều tick và reward không lặp. Chỉ sau đó mới chỉnh cân bằng power/cooldown.

## Giới hạn kiểm chứng

Chưa chạy standalone, Profiler/GPU, phím vật lý Z/X/C/V, stress nhiều pet, ép nhánh crit bằng RNG cố định, bài kiểm nhiều địch hoặc toàn bộ scene. 177 assertion xác nhận wiring và damage ở tình huống mục tiêu hợp lệ đứng yên, không chứng minh tất cả nhánh combat đã đạt. Chưa triển khai cào/lốc xoáy/AoE mới hay căn lại release theo clip; đây là phương án cho đợt tiếp theo. Các thay đổi khác của working tree được giữ nguyên, không commit/push.

# Combat pet: damage, animation và phản hồi — 08/11/2026

Đã triển khai vào runtime hiện có; không đổi công thức tăng tám stats, API save, inventory hoặc AI ra quyết định. DemoScene trên đĩa giữ nguyên SHA256 so với đầu task. Model/FBX/controller nguồn của Trung không sửa; form 02/03 dùng bản controller runtime riêng.

## Thông số

| Kỹ năng mặc định | ID giữ nguyên | Damage/tác dụng | Cooldown |
|---|---|---|---|
| Đánh thường | basic | 100% ATK; pet có stats không cộng cứng 20 | Giữ interval/SPD |
| Z — Cắn | bite | 120% ATK trực tiếp; chảy máu 10% ATK ở giây 1/2/3 | 2 s |
| X — Gầm cường hóa | blast | +20% ATK/SP.ATK trong 6 s; refresh, không stack | 10 s |
| C — Đạn năng lượng | projectile | 160% SP.ATK; tốc độ 12 m/s | 3 s |
| V — Chém gió | wing strike | 180% ATK; tốc độ 16 m/s, một địch | 4,2 s |

Phím đi theo ô được trang bị, không hardcode ID vào Z/X/C/V. Giữ phòng thủ vật lý/phép, tỷ lệ/nhân CRIT. Buff tác động cả đánh thường và skill. Actor legacy thiếu metadata vẫn dùng attackDamage fallback để tương thích.

Chảy máu không crit; snapshot ATK/buff khi gây hiệu ứng rồi qua DEF ở từng nhịp. Đánh lại cùng nguồn thay ba nhịp còn lại. Thu pet không xóa vết thương đã gây. Chết, bắt thành công, disable hoặc đổi scene xóa vết thương; nhịp bị CaptureLocked được tiêu thụ, không replay sau mở khóa. Damage hạ địch và EXP/EV chỉ đi qua một cổng commit. Dummy không thưởng.

## Luồng và cấu hình Inspector

`RequestSkill → PetCombatActionDriver → animation → release token → PetSkillCombatExecutor → ApplyCombatHit → CombatDamageResult → VFX/âm thanh/số damage`.

- **PetSkillDefinition** ở `Assets/Game/Skills/Data/Definitions`: tên/mô tả/cooldown, giữ skillId và legacyIds.
- **PetSkillCombatProfile** được tham chiếu từ definition: power (1,2 = 120%), delivery, range, projectile, bleedPower, buff và retreat. Combat profile rồng còn nằm tại thư mục Bats/Combat cũ để giữ GUID; không có nghĩa dùng chỉ số của dơi.
- **PetSkillPresentationProfile**: chế độ NormalizedAnimation; release 0,36/0,43/0,30/0,45, chain 0,85/0,85/0,85/0,90. Playback Gầm 1,25, các đòn khác 1. Socket/cue/VFX/audio riêng. Chế độ này không chạy timer release song song.
- **Species + CreatureAnimationProfile**: ánh xạ ID sang hành động/state theo form. Không đổi tên clip trong FBX. Gầm dùng clip Blast hiện có phối hiệu ứng/âm thanh; không có clip Roar mới được tạo.
- **Resources/CombatFeelSettings** ở CreatureSystem: cameraAmplitude (0 để tắt), cameraDuration, criticalVisualPause (0 để tắt), basic/bleed FX và âm thanh.
- Menu **Tools → Capstone → Creatures → 14. Apply combat feel and skill balance** dựng preset bằng Unity API, giữ GUID khi chạy lại. Menu này áp lại bộ thông số trong bảng; nếu đã tune Inspector riêng thì không chạy lại. Các menu preset cũ 12/13 có thể ghi đè tham số cũ; dùng 14 để áp lại bộ hiện hành.

Driver tự thêm khi dùng skill normalized. Một cast không bị phím skill khác cắt mất windup. Trong cửa 200 ms cuối recovery có một buffer, lệnh hợp lệ mới nhất thắng; kiểm lại skill/cá thể/gate khi thực thi. Chưa chạy thì chưa mất cooldown. Cooldown theo PetId + skillId trong phiên, không reset khi đổi ô/thu gọi và không thêm trường save.

C/V chỉ lùi khi cần, tối đa 2,4 m ở 12 m/s; vật cản kết thúc bước lùi thay vì chờ một timer trống. Giữ lock/combat/nearest/forward, sphere sweep, vật cản và một hit. Đạn trượt không tự damage theo target; hit tường chỉ có surface cue.

## Trình diễn

- VFX rời trong `Assets/Game/Skills/Data/CombatFeel`: nanh khép, hit tím, tụ/đạn năng lượng, lưỡi gió, gầm/aura và nhịp bleed. Tái dùng shader/texture mềm và audio đã có; không sửa prefab thư viện hoặc tạo portrait.
- Số HP mất thật, quỹ đạo cong so le, pop/fade; normal 28, crit 36, bleed 23 logical px. Cosmetic scatter/pitch không dùng RNG combat. Pool 32 label, không raycast; pool FX nhàn rỗi tối đa 24, âm thanh tối đa tám voice đăng ký.
- Recoil chỉ trên visual, không đẩy collider. Crit pause Animator cục bộ 35 ms, throttle 200 ms; luôn restore khi disable/recall/focus loss, không ghi đè speed mới do hệ khác đã thay. Không đổi Time.timeScale.
- Cinemachine Impulse channel bit 12 cho hai camera free/lock đã có, mạnh nhất thắng, tối đa preset 0,03 m/0,1 s. Scene không có PlayerFreeLookRig vẫn combat/VFX bình thường, bỏ qua rung camera; không tạo camera thay thế. HUD/minimap không gắn listener.

## Kết quả kiểm thực chạy

Unity 6000.3.16f1, Editor Play Mode, save thử riêng trên E:

- Lượt ma trận: **314 PASS**, ba form × cấp 1/20/80, bốn skill, direct/crit/HP delta, buff cả hai loại tấn công, ba nhịp bleed sau recall, resummon, chống ghi đè windup và giữ cooldown khi đổi slot.
- Lượt bổ sung cuối: **52 PASS**, buffer thay lệnh/chưa tiêu CD, recall/focus/UI gate, tường, mục tiêu rời đường đạn, first collision giữa nhiều địch, HP lethal clamp/crit thật, refresh bleed, CaptureLocked, death/reentrant lethal tick, restore Animator speed, pool và hai resolution.
- Lượt release cuối: cap 30 có overshoot 0,01207 s, frame quan sát 0,03434 s; cap 60 có overshoot 0,00008 s, frame quan sát 0,02061 s. Đây là cap được đặt trong Editor, không phải chứng nhận FPS ổn định hoặc số đo Profiler.
- `Screen.width/height` được assert đúng **1920×1080 và 1366×768** ở arena engine trong DemoScene. Capture toàn cảnh DemoScene hữu ích giữ tại 1366×768. Các capture đầu có tên preset không khớp kích thước pixel (16:10 hoặc viewport scaled) đã được thay hoặc loại; không tính là bằng chứng 1920×1080.
- Thao tác OS thật: phím 1/2, P mở/đóng, Z/X/C/V, G khi không có bóng và chuột phải ra lệnh. Quan sát C và Z gây damage trong DemoScene, cooldown X 10 giây. **Backspace** thu pet, skill HUD ẩn ngay đầu recall. Phím 0 không phải phím thu và không được tính là một ca pass.
- Các ca engine dùng arena tạm trong Play Mode để định lượng; các ca phím thật dùng camera/cảnh DemoScene đã author. Không nhập fixture vào scene hoặc save thật.

Những lỗi của fixture đã sửa: target cube lún sàn làm LOS từ chối; test death phải đi qua health/death thật thay vì chỉ sửa snapshot; test hết label phải dừng nguồn bleed đang còn phát số. Runtime được gia cố dọn wound ngay khi chết/bắt và xử lý clear reentrant.

## Giới hạn và bàn giao

- Chưa chạy standalone/Profiler chuyên sâu; không có kết luận CPU/GC/GPU hoặc tăng FPS.
- Chưa kiểm physical hold P/G dài, mọi scene, stress số lượng lớn, tương tác với tất cả loài khác, hoặc audition âm thanh trên thiết bị người dùng. G được thử với số bóng bằng 0, không phải bài kiểm giao dịch bắt thú.
- Camera shake/hit reaction được nối trong engine; chưa có phép đo displacement camera hoặc so ảnh pixel. VFX/âm thanh cần người dùng duyệt cảm giác tại camera chơi. Không có cặp ảnh trước/sau cùng camera được chụp mới trong task.
- Probe QA được chuyển ra E và gỡ khỏi Assets sau kiểm; không có fixture test, recorder hoặc build tạm trong project. Không commit/push.

Báo cáo, danh sách file thay đổi, source probe tái hiện, log kiểm và ảnh engine ở:

`E:/UnityWork/CapstoneProjectData/Artifacts/combat-feel-playtest-2026-11-08/`

Xem `REPORT.md`, `changed-files.json`, `checks.txt`, `advanced-checks.txt`, `damage.csv`, `release-timing.csv`, `physical-input.txt` và `cleanup.json`. Ảnh combat ở hai resolution là engine arena thật, không phải mockup; ảnh runtime là DemoScene thật.

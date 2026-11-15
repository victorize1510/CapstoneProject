# Dummy, số damage và dùng skill không cần khóa — 07/11/2026

## Cách thử trong DemoScene

- Trong Hierarchy tìm **TrainingDummy_Bat_Demo** (tên hiển thị: **Hình nhân tập đánh**), ở gần quầy chợ, phía trước/phải vị trí player ban đầu. Tọa độ đặt bằng Editor: **(42,70; -95,43; 38,30)**, player ban đầu (39,70; -93,90; 33,30).
- Play rồi triệu hồi pet như bình thường. Đến gần dummy, dùng Z/X/C/V hoặc bấm nút skill. Không cần khóa mục tiêu; khóa thủ công vẫn được ưu tiên nếu hợp lệ. Khi có nhiều enemy, tự chọn địch gần nhất có đường nhìn và trong tầm, không ưu tiên đặc biệt dummy.
- **Z mặc định là Cắn**, ID `bite`, hồi chiêu **1,2 giây**. Không phải Cào. Có hiệu ứng hai hàm/nanh và tia nhỏ tại miệng khi ra đòn, thêm phản hồi tại điểm trúng. Không đổi clip Bite Attack hoặc ID trong save.
- X là Gầm cường hóa; C là Đạn năng lượng; V là Chém gió theo loadout mặc định. Người chơi đã đổi loadout thì phím vẫn đi theo ô skill hiện được trang bị.
- Dummy đứng yên, không có AI/capture adapter; **5.000 HP**, bị hạ thì tự hồi đầy sau **2,5 giây**. Không phát event hạ địch, không thưởng EXP/EV hay tính quest kill. Chỉ tạo một instance riêng; source model/prefab của Trung không sửa.

## Hành vi đã sửa

### Chọn mục tiêu và đánh theo hướng

1. Ưu tiên enemy đang khóa bằng camera nếu còn sống, không khóa bắt, trong tầm và có đường nhìn.
2. Nếu không có khóa hợp lệ, dùng mục tiêu pet đã được ra lệnh đánh nếu còn hợp lệ.
3. Nếu vẫn không có, tìm enemy gần nhất hợp lệ trong tầm. Không chọn pet đã sở hữu hoặc NPC không phải đối tượng combat.
4. Không có địch thì vẫn phát animation/VFX và chạy cooldown, theo hướng pet lúc nhận lệnh. Buff tự thân không cần địch.

Melee kiểm vùng trước mặt khi phát đòn, nên skill đánh hụt không gây damage cho địch xuất hiện phía sau. Đạn không cần một target được chọn từ trước: bay theo đường thật, trúng **enemy đầu tiên va chạm**, dừng ở vật cản. Xử lý cả collider đã đè lên vị trí nhả đạn và nhiều collider trên một enemy; token chỉ gây một hit. Target chết/biến mất không tự tạo damage hoặc khiến đạn đã bay bỗng biến mất. Giữ các giới hạn range/lifetime, snapshot damage/crit, bước lùi và cơ chế hủy khi thu pet/UI chặn gameplay.

### Số damage nổi

- uGUI screen-space overlay, số bám vị trí trúng trong thế giới qua camera; bay lên, mờ và hết sau khoảng 0,9 giây. Số vật lý màu vàng nhạt, phép màu xanh nhạt, viền tối; dùng font chung.
- Giá trị là **HP thực đã mất sau phòng thủ và giới hạn HP còn lại**. Ví dụ raw 100 nhưng địch chỉ mất 60 HP thì hiện 60; địch chỉ còn 3 HP thì số kết liễu là 3. Không tạo nhãn crit giả khi dữ liệu chưa truyền cờ crit.
- Nhận cả damage từ kỹ năng/đánh thường trên DummyEnemy và từ damage nhận của pet qua CreatureCombatant. Không hiện số cho đánh hụt, damage 0 hoặc dữ liệu NaN/Infinity.
- Pool tối đa 32 label, tái sử dụng; không có GraphicRaycaster, text không nhận raycast. Không chặn nút/HUB. Dọn khi chuyển active scene, mở UI chặn gameplay hoặc chạy tiến hóa. Không lưu số damage vào save.

### Cooldown Z

Trước đây cast bị từ chối nếu không có target, nên không có cooldown để hiện. Nay không-target là một cast hợp lệ. HUD refresh ngay khi nhận cast; dưới 2 giây hiển thị một chữ số thập phân thay vì làm tròn lên. Đã quan sát ảnh Z đang ở 0,8/0,7 giây. Nếu không triệu hồi pet, đang cooldown hoặc gameplay bị chặn thì lệnh vẫn bị từ chối đúng luồng cũ.

## File Unity sửa/thêm

- `Assets/Game/Skills/PetSkillCombatExecutor.cs`: locked/combat/nearest/forward, melee cone, đạn va chạm enemy đầu tiên, retire token khi trượt.
- `Assets/Game/Skills/PetSkillCombatProfile.cs`: tùy chọn autoAcquireTarget (mặc định bật), meleeHalfAngle 55°.
- `Assets/Game/Skills/PetSkillPresentationPlayer.cs`: CompleteMiss và trục tham chiếu cue.
- `Assets/Game/Skills/PetSkillPresentationProfile.cs`: cue actorSpace tùy chọn, mặc định false giữ hành vi cue cũ.
- `Assets/Game/HudSystem/GameplayHudController.cs`: cooldown tức thời và phần giây lẻ.
- `Assets/Game/HudSystem/FloatingCombatText.cs` + meta: số damage nổi và pool dùng chung.
- `Assets/Scripts/Thắng/PetSystem/DummyEnemy.cs`: phát số HP mất thật, cờ trainingDummy, chặn event/phần thưởng của dummy.
- `Assets/Scripts/Thắng/PetSystem/PetController.cs`: không trao EXP/EV cho trainingDummy.
- `Assets/Game/CreatureSystem/CreatureCombatant.cs`: phát số HP pet thực mất.
- `Assets/Game/CreatureSystem/CreatureRolePresentation.cs`: nhánh riêng giữ HP thử của dummy; enemy thật vẫn khởi tạo theo chỉ số tính toán cũ.
- `Assets/Game/CreatureSystem/Editor/PracticeCombatSetup.cs` + meta: authoring bằng Unity API, idempotent và không tự lưu scene đang có thay đổi chưa lưu.
- `Assets/Game/CreatureSystem/Runtime/Training/TrainingDummy_Bat.prefab`, meta và meta thư mục; chỉ tham chiếu model đã có.
- `Assets/Game/Skills/Data/Tactics/BiteFangs.prefab` + meta.
- `Assets/Game/Skills/Data/Definitions/Skill_bite.asset` và `Data/Presentation/Presentation_bite.asset`: nhãn/mô tả Cắn, VFX nanh; giữ cooldown và ID.
- Các Combat profile hiện có lưu giá trị mặc định autoAcquireTarget/meleeHalfAngle, giữ power/range/delivery đã chốt.
- `Assets/Scenes/DemoScene.unity`: thêm đúng một prefab dummy trên nền trống gần player. Lưu native Unity kèm các giá trị mặc định serializer; không đặt fixture QA hoặc camera QA vào scene.
- Tài liệu này ở `Docs/`. Không đổi API provider, công thức 8 stats, inventory hoặc save schema; không commit/push.

## Kiểm chứng thực chạy

Unity 6000.3.16f1, Editor Play Mode, **197 assertion đạt** ở lượt hồi quy cuối:

- Ba dạng rồng production; cả bốn ô đều nhận cast không target và chạy cooldown, animation/VFX thật. Buff tự thân vẫn tăng đúng 20% phần tấn công.
- Chín đòn định lượng (Cắn/Đạn năng lượng/Chém gió × ba form) dùng tự chọn nearest; damage sau phòng thủ khớp HP mất, đúng một hit và đúng một popup cho mỗi hit. Chi tiết trong `damage.csv`, có cả crit thực phát sinh ở một số cast.
- Khóa enemy xa hơn được ưu tiên so với enemy gần khác. Đạn forward trúng địch mới đi vào đường bay; wall chặn đạn và không gây damage xuyên wall; melee hướng trước không trúng địch xuất hiện phía sau.
- Cooldown Z có số thập phân ngay khi cast; hiệu ứng Cắn xuất hiện cả khi đánh hụt. Thu pet ẩn bốn ô và hủy cast đang chờ.
- Dummy không AI, không bắt được, không EXP/EV hoặc defeat event; tự hồi đầy HP. Số kết liễu giới hạn bởi HP còn lại. Burst 50 hit không vượt pool 32, label hết hạn và không chắn tương tác; UI gate chặn số nổi.
- Native scene chứa dummy đã lưu được dùng trong bài kiểm. Roster/save thử trên E; có reset cooldown và giữ adapter camera trong fixture để cô lập điều kiện, không sửa catalog/giá trị save thật để làm demo.
- Kiểm/chụp 1920×1080 cho form 01/03 và 1366×768 cho form 02. Khu kiểm xa có minimap đen vì ngoài world bounds; ảnh riêng `demoscene-dummy.png` chụp vị trí thật trong scene bằng camera kiểm. Scene sau QA giữ đúng SHA256 sau cấu hình dummy.
- Lỗi phát hiện/sửa: CreatureRolePresentation.Start từng ghi đè 5.000 HP dummy bằng HP tính từ loài. Đã thêm ngoại lệ trainingDummy, không đổi initialization của các enemy thật. Hiệu ứng cắn được giữ vị trí ở socket hàm nhưng dùng trục actor và offset trước miệng để tránh xoay lệch theo trục xương.
- Sau khi gỡ probe: compile cuối thành công **10,40 s**, Mono reload **11,034 s**, không có lỗi C# trong lượt cuối. Meta/GUID của các file có trước trong baseline giữ nguyên. Editor còn phản hồi, không để lệnh QA chờ chạy.

Đây là kiểm bằng raycast EventSystem và pointer callback nút thật, không phải chuột/phím vật lý. Chưa chạy standalone, Profiler, mọi scene/camera, target di chuyển nhanh, chướng ngại phức tạp hay stress nhiều combatant. Không lấy kết quả 146/177 assertion của các task trước cộng vào 197.

## Bằng chứng và chỉnh Inspector

Artifact root: `E:/UnityWork/CapstoneProjectData/Artifacts/practice-damage-freecast-2026-11-07/`. Có `checks.txt`, `damage.csv`, báo cáo, source backup và probe lưu ngoài Git để tái hiện. QA probe được gỡ khỏi Assets sau kiểm. Chỉ giữ năm ảnh cuối hữu ích, dọn trung gian theo ledger; không có recorder/build chạy nền.

- [Damage nổi ở 1366×768](E:/UnityWork/CapstoneProjectData/Artifacts/practice-damage-freecast-2026-11-07/damage-form2.png)
- [Z đánh hụt vẫn có cooldown và VFX](E:/UnityWork/CapstoneProjectData/Artifacts/practice-damage-freecast-2026-11-07/z-cooldown-no-target.png)
- [Dummy ở vị trí thật trong DemoScene](E:/UnityWork/CapstoneProjectData/Artifacts/practice-damage-freecast-2026-11-07/demoscene-dummy.png)

Chỉnh Max Health/Revive Delay ở prefab TrainingDummy_Bat. Chỉnh Auto Acquire Target/Melee Half Angle trong Combat profile; Range/Lifetime vẫn là giới hạn thật. Presentation_bite.Release có Actor Space bật, offset (0,0,0.4), scale 0,7, lifetime 0,3 s; Impact dùng cùng nanh scale 0,9. Các cue khác mặc định Actor Space tắt. Menu `Tools → Capstone → Creatures → 13. Add combat dummy and bite feedback` đã chạy; gọi lại sẽ dựng lại preset VFX/targeting, không tạo thêm instance dummy nếu đã có.

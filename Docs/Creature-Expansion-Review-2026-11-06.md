# Kiểm kê 30 dạng thú và bảng dữ liệu chờ duyệt

Ngày 06/11/2026. Hoàn tất **đợt A — kiểm kê nguồn** của [kế hoạch mở rộng](Creature-Expansion-Plan.md). Chưa triển khai đợt B/C/D cho các loài này.

Đây là kết quả đọc source, prefab/controller YAML và importer `.meta` hiện có. Không mở Play Mode, không thay prefab/Animator/scene, không gán chỉ số hoặc bộ kỹ năng. Ba dạng Dragon Dusk đã tích hợp riêng trong DemoScene không thuộc 30 dạng kiểm kê lần này.

**Cập nhật sau kiểm kê:** lookup chỉ số theo loài/dạng đã được sửa và vượt qua 143 kiểm tra Unity Editor ở lượt tiếp theo. Xem [bản sửa và đề xuất dữ liệu Dơi](Bat-Data-Proposal-2026-11-06.md). Các bảng 30 dạng vẫn chờ duyệt; chưa tạo dữ liệu runtime cho Dơi.

## Kết quả

| Hạng mục | Kết quả trên 30 dạng |
|---|---|
| Phân loại | 10 dòng, mỗi dòng 3 dạng; đủ prefab và 30 avatar khác nhau |
| Animator | Mỗi prefab có một Animator, 30 rig import Generic |
| State/clip | 856 lần xuất hiện state, tham chiếu 455 asset motion khác nhau; đường dẫn đều phân giải được |
| Stat Profile / Skill Set trong Family | Thiếu 30 / 30; chưa có dữ liệu cân bằng được duyệt |
| Runtime Visual / Animation Profile / Pet / Wild riêng | Chưa gắn cho 30 dạng |
| Script nguồn | 10 dạng đầu có PetController và bộ kỹ năng prototype; 20 dạng sau không có PetController |
| Collider 3D trong source prefab | Không có ở cả 30; NavMeshAgent không thay thế collider |
| Scale nguồn khác 1 | 10 dạng, từ 1,2 đến 3,5; chưa đo kích thước/va chạm trong engine |
| Tiến hóa nguồn | 20 rule thiếu evolutionId/targetFormId, cả hai rule trên từng dạng đầu cùng sourceStage 0; 10 Family chưa duyệt |

Các con số này mô tả asset hiện có, không chứng nhận animation phát đúng hoặc cấu hình đã sẵn sàng cho gameplay. Các kiểm object trong Unity ngày 05/11 là bằng chứng lịch sử riêng; lượt này chỉ đối chiếu tên state với báo cáo đó.

## Phân loại từng dòng

Tên giữ nguyên theo source; thứ tự dưới đây là thứ tự authoring 01/02/03 hiện có, chưa phải phê duyệt điều kiện tiến hóa.

| Dòng / ID | Dạng 01 → 02 → 03 | Di chuyển có state trong nguồn | Điểm cần xử lý trước khi tích hợp |
|---|---|---|---|
| Dơi / `bat` | Dơi Nhỏ → Dơi Ma Cà Rồng → Dơi Chúa Tể | Cả ba có Fly Forward In Place | Cả ba có Bite/Cast/Projectile và RigJaw; dạng 03 scale 1,2. Phù hợp làm dòng thử tiếp theo sau khi duyệt dữ liệu. |
| Mèo điện / `cat-electric` | Mèo Điện → Mèo Tia Chớp → Mèo Sấm Sét | Cả ba có Walk/Run In Place | Import Generic; không có bone tên Jaw/Mouth trong source đã đọc. Cần tạo Mouth anchor theo đầu model, không đoán vị trí. Root motion bật trong source của cả ba. |
| Chim / `chick` | Chim Con → Gà Lôi → Gà Gió Lốc | 01 nhảy, 02 đi/chạy, 03 bay | Không ép cả dòng dùng một kiểu Move. Miệng khác nhau: Jaw / Beak / JawUp+Down. Hai rule nguồn thiếu icon đích dù Family đã có avatar. |
| Rồng lửa / `dragon-fire` | Khủng Long Lửa → Rồng Lửa Bay → Rồng Lửa Địa Ngục | 01 đi/chạy; 02 bay; 03 có cả đi/chạy và bay | Cần duyệt mode cho dạng 03; tách với Dragon Dusk. Có RigJaw nhưng timing/hướng phóng vẫn cần xem model. |
| Quái trứng / `egg-shadow` | Trứng Ma Quái → Quái Trứng Quỷ → Quái Trứng Lực SĨ | 01 nhảy, 02/03 đi/chạy | Không có Bite ở cả ba; 01 Head Attack, 02/03 Claw/Slash. Không gán Cắn để lấp đủ bốn ô. |
| Ma / `ghost` | Ma Nhỏ → Ma Mặt Nạ → Ma Nguyền Rủa | Cả ba bay | Không có Bite; dạng đầu có Attack, dạng sau Slash. Dạng 02 scale 2; chọn anchor cast/hand theo từng rig. |
| Ốc / `shell` | Ốc Nhà → Ốc Gai → Ốc Mượn Hồn Vua | 01 đi/chạy/lăn; 02 đi; 03 đi/chạy | Chỉ 01 có Bite; 02 Claw/Hide+Spin, 03 Stab/Slash/Summon. Scale 01/02/03 = 1/2/1,5. |
| Rắn / `snake-fire` | Rắn Lửa → Rắn Bùng Cháy → Rắn 3 Đầu | Slither; 03 có thêm Dash | Dạng 02 dùng đúng tên `Slither Foward In Place` viết thiếu r. Dạng 03 có ba hàm, Bite/Projectile Front/Low: phải chọn đầu và hướng cụ thể. |
| Hướng dương / `sun-blossom` | Hướng Dương Nảy Mầm → Hướng Dương Tiên → Hướng Dương Nữ Hoàng | Cả ba có Fly Forward In Place | Có animation di chuyển, không kết luận phải đứng yên theo hình dáng. Không có Bite; Head/Punch/Petals cần thiết kế skill riêng. Scale = 1/3/3,5. |
| Sói / `wolf` | Sói Tí Nị → Sói Bóng Tối → Người Sói Quỷ | Cả ba đi/chạy | Cả ba có Bite/Cast/Projectile; 03 thêm Slash/Smash, scale 1,5 và rig khác. Cũng phù hợp làm dòng thử sau Dơi. |

## Những điểm phải giải quyết trước đợt C

### 1. Tra chỉ số đang đổi mã dạng của mọi loài sang mã rồng

Trong `Capstone/Assets/Game/HudSystem/PetStatCalculator.cs`, `ResolveProfile` gọi `CanonicalProfileFormId(form)`; hàm này đổi `01/02/03` thành `dragon-dusk-base/winged/elder` mà không nhận hoặc kiểm ID loài. Nếu tạo profile `bat/01` theo Family hiện tại thì lookup bị chuyển thành `bat/dragon-dusk-base` và không khớp.

Ưu tiên sửa trước khi đưa dòng thứ hai vào runtime: lookup theo cặp **definitionId + formId**, chỉ áp dụng alias dạng rồng cho Dragon Dusk và alias rồng đã có. Giữ tương thích save rồng; kiểm cả ID rồng cũ lẫn các cặp `bat/01`, `wolf/01` độc lập nhau. **Lượt kiểm kê chưa sửa; lượt tiếp theo đã sửa và kiểm 143 trường hợp Unity Editor**, xem [bản cập nhật](Bat-Data-Proposal-2026-11-06.md).

### 2. Không dùng thẳng source prefab làm root Pet/Enemy/Wild

Mười dạng đầu chứa `PetController`, `PetHudRuntimeStats`, `PetCollectionMetadata`, `PetEvolutionVisualController` và NavMeshAgent. Mỗi dạng đầu đang có bốn skill prototype `bite/blast/projectile/wing strike`, đồng thời có bản learned tương ứng và bật fallback prototype. Đó không phải bộ kỹ năng đã duyệt cho từng loài. Health 100/100 trong prefab cũng không phải sáu base stat của loài.

Giữ source Trung làm art nguồn. Khi được duyệt, tạo Visual sạch dùng chung mesh/material/controller, rồi root OwnedPet và Wild riêng như dòng rồng. Chưa làm Enemy AI hoặc NPC. Không copy health/genetics/skill mẫu sang tất cả loài; cá thể mới và save nạp lại tiếp tục dùng luồng hiện có.

Các trường definitionId/species của metadata trên mười source dạng đầu đang trống; currentFormId là `base`. ID Family `bat`, `wolf`… là ID authoring hiện có, chưa tự ghi vào cá thể hoặc save cũ. Cần chốt alias/migration nếu có save thật dùng tên loài cũ.

### 3. Rule nguồn chưa đủ để tạo chuỗi tiến hóa

Cả 20 rule có reference visual nhưng thiếu ID và targetFormId; cả hai rule của mỗi dòng đều ghi sourceStage 0 và requiredLevel 0. Không coi các giá trị đó là điều kiện được duyệt hoặc một chuỗi 01→02→03 hợp lệ. Dòng chim còn thiếu hai targetIcon trong rule nguồn. Không ghi ngược vào prefab nguồn để sửa trong đợt này.

### 4. Mapping action, socket và controller cần kiểm thực tế

- 11 dạng không có state Bite: toàn bộ Trứng/Ma/Hướng dương và Ốc 02/03. Không thay Bite bằng Cast/Slash/Punch một cách âm thầm.
- `Move` là hành động chung; animation đích có thể chạy, bay, trườn hoặc nhảy theo dạng. Giữ tên clip/state nguồn; bảng CSV ghi full path thật, gồm cả lỗi chính tả `Foward`.
- Các controller đều có chuyển tiếp không điều kiện. Cần kiểm khi phát skill có bị chuỗi trình diễn tự chuyển state/ngắt hay không; đọc source chưa đủ kết luận đó là lỗi runtime. Nếu cần controller riêng, giữ clip nguồn và tạo controller runtime riêng sau khi xem motion.
- `PetController.ConfigureAnimator` hiện tắt root motion khi bind. Ba source mèo đang bật root motion trong asset; khi tách Visual cần đặt cấu hình có chủ đích và kiểm riêng chuyển động, không suy ra từ checkbox nguồn rằng actor đang bị trôi trong runtime.
- Các đường socket trong bảng là xương/transform tìm thấy trong hierarchy, đã loại Gizmo. Chưa phải socket VFX đã gắn. Miệng không có xương riêng thì cần anchor theo đầu, local offset và trục phóng được xem trực tiếp trên model.
- Giữ kích thước thế giới riêng từng dạng. Không nhân scale hai lần trên actor + visual, cũng không chuẩn hóa tất cả về 1 rồi làm mất chênh lệch kích thước đã author. Collider phải fit sau khi chốt scale; không lấy kích thước mẫu NavMeshAgent làm collider.

### 5. Bộ skill và trình diễn dùng chung chưa đủ cho 30 dạng

| Skill ID hiện có — giữ nguyên | Số dạng khớp ít nhất một state mặc định | Cần duyệt |
|---|---:|---|
| `bite` | 19/30 | Learnset, hệ, cấp học, action/timing và hiệu ứng theo dạng |
| `blast` | 27/30 | Cấu hình mặc định cho phép Cast Spell; **0/30 có Blast Attack**. Khớp fallback không chứng minh có động tác Blast riêng |
| `projectile` | 24/30 | Chọn đúng state Front/Low nếu có, socket phóng và executor/impact thực |
| `wing strike` | 0/30 | Không có Wing Attack trong 30 controller; không phát chỉ vì model có cánh |

Cả bốn Presentation Profile hiện đều chưa gán prefab VFX hoặc sound cho Cast/Release/Impact. `PetSpeciesSkillEntry.presentationOverride` đã cho phép cùng một Skill ID dùng animation/timing/VFX/SFX khác nhau theo loài/dạng; không cần clone một skill gameplay chỉ để đổi màu hiệu ứng.

`PetSkillPresentationPlayer.NotifyImpact` cần collision/combat handler thực gọi bằng cast token; hiện ngoài bộ kiểm chưa thấy caller gameplay. Không coi có clip Projectile là đã có đạn, va chạm hoặc damage skill hoàn chỉnh. Không tự tạo cơ chế mới trong bước kiểm kê.

Các nhóm clip Head/Claw/Slash/Punch/Stab/Spin/Blow Petals là **nguồn động tác để thiết kế thêm**, chưa có SkillDefinition được duyệt tương ứng. Trước khi tạo cần chốt ID, cơ chế thật, cooldown, hệ, cấp học, icon, timing và VFX/SFX. Không suy ra sát thương/buff từ tên clip hoặc hình ảnh.

## Bộ bảng để duyệt

| File | Cách dùng |
|---|---|
| [30-Forms-Inventory.csv](CreatureExpansionReview/30-Forms-Inventory.csv) | 30 dòng nguồn: tên, GUID, avatar, rig/controller, số state, scale, script và dữ liệu prototype. Cột `source_*` là hiện trạng, không phải quyết định mới. |
| [30-Forms-Data-Approval.csv](CreatureExpansionReview/30-Forms-Data-Approval.csv) | Điền hệ theo dạng, sáu base stat, CRIT, khả năng bắt/rate, learnset, bốn ô khởi tạo, mode/scale. Các ô chưa duyệt để trống. |
| [30-Forms-Animation-Approval.csv](CreatureExpansionReview/30-Forms-Animation-Approval.csv) | State có thật cho từng action. Danh sách nhiều state là các ứng viên, không phải phát nối tiếp. Điền lựa chọn Move/Attack và timing sau khi xem motion. |
| [30-Forms-Socket-Approval.csv](CreatureExpansionReview/30-Forms-Socket-Approval.csv) | Xương ứng viên có thật; điền anchor, offset/hướng và collider sau khi xem model. |
| [20-Evolution-Approval.csv](CreatureExpansionReview/20-Evolution-Approval.csv) | Chốt 20 bước tiến hóa; các cột `source_*` giữ giá trị nguồn chưa hoàn chỉnh để đối chiếu. |
| [Skills-Approval.csv](CreatureExpansionReview/Skills-Approval.csv) | Bốn định nghĩa dùng chung hiện có, khả năng khớp state, ô duyệt loài/cấp/action/VFX/SFX. Skill mới chưa có dữ liệu thì chưa thêm ID. |

CSV UTF-8 có BOM. Khi mở bằng Excel nên đặt các cột `form_id`, ID và GUID thành **Text** để giữ số 0 đầu trong `01/02/03`. Các bảng là tài liệu; không có script tự import chúng vào gameplay.

Quy tắc điền dữ liệu:

- Base HP/ATK/SP.ATK/DEF/SP.DEF/SPD là số nguyên 1–255 theo `PetStatProfile`; không điền HP runtime hay tổng chỉ số ở cấp hiện tại vào đây. CRIT RATE/DMG giữ đơn vị phần trăm và công thức đang có.
- `is_catchable` cần chốt true/false. Nếu true, baseCatchRate cần 1–255. Không tự dùng 255/120/45 của Dragon Dusk cho dòng khác.
- Hệ từ metadata dạng đầu chỉ là giá trị nguồn để đối chiếu. Không lan sang dạng 02/03; `Wind` cũ không tự đổi thành `Flying` trong bộ hệ mới.
- Learnset ghi stable skillId + cấp học; default loadout tối đa bốn skill nằm trong learnset. Giữ nguyên ID `wing strike` có dấu cách nếu tái sử dụng. Không đủ bốn skill hợp lệ thì để slot trống/khóa theo luồng hiện có.
- Điều kiện tiến hóa cần ID ổn định, dạng nguồn/đích, trigger, ngưỡng hoặc item thật. Dạng 03 chỉ được đánh dấu cuối sau khi duyệt chuỗi; không tạo điều kiện từ độ hiếm hoặc số liệu minh họa.
- `approved_by` và status chỉ đổi khi đã duyệt. Không coi trường trống là 0 hoặc một giá trị mặc định hợp lệ.

## Thứ tự đề xuất

1. Duyệt dữ liệu **Dơi 01/02/03** trước: animation di chuyển cùng kiểu, có Bite/Cast/Projectile và Jaw ở cả ba; chưa có nghĩa là skill/damage/VFX sẵn sàng.
2. Trước tích hợp, sửa lookup form chỉ số có giới hạn theo loài và kiểm hồi quy Dragon Dusk. Chuẩn bị Visual/Profile/OwnedPet/Wild riêng cho dòng được duyệt; không sửa source Trung.
3. Kiểm summon/recall, trang bị/thực thi skill, tiến hóa đổi model/avatar, giữ cá thể/genetics/HP/loadout, capture và save/load trong scene/save tạm. Chưa đổi spawn/catalog DemoScene cho dòng mới trước khi kiểm đạt.
4. Tiếp Sói hoặc Mèo điện, rồi theo lô 2–3 dòng. Chim/Rồng lửa cần duyệt đổi mode; Trứng/Ma/Ốc/Hướng dương cần bộ skill phù hợp, không sao bộ rồng.

## Kiểm đã chạy và giới hạn

Đã chạy **186 kiểm tra tĩnh**: số dòng/dạng, key/GUID, asset reference, một Animator, rig Generic, đường state/motion và đối chiếu tên state với báo cáo Unity-object trước đó. Không có mục tĩnh thất bại. Các mục authoring thiếu vẫn là thiếu, không được tính thành đã sẵn sàng cho game.

Chưa chạy trong lượt này: xem chuyển động/pose trong Unity, đo world bounds/collider/socket, Play Mode, thao tác bàn phím, standalone, Profiler hoặc hiệu năng bộ 30 dạng. Không tạo prefab runtime, không sửa stat/skill/gameplay/save và không sửa DemoScene.

Báo cáo máy đọc, toàn bộ state→motion, hash bảo toàn và script kiểm kê nằm ngoài Git tại:
`E:/UnityWork/CapstoneProjectData/Artifacts/creature-expansion-audit-2026-11-06/`.

Không tạo ảnh/frame/video/build hoặc save thử trong lượt kiểm kê. Không commit/push.

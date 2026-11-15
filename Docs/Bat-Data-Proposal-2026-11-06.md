# Dòng Dơi — đề xuất ngày 06/11, đã triển khai ngày 07/11/2026

Ngày 06/11/2026. Bước tiếp theo sau [kiểm kê 30 dạng](Creature-Expansion-Review-2026-11-06.md).

Người dùng đã giao triển khai bộ số liệu, skill và tiến hóa dưới đây. Ba dạng Dơi hiện có Stat Profile, SkillDefinition/Skill Set, Visual/OwnedPet/Wild/Enemy runtime và tích hợp DemoScene. Xem [hướng dẫn và kết quả triển khai](Bat-Combat-Enemy-Setup.md) để biết trạng thái hiện hành; tài liệu này giữ phần lý do thiết kế và kiểm lookup ban đầu. Các bảng kiểm kê 30 dạng cũ không được dùng làm bằng chứng runtime cho các loài còn lại.

## 1. Sửa lookup đã thực hiện

`PetStatCalculator` hiện tra theo **definitionId + formId** của từng loài. Alias dạng `01/02/03/base` sang tên profile rồng chỉ áp dụng cho Dragon Dusk/alias rồng; mã dạng của Dơi, Sói và các loài khác giữ nguyên. Khi có cả definitionId và tên species, ID ổn định được ưu tiên; species tiếp tục làm fallback cho dữ liệu cũ.

Không đổi công thức tám chỉ số, HP ×4, giới hạn IV/EV, chỉ số rồng, capture rate rồng hoặc save schema. Profile thiếu vẫn theo chính sách cũ của caller; `TryGetProfile` trả false, không tự lấy profile loài khác hay dạng 01.

Đã chạy **143 kiểm tra thật trong Unity Editor**: profile rồng từ Resources, năm tên/alias rồng, mã dạng số/tên cũ, tám chỉ số rồng trước hai cách gọi; 30 cặp loài/dạng với profile sentinel tạm trong bộ nhớ; ưu tiên ID, fallback species, dạng thiếu, loài lạ, null, generic base và phục hồi cache. Đây là kiểm Edit Mode, không phải Play Mode/standalone hoặc xác nhận gameplay Dơi.

Chạy lại bằng menu **Tools → Capstone → Creatures → 8. Verify scoped stat profile lookup** ở Edit Mode. Kiểm chỉ dùng fixture trong bộ nhớ, không tạo `.asset`, không ghi scene/save; báo cáo ghi ra E. Runner tự động của lượt này đã gỡ.

## 2. Vai trò dự kiến của dòng Dơi

Dơi thiên về tốc độ và đánh vật lý, đổi lại HP/phòng thủ thấp hơn dòng Dragon Dusk hiện có. Cả ba dạng dùng animation bay, Bite, Cast và Projectile đã có trong controller. Đã thêm enemy đánh thường theo yêu cầu mới; chưa thêm AI NPC, hút máu, gây sợ, né tránh hoặc cơ chế bay vượt địa hình.

### Mốc cân bằng với Rồng bóng tối

Theo yêu cầu tiếp theo của người dùng, lấy ba profile Dragon Dusk đang có làm mốc; giữ phân bố lệch theo vai trò, không chia đều sáu stat hoặc ép mọi dòng có cùng tổng điểm.

| Cùng giai đoạn | BST Rồng bóng tối hiện có | BST Dơi đề xuất | Vai trò phân biệt |
|---|---:|---:|---|
| 01 | 330 | 300 | Rồng thiên SP.ATK; Dơi thiên ATK/SPD, ít máu và phòng thủ hơn. |
| 02 | 430 | 410 | Rồng giữ khả năng đánh phép; Dơi tăng áp lực vật lý và tốc độ, không thành thú chịu đòn. |
| 03 | 550 | 520 | Rồng SP.ATK 135/HP 90; Dơi ATK 105/SPD 140 nhưng SP.ATK 65/HP 75. |

Đề xuất giữ bảng Dơi bên dưới làm bộ số ban đầu. BST thấp hơn rồng không tự chứng minh yếu hơn: bộ skill, tầm đánh, cooldown, thời gian trúng đòn và các stat thực sự được executor sử dụng mới quyết định hiệu quả chiến đấu.

**Giới hạn tìm thấy khi đọc code:** đánh thường trong `PetController.CalculateAttackDamage` đang dùng attackDamage + ATK và CRIT; tốc độ di chuyển/theo chủ và attackCooldown là cấu hình riêng. Chưa thấy SPD được nối trực tiếp vào các giá trị này. Vì vậy không được quảng bá SPD cao là đang tự chạy nhanh/đánh nhanh trong game. SP.ATK/SP.DEF cũng cần kiểm tại đường damage của skill tương ứng trước khi xác nhận cân bằng. Lượt này không tự nối stat vào cooldown hoặc đổi công thức combat.

| Dạng | Tên hiện có | Hệ chính / phụ đề xuất | Base HP | ATK | SP.ATK | DEF | SP.DEF | SPD | BST |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| 01 | Dơi Nhỏ | Dark / Flying | 45 | 55 | 35 | 35 | 40 | 90 | 300 |
| 02 | Dơi Ma Cà Rồng | Dark / Flying | 60 | 80 | 50 | 50 | 55 | 115 | 410 |
| 03 | Dơi Chúa Tể | Dark / Flying | 75 | 105 | 65 | 65 | 70 | 140 | 520 |

`Dark/Flying` ở đây là lựa chọn đề xuất theo bộ hệ hiện hành, không phải đổi `Wind` thành `Flying` tự động. Source dạng đầu hiện có Dark, dạng sau chưa gán metadata hệ. Tên loài/dạng vẫn giữ để bạn chỉnh sau.

- CRIT RATE đề xuất chung **6,25%**, CRIT DMG **50%**; dùng công thức đang có, không thêm cơ chế mới.
- Đề xuất bắt được cả ba dạng; baseCatchRate riêng **210 / 100 / 45**. Đây là ba giá trị lựa chọn, không tính từ rarity/BST/giai đoạn. Không đưa số bắt nội bộ ra UI.
- Không đổi số liệu của Dragon Dusk để khớp dòng Dơi.

Đối chiếu ở cùng cấp 20, IV = 15 cho sáu chỉ số, EV = 0, tính khí Serious, không bonus skill:

| Dạng | HP runtime | ATK | SP.ATK | DEF | SP.DEF | SPD |
|---|---:|---:|---:|---:|---:|---:|
| 01 | 204 | 30 | 22 | 22 | 24 | 44 |
| 02 | 228 | 40 | 28 | 28 | 30 | 54 |
| 03 | 252 | 50 | 34 | 34 | 36 | 64 |

Bảng cấp 20 chỉ minh họa tính bằng công thức hiện hành trên các base stat đề xuất. Không phải số đo runtime Dơi hoặc thay đổi chỉ số trên cá thể thật. IV/EV/tính khí thật vẫn thuộc cá thể.

## 3. Kỹ năng: dùng danh mục chung, override trình diễn theo dạng

Bốn definition hiện có là `bite` hệ Nature, `blast` hệ Light, `projectile` hệ Fire và `wing strike` hệ Wind. Đổi VFX/SFX không đổi hệ gameplay của một definition. Không sửa các definition này chỉ để làm Dơi vì sẽ ảnh hưởng thú đang dùng chúng.

Đề xuất ba skill dùng chung mới, chưa tạo asset hay đăng ký ID vào catalog:

| ID dự thảo | Nhãn dự thảo | Hệ | Action | Cấp học đề xuất | Cooldown đề xuất | Ghi chú |
|---|---|---|---|---:|---:|---|
| `bite.normal` | Cắn | Normal | Bite | 1 | 1,6 giây | Có thể dùng chung cho loài khác có animation Cắn; không đổi ID `bite` cũ. |
| `orb.dark` | Đạn bóng tối | Dark | Projectile | 1 | 3,6 giây | Cùng định nghĩa gameplay, màu/âm/socket theo dạng bằng presentationOverride. |
| `pulse.dark` | Xung bóng tối | Dark | Cast | 12 | 5 giây | Chưa thêm buff/debuff; hình thức gây damage và hệ số cần duyệt trước khi nối executor. |

Hệ số damage/cự ly/va chạm của các skill mới **chưa chốt**. Không coi có clip là đã có projectile/executor hoàn chỉnh. Nếu cơ chế mong muốn không có sẵn thì cần duyệt phần gameplay riêng trước khi triển khai, không thêm ngầm khi gắn animation.

Learnset đề xuất cho cả ba dạng: hai skill đầu ở cấp 1, skill thứ ba ở cấp 12. Cá thể khởi tạo mới trang bị hai skill đầu; hai ô còn lại để trống theo hệ hiện có. Học được skill mới thì hiện trong kho để người chơi tự chọn trang bị, không tự ghi đè loadout.

Không ép đủ bốn skill bằng `wing strike`: cả ba controller Dơi không có Wing Attack. Dạng 03 có Kick/Spin nhưng chưa thêm thành skill thứ tư trước khi duyệt cơ chế. Khi tiến hóa hoặc load save, giữ loadout đang có theo luồng hiện hành.

## 4. Ánh xạ animation và socket đề xuất

| Action chung | 01 | 02 | 03 |
|---|---|---|---|
| Idle | Base Layer.Idle | Base Layer.Idle | Base Layer.Idle |
| Move / Fly | Base Layer.Fly Forward In Place | Base Layer.Fly Forward In Place | Base Layer.Fly Forward In Place |
| Bite | Base Layer.Bite Attack | Base Layer.Bite Attack | Base Layer.Bite Attack |
| Cast | Base Layer.Cast Spell | Base Layer.Cast Spell | Base Layer.Cast Spell |
| Projectile | Base Layer.Projectile Attack | Base Layer.Projectile Attack | Base Layer.Projectile Attack |
| Hit | Base Layer.Take Damage | Base Layer.Take Damage | Base Layer.Take Damage |
| Death | Base Layer.Die | Base Layer.Die | Base Layer.Die |

Các state trên có thật theo kiểm kê nguồn. Chưa phát motion để đo frame tiếp xúc, loop, thời điểm release hoặc độ trượt. Chưa có state Spawn riêng; cần xem animation trước khi quyết định phần triệu hồi, không tự biến Cast thành action Spawn trong dữ liệu authoring.

- Mouth: ứng viên `RigJaw` của từng dạng, tạo anchor có local offset/trục phát đúng đầu mõm sau khi xem model. Không gắn Gizmo làm socket.
- Body: dạng 01 dùng ứng viên `RigHub` sau khi kiểm hướng/trục; dạng 02 có `RigSpine`, dạng 03 có `RigSpine1/2/3` để chọn anchor phù hợp. RigHub ở đây là ứng viên cần xem trực tiếp, chưa gắn vào prefab.
- Ground: root visual hoặc anchor sát đất theo kích thước model đã đo.
- Scale nguồn lần lượt **1 / 1 / 1,2**. Giữ tỷ lệ thế giới sau khi tách root/visual, tránh nhân hai lần; chưa chốt collider khi chưa đo bounds.
- VFX/SFX: Bite cần cue gọn, Projectile cần cue phát ở Mouth và impact từ va chạm thật, Cast cần cue Body. Chọn prefab/clip âm thực có trong project hoặc do bạn cung cấp; chưa tự gán asset hay tạo hiệu ứng mới trong bước đề xuất.

## 5. Tiến hóa đề xuất

| Bước | ID rule dự thảo | Điều kiện đề xuất | Item | Chính sách dữ liệu |
|---|---|---|---|---|
| 01 → 02 | `bat.01-to-02.level` | Đủ cấp 18, xác nhận qua luồng tiến hóa hiện có | Không | Giữ cùng cá thể, nickname, IV/EV/tính khí, HP theo quy tắc hiện hành và loadout. |
| 02 → 03 | `bat.02-to-03.level` | Đủ cấp 36, xác nhận qua luồng tiến hóa hiện có | Không | Đổi đúng avatar/model/profile dạng 03; đánh dấu dạng cuối sau khi duyệt. |

Không áp dụng rule sourceStage 0/requiredLevel 0 đang nằm trên source. Không cấp thú/bóng/item, không thêm vào Shop/loot/spawn hoặc thay catalog DemoScene trong lượt này.

## 6. File duyệt và việc tiếp theo

- [Bat-Forms-Proposal.csv](CreatureExpansionReview/Bat-Forms-Proposal.csv): ba bộ base stat, CRIT, hệ và capture; tất cả `PROPOSED_NOT_APPROVED`.
- [Bat-Skills-Proposal.csv](CreatureExpansionReview/Bat-Skills-Proposal.csv): ba skill dự thảo, action/cooldown/cấp học, phần executor/damage chưa duyệt.
- [Bat-Evolution-Proposal.csv](CreatureExpansionReview/Bat-Evolution-Proposal.csv): hai bước tiến hóa dự thảo.

Sau khi duyệt số liệu và cơ chế skill: tạo runtime riêng cho Dơi, xem motion/socket/collider, nối executor đang có và kiểm vòng summon/skill/evolution/capture/save-load trong scene/save thử trên E. Chỉ sau khi đạt mới nối vào DemoScene; chưa làm Enemy AI/NPC.

Thứ tự đợt tiếp theo:

1. Theo yêu cầu bổ sung, chẩn đoán lỗi UI không bấm/thao tác khi chuyển sang DemoScene: xác minh EventSystem, input module, raycast, callback, modal/focus và Back qua runtime. Đây là sửa/rà tương tác, chưa thiết kế lại UI.
2. Chốt vai trò và dữ liệu Dơi theo bảng này; khi triển khai tạo đúng ba bộ Visual/Animation Profile/OwnedPet/Wild riêng và giữ source Trung.
3. Kiểm animation/contact/socket/collider, sau đó nối skill theo khả năng executor thật. Không tuyên bố cân bằng phép/tốc độ trước khi xác minh đường dùng stat; thiếu cơ chế thì tách thành đầu việc gameplay rõ ràng.
4. Chạy summon/recall, chọn/đổi skill, tiến hóa, capture và save/load trên E. So thời gian hạ mục tiêu, thời gian nhận sát thương và nhịp skill cùng điều kiện với rồng; chưa tự đổi spawn/catalog trước khi đạt.
5. Mở rộng theo lô nhỏ sang Sói/Mèo điện; thiết kế UI vẫn để sau.

**Thiết kế/skin UI để sau**, theo yêu cầu người dùng. Yêu cầu bổ sung sau đó đã được xử lý riêng: [lỗi input UI trong DemoScene](UI-DemoScene-Input-Fix-2026-11-06.md) do hai EventSystem cùng active đã được sửa và kiểm runtime; không đổi bố cục, asset hoặc style.

Compile, kết quả Editor và hash bảo toàn được lưu ở:
`E:/UnityWork/CapstoneProjectData/Artifacts/stat-profile-bat-proposal-2026-11-06/`.
Chưa chạy Play Mode/standalone/Profiler cho dòng Dơi. Không commit/push.

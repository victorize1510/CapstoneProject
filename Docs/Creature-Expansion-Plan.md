# Kế hoạch mở rộng thư viện thú — CHỜ DUYỆT

Ngày 05/11/2026. Chỉ là kế hoạch cho mục 4; chưa tạo stat/skill/rule hay chuyển các loài sau vào gameplay.

## Điểm xuất phát

- 11 dòng / 33 dạng đã được lập danh mục. Ba dạng Dragon Dusk có bộ thử runtime riêng.
- Còn 30 dạng thiếu Stat Profile, 30 dạng thiếu Skill Set; 10 dòng chưa duyệt điều kiện tiến hóa.
- Prefab Trung là nguồn art; không sửa trực tiếp, không duplicate FBX/texture cho từng vai trò.
- Family chứa các Form 01/02/03. Mỗi Form có avatar, portrait tùy chọn, stat profile, skill set, visual, animation profile và root gameplay riêng. ID cá thể/genetics/loadout nằm trên cá thể, không nằm trên Family.

## Đợt A — kiểm kê, chưa cân bằng

Tạo bảng cho từng dòng: tên ba dạng, ID authoring, source GUID, avatar, rig, Animator Controller, state có thật, socket miệng/tay/thân và tỉ lệ model/collider. Đánh dấu thiếu thay vì mượn dữ liệu của rồng.

| Dòng | ID authoring hiện có | Cần duyệt trước triển khai |
|---|---|---|
| Dơi | bat | Chạy/bay/đậu, socket và ba dạng |
| Mèo điện | cat-electric | Di chuyển, cắn/đánh, hướng phóng chiêu |
| Chim | chick | Đi/chạy/bay theo từng dạng |
| Rồng lửa | dragon-fire | Tách với Dragon Dusk, action và hiệu ứng riêng |
| Quái trứng | egg-shadow | Dáng di chuyển, chuyển dạng và điểm phát chiêu |
| Ma | ghost | Bay/lơ lửng, vùng nhận đòn và socket |
| Ốc | shell | Tốc độ chuyển động/animation và vị trí nhận đòn |
| Rắn | snake-fire | Rig chuyển động, hướng miệng và collider |
| Hướng dương | sun-blossom | Di chuyển có thật hay đứng yên, điểm phóng chiêu |
| Sói | wolf | Đi/chạy/cắn, scale và vùng nhận đòn |

Các câu trong cột cuối là mục cần kiểm, không phải kết luận rằng model đã có những animation này.

## Đợt B — bạn duyệt bảng dữ liệu

Mỗi dạng cần chốt: 6 base stat dùng trong công thức combat hiện có, bonus CRIT nếu áp dụng, hệ, isCatchable/baseCatchRate, skill học theo cấp, 4 ô khởi tạo, điều kiện tiến hóa và dạng cuối. Không tự dùng rarity hay chỉ số trong ảnh để tạo công thức.

Mỗi skill: stable ID, tên/mô tả, icon, cooldown, cấp học; action logic; loại sát thương và hiệu ứng trình bày chỉ khi cơ chế thật đã được xác nhận. Chốt VFX/SFX dùng chung hay override theo loài. Không coi metadata màu là buff/debuff engine.

Đầu ra duyệt: CSV/Markdown 30 dạng + danh sách skill tái sử dụng/mới + bảng điều kiện tiến hóa. Chưa ghi vào asset runtime cho đến khi bạn duyệt.

## Đợt C — làm một dòng trước

Sau duyệt, chọn một dòng có rig/state đủ rõ để kiểm. Tạo Visual sạch, 3 Animation Profile và root Pet/Wild riêng. Enemy chỉ giữ ranh giới prefab, không làm AI theo phạm vi hiện tại.

Giữ state/clip nguồn; ánh xạ Idle/Move/Bite/Cast/Hit/Death theo state thật. Không có Bite thì báo thiếu, không tự thay bằng một động tác khác. Sửa timing và socket theo từng model; hiệu ứng cùng skill có thể override theo loài.

Kiểm ở scene/save thử: summon/recall, skill thực thi, tiến hóa đổi avatar/model, giữ HP/IV/EV/tính khí/loadout, lưu rồi mở lại, capture nhận đúng cá thể. Chỉ sau khi đạt mới thay nguồn spawn/catalog tương ứng trong DemoScene. Không thả thêm ba bản sao vào scene để thay cho tích hợp catalog.

## Đợt D — mở rộng theo lô nhỏ

Mỗi lô 2–3 dòng, chạy validator để không bỏ sót state/skill/avatar. Dùng cùng bộ kiểm; scene/catalog chỉ đổi cho các dòng đã đạt. Thiếu art/clip hay dữ liệu cân bằng thì giữ ngoài production và ghi rõ người cần bổ sung.

## Tiêu chí bàn giao từng dòng

- Source prefab/meta không đổi; không trùng Pet và Enemy brain trên một actor.
- Không có action/skill ID sai; avatar và socket theo đúng dạng.
- Chỉ số theo dữ liệu đã duyệt, không sao cả bộ rồng.
- Save/load/tiến hóa không nhân đôi cá thể hay làm mất trang bị/genetics.
- QA trên E ngoài Git, có giới hạn frame/dung lượng và ledger dọn dẹp.
- Báo riêng phần compile, callback engine, thao tác người chơi và standalone; không tự coi đã đạt toàn bộ.

**Cập nhật 06/11/2026:** đã hoàn tất Đợt A ở mức kiểm kê source/asset tĩnh theo yêu cầu người dùng.
Xem [báo cáo 30 dạng và bộ CSV chờ duyệt](Creature-Expansion-Review-2026-11-06.md).
Chưa xem motion trong engine và chưa triển khai dữ liệu/runtime cho 30 dạng; Đợt B chờ duyệt bảng dữ liệu trước Đợt C/D.

**Lượt tiếp theo cùng ngày:** đã sửa lookup chỉ số theo loài/dạng và kiểm 143 trường hợp trong Unity Editor. Đã lập [đề xuất riêng dòng Dơi](Bat-Data-Proposal-2026-11-06.md), chưa áp dụng số liệu, skill hoặc điều kiện vào runtime. Đợt B vẫn chờ duyệt; UI để sau theo yêu cầu người dùng.

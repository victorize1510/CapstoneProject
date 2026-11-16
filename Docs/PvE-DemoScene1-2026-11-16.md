# PvE, item và thu phục — DemoScene 1

Ngày: 2026-11-16. Scene làm việc: `Capstone/Assets/Scenes/DemoScene 1.unity`.
Không chạy generator PvE cũ dành cho DemoScene gốc.

## Dữ liệu và cách dùng

- `Tools > Capstone > PvE > 1. Prepare DemoScene 1 (preserve original)` cấu hình scene mới, không thêm đồ vào túi.
- `Assets/Resources/Inventory/ItemCatalog.asset` là danh sách tham chiếu định nghĩa item dùng chung. Thêm định nghĩa thật vào catalog; giữ Stable ID khi đổi tên.
- Mỗi cửa hàng tiếp tục dùng `ShopCatalog`: giá, stock và điều kiện mở bán riêng. Item có trong catalog chung không tự động được bán.
- `Assets/Game/CreatureSystem/Runtime/DemoSceneOne/Loot.asset` chứa danh sách loot có trọng số. Hiện dùng Bình hồi máu/Táo hồi phục; không rơi thuốc MP. Vàng giữ 2–5 và xác suất item giữ 35%.
- `DemoSceneOneSaveIsolation` đặt save chơi thử tại `E:/UnityWork/CapstoneProjectData/PlayProfiles/DemoScene1`, slot `demoscene-1`. Đây là profile chơi riêng, không phải file QA cần tự xóa.
- `InventoryDebugSeeder.seedOnStart` đã tắt trong scene mới. Không ghi đè slot `main`, không xóa đồ đang có của người dùng.

| Item hiện có | Stable ID | Trạng thái |
|---|---|---|
| Bình hồi máu | Potion | Hồi tối đa 100 HP; chỉ dùng cho thú còn sống |
| Táo hồi phục | healing-apple | Hồi tối đa 30 HP; chỉ dùng cho thú còn sống |
| Bình mana | mana-potion | Giữ để tương thích save; không còn tài nguyên MP trong combat |
| Dusk Stone | dusk-stone | Giữ điều kiện tiến hóa hiện có |
| Bóng thu phục cơ bản | capture-ball-basic-01 | Bóng x1 đã nối gameplay |

Thuốc HP nhiều cấp, hồi sinh, giải debuff, buff item và EXP item mới cần duyệt dữ liệu trước khi tạo. EXP skill chưa có hệ nâng cấp hoàn chỉnh. Hai bóng Khá/Tốt vẫn thiếu cấu hình item, không tự đặt hệ số/giá để mở bán. Không thêm trang bị, chế tạo, trồng cây hay hệ chiến đấu theo lượt.

## Quái và thu phục

- Encounter mở đầu cấp 5. Gắn `EncounterLevel` lên encounter khác để chọn một lần trong 3–7; trọng số 10/20/40/20/10%. Không gắn lên pet sở hữu.
- Scene mới dùng một variant dơi hoang có AI đánh thường tại vị trí encounter dơi cũ. Prefab gốc của Trung và scene gốc được giữ nguyên.
- Quái hoang có cấu hình bắt hợp lệ vẫn bắt được khi còn HP. Ở 0 HP, quái gục chờ bắt 15 giây, không nhận thêm sát thương. Hết hạn mới phát thưởng hạ gục.
- Choáng chờ bắt không tự tính thành trạng thái ngủ/tê liệt để nhân tỷ lệ bắt.
- Ném hợp lệ tiêu hao đúng một bóng. Kết quả được tính một lần; animation không tính lại.
- Bắt thành công giữ `max(HP lúc bắt, 25% Max HP)`. HP này được lưu cùng giao dịch. Lưu lỗi hoàn tác HP, bóng và vị trí Party/BOX.
- AI enemy và hiệu ứng gục được tắt khi chuyển thành pet sở hữu; giữ nguyên cá thể, kỹ năng, genetics và cấp.

## EXP và animation

Chốt đội tại thời điểm hạ gục thật: kết liễu 100%, góp sát thương 75%, dự bị 50%; thú 0 HP không nhận EXP, BOX và cá thể vừa bắt không nhận thưởng encounter đó. Mỗi thú áp dụng hệ số chênh cấp hiện có riêng. Tổng EXP đội có thể lớn hơn một suất cơ sở. Tỷ lệ này là luật riêng của project.

Combat dùng cooldown, không trừ MP hoặc EXP khi dùng skill. Trường MP cũ vẫn đọc được để tương thích dữ liệu, nhưng runtime trả 0. Cung thứ hai trên HUB hiển thị EXP.

`CreatureAnimationProfile` ánh xạ `Hit -> Take Damage`, `Death -> Die`, `Downed -> pose gục`. Hit clip không cắt skill hoặc đòn đang cam kết. Hết phản ứng trả lại animation di chuyển. Pet sở hữu ngừng nhận lệnh khi HP bằng 0, phát Die rồi mới ẩn hình, có giới hạn an toàn 3 giây.

`DownedStatusVisual.effectPrefab` nhận prefab hiệu ứng trên đầu, dùng pool hiện có. Hiện gắn `DownedStars_CapstoneFallback.prefab`: đây là sao choáng tự tạo của project, không phải asset của Travis. Khi có Status Effects FREE, dùng variant URP phù hợp rồi thay tham chiếu; không đưa script demo vào gameplay.

## Kiểm tra và giới hạn

Menu `Tools > Capstone > PvE > 2. Verify DemoScene 1 with isolated saves` chạy kiểm có giới hạn 65 giây, save tạm riêng trên E; không ghi scene. Không chạy lại tự động sau khi khởi động Editor.

Kết quả thực chạy, file sửa, hash scene gốc, ảnh hữu ích và giới hạn được lưu tại `E:/UnityWork/CapstoneProjectData/Artifacts/pve-items-capture-demoscene1-2026-11-16/REPORT.md`. Không suy ra kết quả runtime từ mã test. Chưa import gói Status Effects FREE, chưa triển khai debuff chỉ vì có VFX. Standalone và Profiler chuyên sâu không được coi là đã kiểm.

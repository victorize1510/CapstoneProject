# Model, camera và phản hồi PvE — phạm vi cũ

Task này triển khai bản “Model mới, camera, thanh máu quái và phản hồi nhận đồ”. Không áp dụng kế hoạch combat/mana/loot mới được thảo luận sau đó.

## Trạng thái bàn giao hiện tại

**Cập nhật 12/11:** Đã áp dụng thành công prefab/model, icon Common và bàn trong DemoScene. `engine-checks.txt` ghi 56 kiểm engine đã qua, gồm ba LOD/bộ, shader import, camera near-plane, toast Shop/Quest (có kiểm save lỗi/rollback), summon/recall thật qua director, Game view 1920×1080 /1366×768 và bàn nằm trong viewport camera chơi. Scene trên đĩa khớp SHA256 bản kết quả trên E; so với bản bảo toàn chỉ thêm CameraVisibility và prefab bàn. Ảnh trước/sau là phép tắt/bật phần mới trong cùng build, không phải executable lịch sử. Chưa kiểm chuột/phím người dùng và standalone; ProfilerRecorder là toàn scene Editor, mẫu ngắn có nhiễu, không chứng minh tối ưu tốc độ. Những dòng bên dưới là lịch sử chặn của lượt 11/11, đã được xử lý ở cập nhật này.

- Đã xuất model/LOD bằng Blender và kiểm compile C# main + Editor thành công.
- **Chưa hoàn tất áp prefab vào Unity.** Lượt setup dừng ở LODGroup của bóng Common; lỗi kiểm component Unity đã sửa trong source, nhưng bản sửa chưa được chạy lại.
- Unity không còn tiến trình lúc kiểm ngày 11/11, log cuối lúc 20:20. Không xác định được là đóng thủ công hay crash; không tự khởi động lại.
- Chưa chạy QA Play Mode, chưa có ảnh runtime trước/sau, chưa đo Profiler hoặc standalone. Shader fade chưa được xác minh bằng engine.
- Bàn chưa được đặt xong vào scene; prefab bóng gameplay chưa được thay xong. Source generator là đường triển khai còn chờ thực thi, không phải bằng chứng đã áp dụng.
- Không có lệnh setup/QA tự chạy đang chờ. Khi tiếp tục: đợi Editor sẵn sàng → chạy mục 1 → kiểm lỗi import/shader → chạy mục 2.
- Scene gốc vốn có thay đổi chưa lưu. Không tự lưu đè hoặc hoàn tác các thay đổi đó; việc lưu scene kết quả cần được xử lý sau khi người dùng xem xét.

## Thành phần

- `Assets/Game/WorldFeedback/Art`: bốn bộ FBX tối ưu, ba LOD/bộ; texture và icon giữ màu nguồn.
- `Tools/PveModelFeedback/build_runtime_models.py`: quy trình Blender nền tái tạo asset; nguồn gốc ở `E:/Download/ModelItem` không bị sửa.
- Bóng Common tiếp tục dùng prefab gameplay `Assets/Game/PetSummon/Prefabs/BallGreenClose.prefab` để giữ GUID, physics, hand socket và callback. Tên file legacy không có nghĩa vẫn dùng hình bóng xanh.
- Rare/Ultra là prefab visual chuẩn bị trước, không tạo item/giá/luật bắt mới.
- Bàn sáu hốc là prop visual với collision đơn giản và NavMeshObstacle. Không có hồi phục hoặc tương tác giả.
- Camera dùng bộ giải hiện có, kiểm near-plane và overlap. `CameraVisibility` chỉ đổi material tạm trong lượt render camera chơi, khôi phục sau render; minimap và physics không bị đổi.
- Fade có adapter cho material URP Lit/Simple Lit/Unlit opaque/cutout. Shader tùy chỉnh hoặc transparent không được tự ghi đè; số renderer không hỗ trợ được báo trong QA.
- Thanh máu dùng pool 32 phần tử, HP thật, nhạt khi bình thường và rõ khi đang giao chiến/khóa/vừa bị đánh. Actor có thể thêm `EnemyHealthAnchor.head` để chỉnh vị trí chính xác.
- Receipt nhận item chỉ được phát sau mua thành công hoặc sau giao dịch thưởng Quest lưu thành công. Không suy từ inventory delta; tải save/rollback không phát nhận giả.
- `ItemAcquisitionFeedback.Publish(...)` là đầu nối cho pickup tương lai; task này không tạo loot/drop/pickup mới.

## Cách áp dụng

1. Tools → Capstone → World Feedback → **1. Apply models and presentation**.
2. Công cụ giữ các component gameplay trên bóng Common, cập nhật material/icon và tạo hai prefab bóng còn lại cùng bàn.
3. Nếu đang mở DemoScene, công cụ sao lưu cả scene chưa lưu ra E trước khi thêm bàn/camera. Nó giữ scene hiện tại ở trạng thái có thể Undo, **không tự lưu đè DemoScene gốc**; xuất bản scene-copy kết quả ra E để xem xét.
4. **2. Verify isolated runtime** chạy fixture và save thử trên E. Không dùng save thật. Chỉ chạy khi Editor hết import/compile.

Artifacts: `E:/UnityWork/CapstoneProjectData/Artifacts/pve-model-camera-feedback-2026-11-10/`.

## Kết quả asset đã chạy

Blender 5.0.1 đã xuất và render preview:

| Asset | Nguồn (tam giác) | LOD0 | LOD1 | LOD2 |
|---|---:|---:|---:|---:|
| Common | 166886 | 8000 | 2000 | 512 |
| Rare | 158416 | 8000 | 2000 | 512 |
| Ultra | 236136 | 8000 | 2000 | 512 |
| Bàn | 457800 | 40000 | 15000 | 4000 |

Con số thực tế cuối cùng và hash nguồn ở `model-build.json`; kiểm engine được ghi riêng trong `engine-checks.txt`/`qa-status.txt`. Render Blender không được tính là bằng chứng runtime Unity.

## Backlog PvE — chưa triển khai trong task này

| Ưu tiên | Hạng mục | Tình trạng |
|---|---|---|
| P0 | Enemy đi/đánh thường | Dơi đã có chase/attack/return; kiểm đường vòng/NavMesh, nhiều enemy và mốc hit animation còn cần mở rộng. Không có NavMesh thì fallback chỉ dừng trước vật cản. |
| P1 | Encounter | Cần spawn vùng, reset/leash, despawn/respawn độc lập với dummy auto-revive. |
| P1 | Loot | Chưa tạo bảng rơi, vật phẩm trên đất, hút/nhặt hoặc chống nhận trùng. Toast hiện tại không phải loot gameplay. |
| P1 | Thua trận | Cần chốt pet gục/hết đội, HP trainer, điểm hồi sinh. Enemy hiện nhắm pet được gọi. |
| P2 | Bóng và bàn | Rare/Ultra chưa có luật bắt; bàn chưa hồi phục. |
| P2 | Cân bằng | Tuning EXP/EV, kinh tế, rarity/drop theo loài. Không sửa công thức tám stats trong task này. |
| P3 | Boss | Telegraph, phase, arena/reset và thưởng một lần chưa nằm trong phạm vi. |

Các đề xuất sau (F8, gọi về bằng Backspace đơn/đôi, giữ MP thêm EXP, wind volley, gục 0 HP, loot tự hút) được giữ cho lượt sau; task cũ không âm thầm áp dụng chúng.

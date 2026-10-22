# Đồng bộ panel và item hồi phục

Ngày bàn giao: 14/10/2026. Unity 6000.3.16f1, TestChar.

## Giao diện

- Nền ngoài dùng màu ngà của NHIỆM VỤ: `#f8f4eb`; khung nội dung dùng trắng `#ffffff`. Hai hệ UI dùng cùng `MenuPalette`/`MenuTheme.uss`.
- Tiêu đề bắt đầu tại x=66, biểu tượng 24px; giữ font LiberationSans và nút Trở lại dùng chung.
- Kho thú bỏ dòng kho trống; thông báo không tìm thấy khi lọc vẫn được giữ.
- Nhiệm vụ rỗng ẩn toàn bộ ScrollView chi tiết, để vùng trắng phủ hết cột.
- PETS giữ 8 chỉ số trong một cột, thanh chỉ số nằm bên phải. Các ô đội hình cao thêm khoảng 20%; khung cha tự tính chiều cao nên không che dòng HP hoặc hướng dẫn.
- Nội dung PETS cuộn khi thiếu chiều cao, ẩn thanh cuộn. Ba nút thao tác nằm ngoài vùng cuộn, luôn nhìn thấy.
- Preview PETS căn theo đỉnh mesh đã chiếu vào camera, giữ đúng tỷ lệ viewport. Tái dùng RenderTexture/mesh/buffer; chỉ cập nhật pose 10 lần/giây, không gắn gameplay lên bản xem trước.
- HỒ SƠ: 6 ô chỉ có ảnh đầy ô, không tên, số ô hay chỉ số. Dùng EnvelopeParent và RectMask2D để cắt ảnh theo tỷ lệ mà không kéo méo.
- Túi đồ có nền lưới trắng; số vàng và phần tiến độ Cẩm nang được chỉnh tương phản. Trạng thái chọn/khóa và các thanh HP/EXP vẫn giữ màu chức năng.

## Dữ liệu được phép thêm

| ID | Tên | Hiệu ứng | Số lượng đã bổ sung |
|---|---|---|---|
| `healing-apple` | Táo hồi phục | Tối đa 30 HP | 5 |
| `Potion` | Bình hồi máu | Tối đa 100 HP | 5 |
| `mana-potion` | Bình mana | Tối đa 30 MP | 5 |

Giữ GUID/ID Potion cũ cho phần thưởng/save. Dusk Stone giữ nguyên, số lượng hiện tại **97**. Không tự đặt giá Shop.

Ba form dragon đã có mã trong DragonEvolutionCatalog: **01 Dusk / 02 Nightfall / 03 Darkness**. Giữ các mã hiện hữu, không đổi ID instance hoặc khóa save.

Các definition được giữ trong `Assets/Resources/Inventory/RecoveryItems.asset` để save nạp lại đúng hiệu ứng. Không thêm schema save. Trái cây và hai bình đi qua cùng luồng hồi phục từ Túi đồ/phím tắt R/T.

Khi HP/MP đầy hoặc thú không dùng MP, item không bị tiêu hao. Khi lưu thất bại, chỉ số và item được hoàn tác. Công cụ bổ sung item đọc save hiện có trước khi thêm và không đặt lại Túi đồ.

## Kiểm tra đã chạy

- **124 PASS** Play Mode: 7 panel UI Toolkit ở viewport thiết kế 1920×1080 và 1366×768; màu, tiêu đề, Quest rỗng, BOX, PETS không chồng vùng, preview đủ thân/cánh. Kiểm tra HỒ SƠ, MAP, Trainer Menu và mở đúng hộp hồi phục cho cả 3 item.
- **58 PASS** fixture độc lập: lượng hồi đúng cấu hình, giới hạn tối đa, không tác động resource khác, chống nhấn lặp, không tiêu hao khi đầy/không có MP, hoàn tác lỗi lưu, giữ hiệu ứng khi nạp lại inventory. Fixture dùng save riêng trong thư mục tạm.
- **176 PASS** kiểm tra asset; kiểm tra UXML/USS và binding cũ còn nguyên.
- Unity import/compile và `dotnet build Capstone/Assembly-CSharp-Editor.csproj --no-restore --verbosity minimal -m:1`: **0 lỗi**. Lần build đầy đủ có 187 cảnh báo, gồm dependency assembly và API cũ của package.
- Đã xem ảnh chụp các màn trong thư mục `screenshots` và sửa lỗi che đáy ô PETS phát hiện trong ảnh.

Log: `runtime-validation.txt`, `recovery-validation.txt`, `sample-delivery.txt`, `build.log`.

## Giới hạn còn lại

- Dragon hiện có Max MP = 0: bình mana có hiệu ứng thật nhưng bị khóa với thông báo “Thú này chưa sử dụng MP.” Không tự thêm chỉ số MP.
- Provider Cẩm nang, giá Shop, thống kê/nguồn dữ liệu còn hoãn vẫn để nhóm chốt.
- Chưa chạy lại bản PC hoặc QA màn siêu rộng trong lượt này. Các kiểm tra kích thước trên dùng viewport thiết kế UI Toolkit trong Editor.
- Console vẫn có cảnh báo handshake/network từ Unity MCP. Không thay package trong lượt này.
- Không chỉnh SampleScene, Spawn/Recall hoặc dữ liệu gameplay ngoài phạm vi item được duyệt.

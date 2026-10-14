# R/T, Throw và ghim mục tiêu — 14/10/2026

## Đã sửa

### Bổ sung sau phản hồi popup và chuyển mục tiêu

- Sửa lỗi ở callback nút thật: `equipSlotActionType` giờ cập nhật cùng nhãn nút, nên “Gán phím tắt” gửi `AssignQuickSlot` thay vì `EquipItem`. Bài kiểm tra trước gọi thẳng `ShowAssignment` đã bỏ sót lỗi này; bài mới đi qua callback nút Túi đồ và nút T trong popup.
- Bỏ nhánh HUD tự chọn item hồi phục đầu tiên cho R khi binding trống. Chỉ hiển thị item đã gán; thay được item trong cả R và T, giữ nguyên ô còn lại.
- TestChar FreeLook chọn và tự tìm lại mục tiêu theo **Boss → đối thủ gần player nhất**, trong bán kính 35 m. Khi mục tiêu chết, tắt hoặc bị destroy, giữ ý định ghim và chuyển sang mục tiêu hợp lệ; hết mục tiêu thì bỏ ghim. M3 bỏ ghim chủ động không bị tự ghim lại.
- Boss được xác định bằng `DummyEnemy.isBoss` hoặc marker `Boss` có sẵn. Không tự đổi đối thủ thường trong scene thành Boss. Giữ thứ tự con lăn đã sửa và giữ nguyên quy tắc click mới ra lệnh đánh.

- Túi đồ mở hộp chọn **R / T** khi gán vật phẩm hồi phục. Nhận chuột hoặc phím; Esc hủy hộp trước khi đóng Túi đồ. Nút thao tác báo phím đã gán. Trái cây hồi phục cũng có thao tác gán.
- Icon HUD giữ màu trắng/alpha 1 và lớn hơn trong ô. Vật phẩm chưa dùng được vẫn khóa thao tác; hình vật phẩm giữ rõ. Khi đang chọn phím, R/T không đồng thời dùng item.
- Gán phím không tiêu hao vật phẩm. Lỗi lưu hoàn tác binding và hiện thông báo. Dùng khóa chống lặp hiện có.
- Click thường không chạy Throw riêng của player. API Throw do sequence gọi pet vẫn hoạt động.
- Con lăn khi ghim đi qua danh sách mọi đối thủ sống, đang hoạt động trong 35 m quanh player. Thứ tự ban đầu theo góc quanh player, giữ ổn định khi camera/đối thủ di chuyển, quay vòng hai chiều. Đối thủ mới được thêm; chết, tắt hoặc ra ngoài tầm thì loại.
- FreeLook giữ camera lệch vai phải để player ở bên trái; không đổi vai theo chuyển động ngang. Giữ damping Cinemachine đang cấu hình. Setup TestChar cũng đặt mặc định này.
- Ghim và đổi mục tiêu chỉ thay lựa chọn. Click/skill mới gửi lệnh chiến đấu đến mục tiêu đang ghim. Lệnh đánh đã gửi cho mục tiêu cũ tiếp tục cho tới lệnh mới theo logic pet hiện có; đổi ghim không cắt animation chiến đấu.

## Quy tắc thao tác

| Thao tác | Hành vi |
|---|---|
| M3 | TestChar: ghim theo Boss rồi khoảng cách; bấm lại bỏ ghim |
| Con lăn khi ghim | Mục tiêu kế/trước, quay vòng toàn bộ mục tiêu hợp lệ |
| Click trái | Ra lệnh pet đánh mục tiêu đang ghim |
| Đổi ghim | Camera/marker theo lựa chọn mới, chưa ra lệnh đánh mới |
| Mục tiêu chết/mất | Tìm Boss trong tầm trước, rồi đối thủ gần player nhất; chưa ra lệnh đánh mới |
| R/T trong hộp gán | Gán item, không dùng item |
| R/T khi chơi | Dùng item theo điều kiện và cooldown hiện có |

Danh sách chuyển không lọc theo vị trí ngang trên màn hình: các đối thủ cùng đường ngắm vẫn chuyển được. Thuật toán chọn cũ vẫn dành cho camera legacy; quy tắc Boss/khoảng cách áp dụng cho FreeLook TestChar.

## Kiểm tra

- `fixture-validation.txt`: **41 PASS** — thêm tự tìm lại Boss/enemy khi chết hoặc destroy, thoát ghim đúng lúc, thay binding R/T, callback nút Túi đồ và click nút T thật trên dữ liệu riêng, ô chưa gán không tự chọn item.
- `play-validation.txt`: **30 PASS** — chạy fixture mục tiêu trong Play Mode; chọn item và gọi popup qua callback nút Túi đồ thật, thử item khác, Esc và alpha icon. Không gán hoặc tiêu hao item trong save thật qua bài kiểm tra này.
- `assignment.png`: ảnh popup 1920×1080; đã sửa lỗi nền nút mặc định tối và xác nhận lại chữ R/T rõ.
- Kiểm tra cấu trúc UXML/USS và tham chiếu asset: PASS.
- Build `Assembly-CSharp-Editor.csproj`: **0 lỗi, 193 cảnh báo**. Kết quả kiểm tra cuối không có FAIL. Unity MCP vẫn có cảnh báo handshake đã có sẵn.
- Kiểm tra camera bằng fixture xác nhận dấu offset và không đổi vai. Chưa kiểm tra thủ công toàn bộ góc nhìn/địa hình chiến đấu; damping dùng cấu hình hiện có.

Không đổi schema save, dữ liệu gameplay hoặc SampleScene. Không chạy lại setup để ghi đè scene đang mở. TestChar đang ở Edit Mode sau khi kiểm tra.

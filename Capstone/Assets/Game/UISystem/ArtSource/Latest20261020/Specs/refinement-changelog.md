# Tinh chỉnh mặt nền và nhãn chỉ số — 19/10/2026

- Nút hành động và điều hướng: thêm nét khắc hai đầu, cung la bàn, dải vật liệu và mặt vát nhẹ; vùng giữa nhãn giảm họa tiết về 0.
- Dải nhãn dưới hai thẻ lớn có chất liệu và khắc chìm, thay phần fill màu phẳng.
- Tab đang chọn giữ liền với panel; thêm họa tiết ngay trên mặt tab, không tạo thêm đường viền ngang.
- Đầu popup có vật liệu và nét khắc riêng. Nền shell, các panel và ô trống có đường đồng mức, dấu khảo sát và nét khắc ở mép.
- Mảng nhỏ dưới 110px rộng hoặc dưới 40px cao giữ sạch để không nhiễu icon, hint Tab và badge trạng thái.
- Nhãn hiển thị thống nhất **CRIT RATE / CRIT DMG**. Chỉ sửa chữ trong concept và prompt, không đổi field, công thức hoặc dữ liệu.
- Đã cập nhật SVG, PNG alpha, trạng thái, motif rời, 8 mockup, 16 preview và prompt triển khai trong cùng gói.

Họa tiết mới được dựng bằng SVG để giữ mép, độ nghiêng và khả năng chỉnh sửa. Tái dùng chất liệu cùng 7 ảnh imagegen của bộ R2; lượt tinh chỉnh này không sinh thêm ảnh raster AI.

Đối chiếu trước/sau: Boards/05_Surface_Refinement.png. Ảnh trước tinh chỉnh nằm trong Reference/Before_Refinement_*.png. Không sửa Unity hoặc kiểm runtime/standalone/Profiler.

## Bổ sung 20/10/2026

Avatar chỉ còn khung chứa ảnh, không chức năng thay ảnh. Khung player có viền và nét khắc mới. Ba tab dùng một outline cùng panel, bỏ tab-inactive rời. Slot skill đổi sang vuông vát tám cạnh và có sáu trạng thái. Xem Specs/identity-tabs-skills.md và Boards/06_Avatar_Tab_Skill.png. Không sinh thêm ảnh AI, không sửa Unity.

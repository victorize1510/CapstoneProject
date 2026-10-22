# Avatar, cụm tab và skill — cập nhật 20/10/2026

## Khung thông tin player

Thẻ giữ vị trí (768,52), kích thước 776×116. Nền có nét khắc hồ sơ thám hiểm, đường liên kết sát mép, mặt vát nhẹ và la bàn chìm ở vùng phải. Khung kim loại bao trọn thẻ; chỉ xuất một lớp frame chịu co giãn theo đường bao.

Avatar tại (784,66), khung ngoài 88×88, vuông vát góc; cửa ảnh an toàn 64×64 tại (12,12) trong khung. Assets/Identity/avatar-frame.svg/png chỉ có viền, tâm trong suốt. avatar-aperture-mask là mask riêng cho ảnh từ provider có sẵn.

Avatar chỉ hiển thị, không phải nút. Không glyph đại diện, edit, picker, tooltip mời đổi ảnh, focus, hover hoặc sự kiện thay avatar ở Menu. Mockup để trống đúng yêu cầu; khi triển khai dùng ảnh player hiện có.

## Ba tab gắn chung vào inspector

Thay ba mặt tab nổi bằng một outline bao cả ba đỉnh tab và panel. Canvas cụm vẫn 864×620; x bắt đầu 16/296/576, rộng 272, bodyTop52. Tab chọn top3, chưa chọn top11; chân cả ba kéo tới y68 bên trong body. Phần bệ có vật liệu chung, không có khoảng hở dưới tab chưa chọn hoặc đường viền ngang xuyên chân tab chọn.

Nguồn chuẩn: Tools/geometry.cjs → unionPoly/tabCapPoly, Tools/identity-components.cjs → inspectorFace/tabDeck. Ba asset inspector-0/1/2 đã chứa đúng toàn bộ cụm. Không ghép lại bằng ba nút tab-inactive độc lập của bản cũ. Không chồng thêm frame chữ nhật hoặc stroke dưới chân tab.

Có họa tiết khắc ở hai đầu tab, dấu khảo sát và các đường liên kết nhẹ trong vùng nhận diện/viền inspector. Nội dung, số và kỹ năng vẫn có vùng đọc riêng. Chữ/icon không giảm alpha theo nền.

## Skill vuông vát tám cạnh

Giữ chiều rộng bằng chiều cao; vát bốn góc 45° tạo tám cạnh. Đây là vuông cắt góc, không phải bát giác đều, hình thoi hoặc hexagon. Mức cắt khoảng 13% kích thước, tối đa 10px; có một rim kim loại mảnh và hai nét khắc ngắn.

Icon, nền, frame, trạng thái và vùng tương tác dùng cùng polygon skill-oct. Sprite giữ màu/alpha gốc và được cắt trong mask; không bake icon vào frame. Sáu trạng thái ở States/skill-*.svg/png; normal/hover/pressed/focus/selected-focus/disabled. Focus là bracket theo contour, không border chữ nhật.

Giữ CRIT RATE / CRIT DMG; không đổi provider, chỉ số, luật kỹ năng hoặc gameplay. Gói này là tài liệu/ảnh thiết kế, chưa triển khai hoặc chạy Unity.

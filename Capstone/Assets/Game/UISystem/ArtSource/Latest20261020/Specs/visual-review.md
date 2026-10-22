# Rà soát trực quan — 19/10/2026

Phạm vi là ảnh concept và asset raster/vector trong gói này. Đây không phải ảnh chụp runtime Unity.

## Kết quả đã xem

- Menu và Tổng quan ở 1600×900: minh họa nút lớn hơn, nền cảnh riêng cho thẻ pet, phù hiệu có mảng đặc, chevron trong đầu vát. Không thêm pet chính thức vào vùng hero.
- Menu và Kỹ năng ở 1366×768: nhận ra chủ thể trong bốn nút phụ; đủ sáu slot, điều hướng và hành động chính. Preview chỉ là bản scale từ mockup, chưa chứng minh responsive trong engine.
- Ba tab trong gallery và bảng hình học: chân tab chọn nối vào inspector; không có đường ngang đi xuyên chân tab. Hai lớp viền tập trung bên phải/dưới, đầu rail thu nhỏ thay vì kết thúc cụt.
- Thông tin thú: đã đưa thanh cuộn vào trong để tách khỏi rail bên phải; thêm vị trí HP/EXP từ các trường đã có vào cụm nhận diện.
- Tăng cấp, Hồi phục và Đổi tên: footer theo nội dung, cặp nút cân nhau, thông báo ở vùng riêng. Giá trị và thanh minh họa không phải dữ liệu gameplay chính thức.
- Bảng vật liệu: họa tiết địa hình, la bàn và phù văn tập trung ở mép; vùng chữ và chỉ số ít trang trí hơn.
- Bảng icon/trạng thái: glyph lớn dùng relief thép/đồng, icon chỉ số 24px giản lược. Skill giữ ô vuông, sprite màu giữ màu nguồn.
- Bảng alpha: kiểm trực quan khung trên nền sáng, tối vừa và nhiều chi tiết ở các mức 100%, 125%, 150%; không thấy nền chữ nhật thừa tại góc trong các mẫu đã xem.
- Bảng dữ liệu biên: có thiếu portrait/dữ liệu, tên dài, tên lỗi, HP đầy, thiếu vật phẩm và mẫu BOX hai dấu chân trong nền tròn gốc.

## Các tệp đã mở để xem

GALLERY.png; Screens/01_Menu.png; Screens/02_Tong_Quan.png; Screens/05_Thong_Tin_Thu.png; Screens/06_Tang_Cap.png; Screens/07_Hoi_Phuc.png; Screens/08_Doi_Ten_Thu.png; Preview/1366/01_Menu.png; Preview/1366/03_Ky_Nang.png; Boards/01_Material_Art.png; Boards/02_Icons_States.png; Boards/03_Geometry_Alpha.png; Boards/04_EdgeCases.png.

## Giới hạn bàn giao

Ảnh hero 2D và avatar player chính thức cần lấy từ người dùng/provider hiện có. Cảnh nền thế giới chỉ phục vụ mockup, không phải chỉ dẫn thay camera/game world bằng wallpaper. Raster minh họa ở mức concept. Runtime, standalone, Profiler và đối chiếu pixel trong Unity chưa thực hiện.


## Rà lại sau tinh chỉnh mặt nền

Đã mở Boards/05_Surface_Refinement.png để đối chiếu trước/sau, cùng Preview/1366/05_Thong_Tin_Thu.png, Preview/1366/08_Doi_Ten_Thu.png và Boards/02_Icons_States.png. Nét khắc hiện rõ hơn trên hai đầu nút và header, các nhãn CRIT RATE/CRIT DMG còn đủ khoảng cách với thanh chỉ số ở bản thu nhỏ. Badge nhỏ/hint và icon skill giữ sạch. Không thấy nền alpha thừa trong bộ kiểm hình học; các bản này vẫn là mockup, không phải runtime.

## Rà lại 20/10/2026 — avatar, tab và skill

Đã mở hai pilot, Boards/06_Avatar_Tab_Skill.png và Preview/1366/03_Ky_Nang.png. Avatar thay glyph thành cửa rỗng, frame không tràn vào aperture sau khi cắt mask. Cả ba tab có chung outline và chân kéo xuống body; đã xem đủ ba vị trí chọn trong board. Skill giữ dáng vuông, bốn góc cắt nhỏ, giảm viền và bỏ các cụm sọc lặp trên slot. Ở bản1366 đủ sáu pet và CTA; tiêu đề/nút không bị che. Kiểm alpha và đường nối trong QA là kiểm thiết kế, chưa chạy Unity.

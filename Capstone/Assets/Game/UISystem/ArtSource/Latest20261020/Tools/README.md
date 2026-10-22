# Tái xuất gói tổng hợp

Các lệnh dưới đây chỉ sửa gói thiết kế, không sửa Unity.

- Xuất lại đầy đủ: node Tools/build-r2.cjs
- Chỉnh một màn và giữ đủ manifest: node Tools/build-r2.cjs --only=02_Tong_Quan
- Chỉ dựng gallery: node -e "const H=require('./Tools/base-layout.cjs');require('./Tools/build-gallery.cjs')(H)"
- Kiểm thiết kế và lập inventory: node Tools/verify-r2.cjs
- Đóng ZIP và kiểm CRC/SHA256: python Tools/zip.py

Cần Node.js + sharp để render và Python để đóng ZIP. Các PNG đã xuất có sẵn; không cần các runtime này chỉ để xem/giao asset.

Nguồn hình học: geometry.cjs; frame: integrated-frame.cjs; avatar/tab/skill: identity-components.cjs; motif: panel-motifs.cjs; glyph: game-icons.cjs; layout: base-layout.cjs. Font dùng Cambria/Noto Serif và Segoe UI/Arial.

Prompts và tài liệu chốt được giữ trong document-templates.json, tái xuất qua write-r2-docs.cjs. Nếu sửa tài liệu, cập nhật template tương ứng. Không chạy bootstrap cũ hoặc --pilot trên gói đã tổng hợp.

Reference là lịch sử. Boards có thể chứa ảnh trước/sau có nhãn rõ. Screens và Preview là bản hiện hành. Không import Tools/Reference vào Unity như script hoặc sprite runtime.

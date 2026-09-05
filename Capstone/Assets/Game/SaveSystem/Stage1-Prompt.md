# Đợt 1: Củng cố nền dữ liệu và Save hiện có

Thực hiện tại `D:\Download\ProjectVTC\CapstoneProject\Capstone`.

## Mục tiêu
- Khảo sát rồi mở rộng SaveSystem hiện có (PlayerSaveController, PlayerSaveData v5, PlayerJsonFileSaveStore và save Quest cũ), không tạo hệ thống song song.
- Hoàn thiện save/load Quest, Inventory, Party, Box, pet level/stats, skill loadout, nickname và favorite.
- Giữ ID hợp lệ. Phân biệt ID định nghĩa với ID từng cá thể pet. Bổ sung ID skill/item và alias ID cũ; phát hiện ID trùng thay vì đoán dữ liệu.
- Giữ nguyên dữ liệu Profile, Achievement, tiền, tiến hóa và release đã có.

## Phạm vi
- Chỉ sửa nền dữ liệu, persistence và kết nối cần thiết. Không thêm gameplay Pet, Quest reward, World Map hay dọn UI.
- Không sửa code Trung hoặc package gốc. Không sửa scene, prefab, animation, sprite trừ khi có bằng chứng bắt buộc.
- Không xóa prototype đang sử dụng hoặc thay đổi giá trị cân bằng.
- Kiểm tra Git diff và lưu bản đối chiếu trước sửa; giữ mọi thay đổi không liên quan.
- Chia thành nhóm: file save/migration; điều phối load/save; ID và phục hồi dữ liệu; kiểm thử. Compile sau mỗi nhóm.

## An toàn dữ liệu
- Phân biệt chưa có save, save lỗi, load thiếu dữ liệu và phiên bản mới hơn chưa hỗ trợ.
- Chặn autosave/quit-save ghi mặc định đè file lỗi hoặc save chưa được load. Chỉ ghi lại sau khi load thành công hoặc người dùng chủ động reset.
- Ghi qua file tạm và thay thế nguyên tử khi nền tảng hỗ trợ; giữ backup hợp lệ. Không lấy file hỏng ghi đè backup tốt.
- Khi đổi schema, tăng version và migration chạy lại an toàn; giữ bản cũ trước lần ghi đầu tiên sau migration.
- Load lặp không nhân đôi pet/item, cộng lại thưởng hoặc thay đổi thứ tự Party/skill.
- Khi thiếu catalog/định nghĩa, bảo toàn dữ liệu và báo rõ; không tự xóa khỏi save.
- Phân định quyền điều phối giữa save chung và Quest save cũ.
- Dừng giải thích nếu cần quyết định kiến trúc lớn ngoài phạm vi hoặc phải chấp nhận mất dữ liệu. Các sửa lỗi nhỏ tương thích tiếp tục thực hiện.

## Kiểm tra và bàn giao
- Test bằng thư mục/slot riêng, không thao tác save thật.
- Save -> load -> sửa dữ liệu -> save -> load lại; kiểm tra load lặp, thứ tự và ô trống, nickname/favorite, quest đã nhận thưởng.
- Test chưa có file, thiếu field, phiên bản cũ/mới, JSON hỏng, backup, thiếu/trùng ID và migration lặp.
- Compile runtime và Editor, sửa lỗi C# do thay đổi gây ra.
- Báo file sửa/tạo, schema, migration, kết quả test thực tế, giới hạn và dữ liệu catalog còn cần tác giả cấu hình.

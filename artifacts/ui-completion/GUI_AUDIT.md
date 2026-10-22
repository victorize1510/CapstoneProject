# Rà GUI, tối ưu và phần còn thiếu — 13/10/2026

## Đã sửa và giảm việc thừa

| Khu vực | Trước | Sau |
|---|---|---|
| Preview PETS | Bake 10 Hz nhưng camera render liên tục | URP render một lần sau mỗi bake, giữ texture giữa các lần; Play Mode đo 6 lượt render/650 ms |
| Preview PETS | Tìm metadata/mesh và gán background mỗi frame | Cache metadata và mesh tĩnh, dùng một inverse rotation cho mỗi lần bake; chỉ gán background khi đổi RenderTexture |
| MAP | Marker trong nhóm bị bật khi cập nhật vị trí, rồi tắt ở bước gom nhóm | Tách `IsVisible` (đủ điều kiện hiển thị) và trạng thái bị nhóm che; 100 cập nhật liên tiếp không bật lại marker đang nằm trong nhóm |
| MAP | Chuyển số nhóm thành chuỗi và đổi sibling order mỗi frame | Chỉ đổi nhãn khi số lượng thay đổi; sắp xếp sibling khi regroup hoặc khi popup chưa nằm trên cùng |
| MAP | Chọn marker bị gom có thể mất nhãn nếu lấy trạng thái GameObject làm trạng thái hiển thị | Nhãn chọn theo trạng thái logic; test chọn đúng marker trong nhóm vẫn hiện thông tin |
| Cẩm nang | Chuẩn hóa tên tiếng Việt trong từng phép so sánh sort | Chuẩn hóa mỗi tên một lần trong lượt query, tái dùng dictionary và comparison; dựng lại keys mỗi query để tránh stale data |
| USS/Việt hóa | Placeholder còn tiếng Anh; stylesheet tạm tách hai block cùng selector | Việt hóa fallback; gom block placeholder có các thuộc tính độc lập, giữ giá trị cũ |

Nguồn chính: `PetPortraitPreview.cs`, `PetPartyPanelController.cs`, `MapMarkerView.cs`, `MapMarkerClusters.cs`, `WorldMapUIController.cs`, `PetCodexQuery.cs`. Không đổi gameplay, save, SampleScene hay Spawn/Recall.

### Phép đo và kiểm tra

- Editor: **232 kiểm tra đạt**, gồm privacy, query, provider edits, focus, rebind, 4 kích thước màn hình và preview 3 dạng × 3 scale × 3 tư thế.
- Play Mode TestChar: **73 kiểm tra đạt**, gồm camera preview chỉ render theo pose cadence và nhãn chọn marker đã gom; đã chụp lại ảnh màn hình.
- UXML/USS/meta/binding: **145 kiểm tra đạt**.
- Build C#: **0 lỗi, 187 cảnh báo hiện có**. Cảnh báo package vẫn cần xử lý riêng trước release.
- [performance.txt](performance.txt): microbenchmark 320 mục, 20 lượt sắp xếp tên sau 8 lượt làm nóng: **645,82 ms → 46,52 ms** trên máy này. Đây là phép thử thuật toán, không phải tuyên bố tăng FPS toàn game.
- **Đính chính GC:** counter cấp phát .NET của Unity Mono trả 0 cả ở phép thử có `new byte[4096]`; số đo 0 byte trước đây không đáng tin. Kiểm tra tái dùng buffer bằng tham chiếu vẫn đạt. Muốn kết luận GC/FPS toàn GUI cần CPU/GPU Profiler và GC.Alloc trên build đại diện.

### USS còn nhiều block lặp: kết quả kiểm tra

BoxPanel có 26, InventoryPanel 10, PetsPanel 110 và QuestPanel 8 block bổ sung cùng selector. Không tìm thấy khai báo property/value giống hệt bị ghi đè trong các block đó. Phần lớn là các thuộc tính được bổ sung qua từng lần sửa bố cục, không phải cùng thuộc tính lặp lại.

Không gom các block đó tùy tiện: chuyển vị trí khai báo có thể đổi thứ tự cascade với selector khác. Công cụ `clean_repeated_uss.py` có backup và đối chiếu thuộc tính cuối; lượt này bỏ 0 khai báo ở 4 stylesheet chính. Không tính việc này là cải thiện hiệu năng.

## Những phần chưa hoàn thành

| Mục | Bằng chứng hiện tại | Đề xuất giải quyết |
|---|---|---|
| Cẩm nang trong game | Có giao diện và hợp đồng provider; chưa có provider runtime | Khi mở giai đoạn data: nối catalog thật, discovery/capture events và yêu thích theo loài; không dùng fixture Editor làm dữ liệu game |
| Cấp/EXP và một số thống kê Hồ sơ | `PlayerProfileRuntimeProvider` vẫn để nguồn Player Stats/metrics cho giai đoạn sau | Nối sự kiện của các hệ gameplay, giữ 0 khác với chưa biết; làm cùng đợt data |
| Cài đặt | `OpenSettingsFromGameplay`/nút bánh răng đang mở placeholder | Làm panel âm lượng, độ nhạy camera, scale UI, đồ họa và phím điều khiển; chỉ có nút cho tính năng thực sự được nối |
| Shop | `OpenShopFromWorld` đang mở placeholder | Để ở đợt riêng: GUI mua/bán và xác nhận trước, dữ liệu kinh tế sau |
| Trang bị/cho item | Controller có API nhưng chưa tìm thấy subscriber nghiệp vụ cho Equip/Give trong các thư mục game đang rà; UI trang bị hiện chưa bật nút equip | Khi hệ trang bị/sử dụng vật phẩm sẵn sàng mới nối; tiếp tục khóa thao tác chưa có handler |
| Feedback thao tác túi | `InventoryActionPickupFeedback` đang dùng chung PickingUp cho mọi action thành công | Thay bằng thông báo giao diện đúng hành động; chỉ chạy animation khi hành động trong thế giới cần nó. Kiểm tra thêm việc đổi Boy/Girl vì animator của feedback được cache |
| Kiểm tra release | Test chức năng đạt; chưa có GPU/GC benchmark toàn game, test tiêu hao/thả bằng fixture đầy đủ hoặc gamepad toàn bộ | Thêm các bài kiểm tra này trước bàn giao bản chơi chính thức; không thử tiêu hao trên save thật |

## GUI nên bổ sung theo thứ tự

1. **Thông báo ngay trong game:** dùng/thao tác thành công, không đủ điều kiện và lỗi phải hiện gần nơi thao tác hoặc toast. Lý do không nên chỉ nằm trong Console/tooltip. Không kéo animation player vào mọi thao tác UI.
2. **Panel Cài đặt thật:** ưu tiên âm lượng, độ nhạy camera và scale UI; có Áp dụng/Hủy và preview rõ. Đây là chỗ giúp giao diện dùng được trên màn nhỏ hoặc DPI khác nhau.
3. **Icon và font đồng bộ:** Trainer Menu đang dùng chữ viết tắt làm một số icon; MAP và các panel dùng nhiều kiểu khác nhau. Chọn một bộ icon trong project, kiểm tra dấu tiếng Việt và bỏ phụ thuộc vào ký tự lạ dễ thiếu glyph.
4. **Hướng dẫn ngắn về thao tác:** một trang phím tắt hoặc tooltip lấy nhãn từ binding thực tế. Nêu rõ gọi/thu, ghim mục tiêu, đổi pet, kéo thả và double-click; tránh nhãn phím bị lệch khi đổi binding.
5. **Preview pet có điều khiển tùy chọn:** xoay/zoom vừa đủ để xem model, nút trả góc mặc định. Chỉ cập nhật khi panel mở hoặc người dùng thao tác; không mặc định render full FPS.
6. **Polish nhẹ:** hiệu ứng hover/focus/selected thống nhất, chuyển panel ngắn, hỗ trợ giảm chuyển động. Ưu tiên trạng thái rõ và phản hồi trước khi thêm nhiều hiệu ứng.

## Tổ chức code nên làm ở lượt tiếp theo

- Tách dựng Trainer Menu khỏi `GameMenuController` đang khoảng 1.400 dòng; giữ router chịu trách nhiệm màn/input ownership, view chịu trách nhiệm element và binding. Không chia partial chỉ để giảm số dòng nhìn thấy.
- Cẩm nang giữ query thuần dữ liệu, renderer tái dùng element và provider theo hợp đồng hiện có. Có thể tách renderer chi tiết khi thêm data thật; chưa cần tạo abstraction cho mọi Label/Button.
- Chuẩn hóa token màu/font/spacing giữa uGUI và UI Toolkit từng nhóm có ảnh so sánh, giữ palette hiện tại. Với USS, chuyển từng nhóm selector về một nguồn sở hữu rồi kiểm tra cascade; không xóa override hàng loạt.
- Chỉ bổ sung pooling/virtualization BAG/BOX khi Profiler và số lượng dữ liệu thực tế chứng minh cần; không tăng độ phức tạp khi danh sách nhỏ.

Ưu tiên đề nghị: **feedback/toast → Cài đặt → icon/font và hướng dẫn phím**. Provider Cẩm nang, thống kê và các hệ item tiếp tục thuộc phần data/gameplay đã hoãn.

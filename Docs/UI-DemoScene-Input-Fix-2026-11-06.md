# UI không nhận click khi chuyển sang DemoScene

Ngày 06/11/2026. Phạm vi: lỗi tương tác khi dùng bộ UI hiện có trong DemoScene, không thiết kế lại panel hoặc đổi gameplay/save.

## Nguyên nhân đã tái hiện

DemoScene có hai object EventSystem đang bật:

- `EventSystem` tại root scene, GameObject local ID `611010340`.
- `CapstoneGameplay/03_UI/EventSystem`, đi cùng bộ UI được chuyển từ TestChar.

Trong lượt Play Mode trước sửa, `EventSystem.current` là object tại root, có `InputSystemUIInputModule` đang chạy. Nhưng `PanelEventHandler` của UI Toolkit lại nhận `eventSystem` là object trong CapstoneGameplay; object đó chưa phải current nên `currentInputModule` bằng null.

Code uGUI của phiên bản project đang dùng kiểm `eventSystem.currentInputModule` trong `PanelEventHandler.ReadPointerData`. Khi null, pointer event bị bỏ qua. Vì vậy các nút vẫn được vẽ và hit-test chọn đúng vùng, nhưng callback không chạy. Menu/Hồ sơ uGUI có thể vẫn hoạt động trong khi Pet/BAG/Quest/Codex/BOX/Shop/Settings UI Toolkit không nhận thao tác.

Đây là lỗi kết nối input cấp scene; không phải cần gắn callback riêng vào từng chữ hoặc tăng kích thước button.

## Sửa đã áp dụng

Giữ `CapstoneGameplay/03_UI/EventSystem` làm hệ thống input UI duy nhất. Tắt GameObject EventSystem dư ở root; không xóa object, không đổi binding input, controller, API hoặc thiết kế UI.

Thay đổi nguồn trong `Capstone/Assets/Scenes/DemoScene.unity` chỉ là **m_IsActive: 1 → 0** trên object `611010340`. Sau khi Unity SaveScene, đã loại bỏ các thay đổi serialize default/whitespace ngoài phạm vi và giữ nguyên line ending; đối chiếu với bản sao trước task chỉ khác đúng một byte. Các thay đổi sẵn có trong scene được bảo toàn.

Trước khi lưu, đã thử tắt duplicate trong runtime tạm: EventSystem.current và EventSystem của UI Toolkit trở về cùng object, module input có hiệu lực; click Pet đổi tab, BAG đổi chế độ và Back được gọi đúng một lần ở cả 1366×768 và 1920×1080. BAG Back chuyển về Gameplay thay vì đứng nguyên ở Inventory.

## Quy tắc chuyển bộ UI sang scene khác

1. Mỗi scene gameplay chỉ có **một EventSystem đang hoạt động**, dùng `InputSystemUIInputModule` cùng action references đang có.
2. Nếu scene đích đã có EventSystem và bộ CapstoneGameplay mang theo một cái nữa, giữ một hệ thống chung và tắt cái dư; không để hai hệ thống cùng hoạt động rồi chỉ chọn bằng EventSystem.current.
3. Giữ Menu/Profile/Map uGUI và Pet/BAG/Quest/Codex/BOX/Shop/Settings UI Toolkit dùng cùng hệ input. Không tạo EventSystem riêng cho từng panel/popup.
4. Giữ UIDocument, PanelSettings, controller/provider và reference hiện hành. Đừng thêm OnClick lên từng Label để chữa lỗi bridge input.
5. Kiểm khi Play: EventSystem.current, module đang hoạt động và EventSystem mà PanelEventHandler sử dụng phải đồng nhất. Sau đóng UI, router về Gameplay, blocker được thả và cursor được phục hồi theo luồng hiện có.
6. Scene cộng dồn/additive cũng phải kiểm tổng số EventSystem đang active; không chỉ đếm trong từng file scene. Không tự tắt hệ thống của scene khác bằng một script quét mỗi frame.

## Bằng chứng và giới hạn

QA dùng DemoScene thật và save thử riêng trên E. Có báo cáo trước sửa, thử một EventSystem trong runtime và lượt kiểm sau khi lưu/nạp lại scene. Kiểm bằng engine raycast, panel picking và pointer-event handlers, tách khỏi thao tác chuột/bàn phím vật lý.

Lượt cuối kiểm **38 trạng thái** ở 1366×768 và 1920×1080: Menu, Pet ba tab/bốn popup, BAG grid/list, Quest, Codex, BOX, Shop, Settings, Profile/hai popup và Map. Có **43 click-event quan sát được, mỗi lần đúng một callback**; trạng thái ba tab lần lượt 0/1/2 đúng sau click, BAG Back đóng về Gameplay, popup Profile mở và đóng theo modal routing. Đóng hết UI trả về Gameplay và `GameplayInputBlocked=false`.

Các raycast vào nền phía sau modal bị chặn là có chủ đích, không phải nút của popup bị hỏng. Còn một điểm chồng marker trong scene thử: marker khác ở đúng tâm marker người chơi trên Map nên nhận hit trước marker người chơi. Chưa đổi chính sách ưu tiên marker trong lượt sửa input này.

Đã quan sát cửa sổ Unity ở Edit Mode; UI preview trong Edit Mode không được coi là kiểm tương tác runtime. Không thực hiện click game bằng chuột/bàn phím vật lý. Probe và request tự chạy của task được gỡ sau kiểm; không để recorder hoặc chương trình QA chạy nền.

Kết quả cuối và danh sách kiểm thực chạy tại:
`E:/UnityWork/CapstoneProjectData/Artifacts/demo-ui-input-audit-2026-11-06/REPORT.md`.

Không dùng mockup làm bằng chứng runtime. Không chạy standalone/Profiler trong lượt này; không tuyên bố đã kiểm toàn bộ mua bán, tiêu vật phẩm, combat hoặc giao dịch save chỉ từ việc click/Back hoạt động. Thiết kế/skin UI để làm sau.

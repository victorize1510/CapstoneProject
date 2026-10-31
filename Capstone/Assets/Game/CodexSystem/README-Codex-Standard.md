# Cẩm nang — chuẩn Companion, 26/10/2026

Màn dùng UI Toolkit, giữ `IPetCodexProvider`, `PetCodexQuery` và các callback hiện có. `CodexPresentation` chỉ bố trí view; `CodexSurface` dùng cùng contour manifest để vẽ nền, ảnh, viền và hit-test. Header dùng `UnifiedPanelHeader` chung với Pet/Bag/Quest/Profile, trên canvas logical 1600×900.

Gói nguồn: `E:/AIWork/Capstone/Codex_Companion_Standard_2026-10-26`. Các PNG component trong gói là placeholder 1 byte nên không được nhập vào Unity. Khung được dựng từ polygon trong manifest; texture vật liệu và glyph hợp lệ được nhập riêng. Bản SVG đối chiếu nằm ở `Documentation/Codex20261026-SvgTrace/` ngoài Unity Assets. Không dùng ảnh mockup làm UI.

## Dữ liệu

Từ 29/10 có `PetCodexCatalog` và `PetCodexRuntimeProvider` cho ba mục được yêu cầu: 01 Dragon Dusk, 02 Dragon Darkness, 03 Dragon Nightfall. Catalog tham chiếu prefab thật và được tải từ `Resources/PetCodex/PetCodexCatalog.asset`; số Cẩm nang không thay FormId/thứ tự tiến hóa. Controller tự nối provider mặc định trong Play khi không có provider được chỉ định. Thiếu catalog/provider vẫn hiện màn rỗng và khóa controls như trước.

Trạng thái được đọc từ thú thực sự thuộc đội/Kho; danh mục loài độc lập với danh sách sở hữu. Chưa có lịch sử khám phá hay favorite theo loài lưu riêng, không đổi save schema. Loài không thuộc dữ liệu sở hữu hiện tại giữ unknown. Provider không phát sinh fixture hoặc số liệu minh họa; trường chưa có dữ liệu giữ trống. Hướng dẫn nhanh ở `Assets/Game/Skills/README_QUICK_VI.md`.

Khi có adapter thật, gọi `SetProvider` với snapshot của catalog đó. Favorite được gửi qua `CanSetFavorite`/`TrySetFavorite` của provider, độc lập với favorite từng cá thể. Unknown chỉ lộ số thứ tự và bóng mờ/dấu hỏi. `Icon` cho thẻ, `Portrait` cho chi tiết; chỉ fallback Icon khi thiếu Portrait.

`Editor/PetCodexPreviewWindow` là fixture chỉ chạy trong Editor, không phải dữ liệu game. Có thể mở qua **Game Tools → GameToolThang → HUD → Preview Pet Codex**.

## Tùy chọn bốn ô kỹ năng HUD

1. Trong scene TestChar, chọn GameObject **GameplayHUD**, có component **Gameplay Hud Controller** (component này cũng có trong prefab CapstoneGameplay).
2. Ở **Behaviour → Hiển thị 4 ô kỹ năng**, chọn:
   - **Luôn hiện 4 ô**: hiện cả khi chưa gọi thú; điều kiện cho phép dùng kỹ năng vẫn giữ nguyên.
   - **Chỉ khi thú được triệu hồi**: ẩn khi chưa gọi/đang xuất hiện; hiện sau khi triệu hồi hoàn tất; thu thú thì ẩn.
3. Chỉnh ngoài Play Mode để lưu lựa chọn vào scene/prefab. Chỉnh trong Play Mode chỉ để thử.

Mặc định vẫn là luôn hiện, tương thích scene/prefab cũ. Không thay keybind Z/X/C/V, điều kiện dùng kỹ năng, API hay save.

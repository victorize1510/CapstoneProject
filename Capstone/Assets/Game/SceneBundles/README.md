# Copy gameplay sang scene khác

Prefab: `Assets/Game/SceneBundles/CapstoneGameplay.prefab`.

Trong TestChar, có thể copy nguyên object **CapstoneGameplay** trên Hierarchy. Prefab và object này chứa:

- `01_Player`: Player và hai bộ hình nhân vật hiện có.
- `02_Pets`: thú gốc và các model dạng tiến hóa.
- `03_UI`: Canvas, InventoryUI, HUD, Menu/Profile, Quest Tracker, EventSystem.
- `04_Cameras`: Main Camera, camera bản đồ/minimap và TestChar Camera Rig.
- `05_Systems`: Quest/save, bản đồ, Map Icon và điều khiển tiến hóa.

Giữ root và các nhóm ở position 0, rotation 0, scale 1. Khi đổi điểm xuất phát, di chuyển **Player** trong `01_Player`; giữ các target/camera theo cơ chế follow đang có.

Scene đích cần môi trường, collider và NavMesh riêng. Các map, quầy shop và NPC trong TestChar được giữ ngoài prefab. Prefab đã chứa Player, Main Camera và EventSystem, nên khi dùng prefab hãy tránh giữ thêm bản trùng của các hệ này trong scene đích.

Trong Editor:

1. `Game Tools > Scene > Group Gameplay for Copy`: gom object gameplay hiện có, giữ transform thế giới và các tham chiếu.
2. `Game Tools > Scene > Export Gameplay Bundle Prefab`: xuất lại prefab từ nhóm đang có. Công cụ báo lỗi nếu nhóm còn tham chiếu tới object scene bên ngoài.

Các ID thú đã có được giữ nguyên. Những nhận diện actor cần thiết dùng chính Player/Pet, thay vì coi root chung là một actor.

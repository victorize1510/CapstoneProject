# Developer Cheat Panel · Beast of Fantasy

## Cách dùng

1. Trong Unity Editor, mở `DemoScene 3` hoặc `DemoScene 1 - Creature QA`, vào Play Mode và đợi gameplay nạp xong.
2. Bấm **F10** ngoài các màn hội thoại và menu. Bấm **F10** hoặc **Đóng** để trả quyền điều khiển. Panel có thanh cuộn và dùng giao diện đơn giản được dựng bằng code.
3. Nút cheat đầu tiên mở một phiên thử từ trạng thái hiện có. Game ghi giao dịch nhiệm vụ và tiến độ cheat vào bản save riêng trong RAM trong suốt phiên Play. Có thể chuyển cảnh rồi tiếp tục dùng phiên này. Stop Play sẽ xóa bản thử trong RAM.
4. **BOSS READY – CHEAT ALL** dùng đội hiện có, trả và nhận thưởng NV1–8, nhận NV9, đưa nhân vật và pet lên cấp 100, tối đa chỉ số pet, đặt vàng thành 9.999 và bật bỏ hồi chiêu. Sau khi tìm được vị trí có nền và đủ khoảng trống ở ngoài tầm vào trận, panel đóng. Nếu khởi chạy từ scene khác, bản Editor mở DemoScene 3 và nạp lại đội trước khi chạy các bước Chương I.
5. DemoScene 3 hiện chưa gán Boss trong `Chapter I World`. Trong Editor, Boss Ready dùng prefab **Detale** đang được sử dụng trong `DemoScene Boss` và đặt tại mốc đấu trường đã có của hang. Prefab được tạo cho phiên thử. Nếu thiếu NavMesh phù hợp hoặc giao dịch nhiệm vụ thất bại, panel báo bước lỗi và giữ nguyên vị trí của player.
6. **Reset cấp, chỉ số và tiện ích chiến đấu** khôi phục tiến trình cấp và chỉ số đã lưu trước lúc áp cheat cho các đối tượng còn tồn tại, đồng thời tắt các toggle. Tiến độ nhiệm vụ và tiền của phiên thử giữ lại. **Khôi phục toàn bộ dữ liệu lúc bắt đầu phiên thử** nạp lại snapshot ban đầu của phiên RAM.

## Chức năng

| Nhóm | Hoạt động |
| --- | --- |
| Nhân vật | Cấp 100 qua hồ sơ hiện tại. Nhân vật hiện có cấp và EXP. Các hệ thống chiến đấu hiện tại dùng HP, MP và chỉ số chiến đấu trên pet và địch, nên chỉ số tối đa và miễn sát thương áp dụng cho pet do player điều khiển. |
| Pet và quái | Cấp 100 cho pet đang chọn, cả đội hoặc mục tiêu sống được chọn có dữ liệu cấp. Chỉ số pet tối đa là HP/MP 9.999 và ATK/DEF/SP.ATK/SP.DEF 999. Khi dùng chỉ số tối đa, tốc độ lấy từ trước khi áp cheat. |
| Vàng | Đặt số dư thành 9.999 qua ví hiện tại. |
| Chiến đấu | Không hồi chiêu, không tốn Mana và miễn sát thương là ba toggle riêng. Hành động vẫn qua điều kiện tung chiêu và khóa animation. Hồi phục cả đội làm đầy HP/MP, giải trạng thái và hoàn tất giai đoạn thu hồi của pet gục. Boss Ready tự bật bỏ hồi chiêu. Hai toggle còn lại mặc định tắt. |
| Nhiệm vụ | Mỗi nút NV1–8 hoàn thành theo thứ tự tiên quyết đến số đã chọn. Panel mô phỏng mục tiêu giao vật phẩm hoặc giao pet trong bản thử, rồi dùng bước trả nhiệm vụ, phát sự kiện, nhận thưởng và ghi biên nhận thật. Quest đã nhận thưởng được bỏ qua khi chạy lại. |
| Dịch chuyển | Dùng transform hoặc zone đã gắn trong scene. Đích được kiểm tra lại bằng NavMesh, raycast xuống nền và kích thước Character Controller của player. |

## Nhiệm vụ và điểm đến

Trưởng làng (`npc_village_elder`) giao và nhận nhiệm vụ Chương I. NV1 mở sau hướng dẫn. NV2–9 nối theo nhiệm vụ trước. NV9 được nhận để mở trận Boss. Hai NPC dẫn đường hiện có phụ trách vào và ra hang.

| ID | Tên |
| --- | --- |
| `main_quest_01` | Rắc rối nơi đồng cỏ |
| `main_quest_02` | Dấu chân bí ẩn |
| `main_quest_03` | Bất ổn ngày và đêm |
| `main_quest_04` | Manh mối từ Rắn Lửa |
| `main_quest_05` | Mở đường vào bóng tối |
| `main_quest_06` | Nghiên cứu Rồng Chạng Vạng |
| `main_quest_07` | Những quả trứng bị nguyền |
| `main_quest_08` | Tiếng gọi từ bóng tối |
| `main_quest_09` | Cội nguồn hỗn loạn |

| Điểm | Tham chiếu trong DemoScene 3 |
| --- | --- |
| Làng | `ChapterWorldLayout.grassElder`, khu vực Trưởng làng |
| Đồng cỏ dễ, vừa, khó | Box của `Zone_Grass_Easy`, `Zone_Grass_Mid` và `Zone_Grass_Hard` |
| Cửa hang | `cavePortal`, khoảng X −6,90, Y −50,54, Z −1013,85 |
| Hang giữa | `duskPoint`, khoảng X 57,59, Y −87,29, Z −1020,91 |
| Hang sâu | Box của `Zone_Cave_Deep` |
| Trước đấu trường Boss | Mốc `bossPoint`, khoảng X 6,22, Y −59,45, Z −941,00. Player đứng phía lối tiếp cận và ngoài bán kính chiến đấu khi prefab Boss đã có mặt. |

## Cave, bước chân và starter

- `Tools → Capstone → Cave → Scan open drop edges` tìm mép NavMesh nghi mở dựa trên vật cản ngang và tia dò nền hướng xuống. Dấu đỏ là vị trí cần xem trong Scene View trước khi đặt vách chắn. Dùng `Clear drop markers` để dọn dấu.
- DemoScene 3 tạo hai vách sau NPC và sàn đỡ dưới đáy hang khi chạy. Hệ thống theo dõi vị trí đứng an toàn gần nhất và đưa player trở lại nếu rơi sâu mà thiếu chỗ đáp hợp lệ. Lối có tầng đi được ở dưới được kiểm tra trước khi kéo player về. Có điểm về ở cửa hang nếu player chạm sàn đỡ mà chưa có vị trí an toàn.
- `LocomotionSound` được gắn từ `BasicPlayerMovement`. Khu vực hang lấy từ `ChapterWorldLayout` cùng scene, phát `DiTrenDa.mp3`. Ngoài hang phát luân phiên `Di1`, `Di2`, `Di3`. Cả hai dùng bus SFX và chỉ phát khi player di chuyển trên mặt đất.
- Ba lựa chọn ở bàn starter cấp 5 từ lúc nhận. Starter đã có trong save giữ tiến trình của cá thể đó. `Đánh Thường` (`bite.normal`) và `Gầm Uy Lực` (`roar.power`) có sẵn từ cấp 1 trong kho kỹ năng của cả 33 dạng. Gầm tăng sát thương vật lý và phép 15% trong 8 giây. Mỗi pet vẫn có bốn ô trang bị và người chơi có thể thay chiêu trong tab Kỹ năng.

## Tệp thay đổi

- Mới: `Assets/Game/UISystem/DevCheatPanel.cs`, `DevCheatService.cs` và các tệp `.meta`. Panel tự khởi tạo một lần, dùng cho Editor và Development Build.
- Save và hồ sơ: `PlayerJsonFileSaveStore.cs`, `PlayerSaveController.cs`, `QuestSaveController.cs`, `SaveSlotSession.cs`, `PlayerContinueState.cs`, `PlayerProfileRuntimeProvider.cs`.
- Chương I và Boss: `ChapterQuestFlow.cs`, `ChapterWorldLayout.cs`, `BossController.cs`. Tool quét mới: `Assets/Game/QuestSystem/Editor/CaveDropInspection.cs` và `.meta`.
- Pet: `PetCollectionMetadata.cs`, `PetHudRuntimeStats.cs`, `CreatureCombatant.cs`, `CreatureAnimationDriver.cs`, `StarterPetGranter.cs`, `LocomotionSound.cs`.
- Kỹ năng: `PetSkillCatalog.asset`, `Skill_bite.normal.asset`, `Skill_roar.power.asset`, hai sprite `skill_basic_attack.png` và `skill_power_roar.png` cùng các tệp `.meta). Cập nhật 30 bộ skill được tham chiếu bởi 33 form trong `Assets/Game/CreatureSystem/Runtime` và `Assets/Game/Skills/Data/Species`. Cập nhật phép kiểm tra trong `SkillStatusVerification.cs`.
- F10, vách và sàn đỡ được tạo lúc chạy. Dùng prefab Detale hiện có cho Boss Ready Editor. Ba prefab collider hiện có nằm trong `Assets/Game/Prefabs/InvisibleColliders`. GUID của Cylinder và Sphere đều riêng biệt.

## Kiểm chứng

- `Assembly-CSharp` và `Assembly-CSharp-Editor` đã biên dịch thành công bằng tham số Roslyn của Unity. Edit Mode trong Unity đạt **454/454** kiểm tra asset Skill/Status và **5/5** kiểm tra kỹ năng ở panel.
- Đã chạy công cụ quét trên DemoScene 3. Lần chạy quan sát được duyệt 6.889 mẫu trước khi đủ 400 dấu nghi rơi sâu ở bản đầu. Bản công cụ hiện tại tiếp tục đếm tất cả mẫu và giới hạn số dấu vẽ ở 400. Sai số có thể phát sinh ở nóc, nhiều tầng hoặc đoạn chưa bake.
- Còn cần Play Mode để xác nhận phím F10 trong Game View, các toggle, hồi sinh, chuỗi NV1–8 và chống nhận thưởng trùng, chuyển scene và khôi phục, đường vào trận và trận Detale, cùng âm thanh thực tế khi đi từ Grassland sang Cave và các tình huống rơi.
- Build Settings hiện tại không có DemoScene 3 và DemoScene 1 QA. Muốn dùng hai scene trong Development Build, thêm chúng vào cấu hình build thử. Development Build dùng Boss đã gắn sẵn trong scene, vì cách lấy prefab Detale từ AssetDatabase chỉ chạy trong Editor.

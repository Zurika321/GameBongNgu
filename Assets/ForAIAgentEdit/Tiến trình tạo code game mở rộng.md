# Tiến trình tạo code game mở rộng

**Cập nhật gần nhất:** 30/10/2026  
**Rà soát ban đầu:** 25/10/2026  
**Mục tiêu:** Xây dựng bộ hệ thống dùng lại để người làm game có thể tạo nội dung bằng Inspector, asset và scene: kéo thả đối tượng, cấu hình điều kiện/hành động, viết hội thoại và lựa chọn. Sau khi một loại hành vi đã được lập trình, nội dung mới cùng loại không cần viết lại code. Một cơ chế gameplay hoàn toàn mới vẫn cần được triển khai thành hành vi dùng lại được.

## 1. Phạm vi rà soát

Đã đối chiếu mã nguồn, dữ liệu, cấu hình scene và tài liệu trong project Unity:

- 75 file C# trong Assets/Scripts thuộc 14 nhóm thư mục; BongNguUI.cs hiện rỗng.
- 8 scene được bật trong EditorBuildSettings: MainMenu, CoreCombatDemo, Zone1–Zone5 và SampleScene.
- 28 asset hội thoại; 3 asset trong số này được tham chiếu từ scene/prefab hiện tại.
- 8 bảng JSON thiết kế (Session, Level, Monster, Boss, Item, NPC, Skill, Zone) nằm dưới Assets/Scripts/SaveLoad/Resources/Data và được DataManager nạp qua Resources.
- 2 file ngôn ngữ trong Assets/Resources/transitions, cùng dữ liệu ScriptableObject, prefab, công cụ Editor và các scene YAML.
- Đã xem tài liệu CLAUDE.md, AI_QuyTac.md, StoryEventDirector_Guide.md, UNITY_SETUP_GUIDE.md, cốt truyện và hai workbook thiết kế/tiến độ.

Đây là rà soát tĩnh từ file trong project. Chưa mở Unity để xác nhận mọi reference trong Inspector, chạy Play Mode, kiểm tra build hoặc nhìn duyệt từng ảnh/âm thanh.

## 2. Tiến độ hiện tại

| Hệ thống | Đã có | Đánh giá |
|---|---|---|
| Cấu trúc game | Có nhóm riêng cho controller, combat, AI, narrative, save/load, inventory, UI, audio, localization, world và editor. CoreCombatDemo là scene gameplay nền; các map zone được tải bổ sung. | **Nền tảng đã hình thành** |
| Combat và skill | Có ISkill, các skill riêng, controller cho player/enemy, hitbox, projectile, DOT, cooldown và cấu hình slot trong Inspector. | **Có thể cấu hình skill đã hỗ trợ**. Thêm loại hành vi skill mới vẫn cần code và đăng ký trong SkillActions. |
| Inventory skill loadout | PlayerSaveData lưu tối đa 3 skill đang trang bị cùng phím; Inventory có tab Kỹ năng, và PlayerSkillController đồng bộ skill đã trang bị từ save. | **Đã thêm source và scene setup**; BasicSlash và DodgeRoll là hai skill hiện có activeSkillId/action tương ứng. Chưa compile hoặc kiểm tra Play Mode trong Unity. |
| Encounter/spawn | ZoneEnemySpawner hỗ trợ spawn theo khoảng cách hoặc lệnh event, cờ yêu cầu, phần thưởng, clear event và saveOnBattleClear. Snapshot combat được lưu riêng với tiến độ quái bị hạ. | **Đã có nền tảng**. ID encounter ở nhiều scene vẫn để trống và đang dựa vào tên/path hierarchy để tạo ID dự phòng. |
| Story event | StoryEventDirector nhận event ID, one-shot, dialogue profile và action. Action hiện có thể bật/tắt object, đặt/xóa flag, spawn encounter, thêm/xóa item, mở sách và hoàn thành session. Event có thể được gọi từ trigger hoặc clear event của spawner. | **Đã cấu hình được bằng Inspector**, nhưng tập điều kiện/hành động còn giới hạn và chưa có công cụ authoring/kiểm tra tổng quát. |
| Điều kiện lựa chọn | Dialogue choice hỗ trợ một requiredFlag, một requiredItemName, yêu cầu session, kết quả LT/QT và nhảy node. Trigger/spawner cũng có cờ yêu cầu. | **Đã có điều kiện cơ bản**; chưa có nhóm điều kiện AND/OR hoặc bộ điều kiện tái sử dụng. |
| Hội thoại | Có DemoDialogueAsset, node, speaker, choice, portrait, typewriter và tiếp tục hội thoại. DemoNpcDialogueProfile vẫn hỗ trợ cả asset lẫn dữ liệu thoại nhúng trực tiếp trong scene. | **Đã có định dạng asset**, nhưng hai cách lưu thoại cùng tồn tại. |
| Tiến trình truyện | Có ConnectedStoryDirector, flag/session/choice và event cấu hình trong các scene zone. Scene YAML hiện có event ID từ Session 1 đến các mốc như S9A, S13A, S15A. | **Nội dung scene đang tiến xa hơn hướng dẫn**; cần xác định rõ phần nào là nội dung playable đã hoàn chỉnh. |
| Save/load | PlayerSaveData lưu vị trí, HP, zone/session, ngôn ngữ, item, rương, flag, lựa chọn, world drop, battle snapshot và cooldown. DataManager đọc JSON trong save trước rồi mới dùng bảng seed trong Resources. | **Schema nền tảng khá đầy đủ**. Chưa thấy version schema/migration có phiên bản; cần xác nhận hành vi bằng Play Mode. |
| Localization | Có transition, LocalizedText, LanguageButton, hai ngôn ngữ VN/EN và nút chọn trong menu. Lựa chọn ngôn ngữ được lưu vào player data; JSON runtime được nạp từ Resources/transitions. | **Đã nối cho UI/key mẫu**. Hai file ngôn ngữ hiện có 9 key; asset thoại duy nhất có cờ key là LocalizationExample. |
| Menu/âm thanh | Main Menu và CoreCombatDemo có vùng Settings, VolumeSettingsUI, âm lượng master/music/SFX, mute và fullscreen. VolumeManager lưu mức âm lượng bằng PlayerPrefs. | **Có nền tảng**, cần kiểm tra tham chiếu và thao tác trên cả hai scene trong Unity. |
| Inventory/storage | Có inventory kéo-thả, item definition, rương theo container ID, prompt tương tác, cửa sổ storage và builder tạo/cài prefab. | **Có nền tảng**, cần xác minh prefab/reference bằng Inspector. |
| Map và HUD | Có MiniMapCameraView, MapZoneGraphic, MapZoneRegion, PlayerStatusHUD và DemoPauseMenu để điều khiển menu/inventory/map. | **Có mã nguồn và scene setup**, chưa xác nhận đầy đủ hiệu năng, clipping và bật/tắt render trong Play Mode. |
| Công cụ Editor | Có DemoDialogueAssetBuilder, LocalizationSetupTool, QuickCombatDemoSetup, Storageuibuilder. | **Đã có tool dựng một số nội dung/demo**, chưa phải bộ authoring tổng quát cho designer. |

### Phạm vi nội dung truyện

GDD/cốt truyện mô tả 15 session và nhiều nhánh/kết thúc. Có asset thoại cho các session/nhánh đến 15, nhưng chỉ 3 asset thoại được scene/prefab hiện tại tham chiếu; nhiều DemoNpcDialogueProfile trong scene đang chứa thoại inline. Vì vậy số asset có trên ổ đĩa không đồng nghĩa toàn bộ nội dung đã được nối vào luồng chơi.

## 3. Các phần còn dang dở

### Ưu tiên cao — biến hệ thống hiện có thành quy trình authoring

- [ ] Chốt một định dạng chính cho story event: event được khai báo ở scene hay lưu thành asset dùng lại; xác định ID ổn định và cách tham chiếu.
- [ ] Làm Inspector/Editor tool tạo event theo form: ID, điều kiện bắt đầu, đối tượng liên quan, danh sách hành động có thứ tự. Cho phép kéo thả profile thoại, spawner, GameObject, item và scene reference.
- [ ] Mở rộng điều kiện theo các nhu cầu truyện thực tế: flag, item, session, lựa chọn, vùng tương tác và encounter. Cho phép kết hợp nhiều điều kiện thay vì nhập một chuỗi flag rời rạc.
- [ ] Mở rộng danh sách action dùng lại được: mở hội thoại, đặt/xóa flag, thêm/xóa item, bật/tắt object, gọi encounter, chuyển session/zone và các kết quả choice. Action cần có thứ tự rõ ràng.
- [ ] Thêm nút kiểm tra cấu hình trước khi Play: event ID trùng/rỗng, choice không khớp action, thiếu Dialogue Profile, spawner không có ID, flag không được khai báo, reference bị thiếu và key dịch không tồn tại.
- [ ] Thiết lập ID cố định cho encounter và object có trạng thái save. ZoneEnemySpawner đã có persistentSpawnerId, nhưng các giá trị trong nhiều scene đang rỗng; đổi tên hoặc di chuyển object có thể làm ID dự phòng thay đổi.

### Ưu tiên cao — hoàn thiện pipeline hội thoại và bản dịch

- [ ] Chọn DemoDialogueAsset làm nguồn hội thoại thống nhất; chuyển dần thoại inline trong scene sang asset và giữ rõ cơ chế tương thích khi chuyển đổi.
- [ ] Nâng cấp tool thoại để tạo/sửa node, speaker, lựa chọn, điều kiện, kết quả, LT/QT và nhảy node trực tiếp trong Unity.
- [ ] Hoàn tất dịch cho nội dung thoại/choice/result. Bổ sung key cho các dòng thực sự cần dịch và kiểm tra thiếu key giữa vn.json/en.json.
- [ ] Thêm kiểm tra asset thoại chưa được scene dùng, profile thiếu asset, node nhảy sai chỉ số và choice chưa có action tương ứng.
- [ ] Quyết định các asset hội thoại nào thuộc phần chơi hiện tại; asset session 6–15 hiện diện nhưng chưa được nối đầy đủ vào scene theo những gì thấy trong serialized references.

### Ưu tiên cao — cấu hình scene bằng Editor thay vì dựng UI lúc chạy

- [ ] Với UI/game object cần chỉnh bằng Inspector, tạo/cập nhật bằng Editor tool hoặc scene/prefab đã serialize.
- [ ] Rà soát các nhánh tự dựng UI khi thiếu reference, gồm InventoryGridUI, StorageWindowUI, PlayerStatusHUD.Create, thanh máu trong HealthController và transition fade trong PlayerProgressionService.
- [ ] Giữ tạo hitbox/projectile/effect trong runtime khi đó là hành vi chiến đấu; tách rõ việc này khỏi việc dựng layout/UI nội dung.
- [ ] Hoàn tất hoặc bỏ Assets/Scripts/Editor/BongNguUI.cs đang rỗng.

### Ưu tiên cao — xác nhận luồng save, scene và truyện

- [ ] Playtest New Game → Continue → tải scene gameplay/map additive → chuyển zone → quay lại zone.
- [ ] Kiểm tra save giữa trận: HP player, HP/dead state của quái, cooldown, clear encounter, không spawn trùng và không nhận thưởng trùng.
- [ ] Thêm version cho save và migration có chủ đích khi schema thay đổi; kiểm tra save cũ thiếu field và save lỗi.
- [ ] Kiểm tra toàn bộ ID, references và trạng thái bật/tắt object trong các scene/prefab.
- [ ] Xác định SampleScene có cần nằm trong build hay chỉ là scene mẫu.
- [ ] Thống nhất tiến độ playable theo scene: hướng dẫn StoryEventDirector_Guide.md ghi demo đến Session 5 và trỏ tới Assets/Scenes/Test/CoreCombatDemo.unity, nhưng scene hiện tại nằm ở Assets/Scenes/Zones/CoreCombatDemo.unity, còn YAML zone có event ID đến Session 15A.
- [ ] Làm rõ các scene Zone nào được tải additive trong luồng chính và scene nào còn dùng player/save service riêng để tránh cấu hình trùng ngoài ý muốn.

### Ưu tiên trung bình — giảm cấu hình chuỗi và code đăng ký thủ công

- [ ] Cho phép chọn item/skill/session bằng asset reference hoặc danh sách có kiểm tra thay vì gõ chuỗi tên ở nhiều nơi.
- [ ] Tách thông số skill khỏi phần thi hành skill để cân chỉnh skill đã có bằng asset/Inspector. Việc tạo một hành vi skill mới vẫn cần triển khai code dùng lại.
- [ ] Rà soát script cũ: SessionDemoDirector.cs hiện bị comment toàn bộ; xác nhận có reference nào còn cần giữ.
- [ ] Cập nhật các hướng dẫn cũ để cùng phản ánh scene, đường dẫn, phạm vi session và công cụ hiện có.
- [ ] Bổ sung smoke test/Play Mode test cho event, dialogue, save/load, transition scene và volume UI sau khi pipeline authoring ổn định.

## 4. Quy trình đích đề xuất

1. **Designer tạo nội dung:** tạo Dialogue Asset, viết câu thoại/choice và chọn key dịch nếu cần.
2. **Designer tạo event:** mở cửa sổ Story Event, khai báo ID, điều kiện, kéo thả reference, sắp thứ tự action.
3. **Tool kiểm tra:** báo ID trùng, reference thiếu, flag/key sai, node/choice không hợp lệ và encounter chưa có ID ổn định.
4. **Designer nối vào scene:** đặt trigger/spawner/portal và kéo event asset vào Inspector.
5. **Playtest:** chạy checklist scene/save/story; lỗi cấu hình được báo ngay ở Editor.

## 5. Thứ tự triển khai

1. **Chốt cấu trúc dữ liệu và ID ổn định** cho event, dialogue, choice và spawner.
2. **Chuẩn hóa dialogue asset + localization**, chuyển nội dung inline có chọn lọc.
3. **Làm Story Event Editor/Inspector và validation**, dựa trên StoryEventDirector đang có.
4. **Chuẩn hóa scene/prefab và Editor builders**, không sinh layout UI từ runtime fallback.
5. **Kiểm thử luồng truyện/save/load**, cập nhật guide và bảng tiến độ hiện có.

## 6. Tiêu chí hoàn thành mục tiêu

- Tạo một encounter hoặc event mới trong phạm vi hành động đã hỗ trợ bằng Inspector/Editor, không phải sửa C#.
- Viết và nối hội thoại/choice qua asset; bật key dịch và nhận được bản dịch ở VN/EN.
- Kéo thả reference thay vì phụ thuộc vào tên object hoặc chuỗi gõ lặp lại.
- Tool phát hiện cấu hình thiếu/trùng trước khi chạy game.
- Nội dung đã hoàn tất vẫn tiếp tục đúng sau save/load và chuyển scene.
- UI/gameplay objects được quản lý từ scene/prefab/Editor theo quy ước project; runtime chỉ tạo các đối tượng vốn là hiệu ứng/hành vi chiến đấu.

### Cập nhật 30/10/2026 — hệ thống skill trong Inventory

- Lên cấp tiếp tục chỉ mở khóa skill trong PlayerSaveData.unlockedSkills; trang bị được lưu riêng trong equippedSkills với tối đa 3 ô và phím 1–9/Space.
- CoreCombatDemo có tab chuyển giữa Vật phẩm và Kỹ năng. Ba ô đầu dành cho skill đã trang bị; danh sách còn lại lấy từ skill đã mở khóa. Trang bị/gỡ skill đồng bộ các slot runtime của PlayerSkillController.
- Skill.json hiện ánh xạ BasicSlash và DodgeRoll. Những skill khác chỉ hiển thị đến khi có activeSkillId và hành động đã đăng ký trong SkillActions.
- Thay đổi trên chưa được compile hoặc kiểm tra trong Unity Editor/Play Mode. Kế hoạch tiếp theo được ghi ở Assets/ForAIAgentEdit/NewUpdate2.txt.

## 7. Ghi chú tham chiếu

- Báo cáo tiến độ cũ: TienDoGame_BongNgu.xlsx (lập ngày 18/10/2026; đánh giá runtime vẫn cần xác minh).
- Thiết kế game: Assets/BongNgu_GameDesign.xlsx.
- Quy ước: Assets/AI_QuyTac.md, Assets/ForAIAgentEdit/CLAUDE.md.
- Hướng dẫn Story Event hiện tại: Assets/StoryEventDirector_Guide.md.
- Cốt truyện: Assets/cot-truyen-bong-ngu.md.
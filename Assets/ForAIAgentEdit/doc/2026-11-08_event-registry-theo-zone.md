# EventId theo Zone và đồng bộ StoryEvent — 08/11/2026

Yêu cầu: thêm EventId Zone1–Zone5 trong GameIdRegistry; nút tìm StoryEventDirector trong scene tương ứng và tạo event rỗng cho ID đã đăng ký; dropdown RegistryId cho các trường kích hoạt event.

## Đã làm

- RegistryIdAttribute.cs: enum phân loại nội bộ đổi tên thành RegistryIdKind, giữ giá trị cũ Flag=0…Sfx=6, bổ sung EventZone1…5 và EventScene. RegistryIdType trở thành nhóm constant để dùng đúng cú pháp C# `RegistryId(RegistryIdType.Event.Zone1)`; Flag/Item/Skill… giữ nguyên cú pháp cũ. Các thuộc tính này không đổi giá trị string đã serialize hoặc schema save.
- GameIdRegistry.Events.cs: năm nhóm StoryEventZoneRegistry, mỗi nhóm có Scene Path, Event IDs và Director Object Name khi scene có nhiều director. GetList hỗ trợ từng zone và hợp các zone cho prefab chưa có scene.
- GameIdRegistry.asset: gán sẵn scene Zone1–Zone5, nhập 16 ID hiện dùng của Zone1 và 1 ID mỗi Zone2…5 (20 ID tổng cộng). Chỉ đọc scene hiện tại, không lấy _Recovery hoặc lẫn ID của zone khác. Không đổi ID đã có.
- Zone1 có 2 director: `Story Event Settings` (12 event) và `Story Event Khai` (4 event). Đã gán Story Event Settings nhận các ID mới. Khi xét ID đã tồn tại, đồng bộ xét cả 2 director để tránh nhân đôi event trong Story Event Khai. Zone2–5 mỗi scene có 1 director nên tự chọn.
- GameIdRegistryEditor: mỗi zone có Scene asset picker, Director Object Name, danh sách Event IDs, nút đọc ID từ scene và nút Đồng bộ StoryEvent — thêm event rỗng. Nút đồng bộ project hiện có cũng thu Event IDs theo các scene đã gán.
- SyncStoryEventsInZone chỉ thêm ID còn thiếu, bỏ trống/trùng, không sao chép action từ event trước. Giữ event cũ, thứ tự, dialogue/choice/conditions/actions. Object tắt trong scene cũng được tìm. Không có director hoặc chọn không duy nhất thì báo lỗi và không sửa.
- Scene đang mở: dùng dữ liệu đang edit, có Undo/prefab override, đánh dấu dirty rồi người dùng Ctrl+S. Scene chưa mở: mở additive, thêm/lưu/đóng, khôi phục active scene. Nếu không lưu được, giữ scene dirty mở để lưu tay. Chặn lúc Play Mode/chuyển vào Play Mode. Không tự tạo GameObject/director.
- RegistryIdDrawer: Event.Scene tự lọc theo scene của component hoặc director đã tham chiếu. Prefab/Core chưa xác định zone hiển thị nhánh Zone1–5. Event.Zone1…5 luôn giới hạn đúng danh sách chỉ định.
- Gắn RegistryId(Event.Scene) cho StoryEventTrigger.eventId, ZoneEnemySpawner.eventIdOnCleared, StoryLootChest.onOpenedEventId và StoryEventDefinition.eventId. Dùng Scene cho script dùng chung, không cố định Zone1 cho mọi bản thể.
- StoryEventDirector thêm getter EventDefinitions để tool đọc/giữ cấu hình hiện có. GameIdRegistry.ReadYamlValue bỏ qua YAML null/~ khi thu ID từ file, tránh nhập ID giả `null` từ trường rỗng.

## Cách dùng

1. Mở Resources/GameIdRegistry.asset, kéo xuống Story Event — Zone 1 đến Zone 5.
2. Thêm tên vào Event IDs của zone cần tạo; Scene đã gán sẵn. Nếu đổi scene có nhiều director, nhập tên GameObject nhận event vào Director Object Name (tên phải duy nhất trong số director).
3. Bấm Đồng bộ StoryEvent Zone N — thêm event rỗng. Các event mới có oneShot=true theo constructor, chưa có thoại/action/điều kiện: tự cấu hình nội dung trước khi dùng.
4. Nếu scene đang mở, Ctrl+S sau khi kiểm tra. Có thể bấm lại, không tạo trùng.
5. Các ô trigger/rương/spawner trong scene có dropdown đúng zone; prefab chưa có scene cho chọn theo nhánh zone. Khi cần cố định trong script: `[RegistryId(RegistryIdType.Event.Zone1)] public string eventId;`.
6. Đọc Event ID là chiều scene → Registry, chỉ bổ sung, không xóa ID đã tự đăng ký. Đồng bộ StoryEvent là chiều Registry → scene. Đổi/xóa ID không tự sửa các caller hoặc xóa event cũ.

## Kiểm tra

- Build runtime + Editor qua Validation/SkillActions/Editor.csproj: thành công, không lỗi. Runtime.csproj thêm file partial Events mới.
- Validation/StoryEvents/Checks.csproj: 27 kiểm tra production code với Unity Editor API doubles. Bao gồm phân zone, loại enum/cú pháp constant, trim/duplicate, giữ payload và thứ tự, default rỗng, rerun idempotent, chỉ scene được chọn, additive save/close/restore, nhiều director/không director, chỉ định nơi nhận, ID có ở director khác, Play Mode guard, giữ scene dirty khi save thất bại, đọc unsaved fields và parser YAML null/Unicode. ProductionParser.cs trích các helper parser hiện tại từ GameIdRegistry.cs.
- Đối chiếu asset: 5 scene có thật, danh sách không trùng/rỗng, ID trong scene đã nằm đúng registry zone, mỗi zone xác định duy nhất director nhận sync (Zone1 có 2 director nhưng đã chọn đích cụ thể).
- Chưa bấm tool trong Unity Editor thật / chưa chạy Play Mode. Chưa tự thêm placeholder event vào scene trong lượt này; thao tác đó được thực hiện khi người dùng bấm nút mới.
- Không thay workbook/ToDo hoặc scene gameplay trong lượt này. Không commit theo lựa chọn giữ chưa commit của người dùng.

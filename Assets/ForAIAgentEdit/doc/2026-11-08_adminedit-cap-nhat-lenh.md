# AdminEdit — cập nhật các lệnh cũ (08/11/2026)

Người dùng xác nhận F1/tab vẫn mở bình thường, vấn đề là các lệnh cũ hoặc lỗi khi dùng. Giữ menu IMGUI/F1 và component đã có trong CoreCombatDemo; không sửa scene/prefab hoặc tự chạy cheat lên save thật trong lượt này.

## Đã sửa

- AdminEdit bỏ dùng danh sách tên skill mẫu, Skill.json, Item.json, Level.json để chạy lệnh mới. Field registeredSkills cũ giữ HideInInspector để không làm mất override serialize; nguồn skill thật là PlayerProgressionService.SkillConfigAsset, item là ItemDefinition.GetAll, level là LvConfig qua service.
- Thêm PlayerProgressionService.Debug.cs (chỉ UNITY_EDITOR): đặt level/EXP theo cấu hình, mở skill của cấp, đồng bộ controller/passive, cập nhật HP và lưu; mở/khoá skill theo skillId chuẩn, gỡ skill bị khoá khỏi loadout, tăng SkillStateVersion/phát sự kiện và đồng bộ; mở/khoá tất cả dùng cấu hình hiện tại, lưu một lần. Không thay logic mở skill theo cấp khi load save bình thường; hạ level không tự xoá skill đã có.
- Tab Skills hiện tên + skillId + loại, trạng thái mở thật, nút trang bị/gỡ qua API hiện tại và ghi chú Needs Inspector Setup. Không ghi tên hiển thị cũ trực tiếp vào unlockedSkills nữa.
- Tab Inventory tra các ItemDefinition đang có, kể cả loot mới, kiểm tra tên/số lượng trước thêm; có equip/unequip qua ToggleEquipAtSlot nên đi qua đồng bộ GroupBlock. Tối đa 20 kết quả mỗi bộ lọc. Giữ ID item cũ và các API thêm/xoá đồ hiện tại.
- Tab Events liệt kê tất cả StoryEventDirector trong mọi scene đang load (kể cả object tắt); chọn theo scene/tên. Dùng getter EventDefinitions thay cho reflection đọc private events. ID gõ tay được tìm ở director khác trong cùng scene nếu chỉ có một nơi nhận, không tự chạy qua zone khác. Hiện ID đã đăng ký nhưng không nằm trong director đang chọn.
- StoryEventDirector.ResetEventForDebug (chỉ Editor) thay reflection vào completedEvents, xoá trạng thái hoàn tất và flag qua service. Chặn reset khi director đang có hội thoại để không làm hỏng state chờ lựa chọn. Không tự xoá LastEventData/Choice ID hoặc thực thi ngược action cũ.
- Pause: không disable DemoPauseMenu nữa (OnDisable của nó có thể thay đổi overlay/pause). DemoPauseMenu bỏ đọc Esc/Tab khi AdminEdit.AnyOpen. Admin tạm chặn GraphicRaycaster đang bật, khôi phục đúng các raycaster khi đóng/disable; scan lại để nhận UI mới khi load scene. Chỉ Resume pause do chính Admin tạo, giữ pause đã có trước khi mở.
- Sửa numeric editor: chỉ áp dữ liệu khi TextField thật sự được sửa, không để buffer cũ ghi đè EXP/HP/vector đã đổi bằng lệnh hoặc runtime. +EXP làm mới levelInput/buffer/focus. Từ chối NaN/Infinity; không dựng TextField cho string dài >512 ký tự; ẩn field HideInInspector mặc định để tránh JSON legacy rất dài.
- GUI matrix được restore bằng finally; kích thước panel không âm ở Game view hẹp. Bổ sung guard Data chưa sẵn sàng. Lệnh nhảy session lưu sau batch kể cả khi flags không đổi.

## Kiểm tra / dùng thử

- Build runtime + Editor dùng Unity references thành công. Runtime.csproj thêm partial Debug mới.
- Validation/AdminEdit: 26 checks với production debug API và các method menu/edit trích từ AdminEdit, dùng Unity/API doubles: canonical skillId, sync/version/event/save, trùng/unknown ID, gỡ loadout, mở/khoá tất cả, level/EXP/HP, missing level/data, pause ownership, UI restore/idempotency, decimal/NaN/Infinity, buffer numeric không ghi đè khi repaint. Không phải Unity Play Mode.
- Component AdminEdit hiện có/enabled trong CoreCombatDemo giữ nguyên GUID và cấu hình F1. Chưa thử trực tiếp click các lệnh trong Unity hoặc can thiệp save thật.
- Chờ compile rồi Play/F1; thử +EXP, mở/khoá/trang bị skill, thêm/trang bị item và chọn từng director Zone1. Nếu Freeze bật, đóng Admin để enemy/animation/hội thoại tiếp tục chạy. Menu vẫn chỉ dành cho Editor, không có cheat trong build phát hành.
- Không cần gán thêm Inspector. Giữ chưa commit theo lựa chọn người dùng.

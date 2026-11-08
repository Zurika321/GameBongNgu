# NewUpdate3 — inventory, HUD, loot/spawner và loop SFX (08/11/2026)

Theo yêu cầu trực tiếp làm toàn bộ 7 mục NewUpdate3 và ghi trạng thái ngay trong file đó. Người dùng đã xác nhận lưu scene trước đó và chọn giữ chưa commit. Không sửa workbook, LastEventData, Choice/session ID hoặc schema save.

## Thay đổi

- `InventoryGridUI`, `InventorySlotView`, `SkillConfig`, `PlayerProgressionService.Skills`: fallback icon được gán Inspector, giữ icon riêng; tên skill luôn đọc được. Nối nút thông số và popup skill; không tạo thêm Image skill slot lúc chạy. `CoreCombatDemo.unity` có card skill, ảnh/tên/mô tả, đóng card, nút thông số, popup status với refs đặt sẵn. Button lồng không dùng trên Image con của 3 slot bị vô hiệu để nhận click đúng ô; giữ component/fileID.
- `PlayerStatusHUD`: UI đặt sẵn, chỉ số đọc từ progression/health/modifiers; có EXP mục tiêu từ LvConfig, cập nhật unscaled time. `DemoPauseMenu` nhận serialized HUD và không tự dựng/mở HUD. `PlayerController.BaseMoveSpeed` cung cấp tốc độ gốc cho HUD tính tốc độ thực.
- `EnemyCombatConfig.drops`, `ZoneDropDefinition.onlyIfMissing`: loot lấy theo asset. `ZoneEnemySpawner` có reference config, fallback sang config của actor; bỏ lookup rate theo tên vật phẩm. 0% không rơi/100% luôn rơi, hỗ trợ Đá Nguyền Cổ chỉ khi chưa có trong túi/rương hoặc dưới đất. Giữ worldDrops và rewardsGranted trong save hiện có; bảng mới chỉ áp dụng cho lượt thưởng chưa thực hiện, không ghi lại vật phẩm đã lưu.
- 22 config trong Database Quai/Boss nhận loot Excel; 5 profile ngoài hai bảng để trống. 22 spawner scene Zone1–5 đổi reference config thay cho drops cũ, giữ enemy prefab đang gán, số lượng/vị trí/activation/flags/event/persistentSpawnerId. EXP giữ mức thực chạy trước sửa; giờ chỉnh được trực tiếp trong Inspector, không còn lookup override theo tên.
- `Prefabs/Spawer`: 22 prefab trỏ đúng 22 enemy/boss prefab đã đăng ký, combat config, pickup có sẵn. Chưa đặt thêm vào scene để tránh spawn trùng; mặc định mỗi prefab 1 con/proximity, chưa phải số lượng encounter thiết kế vì Excel không có bảng số lượng/bán kính. Prefab không mang ID trận hoặc scene event có sẵn. Bóng Đêm Rình Rập/Bóng Gương Thủ Lĩnh chưa có EXP nên để 0; Hồn Ma EXP 0 đúng bất tử.
- 20 `Resources/ItemDefinitions/Loot_*.asset` thêm để loot lookup được trong inventory, giữ item IDs cũ có tên gần giống (không đổi save). Registry chỉ bổ sung itemIds. Mục có Database Item dùng metadata Excel; mục chỉ xuất hiện ở loot ghi nguồn Database Quai/Boss. Không tự tạo artwork hoặc bịa chỉ số công dụng; Bùa Hồi Máu cần gán Heal Amount do nguồn chưa nêu lượng hồi.
- `CharacterAnimation.Update`: non-combat loop phát lại start SFX khi qua chu kỳ mới; combat giữ xử lý phase hiện có. Pause/stop không lặp âm.
- `ToDo/NewUpdate3.txt`: giữ đủ 7 yêu cầu với trạng thái chi tiết, giới hạn và checklist test Unity.

## Kiểm tra và giới hạn

- Runtime + Editor build dùng Unity assemblies thành công. `Validation/NewUpdate3/prepare_checks.py` trích đúng production Update/loot/reward methods; `Checks.csproj` chạy 26 ca với API doubles: loop/pause/one-shot/combat, xác suất/biên RNG, save drop ID, conditional ownership/storage/ground, config fallback, EXP Inspector. Không phải Unity Play Mode.
- `Validation/NewUpdate3/validate_assets.py`: 1762 assertions cho 22 loot tables, ItemDefinition/Registry, 22 prefab links, scene spawner refs và uGUI graph/fileID/parent-child. Allowlist GUID uGUI package kế thừa từ scene gốc.
- `update_assets.py` là script import một lần, không chạy lại trên dữ liệu đã nhập (có guard tránh tạo trùng prefab/UI). manifest.json ghi nguồn và danh sách output. EnemySkills test doubles bổ sung kiểu loot mới để không lệch schema.
- 36 kiểm tra hồi quy EnemySkills (controller/scheduler/phase) đã qua sau cập nhật kiểu loot trong test doubles.
- Chưa kiểm tra trực tiếp Unity rendering/input/physics/audio. Reload scene trước test. Icon riêng, hình quái/JSON, HP/ATK đã báo thiếu trước đó và lượng hồi Bùa Hồi Máu vẫn cần gán theo thiết kế.
- Không tạo commit theo yêu cầu người dùng.

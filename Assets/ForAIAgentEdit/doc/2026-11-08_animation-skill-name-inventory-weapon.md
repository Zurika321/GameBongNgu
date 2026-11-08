# Tên animation skill và đồng bộ vũ khí inventory — 08/11/2026

Yêu cầu trực tiếp: sửa đánh thường sau khi thêm weapon, nối equip/unequip với API GroupBlock, cho sửa tên animation skill trong Inspector trừ các tên AnimState mặc định. Liên quan phần mapping còn lại trong ToDo/NewUpdate4.txt; không sửa lại ToDo hoặc bản web trong lượt này.

- JSON player hiện tại có `Attack`, trong khi SkillConfig dùng gameplay skillId `BasicSlash`. PlayerBasicAttackController nay chọn animation theo Context.animationName nếu đã gán, tiếp theo tên skillId nếu JSON có, rồi `Attack` khi JSON thiếu tên riêng. Không đổi skillId/cooldown/loadout/save.
- Thêm `Scripts/Animation/CharacterAnimation.Bindings.cs` (partial CharacterAnimation): alias tên animation lưu trên component; kiểm tra trùng/rỗng/tên dành riêng; API `SetSkillAnimationName`, `HasAnimation`, `ResolveBasicAttackAnimationName`, `SyncEquippedWeapon`. Các tên AnimState, Default và Death được giữ nguyên. Alias không viết lại TextAsset JSON hoặc tên nguồn của SFX.
- CharacterAnimationEditor thêm mục **Tên animation skill**: nhập tên mới và Enter; hiển thị tên nguồn bên trái, tên gọi bên phải. Có Undo và lưu prefab override. Tên mặc định bị khóa. Nhập lại tên nguồn để bỏ alias.
- SkillContextConfig thêm `animationName`, SkillContext thêm `AnimationName`. PlayerBasicAttackController, PlayerSkillController (cả charge/cast), EnemySkillController, SkillActions, SkillAnimationPlayer và SkillActionEventRelay dùng tên hình ảnh này; ID gameplay vẫn giữ nguyên. IsPlayingSkill nhận alias bằng cách so animation nguồn.
- PlayerProgressionService.Skills nối NotifyWeaponChanged và đồng bộ sau load/đổi scene tới SyncEquippedWeapon/RefreshEquippedWeapon. InventoryGridUI vốn gọi ToggleEquipAtSlot/UnequipItem nên không cần thay UI. Các đường mất đồ/chuyển vào rương và sửa equippedItemName trực tiếp dùng chung thông báo này.
- Tự nhận group vũ khí qua tên biến thể khớp ItemDefinition loại Weapon. JSON hiện tại `Wepon` / `Dao Găm Gỉ` hoạt động theo dữ liệu đó. Gỡ vũ khí gửi chuỗi rỗng cho group vũ khí, giữ Outfit/nhóm khác. Không reset animation/frame. Không tự đoán khi nhiều nhóm cùng khớp.

## Inspector và thử trong Unity

1. Chờ Unity compile, chọn Player → CharacterAnimation → **Tên animation skill**. Đổi tên custom cho khớp skillId cần gọi. Các tên mặc định như Attack bị khóa.
2. Nếu muốn skill gọi một animation mặc định, mở Resources/SkillConfig → skill tương ứng → Default Context (hoặc Weapon Variants → Context) → **Animation Name**, chọn bằng cách nhập tên, ví dụ `Attack`. Để trống tiếp tục dùng skillId; riêng đánh thường có fallback Attack.
3. CharacterAnimation → GroupBlock → **Weapon Group**: mặc định tự nhận; có thể chọn chính xác group `Wepon`. Nếu tên biến thể khác tên vật phẩm, thêm **Weapon Variant Bindings** với Weapon Id và Variant Name đúng JSON.
4. Play Mode: equip Dao Găm Gỉ, giữ/thả input đánh thường, kéo đồ ra/gỡ trang bị, đổi hướng rồi đánh lại. Kiểm tra vũ khí ẩn/hiện đúng, animation chạy và SFX/cooldown không bị đổi ID.

## Kiểm tra

- Build C# runtime và custom Editor qua Validation/SkillActions/Editor.csproj với references Unity: thành công, không lỗi (tắt cảnh báo thông thường trong lần build).
- Validation/AnimationBindings: 37 kiểm tra logic production bindings với Unity API doubles, gồm JSON player thật, khóa AnimState, alias/collision/reset, stale binding, fallback Attack, equip/switch/unequip, mapping, nhóm độc lập, ambiguity và JSON cũ.
- Validation/EnemySkills: 36 kiểm tra production controller/scheduler/phase với API doubles, bao gồm custom animationName nhưng giữ gameplay skillId.
- Chưa chạy Unity Play Mode/Inspector trực tiếp; kết quả kiểm tra trên không thay cho thử input/renderer trong Unity.
- Không sửa scene, prefab, asset cấu hình hoặc JSON trong lượt này. Validation/SkillActions/Runtime.csproj thêm file partial mới; EnemySkills stubs cập nhật trường context mới.
- Không commit theo yêu cầu giữ nguyên chưa commit của người dùng.

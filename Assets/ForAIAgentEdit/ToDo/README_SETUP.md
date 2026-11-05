# Hệ thống Skill – bản 2 (hoàn thiện) + Hướng dẫn sử dụng

Bản này nối hệ thống skill mới vào `PlayerProgressionService` thật của dự án, và làm thêm:
mục 2 của NewUpdate2 (điều kiện vũ khí, tự gỡ skill – **chỉ 1 ô vũ khí**), tách **đánh thường** ra controller riêng,
đổi Inventory thành **2 nút tab**, và đưa **passive** ra khỏi Inventory (xem ở `PlayerPassiveSkillController`).

> Đã kiểm tra: biên dịch sạch (C# 9, như Unity) với stub Unity + 82 kiểm thử logic đạt (điều kiện vũ khí, tự gỡ skill,
> đổi đòn đánh thường theo vũ khí, migrate save cũ, passive, cooldown). **Chưa chạy trong Unity thật** – hãy làm mục 6 (Checklist).

---

## 1. Danh sách file

| File | Việc cần làm |
|---|---|
| `SkillSystemCore.cs` | THAY. Thêm `SkillWeaponRequirement`, `damageMultiplier`, event `Changed` của modifier store, `ReportHit` |
| `SkillConfig.cs` | THAY. Thêm `weaponRequirement`, `FindEntry` (id hoặc tên), chọn BasicAttack theo loại vũ khí, skill mẫu |
| `ISkill.cs`, `SkillActions.cs` | THAY (SkillActions chỉ thêm `GetConfiguredActionIds`) |
| `PlayerSkillController.cs` | THAY. Chỉ còn **skill chủ động / buff**; không xử lý đánh thường |
| `PlayerBasicAttackController.cs` | **MỚI.** Chỉ xử lý đòn đánh thường, tự đổi theo vũ khí |
| `PlayerPassiveSkillController.cs` | THAY. Tự hỏi `PlayerProgressionService` để lấy passive; hiện danh sách trong Inspector |
| `PlayerBuffController.cs` | THAY |
| `LvConfig.cs` | THAY (đọc cả field `skill` cũ lẫn `skills` mới) |
| `SkillContext.cs` | THAY (thêm `SkillId`, `ActionId`, `WeaponId`) |
| `SkillConfigValidator.cs` | **MỚI.** Menu `Tools > Bóng Ngủ > Kiểm tra hệ thống Skill` |
| `PlayerProgressionService.cs` | THAY (bản đầy đủ). Đã sửa `NormalizeData` không xoá trang bị khi load |
| `PlayerProgressionService.Skills.cs` | **MỚI** (phần skill của cùng partial class) |
| `ItemDefinition.cs` | THAY. Thêm `WeaponType` và field `weaponType` |
| `InventoryGridUI.cs` | THAY. 2 nút tab, chỉ hiện skill chủ động, hiện tên skill thay vì skillId |

**Không cần sửa:** `PlayerSaveData.cs`, `DameDotConfig.cs`, `InventorySlotView.cs`, `InventoryDropTarget.cs`, `InventoryStorage.cs`.
**Xoá:** `PlayerProgressionService_Integration.txt` của bản cũ (đã có service đầy đủ).

---

## 2. Cài đặt (làm theo thứ tự)

1. **Commit/backup** project (git) trước khi chép đè.
2. Chép các file ở bảng trên vào project. `PlayerProgressionService.cs` và `PlayerProgressionService.Skills.cs` phải cùng assembly.
3. Chờ Unity compile. Nếu lỗi, gửi lại nguyên văn lỗi.
4. **ItemDefinition:** mở từng vũ khí (Category = Weapon) và chọn **Weapon Type** (Dao găm → `Dagger`, kiếm → `Sword`...).
   Vũ khí chưa chọn loại được xem là `Other`, nên skill yêu cầu `Dagger` sẽ không nhận nó.
5. **SkillConfig** (`Assets/Resources/SkillConfig.asset`):
   - Skill cũ tự migrate (activeSkillId cũ → `actionId`, `skillId` lấy tạm từ actionId, thông số cũ → `defaultContext`).
   - Menu ⋮ của asset → **Thêm skill mẫu** nếu muốn có sẵn BasicAttack Punch/Dao, Fireball, Passive, Buff để xem cách điền.
   - Menu ⋮ → **Áp điều kiện vũ khí mẫu** để "Chém cơ bản" chỉ dùng được khi cầm Dao găm hoặc Kiếm.
6. **LvConfig:** nhập **skillId** (hoặc tên hiển thị cũ, vẫn nhận) ở mục `skills` của từng level. BasicAttack **không cần** mở khóa.
7. **Player trong scene:**
   - `PlayerProgressionService` có ô **Auto Attach Skill Components** (mặc định bật): nếu Player thiếu `PlayerBasicAttackController`,
     `PlayerBuffController`, `PlayerPassiveSkillController`, `RuntimeStatModifierStore` thì tự thêm khi chạy. Muốn tự gắn tay thì cứ gắn.
   - **Đánh thường:** thêm `PlayerBasicAttackController` cạnh `PlayerSkillController`. Khi thêm trong Editor nó tự sao chép
     phím/chuột/nút UI đánh thường cũ từ `PlayerSkillController`. Nếu PlayerInput đang gọi `PlayerSkillController.OnAttack`
     cho chuột trái, thêm listener `PlayerBasicAttackController.OnAttack` (hoặc bỏ qua: Update của nó đã tự đọc chuột/phím).
8. **Inventory UI (scene/prefab, vì UI không tạo bằng code):**
   - Nhân đôi nút Skill Tab hiện có → đặt tên `Item Tab Button`, đổi chữ thành `VẬT PHẨM`; nút cũ đổi chữ `KỸ NĂNG`.
   - Trong `InventoryGridUI`: gán **Item Tab Button** và **Skill Tab Button**. (Chưa gán Item Tab → giữ kiểu cũ: 1 nút bật/tắt.)
   - Màu tab đang mở / không mở chỉnh ở `Tab Selected Color` / `Tab Unselected Color`.
9. Bấm Play, xem Console: validator in lỗi cấu hình (có thể tắt bằng **Validate Skill Setup On Start**).

---

## 3. Khái niệm và quy ước ID

| Tên | Ý nghĩa | Ví dụ |
|---|---|---|
| `skillId` | Khoá duy nhất của skill. **Lưu trong save** (mở khóa, trang bị, cooldown) | `BasicSlash`, `Fireball_Player` |
| tên hiển thị (`name`) | Chỉ để hiển thị. Mọi hàm của service vẫn nhận cả tên lẫn id | "Chém cơ bản" |
| `actionId` | Tên class thực thi đã đăng ký trong `SkillActions`. Nhiều skill dùng chung được | `NormalAttack`, `Fireball` |
| Weapon ID | `ItemDefinition.itemName` của vũ khí đang cầm; tay không = `Punch` | "Dao Găm Gỉ" |
| `WeaponType` | Loại vũ khí ở `ItemDefinition`; tay không = `Unarmed` | `Dagger`, `Sword` |

Loại skill (`SkillType`):

- **BasicAttack:** đòn đánh thường theo vũ khí. Do `PlayerBasicAttackController` xử lý. Không mở khóa, không trang bị, không hiện ở Inventory.
- **Active:** skill chủ động. Mở khóa theo level, trang bị vào 3 ô, bấm phím. Do `PlayerSkillController` xử lý.
- **BuffAction:** bấm phím để nhận buff có thời hạn (có thể chạy thêm actionId nếu bật `executeActionAfterBuff`).
- **PassiveBuff:** luôn cộng chỉ số khi đã mở khóa.
- **Passive:** kích hoạt theo trigger (`Always`, `OnHit`, `OnKill`, `OnDamageTaken`) với tỉ lệ và thời hạn.

Quy tắc hiển thị: tab Kỹ năng của Inventory chỉ hiện **Active + BuffAction**. BasicAttack và Passive không hiện (đổi quy tắc ở `IsInventorySkill` trong `PlayerProgressionService.Skills.cs`).

---

## 4. Luồng hoạt động

```
LvConfig (lên level) ──► PlayerProgressionService.unlockedSkills (lưu skillId)
                              │
        ┌─────────────────────┼───────────────────────────────┐
        ▼                     ▼                               ▼
 PlayerBasicAttackController  PlayerSkillController     PlayerPassiveSkillController
 (BasicAttack theo vũ khí)    (3 ô Active/BuffAction)   (tự hỏi service: passive đã mở khóa)
        │                     │                               │
        └──────► SkillContext (damage = ATK * damageMultiplier, theo skillId + vũ khí)
                       │                                       │
              SkillActions.Activate(actionId)         PlayerBuffController / RuntimeStatModifierStore
                       │                                       │
        hit/kill/damage ─► SkillCombatEvents ─► passive        └─► GetAttackPower / GetDefensePower / GetMaxHealth
```

Công thức chỉ số: `(gốc + tổng Flat) × (1 + tổng AddPercent) × tích Multiply`. Gốc = chỉ số theo level + bonus vật phẩm đang cầm.
Cùng một nguồn buff áp lại thì **làm mới** thời gian (không cộng dồn); khác nguồn thì cộng dồn.

---

## 5. Cách tạo từng loại skill

### 5.1 Đánh thường theo vũ khí
Thêm entry `Type = BasicAttack`, `actionId` (vd `NormalAttack`), rồi điền `defaultContext` (size, range, cooldown, chargeTime...).
Cách gắn với vũ khí, theo thứ tự ưu tiên:

1. `Weapon Id` khớp **đúng** vũ khí đang cầm (hoặc `Punch`/để trống = tay không).
2. `Weapon Requirement` khớp **loại** vũ khí – ví dụ Accepted Types = `Dagger`, **để trống Weapon Id**: một entry dùng cho mọi con dao.
3. Không khớp gì: dùng đòn tay không.

Controller tự đổi đòn ngay khi bạn đổi/gỡ vũ khí (kể cả lúc đang mở Inventory). Cooldown tính riêng theo skillId.
Muốn tắt đánh thường: tắt component `PlayerBasicAttackController`.

### 5.2 Skill chủ động (Active)
Entry `Type = Active`, `actionId` đã đăng ký trong `SkillActions`, `defaultContext`. Thêm `weaponVariants` nếu cùng skill muốn thông số khác theo vũ khí.
`damageMultiplier` = hệ số nhân ATK (1 = 100%). Mở khóa bằng LvConfig, người chơi tự kéo vào 3 ô.

### 5.3 Buff có thời hạn (BuffAction), Passive, PassiveBuff
- BuffAction: điền `buffConfig` (duration, modifiers). Chỉ buff chỉ số thì không cần class riêng.
- PassiveBuff: `buffConfig.modifiers`, luôn có hiệu lực.
- Passive: `passiveConfig` (trigger, chance, duration, modifiers). **duration > 0** với trigger khác `Always`.
- Chỉ số hỗ trợ: Attack, Defense, MaxHealth, MoveSpeed, CriticalChance, CriticalDamage.
  MoveSpeed: script di chuyển gọi `PlayerProgressionService.Instance.ModifyStat(PlayerStatType.MoveSpeed, tốcĐộGốc)`.
  Chí mạng: nơi gây damage gọi `ApplyCritical(damage, out bool isCrit)`.

### 5.4 Thêm một action mới (class thực thi)
1. Viết class `ISkill` (hoặc MonoBehaviour có `Execute(SkillContext)`).
2. Thêm `case "TênAction"` trong `SkillActions.CreateSkill`.
3. Thêm 1 dòng đăng ký trong `Skill Registrations` của `SkillActions` ngoài scene.
4. Tạo entry SkillConfig dùng `actionId` đó.

---

## 6. Điều kiện vũ khí (mục 2 NewUpdate2 – chỉ 1 ô vũ khí)

**Cấu hình:** entry SkillConfig → mục *Điều kiện vũ khí* → `Accepted Types` (loại) và/hoặc `Accepted Weapon Ids` (vũ khí cụ thể). Hợp lệ khi khớp **loại HOẶC ID**. Để trống = không yêu cầu.
Thêm `Unarmed` vào Accepted Types nếu skill cũng dùng được khi tay không. Không có chuỗi tên vũ khí viết cứng trong gameplay.

**Quy tắc "đang cầm":** vũ khí đang cầm = `equippedItemName` nếu là Category **Weapon**. Tay không, hoặc đang mặc món không phải vũ khí (giáp, bùa) = `Unarmed`/`Punch`.

**Kiểm tra ở đâu:**
1. Lúc trang bị: `GetEquipBlockReason` (UI hiện lý do, ô skill có nhãn "CẦN VŨ KHÍ").
2. Ngay trước khi kích hoạt: `PlayerSkillController.CanActivate`.
3. Passive có điều kiện chỉ có hiệu lực khi vũ khí khớp (danh sách passive ghi rõ lý do).

**Tự gỡ skill khi mất điều kiện** (rồi đồng bộ `PlayerSkillController` và lưu): chạy khi người chơi trang bị/gỡ/đổi vũ khí, vũ khí bị `RemoveItem`,
`StoreAll` (cất cả vào rương), `RevalidateEquipment` (StorageWindowUI gọi sau khi chuyển đồ), và khi load save.
Ngoài ra `LateUpdate` của service tự phát hiện trường hợp code khác sửa thẳng `Data.equippedItemName` hoặc vũ khí không còn trong túi.
Event `SkillAutoUnequipped(skillId, lý do)` để UI hiện thông báo.

**Sửa lỗi load:** `NormalizeData()` không còn gán `equippedItemName = ""` vô điều kiện. Trang bị hợp lệ được giữ khi Continue/load;
chỉ gỡ khi vật phẩm không còn trong túi.

---

## 7. Passive và Inventory

- `PlayerPassiveSkillController` **tự** hỏi `PlayerProgressionService` (`GetUnlockedPassiveSkillIds`, `IsWeaponRequirementMet`), chỉ bật/tắt phần chênh lệch,
  và tự cập nhật khi lên level, đổi vũ khí, load save (so `SkillStateVersion`).
- Khi Play, mở component này trong Inspector: danh sách **Passive Skills** hiện mọi passive Player đang có (id, tên, đang hiệu lực hay không, lý do).
  Code khác đọc qua `PassiveSkills` / `IsPassiveActive(id)`. Ô **Initial Passive Skill Ids** để bật thêm passive khi test.
- Tab Kỹ năng của Inventory: chỉ skill chủ động. Kéo thả đổi chỗ chỉ tác động thứ tự skill chủ động (passive nằm xen trong save không bị đụng).

---

## 8. Hook chiến đấu cho passive (xem Docs/CombatEventHooks.txt)

Passive `OnHit/OnKill/OnDamageTaken` chỉ chạy khi code gây damage báo sự kiện:
`SkillCombatEvents.ReportHit(context, target, targetDied)` sau khi damage đã áp dụng, và `SkillCombatEvents.RaiseDamageTaken(player, amount)` khi Player bị đánh.
Đây là phần phụ thuộc code damage/health của bạn (chưa có trong gói), nên cần thêm tay 2 dòng.

---

## 9. API hay dùng (cho UI/script khác)

- Tra cứu: `ResolveSkillId`, `GetSkillDisplayName`, `GetSkillIcon`, `GetSkillDefinition`, `TryGetSkillType`
- Mở khóa/trang bị: `HasUnlockedSkill`, `EquipSkillAtSlot`, `EquipSkill`, `UnequipSkill`, `GetEquipBlockReason`, `TrySetEquippedSkillKey`, `GetEquippedSkillAtSlot`
- Inventory: `CollectInventorySkillIds(list)`, `MoveInventorySkill(from, to)`, `IsInventorySkill`
- Vũ khí: `GetCurrentWeaponId`, `GetCurrentWeaponType`, `IsWeaponRequirementMet`, `GetWeaponRequirementText`, `RevalidateEquippedSkills`
- Chỉ số: `GetAttackPower`, `GetAttackPowerExact`, `GetDefensePower`, `GetMaxHealth`, `ModifyStat`, `ApplyCritical`
- Event: `SkillUnlocked`, `SkillAutoUnequipped`, `StatsChanged`; `PlayerBasicAttackController.BasicAttackChanged`
- Cooldown cho HUD: `PlayerSkillController.GetRemainingCooldown(id)`, `PlayerBasicAttackController.GetRemainingCooldown()`
- Debug: chuột phải component `PlayerProgressionService` khi đang Play → *Mở khóa mọi skill* / *In trạng thái hệ thống skill*

---

## 10. Save và migration (tự động khi load)

- `unlockedSkills`, `equippedSkills[].skillName`: tên hiển thị cũ → `skillId`; bỏ trùng. Skill SkillConfig chưa nhận ra thì **giữ nguyên** (không xoá dữ liệu).
- Cooldown cũ lưu theo actionId bị bỏ (chỉ mất vài giây hồi chiêu). Cooldown mới lưu theo skillId; đánh thường lưu với owner `PLAYER_BASIC`.
- `PlayerSaveData` không đổi cấu trúc.
- UI cũ đang in thẳng `unlockedSkills` sẽ thấy skillId – dùng `GetSkillDisplayName(id)` (`InventoryGridUI` đã sửa).

---

## 11. Checklist thử trong Unity

1. Play, không lỗi đỏ. Chạy menu **Kiểm tra hệ thống Skill**, xử lý hết Error.
2. Tay không: click trái ra đòn Punch. Cầm dao (kéo vào ô vũ khí) → Inspector của `PlayerBasicAttackController` đổi sang đòn dao ngay. Gỡ dao → về Punch.
3. Mở khóa skill (LvConfig hoặc DEBUG). Tab **KỸ NĂNG** chỉ thấy skill chủ động; passive không thấy.
4. "Chém cơ bản" (cần Dao/Kiếm): tay không bấm TRANG BỊ → bị chặn kèm lý do. Cầm dao → trang bị được, bấm phím chạy.
5. Đang trang bị Chém cơ bản, gỡ dao (hoặc cất cả vào rương) → skill tự rời ô, phím không còn chạy, save giữ trạng thái mới.
6. Thoát/Continue: vũ khí và 3 skill + phím vẫn còn.
7. `PlayerPassiveSkillController` (Inspector khi Play): thấy passive; passive cần dao chuyển hiệu lực theo vũ khí. ATK đổi đúng công thức.
8. Dùng skill rồi đổi phím/tháo-lắp lại: cooldown không bị reset.

## 12. Lỗi thường gặp

| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| Không đánh thường được | Kiểm tra có entry BasicAttack cho tay không (`Punch`/trống) và `actionId` đã đăng ký; xem Console |
| Cầm dao vẫn ra đòn tay không | ItemDefinition chưa chọn Weapon Type, hoặc BasicAttack dao chưa có Weapon Id/Requirement khớp |
| Skill bị chặn dù cầm dao | Vũ khí `Weapon Type` ≠ loại yêu cầu; hoặc chưa mở khóa; hoặc `actionId` chưa đăng ký trong SkillActions |
| Chém cơ bản tự biến mất khỏi ô | Đúng thiết kế khi không còn dao/kiếm |
| Tab Kỹ năng trống | Chỉ hiện Active/BuffAction đã mở khóa; kiểm tra LvConfig và `type` |
| Passive OnHit không chạy | Chưa gọi `ReportHit`/`RaiseDamageTaken`; hoặc `duration`/`chance` = 0 |
| UI hiện `Fireball_Player` thay vì tên | Dùng `GetSkillDisplayName(id)` trong UI tự viết |
| Phím a-z trùng phím di chuyển | Người chơi tự chọn phím; cân nhắc giới hạn danh sách phím |
| Mất thông số level-unlock sau khi thay LvConfig | Asset cũ dùng field khác tên `skill`: gửi LvConfig.cs cũ để thêm field tương thích |

## 13. Chưa làm (ngoài phạm vi lần này)

- NewUpdate2 mục 1 (thời lượng từng frame), 3 (SFX theo skill), 4 (Player Hub); **mục 2 bản 2 ô vũ khí đã bỏ theo yêu cầu: chỉ 1 ô**.
- `SkillContext` chỉ có 1 `DotConfig`; muốn nhiều DOT độc lập cần sửa bộ xử lý DOT (không có trong gói).
- Hook hit/kill/damage vào code damage thật (mục 8).

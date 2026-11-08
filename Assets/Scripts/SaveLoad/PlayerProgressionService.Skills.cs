using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phần hệ thống skill của PlayerProgressionService.
/// Quy ước định danh:
///  - skillId  : khoá chuẩn của skill, dùng để lưu save (unlockedSkills, equippedSkills[].skillName, cooldown).
///  - tên hiển thị (SkillConfigEntry.name) chỉ để hiển thị; mọi hàm public vẫn nhận cả hai để tương thích UI cũ.
///  - actionId : tên class thực thi trong SkillActions (nhiều skillId dùng chung được).
/// </summary>
public partial class PlayerProgressionService
{
    private const string UnarmedWeaponId = "Punch";
    private const string LegacyBasicSlashName = "Chém cơ bản";
    private const float BaseCriticalChance = 0f;
    private const float BaseCriticalMultiplier = 1.5f;

    private static readonly string[] SkillInputKeys =
    {
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "space"
    };

    // Phím người chơi được phép gán cho skill: 1-9, a-z, space
    private static readonly string[] AllowedSkillInputKeys = BuildAllowedSkillInputKeys();

    public static IReadOnlyList<string> AllowedKeys => AllowedSkillInputKeys;

    private RuntimeStatModifierStore statStore;
    private GameObject skillComponentsOwner;
    private bool statsDirty;
    private string lastWatchedWeapon;
    private int skillStateVersion;

    /// <summary>Phát ra khi một skill vừa được mở khóa lúc đang chơi (lên level). Tham số: skillId.</summary>
    public event Action<string> SkillUnlocked;

    /// <summary>Phát ra (tối đa 1 lần/frame) khi chỉ số thay đổi do buff/passive. UI có thể cập nhật chỉ số.</summary>
    public event Action StatsChanged;

    /// <summary>Phát ra khi skill bị tự gỡ khỏi loadout vì không còn hợp điều kiện vũ khí. Tham số: skillId, lý do.</summary>
    public event Action<string, string> SkillAutoUnequipped;

    /// <summary>
    /// Tăng mỗi khi danh sách skill đã mở khóa hoặc điều kiện vũ khí thay đổi. PlayerPassiveSkillController
    /// và UI so sánh số này để biết khi nào cần lấy lại dữ liệu từ service.
    /// </summary>
    public int SkillStateVersion => skillStateVersion;

    /// <summary>Bộ lưu modifier trên Player (null nếu chưa tìm thấy Player).</summary>
    public RuntimeStatModifierStore StatStore => statStore;

    /// <summary>SkillConfig đang dùng (Inspector -> Resources/SkillConfig -> bản mặc định trong bộ nhớ).</summary>
    public SkillConfig SkillConfigAsset => ResolveSkillConfig();

    // ------------------------------------------------------------------ Phím skill

    private static string[] BuildAllowedSkillInputKeys()
    {
        List<string> keys = new List<string>();
        for (char c = '1'; c <= '9'; c++) keys.Add(c.ToString());
        for (char c = 'a'; c <= 'z'; c++) keys.Add(c.ToString());
        keys.Add("space");
        return keys.ToArray();
    }

    /// <summary>Chuẩn hoá chữ người chơi nhập thành tên phím hợp lệ; trả về "" nếu không hợp lệ.</summary>
    public static string ParseSkillInputKey(string raw)
    {
        if (raw == " ") return "space";
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        string candidate = raw.Trim().ToLowerInvariant();
        return Array.IndexOf(AllowedSkillInputKeys, candidate) >= 0 ? candidate : string.Empty;
    }

    private static string NormalizeSkillInputKey(string inputKey)
    {
        if (string.IsNullOrWhiteSpace(inputKey)) return string.Empty;
        string candidate = inputKey.Trim().ToLowerInvariant();
        return Array.IndexOf(AllowedSkillInputKeys, candidate) >= 0 ? candidate : string.Empty;
    }

    private static string FindAvailableSkillInputKey(HashSet<string> usedKeys)
    {
        foreach (string key in SkillInputKeys)
            if (usedKeys == null || !usedKeys.Contains(key)) return key;
        return string.Empty;
    }

    // ------------------------------------------------------------------ Định danh skill

    /// <summary>Tìm entry theo skillId hoặc tên hiển thị. Null nếu SkillConfig không có.</summary>
    public SkillConfigEntry ResolveSkillEntry(string skillIdOrName)
    {
        return string.IsNullOrWhiteSpace(skillIdOrName) ? null : ResolveSkillConfig().FindEntry(skillIdOrName);
    }

    /// <summary>skillId chuẩn nếu SkillConfig có skill đó; nếu không giữ nguyên chuỗi (đã Trim) để không mất dữ liệu.</summary>
    public string ResolveSkillId(string skillIdOrName)
    {
        if (string.IsNullOrWhiteSpace(skillIdOrName)) return string.Empty;
        string key = skillIdOrName.Trim();
        SkillConfigEntry entry = ResolveSkillConfig().FindEntry(key);
        return entry != null && !string.IsNullOrWhiteSpace(entry.skillId) ? entry.skillId.Trim() : key;
    }

    /// <summary>Tên hiển thị của skill (dùng cho UI). Không tìm thấy thì trả lại chuỗi đầu vào.</summary>
    public string GetSkillDisplayName(string skillIdOrName)
    {
        SkillConfigEntry entry = ResolveSkillEntry(skillIdOrName);
        if (entry != null && !string.IsNullOrWhiteSpace(entry.name)) return entry.name.Trim();
        return string.IsNullOrWhiteSpace(skillIdOrName) ? string.Empty : skillIdOrName.Trim();
    }

    public bool TryGetSkillType(string skillIdOrName, out SkillType type)
    {
        SkillConfigEntry entry = ResolveSkillEntry(skillIdOrName);
        type = entry != null ? entry.type : SkillType.Active;
        return entry != null;
    }

    /// <summary>
    /// ID vũ khí đang cầm = equippedItemName nếu đó là vũ khí; tay không hoặc đang trang bị món không phải vũ khí
    /// (giáp, bùa...) = "Punch". SkillConfigEntry.weaponId và weaponVariants[].weaponId phải khớp đúng chuỗi này.
    /// </summary>
    public string GetCurrentWeaponId()
    {
        string weapon = Data != null ? Data.equippedItemName : null;
        if (string.IsNullOrWhiteSpace(weapon)
            || weapon.Equals("none", StringComparison.OrdinalIgnoreCase)
            || weapon.Equals("null", StringComparison.OrdinalIgnoreCase))
            return UnarmedWeaponId;

        ItemDefinition definition = ItemDefinition.Find(weapon);
        if (definition != null && definition.category != ItemCategory.Weapon) return UnarmedWeaponId;
        return weapon.Trim();
    }

    /// <summary>Loại vũ khí đang cầm (ItemDefinition.weaponType); tay không = Unarmed.</summary>
    public WeaponType GetCurrentWeaponType()
    {
        return ItemDefinition.ResolveWeaponType(Data != null ? Data.equippedItemName : null);
    }

    // ------------------------------------------------------------------ Điều kiện vũ khí của skill

    /// <summary>Vũ khí đang cầm có thỏa điều kiện vũ khí của skill không. Skill không có điều kiện (hoặc không có trong SkillConfig) = true.</summary>
    public bool IsWeaponRequirementMet(string skillIdOrName)
    {
        return IsWeaponRequirementMet(ResolveSkillEntry(skillIdOrName));
    }

    public bool IsWeaponRequirementMet(SkillConfigEntry entry)
    {
        if (entry == null || entry.weaponRequirement == null || !entry.weaponRequirement.HasRequirement) return true;
        return entry.weaponRequirement.IsSatisfiedBy(GetCurrentWeaponType(), GetCurrentWeaponId());
    }

    /// <summary>Mô tả điều kiện vũ khí để hiển thị, ví dụ "Dao găm, Kiếm". Rỗng nếu skill không yêu cầu vũ khí.</summary>
    public string GetWeaponRequirementText(string skillIdOrName)
    {
        SkillConfigEntry entry = ResolveSkillEntry(skillIdOrName);
        return entry != null && entry.weaponRequirement != null ? entry.weaponRequirement.Describe() : string.Empty;
    }

    /// <summary>
    /// Gọi mỗi khi vũ khí đang cầm đổi (trang bị, gỡ, đổi, chuyển vào rương, mất vật phẩm...). Đã được gọi sẵn trong
    /// ToggleEquipAtSlot / UnequipItem / RemoveItem / StoreAll / RevalidateEquipment; LateUpdate cũng tự phát hiện
    /// trường hợp code khác sửa Data.equippedItemName trực tiếp.
    /// saveAfter = true: tự lưu nếu có skill bị gỡ (dùng khi người gọi không tự Save).
    /// </summary>
    private void NotifyWeaponChanged(bool saveAfter)
    {
        lastWatchedWeapon = Data != null ? (Data.equippedItemName ?? string.Empty) : string.Empty;
        skillStateVersion++;
        SyncPlayerWeaponVisuals();
        RevalidateEquippedSkills(saveAfter);
    }

    /// <summary>
    /// Gỡ khỏi loadout mọi skill không còn thỏa điều kiện vũ khí, đồng bộ lại PlayerSkillController (+ passive) và lưu.
    /// Trả về số skill bị gỡ.
    /// </summary>
    public int RevalidateEquippedSkills(bool saveAfter)
    {
        if (Data == null || Data.equippedSkills == null) return 0;

        List<EquippedSkillData> invalid = null;
        foreach (EquippedSkillData equipped in Data.equippedSkills)
        {
            if (equipped == null) continue;
            SkillConfigEntry entry = ResolveSkillEntry(equipped.skillName);
            if (entry == null || IsWeaponRequirementMet(entry)) continue;
            if (invalid == null) invalid = new List<EquippedSkillData>();
            invalid.Add(equipped);
        }
        if (invalid == null) return 0;

        foreach (EquippedSkillData equipped in invalid)
        {
            Data.equippedSkills.Remove(equipped);
            string reason = "Cần cầm: " + GetWeaponRequirementText(equipped.skillName);
            Debug.Log($"[PlayerProgressionService] Tự gỡ '{equipped.skillName}' khỏi loadout. {reason}", this);
            SkillAutoUnequipped?.Invoke(equipped.skillName, reason);
        }

        skillStateVersion++;
        SyncPlayerSkillControllers();
        if (saveAfter) Save();
        return invalid.Count;
    }

    private void WatchWeaponChange()
    {
        if (Data == null) return;

        // Vật phẩm đang trang bị không còn trong túi (code khác lấy đi mà không báo): coi như đã mất vũ khí.
        if (!string.IsNullOrEmpty(Data.equippedItemName) && Data.GetItemAmount(Data.equippedItemName) <= 0)
        {
            RevalidateEquipment();
            return;
        }

        string weapon = Data.equippedItemName ?? string.Empty;
        if (string.Equals(weapon, lastWatchedWeapon, StringComparison.Ordinal)) return;
        NotifyWeaponChanged(true);
    }

    private static bool ListContainsIgnoreCase(List<string> list, string value)
    {
        if (list == null || string.IsNullOrWhiteSpace(value)) return false;
        return list.Exists(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------ Mở khóa

    /// <summary>Đã mở khóa chưa. Nhận skillId hoặc tên hiển thị, kể cả khi save đang lưu dạng cũ.</summary>
    public bool HasUnlockedSkill(string skillIdOrName)
    {
        if (string.IsNullOrWhiteSpace(skillIdOrName) || Data == null || Data.unlockedSkills == null)
            return false;

        string key = skillIdOrName.Trim();
        if (ListContainsIgnoreCase(Data.unlockedSkills, key)) return true;

        SkillConfigEntry entry = ResolveSkillEntry(key);
        if (entry == null) return false;
        return ListContainsIgnoreCase(Data.unlockedSkills, entry.skillId)
            || ListContainsIgnoreCase(Data.unlockedSkills, entry.name);
    }

    /// <summary>
    /// Đổi chỗ 2 kỹ năng trong danh sách đã mở khóa; thả vào ô trống thì đưa xuống cuối danh sách.
    /// </summary>
    public bool MoveUnlockedSkill(int fromIndex, int toIndex)
    {
        List<string> skills = Data != null ? Data.unlockedSkills : null;
        if (skills == null || fromIndex < 0 || fromIndex >= skills.Count || toIndex < 0) return false;
        if (fromIndex == toIndex) return true;

        if (toIndex >= skills.Count)
        {
            string moved = skills[fromIndex];
            skills.RemoveAt(fromIndex);
            skills.Add(moved);
        }
        else
        {
            string temp = skills[fromIndex];
            skills[fromIndex] = skills[toIndex];
            skills[toIndex] = temp;
        }

        Save();
        return true;
    }

    /// <summary>
    /// Skill có hiện trong tab Kỹ năng của Inventory không: chỉ skill kích hoạt chủ động (Active, BuffAction).
    /// Passive/PassiveBuff xem ở PlayerPassiveSkillController; BasicAttack tự đổi theo vũ khí nên không cần trang bị.
    /// Skill chưa có trong SkillConfig vẫn hiện để dễ phát hiện lỗi cấu hình.
    /// </summary>
    public bool IsInventorySkill(string skillIdOrName)
    {
        SkillConfigEntry entry = ResolveSkillEntry(skillIdOrName);
        return entry == null || entry.type == SkillType.Active || entry.type == SkillType.BuffAction;
    }

    /// <summary>Điền results bằng các skill đã mở khóa hiển thị trong Inventory (đúng thứ tự trong save). Dùng lại list để tránh cấp phát.</summary>
    public void CollectInventorySkillIds(List<string> results)
    {
        if (results == null) return;
        results.Clear();
        if (Data == null || Data.unlockedSkills == null) return;
        foreach (string unlocked in Data.unlockedSkills)
            if (!string.IsNullOrWhiteSpace(unlocked) && IsInventorySkill(unlocked)) results.Add(unlocked);
    }

    /// <summary>
    /// Đổi chỗ 2 skill trong danh sách hiển thị của Inventory (chỉ số theo danh sách đã lọc);
    /// thả vào ô trống thì đưa xuống cuối. Passive nằm xen kẽ trong save không bị ảnh hưởng.
    /// </summary>
    public bool MoveInventorySkill(int fromIndex, int toIndex)
    {
        List<string> skills = Data != null ? Data.unlockedSkills : null;
        if (skills == null || fromIndex < 0 || toIndex < 0) return false;

        List<int> rawIndexes = new List<int>();
        for (int i = 0; i < skills.Count; i++)
            if (!string.IsNullOrWhiteSpace(skills[i]) && IsInventorySkill(skills[i])) rawIndexes.Add(i);
        if (fromIndex >= rawIndexes.Count) return false;
        if (fromIndex == toIndex) return true;

        int rawFrom = rawIndexes[fromIndex];
        if (toIndex >= rawIndexes.Count)
        {
            string moved = skills[rawFrom];
            skills.RemoveAt(rawFrom);
            skills.Add(moved);
        }
        else
        {
            int rawTo = rawIndexes[toIndex];
            string temp = skills[rawFrom];
            skills[rawFrom] = skills[rawTo];
            skills[rawTo] = temp;
        }

        Save();
        return true;
    }

    /// <summary>
    /// Mở khóa skill theo cấu hình LvConfig của một level. LvConfig có thể chứa skillId (chuẩn mới)
    /// hoặc tên hiển thị cũ; kết quả luôn được lưu dưới dạng skillId nếu SkillConfig nhận ra.
    /// </summary>
    private void UnlockSkillsForLevel(int level, bool notify)
    {
        if (Data == null) return;
        if (Data.unlockedSkills == null) Data.unlockedSkills = new List<string>();

        LvConfig config = ResolveLvConfig();
        string[] skillsToUnlock = config != null ? config.GetSkillsForLevel(level) : Array.Empty<string>();
        if (skillsToUnlock == null) return;

        foreach (string skill in skillsToUnlock)
        {
            if (string.IsNullOrWhiteSpace(skill)) continue;

            string skillId = ResolveSkillId(skill);
            if (HasUnlockedSkill(skillId)) continue;

            Data.unlockedSkills.Add(skillId);
            skillStateVersion++;
            if (ResolveSkillEntry(skillId) == null)
                Debug.LogWarning($"[PlayerProgressionService] LvConfig mở khóa '{skill}' ở level {level} nhưng SkillConfig không có skill này. Kiểm tra skillId trong LvConfig.", this);
            if (notify) SkillUnlocked?.Invoke(skillId);
        }
    }

    /// <summary>
    /// Chạy mỗi lần nạp save: đổi tên hiển thị cũ sang skillId, bỏ trùng, dọn cooldown cũ lưu theo actionId.
    /// Không bao giờ xoá skill chỉ vì SkillConfig tạm thời không nhận ra nó.
    /// </summary>
    private void MigrateSkillSaveData()
    {
        if (Data == null) return;
        if (Data.unlockedSkills == null) Data.unlockedSkills = new List<string>();
        SkillConfig config = ResolveSkillConfig();

        // Giữ hành vi cũ: "Chém cơ bản" không nằm trong danh sách mở khóa (LvConfig sẽ thêm lại nếu cấu hình).
        string legacyId = ResolveSkillId(LegacyBasicSlashName);
        Data.unlockedSkills.RemoveAll(skill =>
            string.Equals(skill, LegacyBasicSlashName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(skill, legacyId, StringComparison.OrdinalIgnoreCase));

        List<string> migrated = new List<string>();
        foreach (string raw in Data.unlockedSkills)
        {
            string key = ResolveSkillId(raw);
            if (key.Length == 0 || ListContainsIgnoreCase(migrated, key)) continue;
            migrated.Add(key);
        }
        Data.unlockedSkills = migrated;

        // Save cũ lưu cooldown theo actionId (vd "NormalAttack"): không khớp skillId nào thì bỏ.
        if (Data.skillCooldowns != null)
        {
            Data.skillCooldowns.RemoveAll(entry => entry != null
                && (entry.ownerId == PlayerSkillController.CooldownOwnerId
                    || entry.ownerId == PlayerBasicAttackController.CooldownOwnerId)
                && config.GetEntryById(entry.skillId) == null);
        }
    }

    // ------------------------------------------------------------------ Tra cứu dữ liệu skill

    /// <summary>SkillData (bảng cũ) của skill, nhận skillId hoặc tên hiển thị.</summary>
    public SkillData GetSkillDefinition(string skillIdOrName)
    {
        if (string.IsNullOrWhiteSpace(skillIdOrName)) return null;
        return ResolveSkillConfig().GetSkill(skillIdOrName);
    }

    /// <summary>Icon riêng của skill, hoặc fallback được gán trong SkillConfig.</summary>
    public Sprite GetSkillIcon(string skillIdOrName)
    {
        SkillConfigEntry entry = ResolveSkillEntry(skillIdOrName);
        return ResolveSkillConfig().GetIcon(entry);
    }

    /// <summary>Entry của skill theo skillId (hoặc tên). Null nếu không có trong SkillConfig.</summary>
    public SkillConfigEntry GetSkillEntryById(string skillId)
    {
        return ResolveSkillEntry(skillId);
    }

    /// <summary>Entry đầy đủ của skill theo tên hiển thị (hoặc skillId).</summary>
    public SkillConfigEntry GetSkillEntry(string skillName)
    {
        return ResolveSkillEntry(skillName);
    }

    /// <summary>
    /// Animation (charge/attack/end) của skill theo skillId + vũ khí, lấy từ SkillConfig.
    /// Trả về null nếu skill không có trong SkillConfig.
    /// </summary>
    public SkillAnimationSet GetSkillAnimationSet(string skillId, string weaponName)
    {
        SkillConfig config = ResolveSkillConfig();
        SkillConfigEntry entry = config.FindEntry(skillId);
        return entry != null ? config.GetAnimationSet(entry.skillId, weaponName) : null;
    }

    /// <summary>
    /// Tạo SkillContext cho 1 skill theo skillId (vd "BasicAttack_Dagger") + vũ khí.
    /// Thông số lấy từ SkillConfig (ưu tiên weaponVariants khớp vũ khí, sau đó defaultContext);
    /// Damage = ATK hiện tại (level + vũ khí + buff) x damageMultiplier của skill.
    /// Trả về null nếu skill không có trong SkillConfig. Luôn trả về instance mới;
    /// người gọi tự điền Caster/Position/Direction.
    /// </summary>
    public SkillContext GetSkillContext(string skillId, string weaponName)
    {
        SkillConfig config = ResolveSkillConfig();
        SkillConfigEntry entry = config.FindEntry(skillId);
        if (entry == null) return null;

        SkillContextConfig contextConfig = config.GetContextConfig(entry.skillId, weaponName);
        if (contextConfig == null) return null;

        return new SkillContext
        {
            Damage = GetAttackPowerExact() * contextConfig.damageMultiplier,
            Size = contextConfig.skillSize,
            Range = contextConfig.range,
            Speed = contextConfig.speed,
            Duration = contextConfig.duration,
            StunTime = contextConfig.stunTime,
            ChargeTime = contextConfig.chargeTime,
            EndTime = contextConfig.endTime,
            CanPierce = contextConfig.canPierce,
            Debug = contextConfig.debug,
            DotConfig = contextConfig.dotConfig != null ? contextConfig.dotConfig : new DameDotConfig(),
            ActionDamageOverrides = contextConfig != null ? contextConfig.actionDamageOverrides : null,
            BreaksGuard = contextConfig != null && contextConfig.breaksGuard,
            BypassesMeleeResistance = contextConfig != null && contextConfig.bypassesMeleeResistance,
            Modules = contextConfig.modules,
            EffectAnimation = contextConfig.effectAnimation,
            AnimationName = contextConfig.animationName,
            SkillId = entry.skillId,
            ActionId = entry.actionId,
            WeaponId = string.IsNullOrWhiteSpace(weaponName) ? GetCurrentWeaponId() : weaponName.Trim()
        };
    }

    // ------------------------------------------------------------------ Trang bị skill

    public EquippedSkillData GetEquippedSkillAtSlot(int slotIndex)
    {
        if (Data == null || Data.equippedSkills == null) return null;
        return Data.equippedSkills.Find(skill => skill != null && skill.slotIndex == slotIndex);
    }

    private EquippedSkillData FindEquipped(string skillKey)
    {
        if (Data == null || Data.equippedSkills == null || string.IsNullOrWhiteSpace(skillKey)) return null;
        return Data.equippedSkills.Find(skill => skill != null
            && string.Equals(skill.skillName, skillKey, StringComparison.OrdinalIgnoreCase));
    }

    public int GetEquippedSkillIndex(string skillIdOrName)
    {
        if (string.IsNullOrWhiteSpace(skillIdOrName)) return -1;
        EquippedSkillData equipped = FindEquipped(ResolveSkillId(skillIdOrName));
        return equipped != null ? equipped.slotIndex : -1;
    }

    public int GetEquippedSkillCount()
    {
        return Data != null && Data.equippedSkills != null ? Data.equippedSkills.Count : 0;
    }

    public bool CanEquipSkill(string skillIdOrName)
    {
        return GetEquipBlockReason(skillIdOrName) == null;
    }

    /// <summary>Trả về null nếu equip được; ngược lại là lý do không equip được (hiển thị trên UI).</summary>
    public string GetEquipBlockReason(string skillIdOrName)
    {
        if (!HasUnlockedSkill(skillIdOrName)) return "Kỹ năng chưa được mở khóa.";

        SkillConfigEntry entry = ResolveSkillEntry(skillIdOrName);
        if (entry == null)
            return $"Không tìm thấy '{skillIdOrName}' trong SkillConfig (nhập skillId hoặc tên hiển thị khớp từng chữ).";

        switch (entry.type)
        {
            case SkillType.BasicAttack:
                return "Đánh thường tự đổi theo vũ khí đang cầm, không cần trang bị.";
            case SkillType.Passive:
            case SkillType.PassiveBuff:
                return "Kỹ năng bị động luôn có hiệu lực khi đã mở khóa, không cần trang bị.";
        }

        bool needsAction = entry.type != SkillType.BuffAction || entry.executeActionAfterBuff;
        if (needsAction)
        {
            if (string.IsNullOrWhiteSpace(entry.actionId))
                return $"Skill '{entry.skillId}' trong SkillConfig chưa có actionId.";
            if (SkillActions.Instance == null)
                return "Không có SkillActions trong scene.";
            if (!SkillActions.Instance.HasRegisteredSkill(entry.actionId))
                return $"actionId '{entry.actionId}' chưa được đăng ký trong SkillActions.";
        }
        else if (entry.buffConfig == null || entry.buffConfig.modifiers == null || entry.buffConfig.modifiers.Count == 0)
        {
            return $"BuffAction '{entry.skillId}' chưa có modifier trong buffConfig.";
        }

        if (!IsWeaponRequirementMet(entry))
            return "Cần cầm: " + GetWeaponRequirementText(entry.skillId) + " để dùng kỹ năng này.";
        return null;
    }

    /// <summary>
    /// Đưa skill vào đúng ô trang bị (kéo thả).
    /// - Skill chưa trang bị: vào ô đó; nếu ô đang có skill khác thì skill mới thay chỗ và dùng phím của skill cũ.
    /// - Skill đã trang bị: chuyển sang ô đó; nếu ô đó có skill khác thì hai skill đổi chỗ cho nhau.
    /// </summary>
    public bool EquipSkillAtSlot(string skillName, int targetSlot, out string message)
    {
        message = null;
        if (Data == null || string.IsNullOrWhiteSpace(skillName)
            || targetSlot < 0 || targetSlot >= PlayerSaveData.EquippedSkillCapacity)
        {
            message = "Ô kỹ năng không hợp lệ.";
            return false;
        }
        if (Data.equippedSkills == null) Data.equippedSkills = new List<EquippedSkillData>();

        string name = ResolveSkillId(skillName);
        EquippedSkillData existing = FindEquipped(name);
        EquippedSkillData occupant = Data.equippedSkills.Find(skill => skill != null && skill.slotIndex == targetSlot);

        if (existing == null)
        {
            string block = GetEquipBlockReason(name);
            if (block != null) { message = block; return false; }

            string key;
            if (occupant != null)
            {
                key = occupant.inputKey;
                Data.equippedSkills.Remove(occupant);
            }
            else
            {
                HashSet<string> usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (EquippedSkillData skill in Data.equippedSkills)
                    if (skill != null && !string.IsNullOrWhiteSpace(skill.inputKey)) usedKeys.Add(skill.inputKey);
                key = FindAvailableSkillInputKey(usedKeys);
            }

            if (string.IsNullOrWhiteSpace(key)) { message = "Không còn phím trống cho kỹ năng này."; return false; }
            Data.equippedSkills.Add(new EquippedSkillData { slotIndex = targetSlot, skillName = name, inputKey = key });
        }
        else if (existing != occupant)
        {
            int oldSlot = existing.slotIndex;
            existing.slotIndex = targetSlot;
            if (occupant != null) occupant.slotIndex = oldSlot; // hoán đổi chỗ
        }
        else
        {
            return true; // thả lại đúng ô cũ
        }

        NormalizeEquippedSkills();
        SyncPlayerSkillControllers();
        Save();
        return true;
    }

    public bool EquipSkill(string skillName)
    {
        if (!CanEquipSkill(skillName)) return false;
        if (GetEquippedSkillIndex(skillName) >= 0) return true;
        if (Data.equippedSkills == null) Data.equippedSkills = new List<EquippedSkillData>();
        if (Data.equippedSkills.Count >= PlayerSaveData.EquippedSkillCapacity) return false;

        bool[] occupiedSlots = new bool[PlayerSaveData.EquippedSkillCapacity];
        HashSet<string> usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EquippedSkillData entry in Data.equippedSkills)
        {
            if (entry == null) continue;
            if (entry.slotIndex >= 0 && entry.slotIndex < occupiedSlots.Length)
                occupiedSlots[entry.slotIndex] = true;
            if (!string.IsNullOrWhiteSpace(entry.inputKey)) usedKeys.Add(entry.inputKey);
        }

        int freeSlot = Array.FindIndex(occupiedSlots, occupied => !occupied);
        string inputKey = FindAvailableSkillInputKey(usedKeys);
        if (freeSlot < 0 || string.IsNullOrWhiteSpace(inputKey)) return false;

        Data.equippedSkills.Add(new EquippedSkillData
        {
            slotIndex = freeSlot,
            skillName = ResolveSkillId(skillName),
            inputKey = inputKey
        });
        NormalizeEquippedSkills();
        SyncPlayerSkillControllers();
        Save();
        return true;
    }

    public bool UnequipSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName) || Data == null || Data.equippedSkills == null)
            return false;
        string key = ResolveSkillId(skillName);
        int removed = Data.equippedSkills.RemoveAll(skill => skill != null
            && string.Equals(skill.skillName, key, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return false;
        SyncPlayerSkillControllers();
        Save();
        return true;
    }

    public bool CycleEquippedSkillKey(string skillName)
    {
        int slotIndex = GetEquippedSkillIndex(skillName);
        EquippedSkillData equipped = GetEquippedSkillAtSlot(slotIndex);
        if (equipped == null) return false;

        string currentKey = NormalizeSkillInputKey(equipped.inputKey);
        int currentIndex = Array.IndexOf(SkillInputKeys, currentKey);
        for (int offset = 1; offset <= SkillInputKeys.Length; offset++)
        {
            int candidateIndex = (currentIndex + offset + SkillInputKeys.Length) % SkillInputKeys.Length;
            string candidate = SkillInputKeys[candidateIndex];
            bool usedByAnotherSkill = Data.equippedSkills.Exists(skill => skill != null
                && skill.slotIndex != slotIndex
                && string.Equals(NormalizeSkillInputKey(skill.inputKey), candidate, StringComparison.OrdinalIgnoreCase));
            if (usedByAnotherSkill) continue;

            equipped.inputKey = candidate;
            SyncPlayerSkillControllers();
            Save();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Đổi phím của skill đang trang bị. Chỉ nhận 1-9, a-z, space.
    /// Nếu phím đã dùng cho skill khác hoặc không hợp lệ thì giữ phím cũ, trả về false kèm message.
    /// </summary>
    public bool TrySetEquippedSkillKey(string skillName, string rawKey, out string message)
    {
        message = null;
        int slotIndex = GetEquippedSkillIndex(skillName);
        EquippedSkillData equipped = GetEquippedSkillAtSlot(slotIndex);
        if (equipped == null)
        {
            message = "Kỹ năng chưa được trang bị.";
            return false;
        }

        string key = ParseSkillInputKey(rawKey);
        if (key.Length == 0)
        {
            message = "Phím chỉ được là 1-9, A-Z hoặc SPACE. Giữ phím cũ.";
            return false;
        }

        if (string.Equals(NormalizeSkillInputKey(equipped.inputKey), key, StringComparison.OrdinalIgnoreCase))
            return true;

        EquippedSkillData other = Data.equippedSkills.Find(skill => skill != null
            && skill.slotIndex != slotIndex
            && string.Equals(NormalizeSkillInputKey(skill.inputKey), key, StringComparison.OrdinalIgnoreCase));
        if (other != null)
        {
            message = $"Phím {key.ToUpperInvariant()} đã dùng cho '{GetSkillDisplayName(other.skillName)}'. Giữ phím cũ.";
            return false;
        }

        equipped.inputKey = key;
        SyncPlayerSkillControllers();
        Save();
        return true;
    }

    private static bool IsEquippableSkillType(SkillConfigEntry entry)
    {
        // Entry không có trong SkillConfig: giữ nguyên (không xoá dữ liệu chỉ vì config tạm thời thiếu).
        return entry == null
            || (entry.type != SkillType.BasicAttack && entry.type != SkillType.Passive && entry.type != SkillType.PassiveBuff);
    }

    private void NormalizeEquippedSkills()
    {
        if (Data == null) return;
        if (Data.equippedSkills == null) Data.equippedSkills = new List<EquippedSkillData>();

        EquippedSkillData[] slots = new EquippedSkillData[PlayerSaveData.EquippedSkillCapacity];
        HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EquippedSkillData entry in Data.equippedSkills)
        {
            string key = entry != null ? ResolveSkillId(entry.skillName) : string.Empty;
            if (entry == null || entry.slotIndex < 0 || entry.slotIndex >= slots.Length
                || slots[entry.slotIndex] != null || key.Length == 0
                || !HasUnlockedSkill(key) || !IsEquippableSkillType(ResolveSkillEntry(key))
                || !usedNames.Add(key))
                continue;
            slots[entry.slotIndex] = new EquippedSkillData
            {
                slotIndex = entry.slotIndex, skillName = key, inputKey = entry.inputKey
            };
        }

        foreach (EquippedSkillData entry in Data.equippedSkills)
        {
            string key = entry != null ? ResolveSkillId(entry.skillName) : string.Empty;
            if (entry == null || key.Length == 0
                || !HasUnlockedSkill(key) || !IsEquippableSkillType(ResolveSkillEntry(key))
                || usedNames.Contains(key))
                continue;
            int freeSlot = Array.FindIndex(slots, slot => slot == null);
            if (freeSlot < 0) break;
            usedNames.Add(key);
            slots[freeSlot] = new EquippedSkillData
            {
                slotIndex = freeSlot, skillName = key, inputKey = entry.inputKey
            };
        }

        HashSet<string> usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Data.equippedSkills = new List<EquippedSkillData>();
        for (int i = 0; i < slots.Length; i++)
        {
            EquippedSkillData entry = slots[i];
            if (entry == null) continue;
            string key = NormalizeSkillInputKey(entry.inputKey);
            if (string.IsNullOrEmpty(key) || usedKeys.Contains(key))
                key = FindAvailableSkillInputKey(usedKeys);
            entry.inputKey = key;
            usedKeys.Add(key);
            Data.equippedSkills.Add(entry);
        }
    }

    // ------------------------------------------------------------------ Đồng bộ controller trên Player

    /// <summary>Đổi GroupBlock vũ khí và đòn đánh thường theo vật phẩm đang trang bị.</summary>
    private void SyncPlayerWeaponVisuals()
    {
        if (player == null) FindPlayer();
        if (player == null || Data == null) return;
        var weaponIds = new List<string>();
        foreach (var item in ItemDefinition.GetAll())
            if (item != null && item.category == ItemCategory.Weapon) weaponIds.Add(item.itemName);
        string weaponId = GetCurrentWeaponType() == WeaponType.Unarmed ? string.Empty : GetCurrentWeaponId();
        foreach (var animation in player.GetComponentsInChildren<CharacterAnimation>(true))
            animation.SyncEquippedWeapon(weaponId, weaponIds);
        foreach (var basic in player.GetComponentsInChildren<PlayerBasicAttackController>(true))
            basic.RefreshEquippedWeapon();
    }

    /// <summary>Đẩy skill đã trang bị và passive đã mở khóa sang controller trên Player.</summary>
    private void SyncPlayerSkillControllers()
    {
        if (player == null) FindPlayer();
        if (player == null || Data == null) return;

        SkillTable table = GetSkillTable();
        PlayerSkillController[] controllers = player.GetComponentsInChildren<PlayerSkillController>(true);
        foreach (PlayerSkillController controller in controllers)
            if (controller != null) controller.SyncEquippedSkills(Data, table);

        SyncPlayerWeaponVisuals();
        SyncPassiveSkills();
    }

    /// <summary>
    /// Báo PlayerPassiveSkillController lấy lại danh sách passive từ service (controller tự gọi
    /// GetUnlockedPassiveSkillIds / IsWeaponRequirementMet rồi bật/tắt phần chênh lệch).
    /// Thường không cần gọi tay: controller tự phát hiện thay đổi qua SkillStateVersion.
    /// </summary>
    public void SyncPassiveSkills()
    {
        if (Data == null) return;
        skillStateVersion++;
        if (player == null) FindPlayer();
        if (player == null) return;

        PlayerPassiveSkillController[] controllers = player.GetComponentsInChildren<PlayerPassiveSkillController>(true);
        foreach (PlayerPassiveSkillController controller in controllers)
            if (controller != null) controller.RefreshFromProgression();
    }

    /// <summary>skillId của mọi Passive/PassiveBuff đã mở khóa (UI có thể dùng để liệt kê).</summary>
    public List<string> GetUnlockedPassiveSkillIds()
    {
        List<string> ids = new List<string>();
        if (Data == null || Data.unlockedSkills == null) return ids;

        foreach (string unlocked in Data.unlockedSkills)
        {
            SkillConfigEntry entry = ResolveSkillEntry(unlocked);
            if (entry != null && !string.IsNullOrWhiteSpace(entry.skillId)
                && (entry.type == SkillType.Passive || entry.type == SkillType.PassiveBuff))
                ids.Add(entry.skillId.Trim());
        }
        return ids;
    }

    /// <summary>
    /// Tìm (hoặc tự thêm nếu autoAttachSkillComponents bật) RuntimeStatModifierStore, SelfBuff,
    /// PlayerPassiveSkillController trên Player. Chỉ chạy 1 lần cho mỗi instance Player.
    /// </summary>
    private void EnsurePlayerSkillComponents()
    {
        if (player == null || skillComponentsOwner == player) return;
        skillComponentsOwner = player;

        if (statStore != null) statStore.Changed -= OnStatModifiersChanged;
        statStore = player.GetComponentInChildren<RuntimeStatModifierStore>(true);
        if (statStore == null && autoAttachSkillComponents)
        {
            statStore = player.AddComponent<RuntimeStatModifierStore>();
            Debug.Log("[PlayerProgressionService] Đã tự thêm RuntimeStatModifierStore lên Player.", this);
        }
        if (statStore != null) statStore.Changed += OnStatModifiersChanged;

        if (!autoAttachSkillComponents) return;

        // Đánh thường là lối chơi cốt lõi: thiếu controller thì Player không đánh được, nên tự thêm (đặt cạnh PlayerSkillController nếu có).
        if (player.GetComponentInChildren<PlayerBasicAttackController>(true) == null)
        {
            PlayerSkillController sibling = player.GetComponentInChildren<PlayerSkillController>(true);
            GameObject host = sibling != null ? sibling.gameObject : player;
            host.AddComponent<PlayerBasicAttackController>();
            Debug.Log("[PlayerProgressionService] Đã tự thêm PlayerBasicAttackController (đánh thường theo vũ khí) lên '" + host.name + "'.", this);
        }
        if (player.GetComponentInChildren<SelfBuff>(true) == null)
        {
            player.AddComponent<SelfBuff>();
            Debug.Log("[PlayerProgressionService] Đã tự thêm SelfBuff lên Player.", this);
        }
        if (player.GetComponentInChildren<PlayerPassiveSkillController>(true) == null)
        {
            player.AddComponent<PlayerPassiveSkillController>();
            Debug.Log("[PlayerProgressionService] Đã tự thêm PlayerPassiveSkillController lên Player.", this);
        }
    }

    private void ReleaseSkillComponents()
    {
        if (statStore != null) statStore.Changed -= OnStatModifiersChanged;
        statStore = null;
        skillComponentsOwner = null;
    }

    // ------------------------------------------------------------------ Modifier chỉ số

    /// <summary>
    /// Áp mọi modifier đang có (buff, passive) lên một chỉ số gốc:
    /// (base + tổng Flat) x (1 + tổng AddPercent) x tích Multiply.
    /// Dùng cho chỉ số do script khác quản lý, ví dụ tốc độ chạy:
    /// moveSpeed = PlayerProgressionService.Instance.ModifyStat(PlayerStatType.MoveSpeed, baseMoveSpeed).
    /// </summary>
    public float ModifyStat(PlayerStatType stat, float baseValue)
    {
        RuntimeStatModifierStore store = statStore;
        return store != null ? store.GetFinalValue(stat, baseValue) : baseValue;
    }

    /// <summary>Tỉ lệ chí mạng 0..1 (gốc 0, cộng từ modifier CriticalChance).</summary>
    public float GetCriticalChance()
    {
        return Mathf.Clamp01(ModifyStat(PlayerStatType.CriticalChance, BaseCriticalChance));
    }

    /// <summary>Hệ số nhân sát thương khi chí mạng (gốc 1.5, chỉnh bằng modifier CriticalDamage).</summary>
    public float GetCriticalMultiplier()
    {
        return Mathf.Max(1f, ModifyStat(PlayerStatType.CriticalDamage, BaseCriticalMultiplier));
    }

    /// <summary>Tung xúc xắc chí mạng. Nơi gây damage gọi hàm này để dùng CriticalChance / CriticalDamage.</summary>
    public float ApplyCritical(float damage, out bool isCritical)
    {
        isCritical = UnityEngine.Random.value < GetCriticalChance();
        return isCritical ? damage * GetCriticalMultiplier() : damage;
    }

    private void OnStatModifiersChanged()
    {
        // Gom mọi thay đổi trong cùng frame (gỡ + thêm lại buff khi làm mới) rồi xử lý một lần ở LateUpdate.
        statsDirty = true;
    }

    private void LateUpdate()
    {
        WatchWeaponChange();
        if (!statsDirty) return;
        statsDirty = false;
        RefreshMaxHealthFromModifiers();
        StatsChanged?.Invoke();
    }

    /// <summary>
    /// Máu tối đa đổi do buff/passive: tăng thì cộng cả phần tăng vào máu hiện tại;
    /// giảm (buff hết hạn) thì chỉ cắt máu hiện tại xuống bằng mức tối đa mới, và không bao giờ làm player chết.
    /// </summary>
    private void RefreshMaxHealthFromModifiers()
    {
        if (playerHealth == null || Data == null) return;

        float previousMax = playerHealth.MaxHealth;
        int newMax = GetMaxHealth();
        if (Mathf.Approximately(previousMax, newMax)) return;

        float current = playerHealth.CurrentHealth;
        float newCurrent = newMax > previousMax ? current + (newMax - previousMax) : Mathf.Min(current, newMax);
        if (current > 0f) newCurrent = Mathf.Max(1f, newCurrent);
        playerHealth.ConfigureHealth(newMax, Mathf.Clamp(newCurrent, 0f, newMax));
        Data.hp = Mathf.CeilToInt(playerHealth.CurrentHealth);
    }

    // ------------------------------------------------------------------ SkillConfig

    private SkillTable GetSkillTable()
    {
        return ResolveSkillConfig().ToSkillTable();
    }

    private SkillConfig ResolveSkillConfig()
    {
        if (skillConfig != null)
            return skillConfig;

        skillConfig = Resources.Load<SkillConfig>("SkillConfig");
        if (skillConfig == null)
        {
            Debug.LogWarning("[PlayerProgressionService] Chưa có SkillConfig. Hãy tạo Assets/Resources/SkillConfig.asset (Create > Bóng Ngủ > Skill Config). Đang dùng skill mặc định.");
            skillConfig = ScriptableObject.CreateInstance<SkillConfig>();
            skillConfig.EnsureDefaultEntries();
        }
        else if (skillConfig.Skills == null || skillConfig.Skills.Count == 0)
        {
            skillConfig.EnsureDefaultEntries();
        }

        return skillConfig;
    }

    private void ValidateSkillSetupIfNeeded()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!validateSkillSetupOnStart) return;
        SkillActions actions = FindAnyObjectByType<SkillActions>(FindObjectsInactive.Include);
        List<SkillIssue> issues = SkillConfigValidator.Validate(ResolveSkillConfig(), ResolveLvConfig(), actions);
        SkillConfigValidator.Log(issues, this);
#endif
    }

    // ------------------------------------------------------------------ Debug (chuột phải component khi đang Play)

    [ContextMenu("DEBUG: Mở khóa mọi skill trong SkillConfig")]
    private void DebugUnlockAllSkills()
    {
        if (Data == null) return;
        int added = 0;
        foreach (SkillConfigEntry entry in ResolveSkillConfig().Skills)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.skillId)) continue;
            string skillId = entry.skillId.Trim();
            if (HasUnlockedSkill(skillId)) continue;
            Data.unlockedSkills.Add(skillId);
            skillStateVersion++;
            SkillUnlocked?.Invoke(skillId);
            added++;
        }

        SyncPlayerSkillControllers();
        Save();
        Debug.Log($"[PlayerProgressionService] DEBUG: đã mở khóa thêm {added} skill.", this);
    }

    [ContextMenu("DEBUG: In trạng thái hệ thống skill")]
    private void DebugPrintSkillState()
    {
        if (Data == null) return;
        System.Text.StringBuilder log = new System.Text.StringBuilder();
        log.AppendLine("Vũ khí: " + GetCurrentWeaponId() + " (loại " + GetCurrentWeaponType() + ")");
        log.AppendLine("ATK " + GetAttackPowerExact().ToString("0.##") + " | DEF " + GetDefensePower()
            + " | MaxHP " + GetMaxHealth() + " | Crit " + GetCriticalChance().ToString("0.##"));

        log.AppendLine("Đã mở khóa:");
        foreach (string unlocked in Data.unlockedSkills)
        {
            SkillConfigEntry entry = ResolveSkillEntry(unlocked);
            log.AppendLine("  - " + unlocked + (entry != null ? " (" + entry.type + ", " + entry.name + ")" : " (KHÔNG có trong SkillConfig)"));
        }

        log.AppendLine("Đang trang bị:");
        foreach (EquippedSkillData equipped in Data.equippedSkills)
            if (equipped != null) log.AppendLine("  - ô " + equipped.slotIndex + ": " + equipped.skillName + " [phím " + equipped.inputKey + "]"
                + (IsWeaponRequirementMet(equipped.skillName) ? string.Empty : " (THIẾU VŨ KHÍ)"));

        log.AppendLine("Passive đang bật: " + string.Join(", ", GetUnlockedPassiveSkillIds()));
        log.AppendLine("Modifier: " + (statStore != null ? statStore.Describe() : "(chưa có RuntimeStatModifierStore)"));
        Debug.Log("[PlayerProgressionService] " + log, this);
    }
}

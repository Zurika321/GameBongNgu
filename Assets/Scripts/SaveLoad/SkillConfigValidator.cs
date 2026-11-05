using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SkillIssueLevel
{
    Info,
    Warning,
    Error
}

public class SkillIssue
{
    public readonly SkillIssueLevel level;
    public readonly string message;

    public SkillIssue(SkillIssueLevel level, string message)
    {
        this.level = level;
        this.message = message;
    }
}

/// <summary>
/// Kiểm tra cấu hình hệ thống skill trước khi chạy: SkillConfig, LvConfig, SkillActions, điều kiện vũ khí và ItemDefinition.
/// - Editor: menu Tools > Bóng Ngủ > Kiểm tra hệ thống Skill.
/// - Play (Editor / Development build): PlayerProgressionService tự chạy khi bật "Validate Skill Setup On Start".
/// </summary>
public static class SkillConfigValidator
{
    private const string UnarmedWeaponId = "Punch";

    public static List<SkillIssue> Validate(SkillConfig config, LvConfig lvConfig, SkillActions actions)
    {
        List<SkillIssue> issues = new List<SkillIssue>();
        if (config == null)
        {
            issues.Add(new SkillIssue(SkillIssueLevel.Error,
                "Không tìm thấy SkillConfig. Tạo Assets/Resources/SkillConfig.asset (Create > Bóng Ngủ > Skill Config)."));
            return issues;
        }

        List<string> registeredActions = actions != null ? actions.GetConfiguredActionIds() : null;
        if (actions == null)
            issues.Add(new SkillIssue(SkillIssueLevel.Info,
                "Không thấy SkillActions trong scene hiện tại: bỏ qua kiểm tra actionId đã đăng ký."));

        HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> basicByWeapon = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        HashSet<WeaponType> requiredTypes = new HashSet<WeaponType>();

        for (int i = 0; i < config.Skills.Count; i++)
        {
            SkillConfigEntry entry = config.Skills[i];
            if (entry == null)
            {
                issues.Add(new SkillIssue(SkillIssueLevel.Warning, $"SkillConfig: phần tử #{i} rỗng."));
                continue;
            }

            string label = string.IsNullOrWhiteSpace(entry.skillId) ? $"#{i}" : entry.skillId.Trim();
            if (string.IsNullOrWhiteSpace(entry.skillId))
            {
                issues.Add(new SkillIssue(SkillIssueLevel.Error, $"Skill {label} ('{entry.name}') chưa có skillId."));
                continue;
            }
            if (!seenIds.Add(entry.skillId.Trim()))
                issues.Add(new SkillIssue(SkillIssueLevel.Error, $"skillId '{label}' bị trùng trong SkillConfig."));

            if (string.IsNullOrWhiteSpace(entry.name))
                issues.Add(new SkillIssue(SkillIssueLevel.Warning, $"Skill '{label}' chưa có tên hiển thị (UI sẽ hiện skillId)."));
            else if (!seenNames.Add(entry.name.Trim()))
                issues.Add(new SkillIssue(SkillIssueLevel.Warning, $"Tên hiển thị '{entry.name}' bị trùng; tra cứu theo tên sẽ lấy skill đầu tiên."));

            ValidateByType(entry, label, registeredActions, issues);
            ValidateWeaponVariants(entry, label, issues);
            ValidateWeaponRequirement(entry, label, issues, requiredTypes);

            if (entry.type == SkillType.BasicAttack && !entry.IsRequirementOnlyBasicAttack)
            {
                string weapon = string.IsNullOrWhiteSpace(entry.weaponId) ? UnarmedWeaponId : entry.weaponId.Trim();
                if (basicByWeapon.TryGetValue(weapon, out string other))
                    issues.Add(new SkillIssue(SkillIssueLevel.Warning,
                        $"BasicAttack '{label}' và '{other}' cùng weaponId '{weapon}'; chỉ entry đầu tiên được dùng."));
                else basicByWeapon[weapon] = label;
            }
        }

        ValidateBasicAttackCoverage(config, issues);
        ValidateItemWeaponTypes(requiredTypes, issues);
        ValidateLvConfig(config, lvConfig, issues);
        return issues;
    }

    private static void ValidateByType(SkillConfigEntry entry, string label, List<string> registeredActions, List<SkillIssue> issues)
    {
        bool needsAction = false;
        switch (entry.type)
        {
            case SkillType.BasicAttack:
            case SkillType.Active:
                needsAction = true;
                if (entry.defaultContext == null)
                    issues.Add(new SkillIssue(SkillIssueLevel.Error, $"Skill '{label}' thiếu defaultContext."));
                break;

            case SkillType.BuffAction:
                needsAction = entry.executeActionAfterBuff;
                if (!HasModifiers(entry.buffConfig != null ? entry.buffConfig.modifiers : null))
                    issues.Add(new SkillIssue(SkillIssueLevel.Error, $"BuffAction '{label}' chưa có modifier trong buffConfig."));
                else if (entry.buffConfig.duration <= 0f)
                    issues.Add(new SkillIssue(SkillIssueLevel.Warning, $"BuffAction '{label}' có duration = 0: buff không bao giờ hết hạn."));
                break;

            case SkillType.PassiveBuff:
                if (!HasModifiers(entry.buffConfig != null ? entry.buffConfig.modifiers : null))
                    issues.Add(new SkillIssue(SkillIssueLevel.Error, $"PassiveBuff '{label}' chưa có modifier trong buffConfig."));
                break;

            case SkillType.Passive:
                PassiveSkillConfig passive = entry.passiveConfig;
                if (passive == null)
                {
                    issues.Add(new SkillIssue(SkillIssueLevel.Error, $"Passive '{label}' thiếu passiveConfig."));
                    break;
                }
                bool hasMods = HasModifiers(passive.modifiers);
                if (passive.trigger == PassiveTriggerType.Always)
                {
                    if (!hasMods && string.IsNullOrWhiteSpace(entry.actionId))
                        issues.Add(new SkillIssue(SkillIssueLevel.Warning, $"Passive '{label}' (Always) không có modifier lẫn class tuỳ biến: không làm gì cả."));
                }
                else
                {
                    if (!hasMods)
                        issues.Add(new SkillIssue(SkillIssueLevel.Error, $"Passive '{label}' ({passive.trigger}) chưa có modifier."));
                    if (passive.duration <= 0f)
                        issues.Add(new SkillIssue(SkillIssueLevel.Warning, $"Passive '{label}' ({passive.trigger}) có duration = 0: buff sau khi kích hoạt sẽ không bao giờ hết hạn."));
                    if (passive.chance <= 0f)
                        issues.Add(new SkillIssue(SkillIssueLevel.Warning, $"Passive '{label}' có chance = 0: không bao giờ kích hoạt."));
                }
                break;
        }

        if (!needsAction) return;
        if (string.IsNullOrWhiteSpace(entry.actionId))
        {
            issues.Add(new SkillIssue(SkillIssueLevel.Error, $"Skill '{label}' ({entry.type}) chưa có actionId."));
            return;
        }
        if (registeredActions != null && !registeredActions.Exists(id => string.Equals(id, entry.actionId.Trim(), StringComparison.OrdinalIgnoreCase)))
            issues.Add(new SkillIssue(SkillIssueLevel.Error,
                $"Skill '{label}': actionId '{entry.actionId}' chưa được đăng ký trong SkillActions (Skill Registrations)."));
    }

    private static void ValidateWeaponVariants(SkillConfigEntry entry, string label, List<SkillIssue> issues)
    {
        if (entry.weaponVariants == null) return;
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SkillContextVariant variant in entry.weaponVariants)
        {
            if (variant == null) continue;
            string weapon = string.IsNullOrWhiteSpace(variant.weaponId) ? UnarmedWeaponId : variant.weaponId.Trim();
            if (!seen.Add(weapon))
                issues.Add(new SkillIssue(SkillIssueLevel.Warning, $"Skill '{label}': weaponVariants có weaponId '{weapon}' bị trùng."));
        }
    }

    private static void ValidateWeaponRequirement(SkillConfigEntry entry, string label, List<SkillIssue> issues, HashSet<WeaponType> requiredTypes)
    {
        SkillWeaponRequirement requirement = entry.weaponRequirement;
        if (requirement == null || !requirement.HasRequirement) return;

        if (requirement.acceptedTypes != null)
        {
            foreach (WeaponType type in requirement.acceptedTypes)
            {
                if (type == WeaponType.None)
                    issues.Add(new SkillIssue(SkillIssueLevel.Warning, $"Skill '{label}': acceptedTypes có 'None' (không bao giờ khớp)."));
                else requiredTypes.Add(type);
            }
        }

        if (requirement.acceptedWeaponIds != null)
        {
            foreach (string weaponId in requirement.acceptedWeaponIds)
            {
                if (string.IsNullOrWhiteSpace(weaponId)) continue;
                if (string.Equals(weaponId.Trim(), UnarmedWeaponId, StringComparison.OrdinalIgnoreCase)) continue;
                if (ItemDefinition.Find(weaponId.Trim()) == null)
                    issues.Add(new SkillIssue(SkillIssueLevel.Warning,
                        $"Skill '{label}': weapon ID '{weaponId}' không có ItemDefinition tương ứng (Resources/ItemDefinitions)."));
            }
        }
    }

    private static void ValidateBasicAttackCoverage(SkillConfig config, List<SkillIssue> issues)
    {
        SkillConfigEntry punch = config.GetBasicAttackForWeapon(UnarmedWeaponId, WeaponType.Unarmed);
        bool hasBasic = false;
        foreach (SkillConfigEntry entry in config.Skills)
            if (entry != null && entry.type == SkillType.BasicAttack) { hasBasic = true; break; }

        if (!hasBasic)
        {
            issues.Add(new SkillIssue(SkillIssueLevel.Error,
                "Chưa có skill type BasicAttack nào: Player sẽ không đánh thường được (PlayerBasicAttackController không có đòn để dùng)."));
            return;
        }
        if (punch == null)
        {
            issues.Add(new SkillIssue(SkillIssueLevel.Error, "Không tìm thấy BasicAttack cho tay không (weaponId 'Punch' hoặc để trống)."));
            return;
        }

        IReadOnlyList<ItemDefinition> items = ItemDefinition.GetAll();
        if (items == null) return;
        foreach (ItemDefinition item in items)
        {
            if (item == null || item.category != ItemCategory.Weapon || string.IsNullOrWhiteSpace(item.itemName)) continue;
            SkillConfigEntry match = config.GetBasicAttackForWeapon(item.itemName, ItemDefinition.ResolveWeaponType(item.itemName));
            if (match == punch)
                issues.Add(new SkillIssue(SkillIssueLevel.Warning,
                    $"Vũ khí '{item.itemName}' chưa có BasicAttack riêng (weaponId hoặc Weapon Requirement): đang dùng đòn tay không."));
        }
    }

    private static void ValidateItemWeaponTypes(HashSet<WeaponType> requiredTypes, List<SkillIssue> issues)
    {
        IReadOnlyList<ItemDefinition> items = ItemDefinition.GetAll();
        if (items == null) return;

        HashSet<WeaponType> available = new HashSet<WeaponType>();
        foreach (ItemDefinition item in items)
        {
            if (item == null || item.category != ItemCategory.Weapon) continue;
            if (item.weaponType == WeaponType.None)
            {
                if (requiredTypes.Count > 0)
                    issues.Add(new SkillIssue(SkillIssueLevel.Warning,
                        $"Vũ khí '{item.itemName}' chưa chọn Weapon Type (đang xem là 'Other'): skill yêu cầu loại vũ khí sẽ không nhận."));
            }
            else available.Add(item.weaponType);
        }

        foreach (WeaponType type in requiredTypes)
        {
            if (type == WeaponType.Unarmed) continue;
            if (!available.Contains(type))
                issues.Add(new SkillIssue(SkillIssueLevel.Warning,
                    $"Có skill yêu cầu loại vũ khí '{type}' nhưng chưa có ItemDefinition vũ khí nào thuộc loại này."));
        }
    }

    private static void ValidateLvConfig(SkillConfig config, LvConfig lvConfig, List<SkillIssue> issues)
    {
        if (lvConfig == null)
        {
            issues.Add(new SkillIssue(SkillIssueLevel.Warning,
                "Không tìm thấy LvConfig (Assets/Resources/LvConfig.asset): không skill nào được mở khóa theo level."));
            return;
        }

        HashSet<string> unlocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (LvConfig.LevelEntry levelEntry in lvConfig.Levels)
        {
            if (levelEntry == null) continue;
            foreach (string raw in lvConfig.GetSkillsForLevel(levelEntry.level))
            {
                SkillConfigEntry found = config.FindEntry(raw);
                if (found == null)
                    issues.Add(new SkillIssue(SkillIssueLevel.Error,
                        $"LvConfig level {levelEntry.level}: '{raw}' không có trong SkillConfig (nhập skillId)."));
                else unlocked.Add(found.skillId.Trim());
            }
        }

        foreach (SkillConfigEntry entry in config.Skills)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.skillId) || entry.type == SkillType.BasicAttack) continue;
            if (!unlocked.Contains(entry.skillId.Trim()))
                issues.Add(new SkillIssue(SkillIssueLevel.Info, $"Skill '{entry.skillId.Trim()}' chưa được mở khóa ở level nào trong LvConfig."));
        }
    }

    private static bool HasModifiers(List<SkillStatModifier> modifiers)
    {
        return modifiers != null && modifiers.Count > 0;
    }

    /// <summary>In kết quả ra Console. Trả về số lỗi (Error).</summary>
    public static int Log(List<SkillIssue> issues, UnityEngine.Object context)
    {
        int errors = 0;
        int warnings = 0;
        foreach (SkillIssue issue in issues)
        {
            string text = "[SkillValidator] " + issue.message;
            switch (issue.level)
            {
                case SkillIssueLevel.Error: errors++; Debug.LogError(text, context); break;
                case SkillIssueLevel.Warning: warnings++; Debug.LogWarning(text, context); break;
                default: Debug.Log(text, context); break;
            }
        }

        if (errors == 0 && warnings == 0)
            Debug.Log("[SkillValidator] Cấu hình skill ổn: không có lỗi hay cảnh báo.", context);
        else
            Debug.Log($"[SkillValidator] Xong: {errors} lỗi, {warnings} cảnh báo.", context);
        return errors;
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Bóng Ngủ/Kiểm tra hệ thống Skill")]
    private static void ValidateFromMenu()
    {
        PlayerProgressionService service = UnityEngine.Object.FindAnyObjectByType<PlayerProgressionService>(FindObjectsInactive.Include);
        SkillConfig config = service != null ? service.SkillConfigAsset : Resources.Load<SkillConfig>("SkillConfig");
        LvConfig lvConfig = Resources.Load<LvConfig>("LvConfig");
        SkillActions actions = UnityEngine.Object.FindAnyObjectByType<SkillActions>(FindObjectsInactive.Include);
        Log(Validate(config, lvConfig, actions), config);
    }
#endif
}

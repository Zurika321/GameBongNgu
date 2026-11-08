using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class SkillConfigEntry
{
    [Header("Thông tin hiển thị / mở khóa")]
    public string branch;
    public string tier;
    [Tooltip("Tên hiển thị tiếng Việt, khớp tên skill trong Level.json nếu đang dùng hệ thống level cũ.")]
    public string name;
    public string unlockCondition;
    [Tooltip("Excel còn thiếu cơ chế/tham số. Gán đủ rồi tắt để cho phép thi triển.")] public bool needsInspectorSetup;
    [TextArea] public string setupNotes;
    public string requires;
    [TextArea] public string effect;
    public Sprite icon;

    [Header("Định danh")]
    [Tooltip("ID duy nhất của skill. Dùng cho unlock, context, cooldown và save. Ví dụ BasicAttack_Dagger.")]
    public string skillId;

    [FormerlySerializedAs("activeSkillId")]
    [Tooltip("ID class thực thi đã đăng ký trong SkillActions. Nhiều skill được phép dùng chung actionId.")]
    [RegistryId(RegistryIdType.Skill)]
    public string actionId;

    public SkillType type = SkillType.Active;

    [RegistryId(RegistryIdType.Item)]
    [Tooltip("Chỉ dùng cho BasicAttack: ID vũ khí (đúng tên/ID vật phẩm đang cầm) để chọn đòn đánh thường. 'Punch' hoặc để trống = tay không. " +
             "Muốn 1 entry dùng cho cả loại vũ khí (mọi dao...) thì để trống và dùng Weapon Requirement bên dưới.")]
    public string weaponId;

    [Header("Context mặc định")]
    public SkillContextConfig defaultContext = new SkillContextConfig();

    [Header("Context biến thể theo từng vũ khí")]
    [Tooltip("Override context mặc định theo weaponId. Bao gồm size, range, tốc độ, DOT và animation.")]
    public List<SkillContextVariant> weaponVariants = new List<SkillContextVariant>();

    [Header("Buff / Passive")]
    public SkillBuffConfig buffConfig = new SkillBuffConfig();
    public PassiveSkillConfig passiveConfig = new PassiveSkillConfig();

    [Tooltip("Chỉ dùng với BuffAction. Bật nếu sau khi áp dụng buff vẫn cần chạy thêm actionId.")]
    public bool executeActionAfterBuff;

    [Header("Điều kiện vũ khí")]
    [Tooltip("Để trống = dùng với mọi vũ khí. Active/BuffAction: chỉ trang bị & dùng được khi vũ khí khớp, mất vũ khí thì skill tự bị gỡ khỏi ô kỹ năng. " +
             "Passive: chỉ có hiệu lực khi vũ khí khớp. BasicAttack: chọn đòn đánh thường cho cả loại vũ khí (ưu tiên thấp hơn weaponId khớp đúng).")]
    public SkillWeaponRequirement weaponRequirement = new SkillWeaponRequirement();

    /// <summary>
    /// BasicAttack không có weaponId nhưng có Weapon Requirement: chỉ được chọn theo điều kiện (loại vũ khí / danh sách ID),
    /// KHÔNG được xem là đòn tay không (weaponId trống thường nghĩa là Punch).
    /// </summary>
    public bool IsRequirementOnlyBasicAttack
    {
        get
        {
            return type == SkillType.BasicAttack && string.IsNullOrWhiteSpace(weaponId)
                && weaponRequirement != null && weaponRequirement.HasRequirement;
        }
    }

    // Các field legacy giữ lại để Unity không làm mất dữ liệu SkillConfig cũ.
    [HideInInspector] public float cooldown = 1f;
    [HideInInspector] public Vector2 skillSize = new Vector2(1.5f, 1f);
    [HideInInspector] public float range = 1f;
    [HideInInspector] public float speed = 10f;
    [HideInInspector] public float duration = 0.2f;
    [HideInInspector] public float stunTime;
    [HideInInspector] public float chargeTime;
    [HideInInspector] public float endTime = 0.15f;
    [HideInInspector] public bool canPierce;
    [HideInInspector] public bool debug = true;
    [HideInInspector] public DameDotConfig dotConfig = new DameDotConfig();

    [SerializeField, HideInInspector] private bool legacyContextMigrated;

    /// <summary>
    /// Chuyển dữ liệu của asset cũ (trước khi tách SkillContextConfig) sang defaultContext và tạo skillId từ actionId cũ.
    /// Chỉ áp dụng cho entry CŨ (chưa có skillId). Entry tạo sau khi viết lại không bị đụng tới,
    /// nên chỉnh sửa trong Inspector không bao giờ bị ghi đè bởi giá trị legacy.
    /// </summary>
    public void MigrateLegacyContextIfNeeded()
    {
        if (legacyContextMigrated) return;
        legacyContextMigrated = true;
        if (!string.IsNullOrWhiteSpace(skillId)) return; // entry mới: giữ nguyên defaultContext

        if (defaultContext == null) defaultContext = new SkillContextConfig();
        defaultContext.cooldown = cooldown > 0f ? cooldown : defaultContext.cooldown;
        defaultContext.skillSize = skillSize;
        defaultContext.range = range;
        defaultContext.speed = speed;
        defaultContext.duration = duration;
        defaultContext.stunTime = stunTime;
        defaultContext.chargeTime = chargeTime;
        defaultContext.endTime = endTime;
        defaultContext.canPierce = canPierce;
        defaultContext.debug = debug;
        defaultContext.dotConfig = dotConfig ?? new DameDotConfig();

        // activeSkillId cũ đã được Unity chuyển vào actionId nhờ FormerlySerializedAs; dùng làm skillId tạm.
        if (!string.IsNullOrWhiteSpace(actionId)) skillId = actionId.Trim();
    }
}

/// <summary>Legacy animation variant để đọc dữ liệu asset cũ. Nên chuyển sang weaponVariants.context.animationSet.</summary>
[Serializable]
public class SkillAnimationVariant
{
    [RegistryId(RegistryIdType.Skill)] public string skillName;
    [RegistryId(RegistryIdType.Item)] public string weaponName;
    public SkillAnimationSet animationSet = new SkillAnimationSet();
}

[CreateAssetMenu(menuName = "Bóng Ngủ/Skill Config", fileName = "SkillConfig")]
public class SkillConfig : ScriptableObject
{
    [SerializeField, Tooltip("Icon dùng khi skill chưa có ảnh riêng. Có thể thay bằng icon chung của dự án.")]
    private Sprite fallbackSkillIcon;
    public Sprite GetIcon(SkillConfigEntry entry) => entry != null ? (entry.icon != null ? entry.icon : fallbackSkillIcon) : null;
    private const string DefaultWeaponName = "Punch";

    [Header("Danh sách skill")]
    [SerializeField] private List<SkillConfigEntry> skills = new List<SkillConfigEntry>();

    [Header("Legacy animation variants - chỉ giữ để tương thích asset cũ")]
    [SerializeField, HideInInspector] private List<SkillAnimationVariant> animationVariants = new List<SkillAnimationVariant>();

    [NonSerialized] private bool cacheBuilt;
    [NonSerialized] private SkillTable cachedTable;
    [NonSerialized] private readonly Dictionary<string, SkillConfigEntry> entryByName =
        new Dictionary<string, SkillConfigEntry>(StringComparer.OrdinalIgnoreCase);
    [NonSerialized] private readonly Dictionary<string, SkillConfigEntry> entryById =
        new Dictionary<string, SkillConfigEntry>(StringComparer.OrdinalIgnoreCase);

    public List<SkillConfigEntry> Skills => skills;

    private void OnEnable()
    {
        MigrateAllLegacyEntries();
        cacheBuilt = false;
    }

    private void MigrateAllLegacyEntries()
    {
        if (skills == null) return;
        foreach (SkillConfigEntry entry in skills)
            if (entry != null) entry.MigrateLegacyContextIfNeeded();
    }

    private void OnValidate()
    {
        if (skills != null)
        {
            foreach (SkillConfigEntry entry in skills)
            {
                if (entry == null) continue;
                entry.MigrateLegacyContextIfNeeded();

                if (entry.weaponRequirement == null) entry.weaponRequirement = new SkillWeaponRequirement();
                if (entry.weaponRequirement.acceptedTypes == null) entry.weaponRequirement.acceptedTypes = new List<WeaponType>();
                if (entry.weaponRequirement.acceptedWeaponIds == null) entry.weaponRequirement.acceptedWeaponIds = new List<string>();

                // Fallback: entry đã đánh dấu migrate nhưng chưa có skillId thì lấy tạm từ actionId.
                if (string.IsNullOrWhiteSpace(entry.skillId) && !string.IsNullOrWhiteSpace(entry.actionId))
                    entry.skillId = entry.actionId.Trim();
            }
        }
        cacheBuilt = false;
    }

    private void EnsureCache()
    {
        if (cacheBuilt) return;

        entryByName.Clear();
        entryById.Clear();
        cachedTable = new SkillTable();
        if (cachedTable.skills == null) cachedTable.skills = new List<SkillData>();

        if (skills != null)
        {
            foreach (SkillConfigEntry entry in skills)
            {
                if (entry == null) continue;
                entry.MigrateLegacyContextIfNeeded();

                string nameKey = Normalize(entry.name);
                string idKey = Normalize(entry.skillId);

                if (nameKey.Length == 0)
                {
                    Debug.LogWarning("[SkillConfig] Bỏ qua entry không có tên hiển thị.", this);
                    continue;
                }

                if (idKey.Length == 0)
                {
                    Debug.LogWarning($"[SkillConfig] Skill '{entry.name}' chưa có skillId.", this);
                    continue;
                }

                if (entryById.ContainsKey(idKey))
                {
                    Debug.LogWarning($"[SkillConfig] Trùng skillId '{idKey}'. Chỉ giữ entry đầu tiên.", this);
                    continue;
                }

                entryById.Add(idKey, entry);
                if (!entryByName.ContainsKey(nameKey)) entryByName.Add(nameKey, entry);
                cachedTable.skills.Add(ToSkillData(entry));
            }
        }

        cacheBuilt = true;
    }

    private static SkillData ToSkillData(SkillConfigEntry entry)
    {
        SkillContextConfig context = entry.defaultContext ?? new SkillContextConfig();
        return new SkillData
        {
            branch = entry.branch,
            tier = entry.tier,
            name = Normalize(entry.name),
            unlockCondition = entry.unlockCondition,
            requires = entry.requires,
            effect = entry.effect,
            // SkillData cũ chưa có skillId riêng. Tạm dùng activeSkillId làm nơi truyền skillId.
            activeSkillId = entry.skillId,
            cooldown = context.cooldown,
            damage = 0f,
            skillSize = context.skillSize,
            range = context.range,
            speed = context.speed,
            duration = context.duration,
            stunTime = context.stunTime,
            debug = context.debug,
            canPierce = context.canPierce
        };
    }

    public SkillConfigEntry GetEntry(string displayName)
    {
        EnsureCache();
        entryByName.TryGetValue(Normalize(displayName), out SkillConfigEntry entry);
        return entry;
    }

    public SkillConfigEntry GetEntryById(string skillId)
    {
        EnsureCache();
        entryById.TryGetValue(Normalize(skillId), out SkillConfigEntry entry);
        return entry;
    }

    /// <summary>Dùng tạm khi slot/save cũ đang lưu actionId thay vì skillId. Có thể trả entry đầu tiên nếu actionId dùng chung.</summary>
    public SkillConfigEntry GetFirstEntryByActionId(string actionId)
    {
        EnsureCache();
        if (skills == null) return null;
        return skills.FirstOrDefault(e => e != null &&
            string.Equals(Normalize(e.actionId), Normalize(actionId), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Tìm entry theo skillId HOẶC tên hiển thị (không phân biệt hoa thường). Ưu tiên skillId.</summary>
    public SkillConfigEntry FindEntry(string skillIdOrName)
    {
        if (string.IsNullOrWhiteSpace(skillIdOrName)) return null;
        return GetEntryById(skillIdOrName) ?? GetEntry(skillIdOrName);
    }

    /// <summary>SkillData (dạng bảng cũ) của skill, tìm theo skillId hoặc tên hiển thị.</summary>
    public SkillData GetSkill(string skillIdOrName)
    {
        EnsureCache();
        SkillConfigEntry entry = FindEntry(skillIdOrName);
        if (entry == null) return null;

        string id = Normalize(entry.skillId);
        return cachedTable.skills.Find(s => s != null &&
            string.Equals(s.activeSkillId, id, StringComparison.OrdinalIgnoreCase));
    }

    public SkillTable ToSkillTable()
    {
        EnsureCache();
        return cachedTable;
    }

    public SkillContextConfig GetContextConfig(string skillId, string weaponId)
    {
        SkillConfigEntry entry = GetEntryById(skillId);
        if (entry == null) return null;

        string key = NormalizeWeapon(weaponId);
        if (entry.weaponVariants != null)
        {
            SkillContextVariant variant = entry.weaponVariants.FirstOrDefault(v => v != null &&
                string.Equals(NormalizeWeapon(v.weaponId), key, StringComparison.OrdinalIgnoreCase));
            if (variant != null && variant.context != null) return variant.context;
        }

        return entry.defaultContext;
    }

    /// <summary>
    /// Chọn BasicAttack theo vũ khí đang cầm. Thứ tự ưu tiên:
    /// 1) entry có weaponId khớp đúng vũ khí; 2) entry có Weapon Requirement khớp loại/ID vũ khí
    /// (dùng 1 entry cho cả loại, ví dụ mọi Dagger); 3) đòn tay không (Punch).
    /// </summary>
    public SkillConfigEntry GetBasicAttackForWeapon(string weaponId, WeaponType weaponType)
    {
        EnsureCache();
        if (skills == null) return null;

        string key = NormalizeWeapon(weaponId);
        SkillConfigEntry exact = skills.FirstOrDefault(e => e != null && e.type == SkillType.BasicAttack
            && !e.IsRequirementOnlyBasicAttack
            && string.Equals(NormalizeWeapon(e.weaponId), key, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        SkillConfigEntry byRequirement = skills.FirstOrDefault(e => e != null && e.type == SkillType.BasicAttack
            && e.weaponRequirement != null && e.weaponRequirement.HasRequirement
            && e.weaponRequirement.IsSatisfiedBy(weaponType, key));
        if (byRequirement != null) return byRequirement;

        return GetBasicAttackForWeapon(weaponId);
    }

    public SkillConfigEntry GetBasicAttackForWeapon(string weaponId)
    {
        EnsureCache();
        if (skills == null) return null;

        string key = NormalizeWeapon(weaponId);
        SkillConfigEntry exact = skills.FirstOrDefault(e => e != null && e.type == SkillType.BasicAttack
            && !e.IsRequirementOnlyBasicAttack
            && string.Equals(NormalizeWeapon(e.weaponId), key, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Cho phép 1 entry BasicAttack có context variant theo weapon, nếu dự án muốn dùng kiểu này.
        SkillConfigEntry variantMatch = skills.FirstOrDefault(e => e != null && e.type == SkillType.BasicAttack
            && e.weaponVariants != null && e.weaponVariants.Any(v => v != null
                && string.Equals(NormalizeWeapon(v.weaponId), key, StringComparison.OrdinalIgnoreCase)));
        if (variantMatch != null) return variantMatch;

        SkillConfigEntry punch = skills.FirstOrDefault(e => e != null && e.type == SkillType.BasicAttack
            && !e.IsRequirementOnlyBasicAttack
            && string.Equals(NormalizeWeapon(e.weaponId), DefaultWeaponName, StringComparison.OrdinalIgnoreCase));
        if (punch != null) return punch;

        return skills.FirstOrDefault(e => e != null && e.type == SkillType.BasicAttack
            && !e.IsRequirementOnlyBasicAttack
            && string.IsNullOrWhiteSpace(e.weaponId));
    }

    public SkillAnimationSet GetAnimationSet(string skillId, string weaponId)
    {
        SkillConfigEntry entry = GetEntryById(skillId);
        string weaponKey = NormalizeWeapon(weaponId);

        // Context variant mới được ưu tiên cao nhất.
        if (entry != null && entry.weaponVariants != null)
        {
            SkillContextVariant variant = entry.weaponVariants.FirstOrDefault(v => v != null
                && string.Equals(NormalizeWeapon(v.weaponId), weaponKey, StringComparison.OrdinalIgnoreCase));
            if (variant != null && variant.context != null && variant.context.animationSet != null)
                return variant.context.animationSet;
        }

        // Tương thích animationVariants của asset cũ.
        SkillAnimationVariant legacy = animationVariants != null
            ? animationVariants.FirstOrDefault(v => v != null
                && string.Equals(Normalize(v.skillName), Normalize(skillId), StringComparison.OrdinalIgnoreCase)
                && string.Equals(NormalizeWeapon(v.weaponName), weaponKey, StringComparison.OrdinalIgnoreCase))
            : null;
        if (legacy != null && legacy.animationSet != null) return legacy.animationSet;

        SkillContextConfig context = entry != null ? entry.defaultContext : null;
        return context != null && context.animationSet != null ? context.animationSet : new SkillAnimationSet();
    }

    public void EnsureDefaultEntries()
    {
        if (skills != null && skills.Count > 0) return;

        SkillConfigEntry basic = new SkillConfigEntry
        {
            branch = "Cận Chiến",
            tier = "T1",
            name = "Đánh thường tay không",
            unlockCondition = "Level 1",
            effect = "Đánh cận chiến mặc định.",
            skillId = "BasicAttack_Punch",
            actionId = "NormalAttack",
            type = SkillType.BasicAttack,
            weaponId = "Punch",
            defaultContext = new SkillContextConfig()
        };

        skills = new List<SkillConfigEntry> { basic };
        cacheBuilt = false;
    }

    [ContextMenu("Thêm skill mẫu (không ghi đè)")]
    private void AddSampleEntries()
    {
        if (skills == null) skills = new List<SkillConfigEntry>();
        cacheBuilt = false;
        int added = 0;

        added += AddSampleIfMissing(NewSample("BasicAttack_Punch", "Đánh thường tay không", SkillType.BasicAttack,
            "NormalAttack", "Punch", 0.5f, new Vector2(1.2f, 1f), 0.8f, 10f, 0.15f));
        // Đánh thường bằng dao: weaponId để trống + Weapon Requirement = loại Dagger -> dùng cho MỌI vũ khí loại dao.
        SkillConfigEntry daggerBasic = NewSample("BasicAttack_Dagger", "Đánh thường bằng dao", SkillType.BasicAttack,
            "NormalAttack", string.Empty, 0.4f, new Vector2(1.3f, 0.8f), 0.9f, 10f, 0.15f);
        daggerBasic.weaponRequirement.acceptedTypes.Add(WeaponType.Dagger);
        added += AddSampleIfMissing(daggerBasic);

        SkillConfigEntry fireball = NewSample("Fireball_Player", "Cầu lửa", SkillType.Active,
            "Fireball", string.Empty, 4f, new Vector2(0.8f, 0.8f), 0.5f, 8f, 1.5f);
        fireball.defaultContext.chargeTime = 0.3f;
        fireball.defaultContext.damageMultiplier = 1.5f;
        added += AddSampleIfMissing(fireball);

        SkillConfigEntry strength = NewSample("Passive_Strength", "Sức mạnh bền bỉ", SkillType.PassiveBuff,
            string.Empty, string.Empty, 0f, Vector2.one, 0f, 0f, 0f);
        strength.effect = "Luôn tăng 10% sức mạnh tấn công.";
        strength.buffConfig = new SkillBuffConfig { duration = 0f };
        strength.buffConfig.modifiers.Add(new SkillStatModifier
            { stat = PlayerStatType.Attack, modifierType = StatModifierType.AddPercent, value = 0.1f });
        added += AddSampleIfMissing(strength);

        SkillConfigEntry frenzy = NewSample("Passive_Frenzy", "Máu chiến", SkillType.Passive,
            string.Empty, string.Empty, 0f, Vector2.one, 0f, 0f, 0f);
        frenzy.effect = "25% cơ hội khi đánh trúng: tăng 20% sức mạnh tấn công trong 3 giây.";
        frenzy.passiveConfig = new PassiveSkillConfig
            { trigger = PassiveTriggerType.OnHit, chance = 0.25f, duration = 3f };
        frenzy.passiveConfig.modifiers.Add(new SkillStatModifier
            { stat = PlayerStatType.Attack, modifierType = StatModifierType.AddPercent, value = 0.2f });
        added += AddSampleIfMissing(frenzy);

        SkillConfigEntry rage = NewSample("Buff_Rage", "Cuồng nộ", SkillType.BuffAction,
            string.Empty, string.Empty, 15f, Vector2.one, 0f, 0f, 0f);
        rage.effect = "Bấm để tăng 30% sức mạnh tấn công trong 5 giây.";
        rage.buffConfig = new SkillBuffConfig { duration = 5f };
        rage.buffConfig.modifiers.Add(new SkillStatModifier
            { stat = PlayerStatType.Attack, modifierType = StatModifierType.AddPercent, value = 0.3f });
        added += AddSampleIfMissing(rage);

        cacheBuilt = false;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"[SkillConfig] Đã thêm {added} skill mẫu. Nhớ gán icon, đặt Weapon Type cho vũ khí (ItemDefinition) và mở khóa skill trong LvConfig.", this);
    }

    [ContextMenu("Áp điều kiện vũ khí mẫu: Chém cơ bản cần Dao găm / Kiếm")]
    private void ApplySampleWeaponRequirement()
    {
        SkillConfigEntry slash = FindEntry("BasicSlash");
        if (slash == null)
        {
            Debug.LogWarning("[SkillConfig] Không tìm thấy skill 'BasicSlash' (Chém cơ bản) để áp điều kiện mẫu.", this);
            return;
        }

        if (slash.weaponRequirement == null) slash.weaponRequirement = new SkillWeaponRequirement();
        if (!slash.weaponRequirement.acceptedTypes.Contains(WeaponType.Dagger)) slash.weaponRequirement.acceptedTypes.Add(WeaponType.Dagger);
        if (!slash.weaponRequirement.acceptedTypes.Contains(WeaponType.Sword)) slash.weaponRequirement.acceptedTypes.Add(WeaponType.Sword);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[SkillConfig] 'Chém cơ bản' giờ chỉ trang bị/dùng được khi cầm vũ khí loại Dao găm hoặc Kiếm (đặt Weapon Type ở ItemDefinition).", this);
    }

    private static SkillConfigEntry NewSample(string id, string displayName, SkillType type, string action,
        string weapon, float cooldown, Vector2 size, float range, float speed, float duration)
    {
        SkillConfigEntry entry = new SkillConfigEntry
        {
            name = displayName,
            skillId = id,
            actionId = action,
            type = type,
            weaponId = weapon
        };
        entry.defaultContext = new SkillContextConfig
        {
            cooldown = cooldown,
            skillSize = size,
            range = range,
            speed = speed,
            duration = duration
        };
        return entry;
    }

    private int AddSampleIfMissing(SkillConfigEntry entry)
    {
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] != null && string.Equals(Normalize(skills[i].skillId), entry.skillId,
                    StringComparison.OrdinalIgnoreCase))
                return 0;
        }

        skills.Add(entry);
        return 1;
    }

    [ContextMenu("Reset về skill mặc định")]
    private void ResetToDefault()
    {
        skills = new List<SkillConfigEntry>();
        EnsureDefaultEntries();
    }

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeWeapon(string value)
    {
        string normalized = Normalize(value);
        if (normalized.Equals("none", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("null", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("empty", StringComparison.OrdinalIgnoreCase))
            return DefaultWeaponName;
        return normalized.Length == 0 ? DefaultWeaponName : normalized;
    }
}

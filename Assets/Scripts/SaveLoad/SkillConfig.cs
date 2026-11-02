using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1 skill trong SkillConfig: gộp thông tin skill (tên Việt, activeSkillId...) và thông số runtime
/// (trước đây nằm ở SkillContextConfig trong SkillContextLibrary).
/// KHÔNG có damage: damage do PlayerProgressionService tính từ level + vũ khí.
/// </summary>
[Serializable]
public class SkillConfigEntry
{
    [Header("Thông tin skill")]
    public string branch;            // Cận Chiến / Sinh Tồn / Quỷ Tính
    public string tier;              // T1..T6
    [Tooltip("Tên tiếng Việt, khớp với field 'skill' trong Level.json / LvConfig.")]
    public string name;
    public string unlockCondition;
    public string requires;
    [TextArea] public string effect;
    [Tooltip("ID tiếng Anh đã đăng ký trong SkillActions.")]
    public string activeSkillId;
    [Tooltip("Ảnh skill hiển thị trong inventory (lưới kỹ năng, ô trang bị, bảng thông tin, lúc kéo thả).")]
    public Sprite icon;
    public float cooldown = 1f;

    [Header("Thông số skill (runtime base) - damage lấy từ PlayerProgressionService")]
    public Vector2 skillSize = new Vector2(1.5f, 1f);
    public float range = 1f;
    public float speed = 10f;
    public float duration = 0.2f;
    [Min(0f)] public float stunTime;
    [Min(0f)] public float chargeTime;
    [Min(0f)] public float endTime = 0.15f;
    public bool canPierce;
    public bool debug = true;
    public DameDotConfig dotConfig = new DameDotConfig();
}

/// <summary>
/// Animation biến thể của 1 skill theo từng vũ khí (chuyển từ SkillContextLibrary sang).
/// Ví dụ: Fireball + Sword, Fireball + Hammer, Fireball + Punch.
/// </summary>
[Serializable]
public class SkillAnimationVariant
{
    [Header("Skill & weapon")]
    [RegistryId(RegistryIdType.Skill)] public string skillName;
    [RegistryId(RegistryIdType.Item)] public string weaponName;

    [Header("Animation set cho biến thể này")]
    public SkillAnimationSet animationSet = new SkillAnimationSet();
}

/// <summary>
/// Asset chứa toàn bộ dữ liệu skill, thay cho Skill.json + SkillContextLibrary.
/// Tạo asset: Create → Bóng Ngủ → Skill Config, đặt tên "SkillConfig" trong Assets/Resources/
/// (hoặc kéo vào field Skill Config của PlayerProgressionService).
/// </summary>
[CreateAssetMenu(menuName = "Bóng Ngủ/Skill Config", fileName = "SkillConfig")]
public class SkillConfig : ScriptableObject
{
    private const string DefaultWeaponName = "Punch";

    [Header("Danh sách skill (tên Việt -> activeSkillId tiếng Anh)")]
    [SerializeField] private List<SkillConfigEntry> skills = new List<SkillConfigEntry>();

    [Header("Animation biến thể theo skill + weapon")]
    [Tooltip("skillName = activeSkillId của skill. Mỗi phần tử: skillName + weaponName + SkillAnimationSet. Không có vũ khí khớp thì fallback về Punch.")]
    [SerializeField] private List<SkillAnimationVariant> animationVariants = new List<SkillAnimationVariant>();

    [NonSerialized] private bool cacheBuilt;
    [NonSerialized] private SkillTable cachedTable;
    [NonSerialized] private readonly Dictionary<string, SkillConfigEntry> entryByName =
        new Dictionary<string, SkillConfigEntry>(StringComparer.OrdinalIgnoreCase);
    [NonSerialized] private readonly Dictionary<string, SkillConfigEntry> entryById =
        new Dictionary<string, SkillConfigEntry>(StringComparer.OrdinalIgnoreCase);
    [NonSerialized] private readonly Dictionary<string, SkillData> dataByName =
        new Dictionary<string, SkillData>(StringComparer.OrdinalIgnoreCase);
    [NonSerialized] private readonly Dictionary<string, Dictionary<string, SkillAnimationSet>> animationsBySkill =
        new Dictionary<string, Dictionary<string, SkillAnimationSet>>(StringComparer.OrdinalIgnoreCase);

    public List<SkillConfigEntry> Skills => skills;
    public List<SkillAnimationVariant> AnimationVariants => animationVariants;

    private void OnValidate()
    {
        cacheBuilt = false; // sửa trong Inspector thì build lại
    }

    private void OnEnable()
    {
        cacheBuilt = false;
    }

    // ------------------------------------------------------------------ cache
    private void EnsureCache()
    {
        if (cacheBuilt) return;

        entryByName.Clear();
        entryById.Clear();
        dataByName.Clear();
        animationsBySkill.Clear();
        cachedTable = new SkillTable();

        if (skills != null)
        {
            foreach (SkillConfigEntry entry in skills)
            {
                if (entry == null) continue;
                string key = Normalize(entry.name);
                if (key.Length == 0)
                {
                    Debug.LogWarning("[SkillConfig] Bỏ qua skill không có tên.", this);
                    continue;
                }
                if (entryByName.ContainsKey(key))
                {
                    Debug.LogWarning($"[SkillConfig] Trùng tên skill '{key}'. Chỉ giữ bản đầu tiên.", this);
                    continue;
                }

                entryByName.Add(key, entry);
                string idKey = Normalize(entry.activeSkillId);
                if (idKey.Length > 0 && !entryById.ContainsKey(idKey))
                    entryById.Add(idKey, entry);
                SkillData data = ToSkillData(entry);
                dataByName.Add(key, data);
                cachedTable.skills.Add(data);
            }
        }

        if (animationVariants != null)
        {
            foreach (SkillAnimationVariant variant in animationVariants)
            {
                if (variant == null) continue;
                string skillKey = Normalize(variant.skillName);
                string weaponKey = Normalize(variant.weaponName);
                if (skillKey.Length == 0)
                {
                    Debug.LogWarning("[SkillConfig] Bỏ qua animation variant không có skillName.", this);
                    continue;
                }
                if (weaponKey.Length == 0)
                {
                    Debug.LogWarning($"[SkillConfig] Skill '{skillKey}' có animation variant thiếu weaponName; mặc định về '{DefaultWeaponName}'.", this);
                    weaponKey = DefaultWeaponName;
                }

                if (!animationsBySkill.TryGetValue(skillKey, out Dictionary<string, SkillAnimationSet> byWeapon))
                {
                    byWeapon = new Dictionary<string, SkillAnimationSet>(StringComparer.OrdinalIgnoreCase);
                    animationsBySkill.Add(skillKey, byWeapon);
                }
                if (byWeapon.ContainsKey(weaponKey))
                {
                    Debug.LogWarning($"[SkillConfig] Trùng cặp skill + weapon: '{skillKey}' + '{weaponKey}'. Chỉ giữ bản đầu tiên.", this);
                    continue;
                }

                byWeapon.Add(weaponKey, variant.animationSet ?? new SkillAnimationSet());
            }
        }

        cacheBuilt = true;
    }

    private static SkillData ToSkillData(SkillConfigEntry entry)
    {
        return new SkillData
        {
            branch = entry.branch,
            tier = entry.tier,
            name = entry.name != null ? entry.name.Trim() : string.Empty,
            unlockCondition = entry.unlockCondition,
            requires = entry.requires,
            effect = entry.effect,
            activeSkillId = entry.activeSkillId,
            cooldown = entry.cooldown,
            damage = 0f, // damage do PlayerProgressionService tính (level + vũ khí), không lấy từ config
            skillSize = entry.skillSize,
            range = entry.range,
            speed = entry.speed,
            duration = entry.duration,
            stunTime = entry.stunTime,
            debug = entry.debug,
            canPierce = entry.canPierce
        };
    }

    // ------------------------------------------------------------------ tra cứu
    /// <summary>Entry đầy đủ (kể cả chargeTime, endTime, dotConfig) theo tên tiếng Việt.</summary>
    public SkillConfigEntry GetEntry(string skillName)
    {
        EnsureCache();
        entryByName.TryGetValue(Normalize(skillName), out SkillConfigEntry entry);
        return entry;
    }

    /// <summary>Entry theo activeSkillId (ID tiếng Anh, vd "BasicSlash").</summary>
    public SkillConfigEntry GetEntryById(string activeSkillId)
    {
        EnsureCache();
        entryById.TryGetValue(Normalize(activeSkillId), out SkillConfigEntry entry);
        return entry;
    }

    /// <summary>SkillData (kiểu cũ) theo tên tiếng Việt, để tương thích code cũ.</summary>
    public SkillData GetSkill(string skillName)
    {
        EnsureCache();
        dataByName.TryGetValue(Normalize(skillName), out SkillData data);
        return data;
    }

    /// <summary>Bảng SkillTable, giữ tương thích với PlayerSkillController.SyncEquippedSkills.</summary>
    public SkillTable ToSkillTable()
    {
        EnsureCache();
        return cachedTable;
    }

    /// <summary>
    /// Animation theo skill (activeSkillId) + weapon. Không có thì fallback 'Punch'; vẫn không có thì trả về set rỗng.
    /// </summary>
    public SkillAnimationSet GetAnimationSet(string skillName, string weaponName)
    {
        EnsureCache();
        string skillKey = Normalize(skillName);
        string weaponKey = NormalizeWeapon(weaponName);
        if (weaponKey.Length == 0) weaponKey = DefaultWeaponName;

        if (TryGetAnimationSet(skillKey, weaponKey, out SkillAnimationSet set))
            return set;

        if (TryGetAnimationSet(skillKey, DefaultWeaponName, out SkillAnimationSet punchSet))
        {
            Debug.LogWarning($"[SkillConfig] Không có animation cho skill '{skillKey}' với vũ khí '{weaponKey}'. Fallback sang '{DefaultWeaponName}'.", this);
            return punchSet;
        }

        Debug.LogWarning($"[SkillConfig] Không có animation cho skill '{skillKey}' với vũ khí '{weaponKey}' và không có fallback '{DefaultWeaponName}'. AnimationSet để rỗng.", this);
        return new SkillAnimationSet();
    }

    private bool TryGetAnimationSet(string skillKey, string weaponKey, out SkillAnimationSet result)
    {
        result = null;
        return animationsBySkill.TryGetValue(skillKey, out Dictionary<string, SkillAnimationSet> byWeapon)
            && byWeapon.TryGetValue(weaponKey, out result);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeWeapon(string value)
    {
        string normalized = Normalize(value);
        if (normalized.Equals("none", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("null", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("empty", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return normalized;
    }

    // ------------------------------------------------------------------ mặc định
    /// <summary>Nếu asset trống thì thêm skill mặc định.</summary>
    public void EnsureDefaultEntries()
    {
        if (skills != null && skills.Count > 0)
            return;

        skills = new List<SkillConfigEntry>
        {
            new SkillConfigEntry
            {
                branch = "Cận Chiến",
                tier = "T1",
                name = "Chém cơ bản",
                unlockCondition = "Level 1",
                requires = "",
                effect = "Chém một nhát phía trước.",
                activeSkillId = "BasicSlash", // TODO: đổi cho đúng ID đã đăng ký trong SkillActions
                cooldown = 1f
            }
        };
        cacheBuilt = false;
    }

    [ContextMenu("Reset về skill mặc định")]
    private void ResetToDefault()
    {
        skills = new List<SkillConfigEntry>();
        EnsureDefaultEntries();
    }
}
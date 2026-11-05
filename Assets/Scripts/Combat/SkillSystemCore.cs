using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Phân loại dữ liệu skill. Chỉ BasicAttack/Active/BuffAction được đưa vào input slot.</summary>
public enum SkillType
{
    BasicAttack,
    Active,
    Passive,
    PassiveBuff,
    BuffAction
}

public enum PlayerStatType
{
    Attack,
    Defense,
    MaxHealth,
    MoveSpeed,
    CriticalChance,
    CriticalDamage
}

public enum StatModifierType
{
    Flat,           // base + value
    AddPercent,     // base * (1 + tổng %)
    Multiply        // nhân sau cùng
}

public enum PassiveTriggerType
{
    Always,
    OnHit,
    OnKill,
    OnDamageTaken
}

[Serializable]
public class SkillContextConfig
{
    [Header("Thông số đánh")]
    public Vector2 skillSize = new Vector2(1.5f, 1f);
    [Min(0f)] public float range = 1f;
    [Min(0f)] public float speed = 10f;
    [Min(0f)] public float duration = 0.2f;
    [Min(0f)] public float stunTime;

    [Header("Sát thương")]
    [Min(0f)]
    [Tooltip("Nhân với sức mạnh tấn công hiện tại của player (đã gồm level, vũ khí, buff). 1 = 100% ATK, 1.5 = 150% ATK.")]
    public float damageMultiplier = 1f;

    [Header("Thời gian")]
    [Min(0f)] public float cooldown = 1f;
    [Min(0f)] public float chargeTime;
    [Min(0f)] public float endTime = 0.15f;

    [Header("Tuỳ chọn")]
    public bool canPierce;
    public bool debug = true;
    public DameDotConfig dotConfig = new DameDotConfig();

    [Header("Âm thanh theo skill / biến thể")]
    public AudioClip chargeSfx;
    public AudioClip castSfx;
    public AudioClip hitSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Nếu true và chargeTime > 0, âm charge sẽ tự cắt ngắn xuống còn bằng thời gian charge. Dùng cho charge clip dài hơn thời gian chờ.")]
    public bool cutChargeSfxToChargeTime;

    [Header("Animation")]
    public SkillAnimationSet animationSet = new SkillAnimationSet();
}

[Serializable]
public class SkillContextVariant
{
    [RegistryId(RegistryIdType.Item)]
    [Tooltip("ID vũ khí. Để trống không được khuyến khích; hãy khai báo vũ khí cụ thể.")]
    public string weaponId;

    public SkillContextConfig context = new SkillContextConfig();
}

[Serializable]
public class SkillStatModifier
{
    public PlayerStatType stat;
    public StatModifierType modifierType;
    [Tooltip("Flat: cộng trực tiếp; AddPercent: nhập 0.1 cho +10%; Multiply: nhập 1.2 cho x1.2.")]
    public float value;
}

[Serializable]
public class SkillBuffConfig
{
    [Min(0f)]
    [Tooltip("Thời gian tồn tại. 0 = vô hạn cho đến khi bị gỡ hoặc scene/player bị huỷ.")]
    public float duration = 5f;

    public List<SkillStatModifier> modifiers = new List<SkillStatModifier>();
}

[Serializable]
public class PassiveSkillConfig
{
    public PassiveTriggerType trigger = PassiveTriggerType.Always;
    [Range(0f, 1f)] public float chance = 1f;

    [Min(0f)]
    [Tooltip("Chỉ dùng khi trigger khác Always: số giây buff tồn tại sau mỗi lần kích hoạt. 0 = không bao giờ hết hạn (không khuyến khích).")]
    public float duration = 3f;

    public List<SkillStatModifier> modifiers = new List<SkillStatModifier>();
}

/// <summary>
/// Hợp đồng để hệ thống chỉ số của dự án nhận modifier.
/// Có thể dùng RuntimeStatModifierStore bên dưới hoặc tự implement interface này.
/// </summary>
public interface IStatModifierReceiver
{
    void AddModifier(string sourceId, PlayerStatType stat, StatModifierType modifierType, float value);
    void RemoveModifiers(string sourceId);
}

/// <summary>
/// Điều kiện vũ khí để trang bị / dùng một skill. Để trống cả hai danh sách = không yêu cầu (mọi vũ khí, kể cả tay không).
/// Hợp lệ khi loại vũ khí đang cầm nằm trong acceptedTypes HOẶC ID vũ khí đang cầm nằm trong acceptedWeaponIds.
/// Loại vũ khí lấy từ ItemDefinition.weaponType; tay không (hoặc đang mặc giáp/bùa thay vì cầm vũ khí) = WeaponType.Unarmed.
/// Gameplay không so chuỗi tên vũ khí: mọi điều kiện đều nằm ở dữ liệu này.
/// </summary>
[Serializable]
public class SkillWeaponRequirement
{
    [Tooltip("Loại vũ khí được chấp nhận (đặt ở ItemDefinition > Weapon Type). Thêm 'Unarmed' nếu skill cũng dùng được khi tay không.")]
    public List<WeaponType> acceptedTypes = new List<WeaponType>();

    [RegistryId(RegistryIdType.Item)]
    [Tooltip("Vũ khí cụ thể được chấp nhận (ID vật phẩm trong ItemDefinition, 'Punch' = tay không). Dùng chung (OR) với danh sách loại vũ khí.")]
    public List<string> acceptedWeaponIds = new List<string>();

    public bool HasRequirement
    {
        get
        {
            return (acceptedTypes != null && acceptedTypes.Count > 0)
                || (acceptedWeaponIds != null && acceptedWeaponIds.Count > 0);
        }
    }

    /// <summary>currentType/currentWeaponId lấy từ PlayerProgressionService.GetCurrentWeaponType()/GetCurrentWeaponId().</summary>
    public bool IsSatisfiedBy(WeaponType currentType, string currentWeaponId)
    {
        if (!HasRequirement) return true;

        if (acceptedTypes != null && currentType != WeaponType.None && acceptedTypes.Contains(currentType))
            return true;

        if (acceptedWeaponIds != null && !string.IsNullOrWhiteSpace(currentWeaponId))
        {
            string current = currentWeaponId.Trim();
            foreach (string id in acceptedWeaponIds)
            {
                if (!string.IsNullOrWhiteSpace(id) && string.Equals(id.Trim(), current, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>Mô tả ngắn để hiển thị cho người chơi, ví dụ "Dao găm, Kiếm, Dao Găm Gỉ". Rỗng nếu không có điều kiện.</summary>
    public string Describe()
    {
        List<string> parts = new List<string>();
        if (acceptedTypes != null)
        {
            foreach (WeaponType type in acceptedTypes)
                if (type != WeaponType.None) parts.Add(WeaponTypeInfo.GetLabel(type));
        }
        if (acceptedWeaponIds != null)
        {
            foreach (string id in acceptedWeaponIds)
                if (!string.IsNullOrWhiteSpace(id)) parts.Add(id.Trim());
        }
        return string.Join(", ", parts);
    }
}

/// <summary>
/// Bộ lưu modifier mặc định, gắn trên Player. PlayerProgressionService tự tìm (hoặc tự thêm nếu thiếu) component này
/// và gọi GetFinalValue trong GetAttackPower / GetDefensePower / GetMaxHealth / ModifyStat.
/// Công thức: (base + tổng Flat) * (1 + tổng AddPercent) * tích Multiply.
/// </summary>
public class RuntimeStatModifierStore : MonoBehaviour, IStatModifierReceiver
{
    [Serializable]
    private class RuntimeModifier
    {
        public string sourceId;
        public PlayerStatType stat;
        public StatModifierType type;
        public float value;
    }

    [SerializeField] private List<RuntimeModifier> modifiers = new List<RuntimeModifier>();

    /// <summary>Phát ra mỗi khi danh sách modifier thay đổi (thêm hoặc gỡ).</summary>
    public event Action Changed;

    public void AddModifier(string sourceId, PlayerStatType stat, StatModifierType modifierType, float value)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            Debug.LogWarning("[RuntimeStatModifierStore] sourceId không được để trống.", this);
            return;
        }

        modifiers.RemoveAll(m => m != null && m.sourceId == sourceId
            && m.stat == stat && m.type == modifierType);
        modifiers.Add(new RuntimeModifier
        {
            sourceId = sourceId,
            stat = stat,
            type = modifierType,
            value = value
        });
        Changed?.Invoke();
    }

    public void RemoveModifiers(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return;
        int removed = modifiers.RemoveAll(m => m == null || m.sourceId == sourceId);
        if (removed > 0) Changed?.Invoke();
    }

    public float GetFinalValue(PlayerStatType stat, float baseValue)
    {
        float flat = 0f;
        float addPercent = 0f;
        float multiplier = 1f;

        foreach (RuntimeModifier modifier in modifiers)
        {
            if (modifier == null || modifier.stat != stat) continue;
            switch (modifier.type)
            {
                case StatModifierType.Flat:
                    flat += modifier.value;
                    break;
                case StatModifierType.AddPercent:
                    addPercent += modifier.value;
                    break;
                case StatModifierType.Multiply:
                    multiplier *= modifier.value;
                    break;
            }
        }

        return (baseValue + flat) * (1f + addPercent) * multiplier;
    }

    /// <summary>Mô tả các modifier đang có (dùng cho log debug).</summary>
    public string Describe()
    {
        if (modifiers.Count == 0) return "(không có modifier)";
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        foreach (RuntimeModifier modifier in modifiers)
        {
            if (modifier == null) continue;
            builder.AppendLine(modifier.sourceId + ": " + modifier.stat + " " + modifier.type + " " + modifier.value);
        }
        return builder.ToString();
    }

    private void OnDestroy()
    {
        modifiers.Clear();
    }
}

/// <summary>
/// Các event chiến đấu mà passive có thể lắng nghe.
/// Damage/combat script cần gọi RaiseHitLanded/RaiseKill/RaiseDamageTaken (hoặc ReportHit) tại đúng thời điểm.
/// </summary>
public static class SkillCombatEvents
{
    public static event Action<GameObject, GameObject, SkillContext> HitLanded;
    public static event Action<GameObject, GameObject> Killed;
    public static event Action<GameObject, float> DamageTaken;

    // Khi tắt "Reload Domain" trong Enter Play Mode Options, event static không tự reset giữa các lần Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        HitLanded = null;
        Killed = null;
        DamageTaken = null;
    }

    public static void RaiseHitLanded(GameObject attacker, GameObject target, SkillContext context)
    {
        HitLanded?.Invoke(attacker, target, context);
    }

    public static void RaiseKill(GameObject killer, GameObject target)
    {
        Killed?.Invoke(killer, target);
    }

    public static void RaiseDamageTaken(GameObject receiver, float amount)
    {
        DamageTaken?.Invoke(receiver, amount);
    }

    /// <summary>
    /// Tiện ích 1 dòng cho nơi gây damage: báo đã trúng đòn, và báo hạ gục nếu mục tiêu chết.
    /// Gọi SAU KHI damage đã được áp dụng thành công.
    /// </summary>
    public static void ReportHit(SkillContext context, GameObject target, bool targetDied)
    {
        if (context == null || context.Caster == null || target == null) return;
        RaiseHitLanded(context.Caster, target, context);
        if (targetDied) RaiseKill(context.Caster, target);
    }
}

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
    CriticalDamage,
    AttackSpeed,
    StunResistance,
    DotResistance
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
    [Tooltip("Tên animation JSON/Inspector. Trống = dùng skillId; đánh thường có thể dùng Attack mặc định.")]
    public string animationName;
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
    [HideInInspector] public AudioClip chargeSfx;
    [HideInInspector] public AudioClip castSfx;
    [HideInInspector] public AudioClip hitSfx;
    [HideInInspector, Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Nếu true và chargeTime > 0, âm charge sẽ tự cắt ngắn xuống còn bằng thời gian charge. Dùng cho charge clip dài hơn thời gian chờ.")]
    [HideInInspector] public bool cutChargeSfxToChargeTime;

    [Header("Catalog action overrides")]
    public List<SkillActionDamageOverride> actionDamageOverrides = new List<SkillActionDamageOverride>();
    public bool breaksGuard;
    public bool bypassesMeleeResistance;
    public SkillModuleConfig modules = new SkillModuleConfig();
    public SkillEffectAnimation effectAnimation = new SkillEffectAnimation();

    [Header("Animation (legacy)")]
    [HideInInspector] public SkillAnimationSet animationSet = new SkillAnimationSet();
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
        ConditionGate.RecordHit(attacker);
        HitLanded?.Invoke(attacker, target, context);
    }

    public static void RaiseKill(GameObject killer, GameObject target)
    {
        Killed?.Invoke(killer, target);
    }

    public static void RaiseDamageTaken(GameObject receiver, float amount)
    {
        ConditionGate.RecordDamage(receiver);
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


[System.Serializable]
public sealed class SkillEffectAnimation
{
    [Tooltip("Cast/flight frames. Four-direction layout: Down, Up, Left, Right.")]
    public Sprite[] castFrames;
    public Sprite[] endFrames;
    public bool fourDirections;
    [Min(0.001f)] public float frameInterval = 0.06f;

    public Sprite[] GetCast(Vector2 direction) => Select(castFrames, direction);
    public Sprite[] GetEnd(Vector2 direction) => Select(endFrames, direction);
    private Sprite[] Select(Sprite[] frames, Vector2 direction)
        => fourDirections ? SkillAnimationPlayer.GetDirectionalFrames(frames, direction) : frames ?? System.Array.Empty<Sprite>();
}

public static class SkillEffectPlayer
{
    public static void Attach(GameObject target, SkillContext context)
    {
        if (target == null || context?.EffectAnimation == null || SkillActions.Instance == null) return;
        SkillActions.Instance.StartCoroutine(Animate(target, context));
    }

    private static System.Collections.IEnumerator Animate(GameObject target, SkillContext context)
    {
        SkillEffectAnimation effect = context.EffectAnimation;
        Sprite[] cast = effect.GetCast(context.Direction);
        Sprite[] end = effect.GetEnd(context.Direction);
        if (cast.Length == 0 && end.Length == 0) yield break;
        Transform existing = target.transform.Find("Visual");
        GameObject visual = existing != null ? existing.gameObject : new GameObject("SkillEffect");
        if (existing == null) visual.transform.SetParent(target.transform, false);
        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = visual.AddComponent<SpriteRenderer>();
        SpriteRenderer owner = context.Caster != null ? context.Caster.GetComponentInChildren<SpriteRenderer>() : null;
        if (owner != null) { renderer.sortingLayerID = owner.sortingLayerID; renderer.sortingOrder = owner.sortingOrder + 1; }
        Vector3 position = target.transform.position;
        Quaternion rotation = target.transform.rotation;
        float elapsed = 0f;
        float interval = Mathf.Max(0.001f, effect.frameInterval);
        while (target != null)
        {
            position = target.transform.position;
            rotation = target.transform.rotation;
            if (renderer != null && cast.Length > 0) renderer.sprite = cast[Mathf.FloorToInt(elapsed / interval) % cast.Length];
            if (!GameplayPause.IsPaused) elapsed += Time.deltaTime;
            yield return null;
        }
        if (end.Length == 0) yield break;
        GameObject ending = new GameObject("SkillEndEffect");
        ending.transform.SetPositionAndRotation(position, rotation);
        SpriteRenderer endRenderer = ending.AddComponent<SpriteRenderer>();
        if (owner != null) { endRenderer.sortingLayerID = owner.sortingLayerID; endRenderer.sortingOrder = owner.sortingOrder + 1; }
        // Ensure cleanup even if the coroutine host is removed while the effect is playing.
        UnityEngine.Object.Destroy(ending, end.Length * interval + 1f);
        elapsed = 0f;
        while (ending != null && elapsed < end.Length * interval)
        {
            endRenderer.sprite = end[Mathf.Min(end.Length - 1, Mathf.FloorToInt(elapsed / interval))];
            if (!GameplayPause.IsPaused) elapsed += Time.deltaTime;
            yield return null;
        }
        if (ending != null) UnityEngine.Object.Destroy(ending);
    }
}

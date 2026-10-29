using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SkillRegistration
{
    [Tooltip("Tên skill phải khớp với skillName trong PlayerSkillSlot/EnemySkillSlot.")]
    public string skillName;

    [Tooltip("Sprite riêng cho skill: FlyAttack/DashAttack2 theo thứ tự Down, Up, Left, Right; Fireball gồm 6 frame bay rồi 6 frame nổ.")]
    public Sprite[] skillSprites;
}

/// <summary>
/// Đặt 1 GameObject duy nhất chứa script này trong scene (vd tên "SkillActions").
/// Player hoặc Enemy chỉ cần gọi:
///     SkillActions.Instance.Activate("ArcAttack", context);
/// Sprite của từng skill được cấu hình theo tên ở skillRegistrations, không truyền qua SkillContext.
/// </summary>
public class SkillActions : MonoBehaviour
{
    [Header("Skill registry - tên phải khớp với PlayerSkillSlot/EnemySkillSlot")]
    [SerializeField] private SkillRegistration[] skillRegistrations =
    {
        new SkillRegistration { skillName = "BasicSlash" },
        new SkillRegistration { skillName = "DodgeRoll" },
        new SkillRegistration { skillName = "RatBleedBite" },
        new SkillRegistration { skillName = "HoundRush" },
        new SkillRegistration { skillName = "SpiderPoisonBite" },
        new SkillRegistration { skillName = "ArcAttack" },
        new SkillRegistration { skillName = "NormalAttack" },
        new SkillRegistration { skillName = "FlyAttack" },
        new SkillRegistration { skillName = "Fireball" },
        new SkillRegistration { skillName = "DashAttack2" }
    };

    public static SkillActions Instance { get; private set; }

    private readonly Dictionary<string, ISkill> skillMap = new Dictionary<string, ISkill>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
        RegisterSkills();
    }

    private void RegisterSkills()
    {
        if (skillRegistrations == null || skillRegistrations.Length == 0)
        {
            Debug.LogWarning("[SkillActions] Chưa cấu hình skillRegistrations trong Inspector.", this);
            return;
        }

        foreach (SkillRegistration registration in skillRegistrations)
        {
            if (registration == null || string.IsNullOrWhiteSpace(registration.skillName))
            {
                Debug.LogWarning("[SkillActions] Bỏ qua đăng ký skill không có tên.", this);
                continue;
            }

            if (skillMap.ContainsKey(registration.skillName))
            {
                Debug.LogWarning($"[SkillActions] Tên skill '{registration.skillName}' bị đăng ký trùng.", this);
                continue;
            }

            ISkill skill = CreateSkill(registration.skillName);
            if (skill == null)
            {
                Debug.LogWarning($"[SkillActions] Không có class skill được hỗ trợ cho '{registration.skillName}'.", this);
                continue;
            }

            if (skill is ISkillSpriteConfigurable configurable)
            {
                configurable.SetSkillSprites(registration.skillSprites);
            }
            else if (registration.skillSprites != null && registration.skillSprites.Length > 0)
            {
                Debug.LogWarning($"[SkillActions] Skill '{registration.skillName}' không nhận cấu hình sprite skill.", this);
            }

            skillMap.Add(registration.skillName, skill);
        }
    }

    private ISkill CreateSkill(string skillName)
    {
        switch (skillName)
        {
            case "BasicSlash":
                return new BasicMeleeSkill();
            case "DodgeRoll":
                return new DodgeRollSkill();
            case "RatBleedBite":
                return new RatBleedBiteSkill();
            case "HoundRush":
                return new HoundRushSkill();
            case "SpiderPoisonBite":
                return new SpiderPoisonBiteSkill();
            case "ArcAttack":
                return GetOrAddSkill<ArcAttackSkill>();
            case "NormalAttack":
                return GetOrAddSkill<NormalAttackSkill>();
            case "FlyAttack":
                return GetOrAddSkill<FlyAttackSkill>();
            case "Fireball":
                return GetOrAddSkill<FireballAttackSkill>();
            case "DashAttack2":
                return new DashAttackSkill2();
            default:
                return null;
        }
    }

    private T GetOrAddSkill<T>() where T : MonoBehaviour, ISkill
    {
        T skill = GetComponent<T>();
        return skill != null ? skill : gameObject.AddComponent<T>();
    }

    public bool HasRegisteredSkill(string skillName)
    {
        return !string.IsNullOrWhiteSpace(skillName) && skillMap.ContainsKey(skillName);
    }

    /// <summary>
    /// Gọi hàm này từ Player hoặc Enemy để kích hoạt skill theo tên.
    /// Không tìm thấy skill -> chỉ Debug.Log ra, không văng lỗi.
    /// </summary>
    public void Activate(string skillName, SkillContext context)
    {
        if (GameplayPause.IsPaused) return;

        if (string.IsNullOrWhiteSpace(skillName))
        {
            Debug.LogWarning("[SkillActions] Không thể kích hoạt skill khi tên bị trống.", this);
            return;
        }

        if (context == null)
        {
            Debug.LogWarning($"[SkillActions] SkillContext null khi gọi skill '{skillName}'.");
            return;
        }

        if (skillMap.TryGetValue(skillName, out ISkill skill))
        {
            skill.Execute(context);
        }
        else
        {
            Debug.LogWarning($"[SkillActions] Không tìm thấy skill có tên '{skillName}'.", this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        skillMap.Clear();
    }
}

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SkillRegistration
{
    [Tooltip("TÃªn skill pháº£i khá»›p vá»›i skillName trong PlayerSkillSlot/EnemySkillSlot.")]
    public string skillName;

    [Tooltip("Sprite riÃªng cho skill: FlyAttack/DashAttack2 theo thá»© tá»± Down, Up, Left, Right; Fireball gá»“m 6 frame bay rá»“i 6 frame ná»•.")]
    public Sprite[] skillSprites;
}

/// <summary>
/// Äáº·t 1 GameObject duy nháº¥t chá»©a script nÃ y trong scene (vd tÃªn "SkillActions").
/// Player hoáº·c Enemy chá»‰ cáº§n gá»i:
///     SkillActions.Instance.Activate("ArcAttack", context);
/// Sprite cá»§a tá»«ng skill Ä‘Æ°á»£c cáº¥u hÃ¬nh theo tÃªn á»Ÿ skillRegistrations, khÃ´ng truyá»n qua SkillContext.
/// </summary>
public class SkillActions : MonoBehaviour
{
    [Header("Skill registry - tÃªn pháº£i khá»›p vá»›i PlayerSkillSlot/EnemySkillSlot")]
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
            Debug.LogWarning("[SkillActions] ChÆ°a cáº¥u hÃ¬nh skillRegistrations trong Inspector.", this);
            return;
        }

        foreach (SkillRegistration registration in skillRegistrations)
        {
            if (registration == null || string.IsNullOrWhiteSpace(registration.skillName))
            {
                Debug.LogWarning("[SkillActions] Bá» qua Ä‘Äƒng kÃ½ skill khÃ´ng cÃ³ tÃªn.", this);
                continue;
            }

            if (skillMap.ContainsKey(registration.skillName))
            {
                Debug.LogWarning($"[SkillActions] TÃªn skill '{registration.skillName}' bá»‹ Ä‘Äƒng kÃ½ trÃ¹ng.", this);
                continue;
            }

            ISkill skill = CreateSkill(registration.skillName);
            if (skill == null)
            {
                Debug.LogWarning($"[SkillActions] KhÃ´ng cÃ³ class skill Ä‘Æ°á»£c há»— trá»£ cho '{registration.skillName}'.", this);
                continue;
            }

            if (skill is ISkillSpriteConfigurable configurable)
            {
                configurable.SetSkillSprites(registration.skillSprites);
            }
            else if (registration.skillSprites != null && registration.skillSprites.Length > 0)
            {
                Debug.LogWarning($"[SkillActions] Skill '{registration.skillName}' khÃ´ng nháº­n cáº¥u hÃ¬nh sprite skill.", this);
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

    /// <summary>
    /// Gá»i hÃ m nÃ y tá»« Player hoáº·c Enemy Ä‘á»ƒ kÃ­ch hoáº¡t skill theo tÃªn.
    /// KhÃ´ng tÃ¬m tháº¥y skill -> chá»‰ Debug.Log ra, khÃ´ng vÄƒng lá»—i.
    /// </summary>
    public void Activate(string skillName, SkillContext context)
    {
        if (GameplayPause.IsPaused) return;

        if (string.IsNullOrWhiteSpace(skillName))
        {
            Debug.LogWarning("[SkillActions] KhÃ´ng thá»ƒ kÃ­ch hoáº¡t skill khi tÃªn bá»‹ trá»‘ng.", this);
            return;
        }

        if (context == null)
        {
            Debug.LogWarning($"[SkillActions] SkillContext null khi gá»i skill '{skillName}'.");
            return;
        }

        if (skillMap.TryGetValue(skillName, out ISkill skill))
        {
            skill.Execute(context);
        }
        else
        {
            Debug.LogWarning($"[SkillActions] KhÃ´ng tÃ¬m tháº¥y skill cÃ³ tÃªn '{skillName}'.", this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        skillMap.Clear();
    }
}

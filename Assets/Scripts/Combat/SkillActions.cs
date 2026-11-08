using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SkillRegistration
{
    [Tooltip("Phải khớp actionId trong SkillConfigEntry. Không phải skillId.")]
    public string skillName;
}

/// <summary>
/// Registry class thực thi. Nhiều skillId có thể cùng gọi một actionId, ví dụ
/// BasicAttack_Dagger và BasicAttack_Punch đều gọi NormalAttack.
/// </summary>
public class SkillActions : MonoBehaviour
{
    [Header("Action registry - nhập actionId, không nhập skillId")]
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
        new SkillRegistration { skillName = "DashAttack2" },
        new SkillRegistration { skillName = "ApplyStatus" },
        new SkillRegistration { skillName = "SelfBuff" },
        new SkillRegistration { skillName = "SummonEntity" },
        new SkillRegistration { skillName = "CasterMove" },
        new SkillRegistration { skillName = "ModifyResource" },
        new SkillRegistration { skillName = "DefenseWindow" },
        new SkillRegistration { skillName = "ScreenEffect" },
        new SkillRegistration { skillName = "ChallengeMeter" },
        new SkillRegistration { skillName = "ReplayAction" },
        new SkillRegistration { skillName = "ConditionGate" }
    };

    [SerializeField] private SkillActionCatalog actionCatalog;

    public static SkillActions Instance { get; private set; }

    private readonly Dictionary<string, ISkill> skillMap = new Dictionary<string, ISkill>(StringComparer.OrdinalIgnoreCase);

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
        skillMap.Clear();
        if (skillRegistrations == null) skillRegistrations = Array.Empty<SkillRegistration>();

        foreach (SkillRegistration registration in skillRegistrations)
        {
            if (registration == null || string.IsNullOrWhiteSpace(registration.skillName))
            {
                Debug.LogWarning("[SkillActions] Bỏ qua registration không có actionId.", this);
                continue;
            }

            string actionId = registration.skillName.Trim();
            if (skillMap.ContainsKey(actionId))
            {
                Debug.LogWarning($"[SkillActions] actionId '{actionId}' bị đăng ký trùng.", this);
                continue;
            }

            ISkill skill = CreateSkill(actionId);
            if (skill == null)
            {
                Debug.LogWarning($"[SkillActions] Không có class thực thi cho actionId '{actionId}'.", this);
                continue;
            }

            skillMap.Add(actionId, skill);
        }
        if (actionCatalog != null)
        {
            List<string> errors = actionCatalog.Validate();
            foreach (string error in errors) Debug.LogError("[SkillActions] " + error, actionCatalog);
            if (errors.Count > 0) return;
            foreach (var definition in actionCatalog.actions)
            {
                if (definition == null || !definition.enabled || string.IsNullOrWhiteSpace(definition.actionId)) continue;
                string id = definition.actionId.Trim();
                if (skillMap.TryGetValue(id, out ISkill existing))
                {
                    if (!(existing is ConfiguredSkillAction)) Debug.LogError("[SkillActions] Catalog ID conflicts with a legacy action: " + id, this);
                    continue;
                }
                skillMap.Add(id, new ConfiguredSkillAction(definition, this));
            }
        }
    }

    private ISkill CreateSkill(string actionId)
    {
        switch (actionId)
        {
            case "BasicSlash": return new BasicMeleeSkill();
            case "DodgeRoll": return new DodgeRollSkill();
            case "RatBleedBite": return new RatBleedBiteSkill();
            case "HoundRush": return new HoundRushSkill();
            case "SpiderPoisonBite": return new SpiderPoisonBiteSkill();
            case "ArcAttack": return GetOrAddSkill<StaticDamageZone>();
            case "NormalAttack": return GetOrAddSkill<NormalAttackSkill>();
            case "FlyAttack": return GetOrAddSkill<MovingHitbox>();
            case "Fireball": return GetOrAddSkill<FireballAttackSkill>();
            case "DashAttack2": return new DashAttackSkill2();
            case "ApplyStatus": return GetOrAddSkill<ApplyStatus>();
            case "SelfBuff": return new SelfBuffAction();
            case "SummonEntity": return GetOrAddSkill<SummonEntity>();
            case "CasterMove": return GetOrAddSkill<CasterMove>();
            case "ModifyResource": return GetOrAddSkill<ModifyResource>();
            case "DefenseWindow": return GetOrAddSkill<DefenseWindow>();
            case "ScreenEffect": return GetOrAddSkill<ScreenEffect>();
            case "ChallengeMeter": return GetOrAddSkill<ChallengeMeter>();
            case "ReplayAction": return GetOrAddSkill<ReplayAction>();
            case "ConditionGate": return GetOrAddSkill<ConditionGate>();
            default:
                SkillActionDefinition definition = actionCatalog != null ? actionCatalog.Find(actionId) : null;
                return definition != null && definition.enabled ? new ConfiguredSkillAction(definition, this) : null;
        }
    }

    private T GetOrAddSkill<T>() where T : MonoBehaviour, ISkill
    {
        T skill = GetComponent<T>();
        return skill != null ? skill : gameObject.AddComponent<T>();
    }

    public T GetModule<T>() where T : MonoBehaviour, ISkill => GetOrAddSkill<T>();
    public void CancelAction(string actionId, GameObject caster)
    {
        if (!string.IsNullOrWhiteSpace(actionId) && skillMap.TryGetValue(actionId, out ISkill action) && action is ConfiguredSkillAction configured) configured.Cancel(caster);
    }

    public void RemoveActionBuffs(string actionId, GameObject caster)
    {
        CancelAction(actionId, caster);
        DefenseWindow.RemoveForAction(caster, actionId);
        if (caster != null) caster.GetComponentInChildren<SelfBuff>(true)?.RemoveActionSources(actionId);
    }

    public bool IsActionRunning(string actionId, GameObject caster) => !string.IsNullOrWhiteSpace(actionId) && skillMap.TryGetValue(actionId, out ISkill action) && action is ConfiguredSkillAction configured && configured.IsRunning(caster);

    public bool HasRegisteredSkill(string actionId)
    {
        return !string.IsNullOrWhiteSpace(actionId) && skillMap.ContainsKey(actionId.Trim());
    }

    /// <summary>
    /// Danh sách actionId đã khai báo trong Inspector (không cần Play). SkillConfigValidator dùng để
    /// kiểm tra actionId trong SkillConfig đã được đăng ký.
    /// </summary>
    public List<string> GetConfiguredActionIds()
    {
        List<string> ids = new List<string>();
        if (actionCatalog?.actions != null)
            foreach (var action in actionCatalog.actions)
                if (action != null && action.enabled && !string.IsNullOrWhiteSpace(action.actionId) && !ids.Contains(action.actionId)) ids.Add(action.actionId);
        if (skillRegistrations == null) return ids;
        foreach (SkillRegistration registration in skillRegistrations)
        {
            if (registration != null && !string.IsNullOrWhiteSpace(registration.skillName))
                if (!ids.Exists(id => string.Equals(id, registration.skillName.Trim(), StringComparison.OrdinalIgnoreCase))) ids.Add(registration.skillName.Trim());
        }
        return ids;
    }

    /// <summary>actionId chọn class; context chứa thông số của skillId cụ thể.</summary>
    public void Activate(string actionId, SkillContext context)
    {
        if (GameplayPause.IsPaused) return;
        if (string.IsNullOrWhiteSpace(actionId))
        {
            Debug.LogWarning("[SkillActions] actionId bị trống.", this);
            return;
        }
        if (context == null)
        {
            Debug.LogWarning($"[SkillActions] SkillContext null khi gọi '{actionId}'.", this);
            return;
        }

        actionId = actionId.Trim();
        if (!skillMap.TryGetValue(actionId, out ISkill skill))
        {
            Debug.LogWarning($"[SkillActions] Không tìm thấy actionId '{actionId}'.", this);
            return;
        }

        CharacterAnimation animation = context.Caster != null ? context.Caster.GetComponentInChildren<CharacterAnimation>() : null;
        if (animation != null)
        {
            animation.SetFacing(context.Direction);
            string animationName = string.IsNullOrWhiteSpace(context.AnimationName) ? context.SkillId : context.AnimationName.Trim();
            if (!animation.IsPlayingSkill(animationName)) animation.PlayAnimationSkill(animationName);
            animation.BeginSkillCast();
        }
        context.ActionId = actionId;
        skill.Execute(context);
        ReplayAction.Record(actionId, context);
    }

    private void OnDestroy()
    {
        foreach (var action in skillMap.Values) if (action is ConfiguredSkillAction configured) configured.Shutdown();
        if (Instance == this) Instance = null;
        skillMap.Clear();
    }
}

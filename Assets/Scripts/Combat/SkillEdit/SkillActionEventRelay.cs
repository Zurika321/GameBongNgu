using UnityEngine;

/// <summary>Inspector bridge for death, phase and UI/input events. Does not create scene objects.</summary>
public sealed class SkillActionEventRelay : MonoBehaviour
{
    [SerializeField] private GameObject caster;
    [SerializeField] private GameObject target;
    [SerializeField] private SkillCasterType casterType;
    [SerializeField, RegistryId(RegistryIdType.Skill)] private string actionId;
    [SerializeField] private string animationSkillId;
    [SerializeField, Min(0f), Tooltip("Base ATK before the catalog's per-step percentages.")] private float baseDamage;
    [SerializeField] private SkillContextConfig contextConfig = new SkillContextConfig();
    [SerializeField] private bool castOnDeath;
    private bool held;
    private bool deathTriggered;
    private void OnEnable() { deathTriggered = false; SkillCombatEvents.Killed += OnKilled; }
    private void OnDisable() { SkillCombatEvents.Killed -= OnKilled; held = false; Cancel(); }
    private GameObject Actor => caster != null ? caster : gameObject;
    private void OnKilled(GameObject attacker, GameObject victim)
    {
        if (castOnDeath && !deathTriggered && victim == Actor) { deathTriggered = true; CastAt(attacker); }
    }
    public void BeginHold() { held = true; Cast(); }
    public void EndHold() { held = false; }
    public void Cast() => CastAt(target);
    public void CastAt(GameObject opponent)
    {
        if (SkillActions.Instance == null || string.IsNullOrWhiteSpace(actionId) || contextConfig == null)
        { Debug.LogWarning("[SkillActionEventRelay] Assign actionId/context and a scene SkillActions registry.", this); return; }
        GameObject actor = Actor;
        IFacingProvider facing = actor.GetComponent<IFacingProvider>();
        Vector2 direction = facing != null ? facing.FacingDirection : Vector2.zero;
        if (opponent != null) direction = ((Vector2)(opponent.transform.position - actor.transform.position)).normalized;
        var context = new SkillContext
        {
            Caster = actor, Target = opponent, CasterType = casterType,
            SkillId = animationSkillId, AnimationName = contextConfig.animationName, ActionId = actionId, Position = actor.transform.position,
            Direction = direction, Damage = baseDamage * contextConfig.damageMultiplier,
            Modules = contextConfig.modules, EffectAnimation = contextConfig.effectAnimation,
            ActionDamageOverrides = contextConfig.actionDamageOverrides,
            BreaksGuard = contextConfig.breaksGuard, BypassesMeleeResistance = contextConfig.bypassesMeleeResistance,
            IsHeld = () => held, Debug = contextConfig.debug
        };
        SkillActions.Instance.Activate(actionId, context);
    }
    public void Cancel() { if (SkillActions.Instance != null) SkillActions.Instance.CancelAction(actionId, Actor); }
    public void EndPhase() { if (SkillActions.Instance != null) SkillActions.Instance.RemoveActionBuffs(actionId, Actor); }
}

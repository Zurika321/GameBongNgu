using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Timed defense applied before HealthController subtracts HP; reflection cannot reflect recursively.</summary>
public sealed class DefenseWindow : MonoBehaviour, ISkill
{
    private sealed class Window { public GameObject target; public float until; public DefenseConfig config; public SkillContext context; public DefenseWindow owner; }
    private static readonly Dictionary<GameObject, Window> windows = new Dictionary<GameObject, Window>();
    private static readonly Dictionary<GameObject, Window> pendingReflections = new Dictionary<GameObject, Window>();
    private static bool reflecting;
    private static int counterDepth;
    [SerializeField] private UnityEvent<GameObject> onDefended = new UnityEvent<GameObject>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { windows.Clear(); pendingReflections.Clear(); reflecting = false; counterDepth = 0; }
    public void Execute(SkillContext context)
    {
        GameObject target = context?.Target != null ? context.Target : context?.Caster;
        DefenseConfig config = context?.Modules?.defense;
        if (target == null || config == null || config.duration < 0f) return;
        HealthController health = target.GetComponentInParent<HealthController>();
        if (health != null) target = health.gameObject;
        windows[target] = new Window { target = target, until = config.duration > 0f ? Time.time + config.duration : float.PositiveInfinity, config = config, context = context, owner = this };
    }
    public static float FilterDamage(GameObject target, GameObject attacker, float damage, SkillContext hit = null)
    {
        if (reflecting || target == null || !windows.TryGetValue(target, out Window window)) return damage;
        if (window.target == null || window.owner == null || !window.owner.isActiveAndEnabled || window.until <= Time.time)
        { windows.Remove(target); return damage; }
        DefenseConfig config = window.config;
        if (config.meleeOnly && (hit == null || hit.DamageKind != SkillDamageKind.Melee))
        {
            if (config.breakable && hit != null && hit.BreaksGuard) windows.Remove(target);
            return damage;
        }
        if (hit != null && (hit.BreaksGuard || (config.mode == DefenseMode.Reduce && hit.BypassesMeleeResistance)))
        { if (config.breakable) windows.Remove(target); return damage; }
        if (config.consumeOnHit) windows.Remove(target);
        if (config.mode == DefenseMode.Reflect && attacker != null && attacker != target)
            pendingReflections[target] = window;
        window.owner.onDefended.Invoke(attacker);
        window.context.OnDefenseCallback?.Invoke(attacker);
        if (counterDepth < 8 && config.mode == DefenseMode.Parry && !string.IsNullOrWhiteSpace(config.counterActionId) && SkillActions.Instance != null)
        {
            SkillContext counter = window.context.CopyFor(target);
            counter.Target = attacker;
            if (attacker != null) counter.Direction = ((Vector2)(attacker.transform.position - target.transform.position)).normalized;
            counterDepth++;
            try { SkillActions.Instance.Activate(config.counterActionId, counter); }
            finally { counterDepth--; }
        }
        return config.mode == DefenseMode.Parry || config.mode == DefenseMode.Immune ? 0f
            : config.mode == DefenseMode.Reduce ? damage * (1f - Mathf.Clamp01(config.fraction)) : damage;
    }
    public static void RemoveForAction(GameObject caster, string actionId)
    {
        foreach (var pair in new List<KeyValuePair<GameObject, Window>>(windows))
            if (pair.Value.context.Caster == caster && pair.Value.context.ActionId == actionId)
                windows.Remove(pair.Key);
        foreach (var pair in new List<KeyValuePair<GameObject, Window>>(pendingReflections))
            if (pair.Value.context.Caster == caster && pair.Value.context.ActionId == actionId)
                pendingReflections.Remove(pair.Key);
    }
    public static void AfterDamage(GameObject target, GameObject attacker, float actualDamage)
    {
        if (reflecting || target == null || !pendingReflections.TryGetValue(target, out Window window)) return;
        pendingReflections.Remove(target);
        if (attacker == null || actualDamage <= 0f) return;
        reflecting = true;
        try { attacker.GetComponentInParent<IDamage>()?.TakeDamage(actualDamage * Mathf.Clamp01(window.config.fraction), target); }
        finally { reflecting = false; }
    }

}

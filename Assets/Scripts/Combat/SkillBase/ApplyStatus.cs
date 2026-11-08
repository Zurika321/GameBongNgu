using System.Collections.Generic;
using UnityEngine;

/// <summary>Timed, non-stacking status state. Slow is read by the movement controllers.</summary>
public sealed class ApplyStatus : MonoBehaviour, ISkill
{
    private sealed class State { public GameObject target; public float until; public float strength; }
    private static readonly Dictionary<(GameObject, string), State> states = new Dictionary<(GameObject, string), State>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => states.Clear();
    public void Execute(SkillContext context) => Apply(context?.Target != null ? context.Target : context?.Caster, context?.Modules?.status);
    public static void Apply(GameObject target, StatusConfig config)
    {
        if (target == null || config == null || config.duration <= 0f) return;
        Component health = target.GetComponentInParent<HealthController>();
        if (health != null) target = health.gameObject;
        if (config.kind == SkillStatusKind.Stun)
        {
            var stats = target.GetComponent<RuntimeStatModifierStore>();
            float duration = config.duration * (1f - Mathf.Clamp01(stats != null ? stats.GetFinalValue(PlayerStatType.StunResistance, 0f) : 0f));
            PlayerController player = target.GetComponent<PlayerController>();
            EnemyController enemy = target.GetComponent<EnemyController>();
            if (player != null) player.ApplyHitReaction(duration);
            else if (enemy != null) enemy.ApplyHitReaction(duration);
            return;
        }
        var key = Key(target, config.kind == SkillStatusKind.Slow ? "Slow" : config.statusId);
        states[key] = new State { target = target, until = Time.time + config.duration, strength = Mathf.Clamp01(config.strength) };
        Prune();
    }
    private static (GameObject, string) Key(GameObject target, string id) => (target, id);
    public static bool HasStatus(GameObject target, string id) => Get(target, id) != null;
    public static float MovementMultiplier(GameObject target) => 1f - (Get(target, "Slow")?.strength ?? 0f);
    private static State Get(GameObject target, string id)
    {
        if (target == null) return null;
        var key = Key(target, id);
        if (!states.TryGetValue(key, out State state)) return null;
        if (state.target == null || state.until <= Time.time) { states.Remove(key); return null; }
        return state;
    }
    private static void Prune()
    {
        var expired = new List<(GameObject, string)>();
        foreach (var item in states) if (item.Value.target == null || item.Value.until <= Time.time) expired.Add(item.Key);
        foreach (var key in expired) states.Remove(key);
    }
}

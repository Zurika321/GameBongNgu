using System.Collections.Generic;
using UnityEngine;

/// <summary>Reusable branch gate. Counters record actual combat events and expire by the configured window.</summary>
public sealed class ConditionGate : MonoBehaviour, ISkill
{
    private sealed class Sample { public GameObject caster; public float time; public bool hit; }
    private static readonly List<Sample> samples = new List<Sample>();
    private static int depth;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { samples.Clear(); depth = 0; }
    public static void RecordHit(GameObject caster) => Record(caster, true);
    public static void RecordDamage(GameObject caster) => Record(caster, false);
    private static void Record(GameObject caster, bool hit)
    {
        if (caster == null) return;
        samples.RemoveAll(sample => sample.caster == null);
        samples.Add(new Sample { caster = caster, time = Time.time, hit = hit });
        if (samples.Count > 2048) samples.RemoveAt(0);
    }
    public static bool Evaluate(SkillContext context, ConditionConfig config)
    {
        if (context?.Caster == null || config == null) return false;
        IHealth caster = context.Caster.GetComponent<IHealth>();
        IHealth target = context.Target != null ? context.Target.GetComponentInParent<IHealth>() : null;
        bool result;
        switch (config.kind)
        {
            case SkillConditionKind.KilledByMelee:
                HealthController health = context.Caster.GetComponent<HealthController>();
                result = health != null && health.IsDead && health.LastDamageKind == SkillDamageKind.Melee; break;
            case SkillConditionKind.Chance: result = Random.value < config.threshold; break;
            case SkillConditionKind.CasterHealthBelow: result = caster != null && caster.CurrentHealth / Mathf.Max(1f, caster.MaxHealth) <= config.threshold; break;
            case SkillConditionKind.TargetHealthBelow: result = target != null && target.CurrentHealth / Mathf.Max(1f, target.MaxHealth) <= config.threshold; break;
            case SkillConditionKind.NearWall: result = Physics2D.OverlapCircle(context.Position, config.wallDistance, config.wallMask) != null; break;
            case SkillConditionKind.Flag: result = PlayerProgressionService.Instance?.Data != null && PlayerProgressionService.Instance.Data.HasFlag(config.flag); break;
            case SkillConditionKind.HitCount:
            case SkillConditionKind.DamageTakenCount:
                bool hits = config.kind == SkillConditionKind.HitCount;
                result = samples.FindAll(sample => sample.caster == context.Caster && sample.hit == hits && Time.time - sample.time <= config.withinSeconds).Count >= config.requiredCount; break;
            default: result = true; break;
        }
        return config.invert ? !result : result;
    }
    public void Execute(SkillContext context)
    {
        ConditionConfig config = context?.Modules?.condition;
        if (config == null || depth >= 8 || SkillActions.Instance == null) return;
        string action = Evaluate(context, config) ? config.passActionId : config.failActionId;
        if (string.IsNullOrWhiteSpace(action)) return;
        depth++;
        try { SkillActions.Instance.Activate(action, context); }
        finally { depth--; }
    }
}

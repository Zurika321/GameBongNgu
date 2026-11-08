using UnityEngine;

public static class SkillActionDamage
{
    public static float Deal(SkillContext context, GameObject target)
    {
        if (context == null || target == null) return 0f;
        HealthController health = target.GetComponentInParent<HealthController>();
        IDamage receiver = target.GetComponentInParent<IDamage>();
        if (receiver == null) return 0f;
        float before = health != null ? health.CurrentHealth : 0f;
        if (health != null) health.TakeSkillDamage(context);
        else receiver.TakeDamage(context.Damage, context.Caster, context.StunTime);
        float dealt = health != null ? Mathf.Max(0f, before - health.CurrentHealth) : 0f;
        context.DamageDealt = dealt;
        if (dealt > 0f) SkillCombatEvents.ReportHit(context, target, health != null && health.IsDead);
        return dealt;
    }
}

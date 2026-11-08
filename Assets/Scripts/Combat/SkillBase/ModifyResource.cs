using System.Collections;
using UnityEngine;

/// <summary>Health changes never create story choices. LT/QT use the existing player data fields.</summary>
public sealed class ModifyResource : MonoBehaviour, ISkill
{
    public void Execute(SkillContext context)
    {
        if (context?.Caster == null || context.Modules?.resource == null) return;
        StartCoroutine(Run(context));
    }
    private IEnumerator Run(SkillContext context)
    {
        ResourceConfig config = context.Modules.resource;
        GameObject target = context.Target != null ? context.Target : context.Caster;
        IHealth health = target.GetComponent<IHealth>();
        float hp = health != null ? health.CurrentHealth : 0f;
        Vector3 position = target.transform.position;
        for (int i = 0; i < Mathf.Max(1, config.ticks) && target != null; i++)
        {
            if (config.cancelOnMove && Vector3.Distance(position, target.transform.position) > config.moveTolerance) yield break;
            if (config.cancelOnDamage && health != null && health.CurrentHealth < hp) yield break;
            Apply(context, config, target);
            if (health != null) hp = health.CurrentHealth;
            if (i + 1 < config.ticks) yield return new WaitForSeconds(Mathf.Max(0.01f, config.interval));
        }
    }
    public static void Apply(SkillContext context, ResourceConfig config, GameObject target)
    {
        if (context == null || config == null || target == null) return;
        HealthController health = target.GetComponentInParent<HealthController>();
        float amount = config.amount;
        if (config.amountMode == ResourceAmountMode.FractionMaxHealth) amount *= health != null ? health.MaxHealth : 0f;
        else if (config.amountMode == ResourceAmountMode.FractionDamage) amount *= Mathf.Max(0f, context.DamageDealt);
        if (config.resource == SkillResource.Health)
        {
            if (health == null || health.IsDead) return;
            if (amount >= 0f) health.Heal(amount);
            else health.TakeDamage(-amount, context.Caster);
            return;
        }
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress?.Data == null || target.GetComponentInParent<PlayerController>() == null) return;
        if (config.resource == SkillResource.Sanity) progress.Data.lt = Mathf.Max(0, progress.Data.lt + Mathf.RoundToInt(amount));
        else progress.Data.qt = Mathf.Max(0, progress.Data.qt + Mathf.RoundToInt(amount));
        if (config.persist) progress.Save();
    }
}

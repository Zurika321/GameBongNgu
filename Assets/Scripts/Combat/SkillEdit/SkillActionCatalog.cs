using System;
using System.Collections.Generic;
using UnityEngine;

public enum ActionModule { DamageZone, Projectile, DotOnHit, EffectArea, Status, Buff, Summon, Move, Resource, Defense, Screen, Challenge, Replay, Condition }
public enum ActionTrigger { Timeline, Hit, ProjectileEnd, Defended, ChallengeFailed }
public enum ActionTarget { Caster, Opponent, HitTarget }
public enum SkillDamageKind { Unknown, Melee, Projectile, DamageOverTime }

[CreateAssetMenu(fileName = "SkillActionCatalog", menuName = "Bong Ngu/Skill Action Catalog")]
public sealed class SkillActionCatalog : ScriptableObject
{
    [Tooltip("Recipes imported from the workbook; all values remain editable.")]
    public List<SkillActionDefinition> actions = new List<SkillActionDefinition>();
    public SkillActionDefinition Find(string id) => actions?.Find(action => action != null && string.Equals(action.actionId, id, StringComparison.OrdinalIgnoreCase));
    public List<string> Validate()
    {
        var errors = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (actions == null) { errors.Add("No action list."); return errors; }
        foreach (var action in actions)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.actionId)) { errors.Add("Missing action ID."); continue; }
            if (!names.Add(action.actionId)) errors.Add("Duplicate action ID: " + action.actionId);
            if (action.steps == null || action.steps.Count == 0) { errors.Add(action.actionId + ": no steps."); continue; }
            for (int i = 0; i < action.steps.Count; i++)
            {
                var step = action.steps[i];
                if (step == null) { errors.Add(action.actionId + ": null step."); continue; }
                if (float.IsNaN(step.delay) || float.IsInfinity(step.delay) || float.IsNaN(step.interval) || float.IsInfinity(step.interval) || step.delay < 0f || step.repeatCount < 0 || (step.repeatCount != 1 && step.interval <= 0f)) errors.Add(action.actionId + ": invalid schedule at step " + i);
                if (step.trigger != ActionTrigger.Timeline && (step.sourceStep < 0 || step.sourceStep >= action.steps.Count || step.sourceStep >= i)) errors.Add(action.actionId + ": invalid event source at step " + i);
            }
        }
        return errors;
    }
}
[Serializable]
public sealed class SkillActionDefinition
{
    public string actionId;
    public string displayName;
    public bool enabled = true;
    public bool lockMovement;
    public bool allowDeadCaster;
    public bool requiresHold;
    public bool cancelOnMove;
    public bool cancelOnDamage;
    [Min(0f)] public float moveTolerance = 0.1f;
    public bool maintainCondition;
    public ConditionConfig condition = new ConditionConfig();
    [TextArea] public string setupNotes;
    public List<SkillActionStep> steps = new List<SkillActionStep>();
}
[Serializable]
public sealed class SkillActionStep
{
    public ActionModule module;
    public ActionTrigger trigger;
    [Tooltip("Zero-based source step for Hit/ProjectileEnd/Defended/ChallengeFailed.")]
    public int sourceStep = -1;
    [Tooltip("-1 = any repetition. Otherwise zero-based iteration, e.g. 4 = final hit of a 5-hit combo.")]
    public int sourceIteration = -1;
    [Min(0f)] public float delay;
    [Tooltip("0 = repeat until action cancelled; interval must be positive.")]
    [Min(0)] public int repeatCount = 1;
    [Min(0f)] public float interval;
    public ActionTarget target;
    public bool requirePreviousHit;
    [Min(1)] public int minimumConsecutiveHits = 1;
    [Range(0f, 1f)] public float chance = 1f;
    [Header("Hitbox / projectile")]
    public HitboxShape shape;
    public Vector2 size;
    [Min(0f)] public float sizeGrowthPerRepeat;
    public float forwardOffset;
    [Range(1f, 360f)] public float coneAngle = 180f;
    [Min(0f)] public float lifetime;
    [Min(0f)] public float speed;
    [Min(0f)] public float travelDistance;
    public bool canPierce;
    public bool followCaster;
    public bool telegraph;
    [Header("Damage, percentages of incoming skill damage")]
    public float[] damagePercents;
    [Tooltip("Negative = use percentage damage. Nonnegative = fixed damage.")]
    public float fixedDamage = -1f;
    public SkillDamageKind damageKind;
    [Range(0f, 1f)] public float armorPenetration;
    public bool breaksGuard;
    public bool bypassesMeleeResistance;
    [Min(0f)] public float knockback;
    [Header("Module settings")]
    [Tooltip("Use context.Modules instead of the recipe's module settings (e.g. per-weapon summon prefab).")]
    public bool useSkillModuleSettings;
    public SkillModuleConfig settings = new SkillModuleConfig();
    public DameDotConfig dot = new DameDotConfig();
    public bool clearBuffWhenCancelled;
}
public static class SkillActionMath
{
    public static float Damage(float baseDamage, SkillActionStep step, int iteration)
    {
        if (step.fixedDamage >= 0f) return step.fixedDamage;
        if (step.damagePercents == null || step.damagePercents.Length == 0) return 0f;
        return Mathf.Max(0f, baseDamage * step.damagePercents[Mathf.Clamp(iteration, 0, step.damagePercents.Length - 1)] / 100f);
    }
    public static float ScheduledTime(SkillActionStep step, int iteration) => step.delay + Mathf.Max(0, iteration) * step.interval;
}

[Serializable]
public sealed class SkillActionDamageOverride
{
    [Min(0)] public int stepIndex;
    public float[] damagePercents;
}

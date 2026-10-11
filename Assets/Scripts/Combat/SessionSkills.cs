using System.Collections;
using UnityEngine;

public class BasicMeleeSkill : ISkill
{
    public void Execute(SkillContext context)
    {
        if (context == null || context.Caster == null) return;

        ArcAttackSkill.CreateHitbox(context);
        PlayerController controller = context.Caster.GetComponent<PlayerController>();
        if (controller != null && SkillActions.Instance != null)
            SkillActions.Instance.StartCoroutine(UnlockAfterAttack(controller, context.Duration));
    }

    private static IEnumerator UnlockAfterAttack(PlayerController controller, float duration)
    {
        controller.LockMovement(true);
        yield return new WaitForSeconds(Mathf.Max(0.08f, duration));
        if (controller != null) controller.LockMovement(false);
    }
}

public class DodgeRollSkill : ISkill
{
    public void Execute(SkillContext context)
    {
        if (context == null || context.Caster == null || SkillActions.Instance == null) return;
        SkillActions.Instance.StartCoroutine(DodgeRoutine(context));
    }

    private static IEnumerator DodgeRoutine(SkillContext context)
    {
        GameObject caster = context.Caster;
        Rigidbody2D body = caster.GetComponent<Rigidbody2D>();
        HealthController health = caster.GetComponent<HealthController>();
        PlayerController controller = caster.GetComponent<PlayerController>();
        if (body == null || health == null) yield break;

        Vector2 direction = context.Direction.sqrMagnitude > 0.001f
            ? context.Direction.normalized
            : Vector2.down;
        float duration = Mathf.Max(0.08f, context.Duration);
        health.SetInvincible(true);
        if (controller != null)
        {
            controller.Dash(direction, context.Speed, duration);
        }
        else
        {
            body.linearVelocity = direction * Mathf.Max(1f, context.Speed);
        }
        yield return new WaitForSeconds(duration);

        if (controller == null && body != null) body.linearVelocity = Vector2.zero;
        if (health != null) health.SetInvincible(false);
    }
}

public class RatBleedBiteSkill : ISkill
{
    public void Execute(SkillContext context)
    {
        if (context != null) ArcAttackSkill.CreateHitbox(context);
    }
}

public class HoundRushSkill : ISkill
{
    public void Execute(SkillContext context)
    {
        if (context == null || context.Caster == null || SkillActions.Instance == null) return;
        SkillActions.Instance.StartCoroutine(RushRoutine(context));
    }

    private static IEnumerator RushRoutine(SkillContext context)
    {
        GameObject caster = context.Caster;
        Rigidbody2D body = caster.GetComponent<Rigidbody2D>();
        if (body == null) yield break;

        Vector2 direction = context.Direction.sqrMagnitude > 0.001f
            ? context.Direction.normalized
            : Vector2.right;
        EnemyController controller = caster.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.Dash(direction, context.Speed, context.Duration);
        }
        else
        {
            body.linearVelocity = direction * Mathf.Max(1f, context.Speed);
        }
        yield return new WaitForSeconds(Mathf.Max(0.08f, context.Duration));

        if (controller == null && body != null) body.linearVelocity = Vector2.zero;
        context.Position = caster.transform.position;
        ArcAttackSkill.CreateHitbox(context);
    }
}

public class SpiderPoisonBiteSkill : ISkill
{
    public void Execute(SkillContext context)
    {
        if (context != null) ArcAttackSkill.CreateHitbox(context);
    }
}

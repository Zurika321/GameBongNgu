using System.Collections;
using UnityEngine;

public class BasicMeleeSkill : ISkill
{
    private enum DirectionBlock
    {
        Down = 0,
        Up = 1,
        Left = 2,
        Right = 3
    }

    public void Execute(SkillContext context)
    {
        if (context == null || context.Caster == null) return;

        ArcAttackSkill.CreateHitbox(context);
        PlayerController controller = context.Caster.GetComponent<PlayerController>();
        CharacterAnimation animation = context.Caster.GetComponentInChildren<CharacterAnimation>();
        Sprite[] animationFrames = GetDirectionalFrames(context.CharacterAnimationSprites, context.Direction);
        float duration = context.Duration > 0f ? context.Duration : 0.2f;

        if (SkillActions.Instance != null)
        {
            SkillActions.Instance.StartCoroutine(AttackRoutine(
                controller, animation, animationFrames, duration));
        }
        else if (controller != null)
        {
            Debug.LogWarning("[BasicMeleeSkill] SkillActions is missing; cannot play the attack animation or unlock movement.", context.Caster);
        }
    }

    private static IEnumerator AttackRoutine(
        PlayerController controller,
        CharacterAnimation animation,
        Sprite[] frames,
        float duration)
    {
        if (controller != null) controller.LockMovement(true);
        if (animation != null && frames.Length > 0)
            animation.PlayCustom(frames, frames.Length / duration, false);

        yield return new WaitForSeconds(duration);
        if (controller != null) controller.LockMovement(false);
    }

    private static Sprite[] GetDirectionalFrames(Sprite[] sprites, Vector2 direction)
    {
        if (sprites == null || sprites.Length < 4 || sprites.Length % 4 != 0)
            return System.Array.Empty<Sprite>();

        int framesPerDirection = sprites.Length / 4;
        int startIndex = ResolveDirectionBlock(direction) * framesPerDirection;
        Sprite[] frames = new Sprite[framesPerDirection];
        System.Array.Copy(sprites, startIndex, frames, 0, framesPerDirection);
        return frames;
    }

    private static int ResolveDirectionBlock(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return (int)DirectionBlock.Down;
        if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x))
            return direction.y >= 0f ? (int)DirectionBlock.Up : (int)DirectionBlock.Down;
        return direction.x >= 0f ? (int)DirectionBlock.Right : (int)DirectionBlock.Left;
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

using UnityEngine;

public class DashAttackSkill2 : ISkill
{
    public void Execute(SkillContext context)
    {
        if (context.Caster == null) return;

        Sprite[] skillSprites = context.EffectAnimation?.GetCast(context.Direction);
        SetCasterVisible(context, false);
        SetCasterInvincible(context, true);

        Sprite visual = skillSprites != null && skillSprites.Length > 0
            ? skillSprites[0]
            : null;

        context.CanPierce = true;
        GameObject dashProjectile = MovingHitbox.CreateProjectile(context, visual, finalPosition =>
        {
            if (context.Caster != null)
            {
                context.Caster.transform.position = finalPosition;
            }

            SetCasterVisible(context, true);
            SetCasterInvincible(context, false);
        });

        SkillEffectPlayer.Attach(dashProjectile, context);

        if (dashProjectile == null)
        {
            SetCasterVisible(context, true);
            SetCasterInvincible(context, false);
        }
    }

    private static int ResolveDirectionBlock(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return 0;
        if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x))
            return direction.y >= 0f ? 1 : 0;
        return direction.x >= 0f ? 3 : 2;
    }

    private void SetCasterVisible(SkillContext context, bool visible)
    {
        if (context.Caster == null) return;

        SpriteRenderer spriteRenderer = context.Caster.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }
    }

    private void SetCasterInvincible(SkillContext context, bool invincible)
    {
        if (context.CasterType == SkillCasterType.Player)
        {
            IHealth health = context.Caster.GetComponent<IHealth>();
            if (health != null) health.SetInvincible(invincible);
        }
        else if (context.CasterType == SkillCasterType.Enemy)
        {
            IHealth health = context.Caster.GetComponent<IHealth>();
            if (health != null) health.SetInvincible(invincible);
        }
    }
}
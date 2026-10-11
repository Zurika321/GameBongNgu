using UnityEngine;

public class DashAttackSkill2 : ISkill, ISkillSpriteConfigurable
{
    private Sprite[] skillSprites;

    public void SetSkillSprites(Sprite[] sprites)
    {
        skillSprites = sprites;
    }

    public void Execute(SkillContext context)
    {
        if (context.Caster == null) return;

        SetCasterVisible(context, false);
        SetCasterInvincible(context, true);

        Sprite visual = skillSprites != null && skillSprites.Length >= 4
            ? skillSprites[ResolveDirectionBlock(context.Direction)]
            : null;

        context.CanPierce = true;
        GameObject dashProjectile = FlyAttackSkill.CreateProjectile(context, visual, finalPosition =>
        {
            if (context.Caster != null)
            {
                context.Caster.transform.position = finalPosition;
            }

            SetCasterVisible(context, true);
            SetCasterInvincible(context, false);
        });

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
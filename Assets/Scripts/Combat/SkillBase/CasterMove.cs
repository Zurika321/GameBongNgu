using System.Collections;
using UnityEngine;

/// <summary>Reusable dash with a configurable invulnerability interval. Uses existing controllers.</summary>
public sealed class CasterMove : MonoBehaviour, ISkill
{
    public void Execute(SkillContext context) => Perform(context);
    public static Coroutine Perform(SkillContext context, bool forceInvincible = false)
    {
        if (context?.Caster == null || SkillActions.Instance == null) return null;
        return SkillActions.Instance.StartCoroutine(Run(context, forceInvincible));
    }
    private static IEnumerator Run(SkillContext context, bool forceInvincible)
    {
        GameObject caster = context.Caster;
        Rigidbody2D body = caster.GetComponent<Rigidbody2D>();
        IHealth health = caster.GetComponent<IHealth>();
        if (body == null) yield break;
        CasterMoveConfig config = context.Modules?.movement ?? new CasterMoveConfig();
        float duration = Mathf.Max(0.01f, context.Duration);
        Vector2 direction = context.Direction.sqrMagnitude > 0.001f ? context.Direction.normalized : Vector2.down;
        PlayerController player = caster.GetComponent<PlayerController>();
        EnemyController enemy = caster.GetComponent<EnemyController>();
        if (player != null) player.Dash(direction, context.Speed, duration);
        else if (enemy != null) enemy.Dash(direction, context.Speed, duration);
        bool originalInvincible = health != null && health.IsInvincible;
        bool changed = false;
        try
        {
            float elapsed = 0f;
            while (caster != null && elapsed < duration)
            {
                bool immune = (forceInvincible || config.invincible) && elapsed >= config.invincibleStart
                    && elapsed < (config.invincibleEnd > config.invincibleStart ? config.invincibleEnd : duration);
                if (health != null && immune != changed) { health.SetInvincible(originalInvincible || immune); changed = immune; }
                if (player == null && enemy == null) body.linearVelocity = GameplayPause.IsPaused ? Vector2.zero : direction * Mathf.Max(0f, context.Speed);
                if (!GameplayPause.IsPaused) elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            if (caster != null && health != null && changed) health.SetInvincible(originalInvincible);
            if (body != null && player == null && enemy == null) body.linearVelocity = Vector2.zero;
        }
    }
}

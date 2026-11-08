using UnityEngine;

/// <summary>
/// Skill "vòng cung trước mặt" — PHIÊN BẢN MỚI dùng ZoneHitboxSpawner.
/// Hitbox (ZoneHitboxSpawner + HitboxReporter) chỉ lo geometry + debug + báo callback khi trúng ai.
/// Toàn bộ việc "trúng thì làm gì" (gây dame, áp DOT) nằm Ở ĐÂY, trong skill —
/// không còn dính thẳng vào bên trong hitbox như bản StaticDamageZone cũ.
/// </summary>
public class StaticDamageZone : MonoBehaviour, ISkill
{
    public void Execute(SkillContext context)
    {
        if (context == null) return;

        CreateHitbox(context);

        // DOT kiểu Zone tạo thêm một vùng DOT riêng, tồn tại độc lập với hitbox đòn đánh này.
        if (context.DotConfig != null && context.DotConfig.activationType == DotActivationType.Zone)
        {
            DotZone.CreateZone(context);
        }
    }

    public static GameObject CreateHitbox(
        SkillContext context,
        Vector2? overridePosition = null,
        Vector2? overrideSize = null,
        bool rotateWithDirection = true)
    {
        if (context == null) return null;

        Vector2 dir = context.Direction.sqrMagnitude > 0.001f ? context.Direction.normalized : Vector2.right;
        Vector2 spawnPos = overridePosition ?? context.Position + dir * context.Range;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        float lifeTime = context.Duration > 0f ? context.Duration : 0.2f;
        Vector2 size = overrideSize ?? context.Size;
        if (size.sqrMagnitude <= 0.001f) size = new Vector2(1f, 1f);

        GameObject hitbox = ZoneHitboxSpawner.Spawn(
            owner: context.Caster,
            position: spawnPos,
            shape: HitboxShape.Box,
            size: size,
            lifeTime: lifeTime,
            targetTag: context.ResolveTargetTag(),
            showDebug: context.Debug,
            onHit: (hitGO, hitPos) => OnHit(context, hitGO, hitPos),
            rotationDeg: angle,
            rotateWithDirection: rotateWithDirection
        );
        SkillEffectPlayer.Attach(hitbox, context);
        return hitbox;
    }

    /// <summary>
    /// Nơi DUY NHẤT xử lý "trúng đòn thì làm gì" cho ArcAttack: gây damage + áp DOT (nếu Immediate).
    /// Hitbox không còn tự quyết định việc này nữa.
    /// </summary>
    private static void OnHit(SkillContext context, GameObject hitGO, Vector2 hitPos)
    {
        IDamage damageable = hitGO.GetComponentInParent<IDamage>();
        bool targetDied = false;
        if (damageable != null)
        {
            IHealth targetHealth = hitGO.GetComponentInParent<IHealth>();
            float before = targetHealth != null ? targetHealth.CurrentHealth : 0f;
            if (context.DamageKind == SkillDamageKind.Unknown) context.DamageKind = SkillDamageKind.Melee;
            HealthController targetController = hitGO.GetComponentInParent<HealthController>();
            if (targetController != null) targetController.TakeSkillDamage(context);
            else damageable.TakeDamage(context.Damage, context.Caster, context.StunTime);
            context.DamageDealt = targetHealth != null ? Mathf.Max(0f, before - targetHealth.CurrentHealth) : 0f;
            HealthController health = hitGO.GetComponentInParent<HealthController>();
            targetDied = health != null && health.IsDead;
        }
        else
        {
            Debug.Log($"[StaticDamageZone] '{hitGO.name}' chưa implement IDamage -> {context.Damage} dame không được áp dụng.");
        }

        if (context.DotConfig != null && context.DotConfig.activationType == DotActivationType.Immediate)
        {
            DotOnHit.ApplyOrRefresh(hitGO, context.Caster, context.DotConfig);
        }

        SkillCombatEvents.ReportHit(context, hitGO, targetDied);
        context.OnHitCallback?.Invoke(hitGO, hitPos);
    }
}

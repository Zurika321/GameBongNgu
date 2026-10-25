using System;
using UnityEngine;

/// <summary>
/// Skill đạn bay — PHIÊN BẢN MỚI dùng ProjectileHitboxSpawner.
/// Hitbox (ProjectileHitboxSpawner + ProjectileHitboxReporter) chỉ lo di chuyển + geometry + debug
/// + báo callback khi trúng ai. Sát thương/DOT xử lý Ở ĐÂY, trong skill.
///
/// </summary>
public class FlyAttackSkill : MonoBehaviour, ISkill, ISkillSpriteConfigurable
{
    private enum DirectionBlock { Down = 0, Up = 1, Left = 2, Right = 3 }

    [SerializeField] private Sprite[] skillSprites;

    public void SetSkillSprites(Sprite[] sprites)
    {
        skillSprites = sprites;
    }

    public void Execute(SkillContext context)
    {
        if (context == null) return;
        bool hasDirectionalSprites = HasFourDirectionSprites(skillSprites);
        CreateProjectile(
            context,
            ResolveDirectionSprite(skillSprites, context.Direction),
            null,
            skillSprites,
            !hasDirectionalSprites);
    }

    public static GameObject CreateProjectile(SkillContext context)
    {
        return CreateProjectile(context, null, null, null);
    }

    /// <summary>Overload cho skill khác cần sprite riêng + callback báo vị trí đạn biến mất.</summary>
    public static GameObject CreateProjectile(
        SkillContext context,
        Sprite visualSprite,
        Action<Vector2> onDisappear,
        Sprite[] configuredSprites = null,
        bool rotateWithDirection = true)
    {
        if (context == null) return null;

        Vector2 dir = context.Direction.sqrMagnitude > 0.001f ? context.Direction.normalized : Vector2.right;
        Vector2 spawnPos = context.Position + dir * context.Range;

        float lifeTime = context.Duration > 0f ? context.Duration : 0.2f;
        float speed = context.Speed > 0f ? context.Speed : 5f;
        Vector2 size = context.Size.sqrMagnitude > 0.001f ? context.Size : new Vector2(1f, 1f);

        GameObject projectileObj = ProjectileHitboxSpawner.Spawn(
            owner: context.Caster,
            spawnPos: spawnPos,
            direction: dir,
            speed: speed,
            lifeTime: lifeTime,
            shape: HitboxShape.Box,
            size: size,
            targetTag: context.ResolveTargetTag(),
            canPierce: context.CanPierce,
            showDebug: context.Debug,
            onHit: (hitGO, hitPos) => OnHit(context, hitGO, hitPos),
            onDisappear: onDisappear,
            rotateWithDirection: rotateWithDirection
        );

        AttachVisualSprite(projectileObj, context, dir, visualSprite, configuredSprites);

        return projectileObj;
    }

    /// <summary>
    /// Nơi DUY NHẤT xử lý "trúng đòn thì làm gì" cho FlyAttack: gây damage + áp DOT (nếu Immediate).
    /// Hitbox (ProjectileHitboxReporter) không còn tự TakeDamage() như FlyProjectile cũ nữa.
    /// </summary>
    private static void OnHit(SkillContext context, GameObject hitGO, Vector2 hitPos)
    {
        IDamage damageable = hitGO.GetComponentInParent<IDamage>();
        if (damageable != null)
        {
            damageable.TakeDamage(context.Damage, context.Caster, context.StunTime);
        }
        else
        {
            Debug.Log($"[FlyAttackSkill] '{hitGO.name}' chưa implement IDamage -> {context.Damage} dame không được áp dụng.");
        }

        if (context.DotConfig != null && context.DotConfig.activationType == DotActivationType.Immediate)
        {
            DameDotEffect.ApplyOrRefresh(hitGO, context.Caster, context.DotConfig);
        }

        context.OnHitCallback?.Invoke(hitGO, hitPos);
    }

    private static void AttachVisualSprite(
        GameObject projectileObj,
        SkillContext context,
        Vector2 dir,
        Sprite visualSprite,
        Sprite[] configuredSprites)
    {
        Sprite spriteToUse = visualSprite != null ? visualSprite : ResolveDirectionSprite(configuredSprites, dir);
        if (spriteToUse == null) return;

        GameObject visualObj = new GameObject("Visual");
        visualObj.transform.SetParent(projectileObj.transform, false);
        visualObj.transform.localRotation = Quaternion.identity;

        SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
        sr.sprite = spriteToUse;

        sr.sortingLayerName = "skill";
        SpriteRenderer casterSprite = context.Caster != null
            ? context.Caster.GetComponentInChildren<SpriteRenderer>()
            : null;
        if (casterSprite != null)
            sr.sortingOrder = casterSprite.sortingOrder;
    }

    private static Sprite ResolveDirectionSprite(Sprite[] sprites, Vector2 direction)
    {
        if (sprites == null || sprites.Length == 0) return null;
        if (!HasFourDirectionSprites(sprites)) return sprites[0];
        return sprites[(int)ResolveDirectionBlock(direction)];
    }

    private static bool HasFourDirectionSprites(Sprite[] sprites)
    {
        return sprites != null && sprites.Length == 4;
    }

    private static DirectionBlock ResolveDirectionBlock(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return DirectionBlock.Down;
        if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x))
            return direction.y >= 0f ? DirectionBlock.Up : DirectionBlock.Down;
        return direction.x >= 0f ? DirectionBlock.Right : DirectionBlock.Left;
    }
}

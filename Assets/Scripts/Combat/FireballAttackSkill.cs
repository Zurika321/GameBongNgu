using System;
using UnityEngine;

/// <summary>
/// Skill "cầu lửa": bay đi theo hướng cast (dùng lại logic bay/va chạm/damage của FlyAttackSkill),
/// khi va chạm mục tiêu thì:
///   1. FlyAttackSkill gây damage trực tiếp lên mục tiêu va chạm.
///   2. Skill này spawn thêm 1 vùng nổ (ArcAttackSkill) NGAY tại điểm va chạm để gây thêm damage diện rộng.
///
/// Gán sprite trong SkillActions.skillRegistrations: index 0-5 là frame bay, index 6-11 là frame nổ.
///   - Index 0-5  : animation cầu lửa lúc đang bay.
///   - Index 6-11 : animation vụ nổ.
///
/// </summary>
public class FireballAttackSkill : MonoBehaviour, ISkill, ISkillSpriteConfigurable
{
    private const int FLY_FRAME_COUNT = 6;
    private const int EXPLOSION_FRAME_COUNT = 6;
    private const int DIRECTION_COUNT = 4;
    private const int FRAMES_PER_DIRECTION = FLY_FRAME_COUNT + EXPLOSION_FRAME_COUNT;

    // Tốc độ chạy animation (giây/frame) cho cả 2 animation bay và nổ. ~16 fps.
    // Chỉnh số này nếu muốn cầu lửa/vụ nổ chạy nhanh/chậm hơn.
    private const float DEFAULT_FRAME_INTERVAL = 0.06f;

    [SerializeField] private Sprite[] skillSprites;

    public void SetSkillSprites(Sprite[] sprites)
    {
        skillSprites = sprites;
    }

    public void Execute(SkillContext context)
    {
        CreateFireball(context, skillSprites);
    }

    /// <summary>
    /// Logic tạo cầu lửa dùng chung cho các skill combo.
    /// </summary>
    public static GameObject CreateFireball(SkillContext context, Sprite[] skillSprites)
    {
        if (context == null || context.Caster == null) return null;

        bool hasDirectionalAnimations = skillSprites != null
            && skillSprites.Length == FRAMES_PER_DIRECTION * DIRECTION_COUNT;
        int directionOffset = hasDirectionalAnimations
            ? ResolveDirectionBlock(context.Direction) * FRAMES_PER_DIRECTION
            : 0;
        Sprite[] flyFrames = ExtractFrames(skillSprites, directionOffset, FLY_FRAME_COUNT);
        Sprite[] explosionFrames = ExtractFrames(
            skillSprites,
            directionOffset + FLY_FRAME_COUNT,
            EXPLOSION_FRAME_COUNT);

        // Use a separate context so explosion hit callbacks do not recursively spawn explosions.
        Action<GameObject, Vector2> originalOnHit = context.OnHitCallback;
        SkillContext projectileContext = CopyContext(context);
        projectileContext.OnHitCallback = (hitObj, hitPos) =>
        {
            originalOnHit?.Invoke(hitObj, hitPos);
            SpawnExplosion(context, hitPos, explosionFrames);
        };

        Sprite firstFlyFrame = flyFrames.Length > 0 ? flyFrames[0] : null;

        // Tái sử dụng nguyên FlyAttackSkill để tạo đạn bay + damage + collider + rigidbody.
        // Pass the first flight frame so FlyAttackSkill does not interpret the 12-frame sheet as four directions.
        GameObject projectileObj = FlyAttackSkill.CreateProjectile(
            projectileContext,
            firstFlyFrame,
            null,
            rotateWithDirection: !hasDirectionalAnimations
        );

        // Gắn animation bay (loop) lên đúng SpriteRenderer con "Visual" mà FlyAttackSkill vừa tạo.
        if (flyFrames.Length > 0)
        {
            Transform visual = projectileObj.transform.Find("Visual");
            SpriteRenderer sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;

            if (sr != null)
            {
                CharacterAnimation flipbook = visual.gameObject.AddComponent<CharacterAnimation>();
                flipbook.PlayCustom(flyFrames, 1f / DEFAULT_FRAME_INTERVAL, true);
            }
        }

        return projectileObj;
    }

    private static SkillContext CopyContext(SkillContext context)
    {
        return new SkillContext
        {
            Caster = context.Caster,
            CasterType = context.CasterType,
            Position = context.Position,
            Direction = context.Direction,
            TargetPosition = context.TargetPosition,
            Damage = context.Damage,
            Size = context.Size,
            Range = context.Range,
            Speed = context.Speed,
            Duration = context.Duration,
            StunTime = context.StunTime,
            OverrideTargetTag = context.OverrideTargetTag,
            Debug = context.Debug,
            CharacterAnimationSprites = context.CharacterAnimationSprites,
            CanPierce = context.CanPierce,
            DotConfig = context.DotConfig,
            OnHitCallback = context.OnHitCallback
        };
    }

    // Vùng nổ to hơn hitbox gốc của skill bao nhiêu lần. Chỉnh số này nếu muốn nổ to/nhỏ hơn.
    private const float EXPLOSION_SIZE_MULTIPLIER = 1.5f;

    private static void SpawnExplosion(
        SkillContext context,
        Vector2 position,
        Sprite[] explosionFrames)
    {
        // Tái sử dụng ArcAttackSkill để tạo vùng damage diện rộng, nổ đúng tại điểm va chạm với size
        // to hơn context.Size (overload mới CreateHitbox(context, overridePosition, overrideSize)).
        Vector2 baseSize = context.Size.sqrMagnitude > 0.001f ? context.Size : new Vector2(1f, 1f);
        Vector2 explosionSize = baseSize * EXPLOSION_SIZE_MULTIPLIER;

        GameObject hitboxObj = ArcAttackSkill.CreateHitbox(
            context,
            position,
            explosionSize,
            rotateWithDirection: false);

        if (hitboxObj == null || explosionFrames.Length == 0)
        {
            return;
        }

        GameObject visualObj = new GameObject("Visual_Explosion");
        visualObj.transform.SetParent(hitboxObj.transform, false);
        visualObj.transform.localPosition = Vector3.zero;
        visualObj.transform.localRotation = Quaternion.identity;

        SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
        sr.sprite = explosionFrames[0];

        // Copy sorting layer/order theo caster, giống cách FlyAttackSkill đang làm cho đạn.
        SpriteRenderer casterSprite = context.Caster != null
            ? context.Caster.GetComponentInChildren<SpriteRenderer>()
            : null;

        if (casterSprite != null)
        {
            sr.sortingLayerName = casterSprite.sortingLayerName;
            sr.sortingOrder = casterSprite.sortingOrder;
        }

        CharacterAnimation flipbook = visualObj.AddComponent<CharacterAnimation>();
        flipbook.PlayCustom(explosionFrames, 1f / DEFAULT_FRAME_INTERVAL, false);
    }

    private static int ResolveDirectionBlock(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return 0;
        if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x))
        {
            return direction.y >= 0f ? 1 : 0;
        }

        return direction.x < 0f ? 2 : 3;
    }

    /// <summary>
    /// Lấy ra count phần tử liên tiếp từ startIndex trong source. Không đủ điều kiện (null/thiếu phần tử)
    /// -> trả về mảng rỗng, coi như "không có sprite" thay vì throw lỗi.
    /// </summary>
    private static Sprite[] ExtractFrames(Sprite[] source, int startIndex, int count)
    {
        if (source == null || source.Length < startIndex + count)
        {
            return Array.Empty<Sprite>();
        }

        Sprite[] result = new Sprite[count];
        Array.Copy(source, startIndex, result, 0, count);
        return result;
    }
}
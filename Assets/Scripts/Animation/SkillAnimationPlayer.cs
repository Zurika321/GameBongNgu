using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Animation 3 pha dùng chung cho mọi skill: Tích chiêu -> Ra đòn -> Kết thúc.
/// Mọi bộ đếm chỉ cộng khi GameplayPause.IsPaused == false.
/// </summary>
public static class SkillAnimationPlayer
{
    // fps rất nhỏ để 1 frame được giữ nguyên cho đến khi mình đẩy frame kế tiếp
    private const float HoldFps = 0.01f;

    private enum DirectionBlock { Down = 0, Up = 1, Left = 2, Right = 3 }

    // ===== PHA 2 + 3: gọi từ skill =====
    public static Coroutine Play(SkillContext context)
    {
        if (context == null || context.Caster == null || SkillActions.Instance == null) return null;
        return SkillActions.Instance.StartCoroutine(ExecuteRoutine(context));
    }

    private static IEnumerator ExecuteRoutine(SkillContext context)
    {
        GameObject caster = context.Caster;
        PlayerController controller = caster.GetComponent<PlayerController>();
        CharacterAnimation anim = caster.GetComponentInChildren<CharacterAnimation>();
        SkillAnimationSet set = context.AnimationSet;
        Vector2 dir = ResolveSkillDirection(caster, context.Direction);

        float attackTime = context.Duration > 0f ? context.Duration : 0.2f;

        if (controller != null) controller.LockMovement(true);
        yield return RunPhase(caster, anim, set != null ? set.GetAttackFrames(dir) : GetDirectionalFrames(null, dir), attackTime);
        if (caster == null) yield break;

        if (context.EndTime <= 0f)
        {
            if (controller != null) controller.LockMovement(false);
            if (anim != null)
            {
                anim.StopCustomPlayback();
                anim.Play(AnimState.Idle);
            }
            yield break;
        }

        yield return RunPhase(caster, anim, set != null ? set.GetEndFrames(dir) : GetDirectionalFrames(null, dir), context.EndTime);

        if (controller != null) controller.LockMovement(false);

        if (anim != null)
        {
            anim.StopCustomPlayback();
            anim.Play(AnimState.Idle);
        }
    }

    // ===== PHA 1: tích chiêu, gọi từ PlayerSkillController =====
    public static IEnumerator ChargeRoutine(
        GameObject caster,
        float chargeTime,
        SkillAnimationSet set,
        Func<Vector2> getDirection,
        Func<bool> isHeld,
        Func<bool> shouldCancel,
        Action<bool> onFinished)
    {
        PlayerController controller = caster.GetComponent<PlayerController>();
        CharacterAnimation anim = caster.GetComponentInChildren<CharacterAnimation>();
        float elapsed = 0f;
        int lastIndex = -1;

        if (controller != null) controller.LockMovement(true);

        while (true)
        {
            if (caster == null) { onFinished?.Invoke(false); yield break; }

            if (shouldCancel != null && shouldCancel())
            {
                if (controller != null) controller.LockMovement(false);
                onFinished?.Invoke(false);
                yield break;
            }

            Vector2 direction = getDirection != null ? ResolveSkillDirection(caster, getDirection()) : ResolveSkillDirection(caster, Vector2.zero);
            Sprite[] frames = set != null ? set.GetChargeFrames(direction) : GetDirectionalFrames(null, direction);
            ShowFrame(anim, frames, chargeTime > 0f ? elapsed / chargeTime : 1f, ref lastIndex);

            if (!GameplayPause.IsPaused)
            {
                elapsed += Time.deltaTime;

                // Đã đếm xong (về 0) và đã buông nút -> ra skill
                if (elapsed >= chargeTime && !isHeld()) break;
            }

            yield return null;
        }

        // Giữ khoá di chuyển; pha ra đòn sẽ mở khoá khi hết Duration
        onFinished?.Invoke(true);
    }

    // ===== Helpers =====
    private static IEnumerator RunPhase(GameObject caster, CharacterAnimation anim, Sprite[] frames, float duration)
    {
        if (duration <= 0f) yield break;

        float elapsed = 0f;
        int lastIndex = -1;
        while (elapsed < duration)
        {
            if (caster == null) yield break;
            ShowFrame(anim, frames, elapsed / duration, ref lastIndex);
            if (!GameplayPause.IsPaused) elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static Vector2 ResolveSkillDirection(GameObject caster, Vector2 requestedDirection)
    {
        if (requestedDirection.sqrMagnitude > 0.001f)
            return requestedDirection.normalized;

        if (caster == null)
            return Vector2.down;

        IFacingProvider facing = caster.GetComponentInParent<IFacingProvider>();
        if (facing != null)
            return facing.FacingDirection.sqrMagnitude > 0.001f ? facing.FacingDirection.normalized : Vector2.down;

        PlayerController controller = caster.GetComponent<PlayerController>();
        if (controller != null)
            return controller.FacingDirection.sqrMagnitude > 0.001f ? controller.FacingDirection.normalized : Vector2.down;

        return Vector2.down;
    }

    private static void ShowFrame(CharacterAnimation anim, Sprite[] frames, float progress, ref int lastIndex)
    {
        if (anim == null || frames == null || frames.Length == 0) return;
        int index = Mathf.Min(frames.Length - 1, (int)(Mathf.Clamp01(progress) * frames.Length));
        if (index == lastIndex) return;
        lastIndex = index;
        anim.PlayCustom(new[] { frames[index] }, HoldFps, false);
    }

    public static Sprite[] GetDirectionalFrames(Sprite[] sprites, Vector2 direction)
    {
        if (sprites == null || sprites.Length == 0) return Array.Empty<Sprite>();
        if (sprites.Length < 4 || sprites.Length % 4 != 0) return sprites;

        int perDirection = sprites.Length / 4;
        Sprite[] frames = new Sprite[perDirection];
        Array.Copy(sprites, ResolveDirectionBlock(direction) * perDirection, frames, 0, perDirection);
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
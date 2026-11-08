using System;
using System.Collections;
using UnityEngine;

public static class SkillAnimationPlayer
{
    public static Coroutine Play(SkillContext context)
    {
        if (context?.Caster == null || SkillActions.Instance == null) return null;
        return SkillActions.Instance.StartCoroutine(WaitForAction(context));
    }

    private static IEnumerator WaitForAction(SkillContext context)
    {
        GameObject caster = context.Caster;
        PlayerController controller = caster.GetComponent<PlayerController>();
        if (controller != null) controller.LockMovement(true);
        CharacterAnimation animation = caster.GetComponentInChildren<CharacterAnimation>();
        float time = 0f;
        while (caster != null && (time < Mathf.Max(0f, context.Duration) + Mathf.Max(0f, context.EndTime)
            || (animation != null && animation.IsPlayingSkill(string.IsNullOrWhiteSpace(context.AnimationName) ? context.SkillId : context.AnimationName.Trim()))))
        {
            if (!GameplayPause.IsPaused) time += Time.deltaTime;
            yield return null;
        }
        if (controller != null) controller.LockMovement(false);
    }

    public static IEnumerator ChargeRoutine(GameObject caster, float chargeTime, string skillId,
        Func<Vector2> getDirection, Func<bool> isHeld, Func<bool> shouldCancel, Action<bool> onFinished)
    {
        PlayerController controller = caster != null ? caster.GetComponent<PlayerController>() : null;
        CharacterAnimation animation = caster != null ? caster.GetComponentInChildren<CharacterAnimation>() : null;
        if (controller != null) controller.LockMovement(true);
        if (animation != null)
        {
            animation.SetFacing(getDirection != null ? getDirection() : Vector2.down);
            animation.PlayAnimationSkill(skillId, true);
        }
        float time = 0f;
        while (true)
        {
            if (caster == null || (shouldCancel != null && shouldCancel()))
            {
                if (animation != null) animation.CancelSkillAnimation();
                if (controller != null) controller.LockMovement(false);
                onFinished?.Invoke(false);
                yield break;
            }
            if (!GameplayPause.IsPaused)
            {
                time += Time.deltaTime;
                if (time >= chargeTime && (isHeld == null || !isHeld())) break;
            }
            yield return null;
        }
        if (animation != null) animation.BeginSkillCast();
        if (controller != null) controller.LockMovement(false);
        onFinished?.Invoke(true);
    }

    private enum DirectionBlock { Down = 0, Up = 1, Left = 2, Right = 3 }
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

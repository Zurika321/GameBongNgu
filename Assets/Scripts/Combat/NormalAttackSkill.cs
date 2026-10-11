using System.Collections;
using UnityEngine;

public class NormalAttackSkill : MonoBehaviour, ISkill
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

        float totalDuration = context.Duration > 0f ? context.Duration : 0.2f;
        CharacterAnimation animation = context.Caster.GetComponentInChildren<CharacterAnimation>();
        Sprite[] animationFrames = GetDirectionalFrames(context.CharacterAnimationSprites, context.Direction);

        if (SkillActions.Instance == null)
        {
            Debug.LogWarning("[NormalAttackSkill] Chưa có SkillActions trong scene để chạy animation/khóa di chuyển.", context.Caster);
            return;
        }
        SkillActions.Instance.StartCoroutine(AttackRoutine(context, animation, animationFrames, totalDuration));
    }

    private IEnumerator AttackRoutine(SkillContext context, CharacterAnimation animation, Sprite[] frames, float duration)
    {
        SetCasterMovementLock(context, true);
        if (animation != null && frames.Length > 0)
        {
            animation.PlayCustom(frames, frames.Length / duration, false);
        }

        yield return new WaitForSeconds(duration);
        SetCasterMovementLock(context, false);
    }

    private void SetCasterMovementLock(SkillContext context, bool locked)
    {
        if (context.CasterType == SkillCasterType.Player)
        {
            PlayerController controller = context.Caster.GetComponent<PlayerController>();
            if (controller != null) controller.LockMovement(locked);
        }
        else if (context.CasterType == SkillCasterType.Enemy)
        {
            EnemyController enemyController = context.Caster.GetComponent<EnemyController>();
            if (enemyController != null) enemyController.LockMovement(locked);
        }
    }

    private Sprite[] GetDirectionalFrames(Sprite[] characterAnimationSprites, Vector2 direction)
    {
        if (characterAnimationSprites == null || characterAnimationSprites.Length < 4 || characterAnimationSprites.Length % 4 != 0)
            return System.Array.Empty<Sprite>();

        int frameCount = characterAnimationSprites.Length / 4;
        int start = ResolveDirectionBlock(direction) * frameCount;
        Sprite[] frames = new Sprite[frameCount];
        System.Array.Copy(characterAnimationSprites, start, frames, 0, frameCount);
        return frames;
    }

    private int ResolveDirectionBlock(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return (int)DirectionBlock.Down;
        if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x))
        {
            return direction.y >= 0f ? (int)DirectionBlock.Up : (int)DirectionBlock.Down;
        }

        return direction.x >= 0f ? (int)DirectionBlock.Right : (int)DirectionBlock.Left;
    }
}
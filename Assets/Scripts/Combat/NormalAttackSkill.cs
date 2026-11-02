using UnityEngine;

public class NormalAttackSkill : MonoBehaviour, ISkill
{
    public void Execute(SkillContext context)
    {
        if (context == null || context.Caster == null) return;

        ArcAttackSkill.CreateHitbox(context);

        if (SkillActions.Instance == null)
        {
            Debug.LogWarning("[NormalAttackSkill] Chưa có SkillActions trong scene để chạy animation/khóa di chuyển.", context.Caster);
            return;
        }

        SkillAnimationPlayer.Play(context);
    }
}
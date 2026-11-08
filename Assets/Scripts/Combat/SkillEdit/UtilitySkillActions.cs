using UnityEngine;

/// <summary>Adapter that uses the caster's existing buff component rather than adding one to the action registry.</summary>
public sealed class SelfBuffAction : ISkill
{
    public void Execute(SkillContext context)
    {
        if (context?.Caster == null || context.Modules?.selfBuff == null) return;
        SelfBuff receiver = context.Caster.GetComponentInChildren<SelfBuff>(true);
        if (receiver == null) { Debug.LogWarning("[SelfBuff] Configure SelfBuff on the caster.", context.Caster); return; }
        receiver.ApplyBuff(context.SkillId, context.Modules.selfBuff);
    }
}

using UnityEngine;

/// <summary>Contract cho action chủ động: class thực thi nhận runtime SkillContext.</summary>
public interface ISkill
{
    void Execute(SkillContext context);
}

/// <summary>
/// Contract mở rộng cho passive custom có lifecycle riêng.
/// PassiveSkillController mặc định xử lý PassiveBuff bằng stat modifier;
/// passive logic đặc biệt có thể triển khai interface này ở component riêng.
/// </summary>
public interface IPassiveSkill
{
    void OnApply(PassiveSkillRuntimeContext context);
    void OnRemove(PassiveSkillRuntimeContext context);
}

public class PassiveSkillRuntimeContext
{
    public GameObject Owner { get; private set; }
    public SkillConfigEntry Definition { get; private set; }
    public IStatModifierReceiver StatReceiver { get; private set; }

    public PassiveSkillRuntimeContext(GameObject owner, SkillConfigEntry definition, IStatModifierReceiver statReceiver)
    {
        Owner = owner;
        Definition = definition;
        StatReceiver = statReceiver;
    }
}

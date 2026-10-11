using UnityEngine;

/// <summary>
/// Mọi skill (zone tròn, vòng cung, dash, đạn bay...) implement interface này.
/// SkillActions sẽ gọi Execute() khi cần kích hoạt skill theo tên.
/// </summary>
public interface ISkill
{
    void Execute(SkillContext context);
}

public interface ISkillSpriteConfigurable
{
    void SetSkillSprites(Sprite[] sprites);
}

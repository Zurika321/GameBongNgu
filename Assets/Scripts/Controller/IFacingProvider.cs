using UnityEngine;

/// <summary>
/// Cung cấp hướng mặt (hướng đang quay/di chuyển) hiện tại của Player/Enemy.
/// PlayerSkillController / EnemySkillController dùng GetComponentInParent&lt;IFacingProvider&gt;()
/// để biết skill nên bắn/đánh theo hướng nào.
/// </summary>
public interface IFacingProvider
{
    Vector2 FacingDirection { get; }
}

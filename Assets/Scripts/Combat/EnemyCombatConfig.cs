using System;
using UnityEngine;

[Serializable]
public sealed class EnemyPhaseRule
{
    [Min(2)] public int phase = 2;
    [Range(0f, 1f)] public float healthBelow;
    [Tooltip("Bật nếu Excel chưa nêu ngưỡng hoặc cần quyết định truyện; gọi SetPhase từ event hoặc gán cờ rồi tắt.")] public bool manualOnly;
    public string requiredProgressFlag;
    [TextArea] public string notes;
}
[CreateAssetMenu(menuName = "Bóng Ngủ/Enemy Combat Config")]
public sealed class EnemyCombatConfig : ScriptableObject
{
    public string actorId;
    public string displayName;
    public bool boss;
    [Min(0f)] public float maxHealth;
    [Min(0f)] public float attack;
    public bool needsHealth;
    public bool needsAttack;
    public bool invincible;
    [Header("Vật phẩm rơi khi bị hạ (theo Excel)")]
    public ZoneDropDefinition[] drops = Array.Empty<ZoneDropDefinition>();
    [TextArea] public string setupNotes;
    public EnemyPhaseRule[] phases = Array.Empty<EnemyPhaseRule>();
    public EnemySkillSlot[] skills = Array.Empty<EnemySkillSlot>();
}

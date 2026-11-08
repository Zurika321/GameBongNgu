using System;
using UnityEngine;

// Optional per-skill settings. Action classes can also call each module's public API directly.
[Serializable]
public sealed class SkillModuleConfig
{
    public StatusConfig status = new StatusConfig();
    public SummonConfig summon = new SummonConfig();
    public CasterMoveConfig movement = new CasterMoveConfig();
    public ResourceConfig resource = new ResourceConfig();
    public DefenseConfig defense = new DefenseConfig();
    public ScreenEffectConfig screen = new ScreenEffectConfig();
    public ChallengeConfig challenge = new ChallengeConfig();
    public ReplayConfig replay = new ReplayConfig();
    public ConditionConfig condition = new ConditionConfig();
    public SkillBuffConfig selfBuff = new SkillBuffConfig();
}
public enum SkillStatusKind { Stun, Slow, Curse }
[Serializable] public sealed class StatusConfig
{
    public SkillStatusKind kind;
    [Min(0f)] public float duration = 1f;
    [Range(0f, 1f)] public float strength = 0.4f;
    public string statusId = "Curse";
}
[Serializable] public sealed class SummonConfig
{
    public GameObject prefab;
    [Min(1)] public int count = 1;
    [Min(1)] public int maximumAlive = 4;
    [Min(0f)] public float radius = 1f;
    [Min(0f)] public float lifetime = 10f;
    [Min(0f)] public float ownerHealthFraction = 0.5f;
}
[Serializable] public sealed class CasterMoveConfig
{
    public bool invincible;
    [Min(0f)] public float invincibleStart;
    [Min(0f)] public float invincibleEnd;
}
public enum SkillResource { Health, Sanity, DemonNature }
public enum ResourceAmountMode { Flat, FractionMaxHealth, FractionDamage }
[Serializable] public sealed class ResourceConfig
{
    public SkillResource resource;
    public ResourceAmountMode amountMode;
    public float amount;
    [Min(1)] public int ticks = 1;
    [Min(0.01f)] public float interval = 1f;
    public bool cancelOnMove;
    public bool cancelOnDamage;
    [Min(0f)] public float moveTolerance = 0.1f;
    public bool persist;
}
public enum DefenseMode { Parry, Immune, Reduce, Reflect }
[Serializable] public sealed class DefenseConfig
{
    public DefenseMode mode;
    [Min(0f)] public float duration = 0.5f;
    [Range(0f, 1f)] public float fraction = 0.2f;
    public string counterActionId;
    public bool consumeOnHit = true;
    public bool meleeOnly;
    public bool breakable;
}
[Serializable] public sealed class ScreenEffectConfig
{
    [Range(0f, 1f)] public float strength = 0.7f;
    [Min(0f)] public float fadeSeconds = 0.5f;
    [Min(0f)] public float duration = 5f;
}
public enum ChallengeMode { RepeatedInput, MaintainMeter }
[Serializable] public sealed class ChallengeConfig
{
    public ChallengeMode mode;
    [Min(0.01f)] public float duration = 6f;
    [Range(0f, 1f)] public float initialValue = 0.5f;
    [Range(0f, 1f)] public float requiredValue = 0.5f;
    [Min(0f)] public float drainPerSecond = 0.1f;
    [Min(0f)] public float gainPerInput = 0.1f;
    [Min(0f)] public float failureGraceSeconds = 3f;
    public string immunityFlag;
    public bool consumeImmunity;
}
public enum ReplaySelection { Last, Strongest, Random }
[Serializable] public sealed class ReplayConfig
{
    public ReplaySelection selection;
    [Min(0f)] public float range = 10f;
    [Range(0f, 1f)] public float damageFraction = 1f;
}
public enum SkillConditionKind { Always, Chance, CasterHealthBelow, TargetHealthBelow, NearWall, Flag, HitCount, DamageTakenCount, KilledByMelee }
[Serializable] public sealed class ConditionConfig
{
    public SkillConditionKind kind;
    [Range(0f, 1f)] public float threshold = 0.5f;
    public LayerMask wallMask;
    [Min(0f)] public float wallDistance = 0.5f;
    public string flag;
    [Min(1)] public int requiredCount = 1;
    [Min(0f)] public float withinSeconds = 5f;
    public bool invert;
    public string passActionId;
    public string failActionId;
}

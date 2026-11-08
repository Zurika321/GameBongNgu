using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Hình dạng vùng phát hiện cho từng skill - admin chọn Box hoặc Circle trong Inspector.
/// </summary>
public enum DetectionShape
{
    Box,
    Circle
}

/// <summary>
/// 1 dòng trong mảng skill của Enemy. Admin chỉnh trực tiếp trong Inspector, không cần sửa code.
/// </summary>
public enum EnemySkillTrigger { Automatic, CombatStart, PhaseEnter, Death }

[System.Serializable]
public class EnemySkillSlot
{
    [Header("SkillConfig (ưu tiên; bỏ trống để dùng dữ liệu cũ bên dưới)")]
    public string skillId;
    public EnemySkillTrigger trigger;
    [Tooltip("Trống = mọi phase. Phase đánh số từ 1.")] public int[] phases;
    [Min(0f)] public float minimumDistance;
    [Min(0f)] public float initialDelay;
    public int priority;
    public bool oncePerCombat;
    public bool skipWhileActionRunning;
    public bool requiresAttack = true;
    [Tooltip("Bật khi Excel thiếu tham số bắt buộc; chỉ tắt sau khi đã gán.")] public bool needsInspectorSetup;
    [TextArea] public string setupNotes;
    public ConditionConfig castCondition = new ConditionConfig();
    [Min(0f)] public float castLockSeconds;
    [System.NonSerialized] public bool used;

    [Tooltip("JSON animation set name / skillId. Empty preserves the existing skillName fallback.")]
    public string animationSkillId;
    [Header("Tên skill (khớp với tên đăng ký trong SkillActions)")]
    [RegistryId(RegistryIdType.Skill)]public string skillName;

    [Header("Hồi chiêu")]
    public float cooldown = 2f;

    [Header("Vùng phát hiện mục tiêu (để tự tung skill)")]
    public DetectionShape shape = DetectionShape.Circle;
    public Vector2 detectionSize = new Vector2(3f, 3f); // Circle: chỉ dùng detectionSize.x làm bán kính
    public Vector2 detectionOffset = Vector2.zero;       // lệch tâm vùng phát hiện so với object con này
    public string detectTag = "HitBoxPlayer";            // tag cần phát hiện để kích hoạt skill

    [Header("Thông số skill truyền vào SkillContext")]
    public SkillModuleConfig modules = new SkillModuleConfig();
    public SkillEffectAnimation effectAnimation = new SkillEffectAnimation();
    public float damage = 10f;
    public Vector2 skillSize = new Vector2(1.5f, 1f);
    public float range = 1f;
    public float speed = 10f;
    public float duration = 0.2f;
    [Min(0f)] public float stunTime;

    [Header("Sprite animation của Enemy theo hướng")]
    [FormerlySerializedAs("imageSkill")]
    public Sprite[] downFrames;
    public Sprite[] upFrames;
    public Sprite[] rightFrames;
    public Sprite[] leftFrames;

    [Header("Debug")]
    public bool debug = true;   // true = skill hiện hitbox debug (LineRenderer) trực tiếp trong Game

    [Header("Tuỳ chọn xuyên (dùng cho skill đạn bay)")]
    public bool canPierce = false; // true = xuyên qua mục tiêu, false = dính là dừng+xoá

    [Header("DOT (Damage over Time) - để None nếu skill không gây DOT")]
    public DameDotConfig dotConfig = new DameDotConfig();

    // runtime, không serialize xuống scene/prefab
    [System.NonSerialized] public float lastUsedTime = -999f;
}

/// <summary>Resolve context/cooldown and cast. EnemySkillScheduler owns automatic selection.</summary>
[DefaultExecutionOrder(-50)]
public class EnemySkillController : MonoBehaviour
{
    [SerializeField] private EnemyCombatConfig combatConfig;
    [SerializeField] private SkillConfig skillConfig;
    [SerializeField] private EnemySkillSlot[] skillSlots;
    [SerializeField] private GameObject ownerOverride;
    private GameObject ownerRoot;
    private EnemyController enemyController;
    private HealthController health;
    private RuntimeStatModifierStore stats;
    private CharacterAnimation animationPlayer;
    private float busyRemaining;
    private int phase = 1;
    private bool deathHandled;
    private float? inheritedAttack;
    private bool hasScheduler;
    private bool ownsMovementLock;
    private bool spawnHealthConfigured;
    public EnemyCombatConfig CombatConfig => combatConfig;
    public EnemySkillSlot[] Slots => skillSlots;
    public int Phase => phase;
    public bool IsBusy => busyRemaining > 0f;
    public bool IsDead => health != null && health.IsDead;
    public GameObject Owner => ownerRoot;

    private void Awake()
    {
        enemyController = GetComponentInParent<EnemyController>();
        ownerRoot = ownerOverride != null ? ownerOverride : enemyController != null ? enemyController.gameObject : gameObject;
        health = ownerRoot.GetComponent<HealthController>();
        hasScheduler = GetComponent<EnemySkillScheduler>() != null;
        stats = ownerRoot.GetComponent<RuntimeStatModifierStore>();
        animationPlayer = ownerRoot.GetComponentInChildren<CharacterAnimation>();
        if (combatConfig != null)
        {
            // Clone serialized slots so cooldowns and once-only state never leak across prefab instances.
            skillSlots = JsonUtility.FromJson<SlotList>(JsonUtility.ToJson(new SlotList { slots = combatConfig.skills })).slots;
            if (health != null && combatConfig.maxHealth > 0f)
                health.ConfigureHealth(combatConfig.maxHealth, combatConfig.maxHealth);
            if (health != null) health.SetInvincible(combatConfig.invincible);
            if ((combatConfig.needsHealth && combatConfig.maxHealth <= 0f) || (combatConfig.needsAttack && combatConfig.attack <= 0f))
                Debug.LogWarning("[EnemySkillController] " + name + ": gán HP/ATK còn thiếu ở Combat Config trước khi test sát thương.", this);
        }
        if (skillSlots == null) skillSlots = System.Array.Empty<EnemySkillSlot>();
        foreach (var slot in skillSlots)
        {
            if (slot == null) continue;
            slot.lastUsedTime = float.NegativeInfinity; slot.used = false;
            if (!string.IsNullOrWhiteSpace(slot.skillId) && (skillConfig == null || skillConfig.FindEntry(slot.skillId) == null))
            { slot.needsInspectorSetup = true; Debug.LogError("[EnemySkillController] Thiếu SkillConfig entry: " + slot.skillId, this); }
        }
        if (skillConfig == null && combatConfig != null) Debug.LogError("[EnemySkillController] Chưa gán SkillConfig.", this);
    }
    [System.Serializable] private class SlotList { public EnemySkillSlot[] slots; }
    private void Update()
    {
        if (GameplayPause.IsPaused) return;
        busyRemaining = Mathf.Max(0f, busyRemaining - Time.deltaTime);
        if (ownsMovementLock && busyRemaining <= 0f) { enemyController?.LockMovement(false); ownsMovementLock = false; }
        if (!IsDead)
        {
            // Existing scene slots continue to work even without the new prefab scheduler.
            if (!hasScheduler && !IsBusy)
            {
                for (int i = 0; i < skillSlots.Length; i++)
                {
                    var slot = skillSlots[i];
                    if (slot == null || slot.trigger != EnemySkillTrigger.Automatic) continue;
                    Vector2 center = (Vector2)ownerRoot.transform.position + slot.detectionOffset;
                    var hits = slot.shape == DetectionShape.Circle ? Physics2D.OverlapCircleAll(center, slot.detectionSize.x) : Physics2D.OverlapBoxAll(center, slot.detectionSize, 0f);
                    bool cast = false;
                    foreach (var hit in hits) if (hit != null && hit.CompareTag(slot.detectTag) && TryCast(i, hit.gameObject)) { cast = true; break; }
                    if (cast) break;
                }
            }
            return;
        }
        if (deathHandled) return;
        deathHandled = true;
        CancelAll();
        for (int i = 0; i < skillSlots.Length; i++)
            if (skillSlots[i]?.trigger == EnemySkillTrigger.Death) TryCast(i, enemyController?.Target?.gameObject, true);
    }
    public float Cooldown(EnemySkillSlot slot)
    {
        var context = ConfigFor(slot);
        float speed = stats != null ? stats.GetFinalValue(PlayerStatType.AttackSpeed, 1f) : 1f;
        return Mathf.Max(0f, context != null ? context.cooldown : slot.cooldown) / Mathf.Max(0.01f, speed);
    }
    private SkillContextConfig ConfigFor(EnemySkillSlot slot) => skillConfig != null && !string.IsNullOrWhiteSpace(slot?.skillId) ? skillConfig.GetContextConfig(slot.skillId, null) : null;
    public bool IsPhaseAllowed(EnemySkillSlot slot) => slot.phases == null || slot.phases.Length == 0 || System.Array.IndexOf(slot.phases, phase) >= 0;
    public void SetPhase(int value)
    {
        if (value < 1 || phase == value) return;
        CancelAll(); phase = value;
    }
    public void BeginCombat()
    {
        foreach (var slot in skillSlots) if (slot != null) slot.used = false;
    }
    public void EndCombat()
    {
        CancelAll();
        foreach (var slot in skillSlots) if (slot != null) slot.used = false;
    }
    public void CancelAll()
    {
        StopAllCoroutines(); busyRemaining = 0f;
        if (ownsMovementLock) enemyController?.LockMovement(false);
        ownsMovementLock = false;
        foreach (var slot in skillSlots ?? System.Array.Empty<EnemySkillSlot>())
        {
            if (slot == null) continue;
            var entry = skillConfig != null ? skillConfig.FindEntry(slot.skillId) : null;
            SkillActions.Instance?.RemoveActionBuffs(entry != null ? entry.actionId : slot.skillName, ownerRoot);
        }
        animationPlayer?.CancelSkillAnimation();
    }
    public void TryCastSlot(int index, GameObject detectedTarget) => TryCast(index, detectedTarget);
    public bool TryCast(int index, GameObject detectedTarget, bool death = false)
    {
        if (GameplayPause.IsPaused || index < 0 || index >= skillSlots.Length || ownerRoot == null) return false;
        EnemySkillSlot slot = skillSlots[index];
        if (slot == null || slot.needsInspectorSetup || !IsPhaseAllowed(slot)) return false;
        if (death != IsDead || (death && slot.trigger != EnemySkillTrigger.Death)) return false;
        if (!death && (IsBusy || (enemyController != null && enemyController.IsActionLocked))) return false;
        if (combatConfig != null && ((combatConfig.needsHealth && combatConfig.maxHealth <= 0f && !spawnHealthConfigured) || (slot.requiresAttack && combatConfig.needsAttack && combatConfig.attack <= 0f && !inheritedAttack.HasValue))) return false;
        if (slot.oncePerCombat && slot.used) return false;
        if (Time.time - slot.lastUsedTime < Cooldown(slot)) return false;
        var entry = skillConfig != null ? skillConfig.FindEntry(slot.skillId) : null;
        if (entry != null && entry.needsInspectorSetup) return false;
        string action = entry != null ? entry.actionId : slot.skillName;
        if (SkillActions.Instance == null || !SkillActions.Instance.HasRegisteredSkill(action)) return false;
        if (slot.skipWhileActionRunning && SkillActions.Instance.IsActionRunning(action, ownerRoot)) return false;
        var config = ConfigFor(slot);
        var targetHealth = detectedTarget != null ? detectedTarget.GetComponentInParent<HealthController>() : null;
        GameObject target = targetHealth != null ? targetHealth.gameObject : detectedTarget;
        if (targetHealth != null && targetHealth.IsDead) return false;
        Vector2 direction = target != null ? (Vector2)(target.transform.position - ownerRoot.transform.position) : enemyController != null ? enemyController.FacingDirection : Vector2.right;
        if (direction.sqrMagnitude > 0f) direction.Normalize();
        float attack = inheritedAttack ?? (combatConfig != null ? combatConfig.attack : slot.damage);
        if (stats != null) attack = stats.GetFinalValue(PlayerStatType.Attack, attack);
        var context = new SkillContext
        {
            Caster = ownerRoot, Target = target, CasterType = SkillCasterType.Enemy,
            SkillId = entry != null ? entry.skillId : string.IsNullOrWhiteSpace(slot.animationSkillId) ? slot.skillName : slot.animationSkillId,
            AnimationName = config?.animationName,
            ActionId = action, Position = ownerRoot.transform.position, Direction = direction,
            TargetPosition = target != null ? (Vector2)target.transform.position : (Vector2)ownerRoot.transform.position,
            Damage = attack * (config != null ? config.damageMultiplier : 1f),
            Size = config != null ? config.skillSize : slot.skillSize, Range = config != null ? config.range : slot.range,
            Speed = config != null ? config.speed : slot.speed, Duration = config != null ? config.duration : slot.duration,
            StunTime = config != null ? config.stunTime : slot.stunTime, Debug = config != null ? config.debug : slot.debug,
            ChargeTime = config != null ? config.chargeTime : 0f, EndTime = config != null ? config.endTime : 0f,
            CanPierce = config != null ? config.canPierce : slot.canPierce, DotConfig = config != null ? config.dotConfig : slot.dotConfig,
            Modules = config != null ? config.modules : slot.modules, EffectAnimation = config != null ? config.effectAnimation : slot.effectAnimation,
            ActionDamageOverrides = config?.actionDamageOverrides, BreaksGuard = config != null && config.breaksGuard,
            BypassesMeleeResistance = config != null && config.bypassesMeleeResistance,
            downFrames = slot.downFrames, upFrames = slot.upFrames, rightFrames = slot.rightFrames, leftFrames = slot.leftFrames
        };
        if (!ConditionGate.Evaluate(context, slot.castCondition)) return false;
        slot.lastUsedTime = Time.time; slot.used = true;
        busyRemaining = slot.castLockSeconds;
        if (!death && busyRemaining > 0f && enemyController != null && !enemyController.IsActionLocked)
        { ownsMovementLock = true; enemyController.LockMovement(true); }
        if (!death && animationPlayer != null) { animationPlayer.SetFacing(direction); animationPlayer.PlayAnimationSkill(string.IsNullOrWhiteSpace(context.AnimationName) ? context.SkillId : context.AnimationName.Trim()); animationPlayer.BeginSkillCast(); }
        SkillActions.Instance.Activate(action, context);
        return true;
    }
    public void NotifySpawnHealthConfigured() => spawnHealthConfigured = true;
    public void InheritAttack(float attack) => inheritedAttack = Mathf.Max(0f, attack);
    public void CaptureCooldowns(PlayerSaveData data, string enemyId)
    {
        if (data == null || string.IsNullOrWhiteSpace(enemyId)) return;
        if (data.skillCooldowns == null) data.skillCooldowns = new System.Collections.Generic.List<SkillCooldownSnapshot>();
        data.skillCooldowns.RemoveAll(entry => entry != null && entry.ownerId == enemyId);
        foreach (var slot in skillSlots)
        {
            if (slot == null) continue;
            float remaining = Mathf.Max(0f, Cooldown(slot) - (Time.time - slot.lastUsedTime));
            if (remaining > 0f) data.skillCooldowns.Add(new SkillCooldownSnapshot { ownerId = enemyId, skillId = SlotKey(slot), remainingSeconds = remaining });
        }
    }
    private string SlotKey(EnemySkillSlot slot) => string.IsNullOrWhiteSpace(slot.skillId) ? slot.skillName : slot.skillId;
    public void RestoreCooldowns(PlayerSaveData data, string enemyId)
    {
        if (data?.skillCooldowns == null || string.IsNullOrWhiteSpace(enemyId)) return;
        foreach (var slot in skillSlots)
        {
            if (slot == null) continue;
            var saved = data.skillCooldowns.Find(entry => entry != null && entry.ownerId == enemyId && entry.skillId == SlotKey(slot));
            float remaining = saved != null ? Mathf.Clamp(saved.remainingSeconds, 0f, Cooldown(slot)) : 0f;
            slot.lastUsedTime = remaining > 0f ? Time.time - (Cooldown(slot) - remaining) : float.NegativeInfinity;
        }
    }
    private void OnDisable() => CancelAll();
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Serialization;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum MouseButtonSelect
{
    None,
    Left,
    Right,
    Middle
}

[System.Serializable]
public class PlayerSkillSlot
{
    [Header("Định danh skill")]
    [FormerlySerializedAs("skillName")]
    [Tooltip("ID riêng của skill, ví dụ BasicAttack_Dagger. Không phải tên class.")]
    public string skillId;

    [Tooltip("ID class trong SkillActions, ví dụ NormalAttack. Nhiều skillId được dùng chung actionId.")]
    [RegistryId(RegistryIdType.Skill)]
    public string actionId;

    [Tooltip("skillId (hoặc tên hiển thị) phải được mở khóa mới dùng được. Để trống nếu không cần kiểm tra unlock.")]
    public string requiredUnlockSkill;

    [Header("Hồi chiêu")]
    [Min(0f)] public float cooldown = 1f;

    [Header("Input PC")]
    public string pcKey = "";

    [Header("Input UI")]
    public Button uiButton;

    [Header("Input chuột")]
    public MouseButtonSelect mouseButton = MouseButtonSelect.None;

    [Header("Fallback context nếu không tìm thấy SkillConfig")]
    public float damage = 10f;
    public Vector2 skillSize = new Vector2(1.5f, 1f);
    public float range = 1f;
    public float speed = 10f;
    public float duration = 0.2f;
    [Min(0f)] public float stunTime;
    [Min(0f)] public float chargeTime;
    [Min(0f)] public float endTime = 0.15f;
    public bool debug = true;
    public bool canPierce;
    public DameDotConfig dotConfig = new DameDotConfig();

    [Header("Runtime")]
    [System.NonSerialized] public bool isCharging;
    [System.NonSerialized] public bool uiHeld;
    [System.NonSerialized] public float lastUsedTime = -999f;
    [System.NonSerialized] public bool pcKeyInvalid;
}

/// <summary>
/// Điều khiển skill chủ động (Active / BuffAction): nhận input và gọi actionId với context được resolve theo skillId + vũ khí.
/// - Skill lấy từ danh sách đã trang bị trong PlayerSaveData.equippedSkills (3 ô) và các slot Inspector không cần mở khóa.
/// - KHÔNG xử lý đánh thường: đòn đánh thường (tay không, dao...) do PlayerBasicAttackController đảm nhiệm.
/// - Cooldown được lưu theo skillId (không theo ô), nên đổi vũ khí hay tháo/lắp lại skill không reset hồi chiêu.
/// </summary>
public class PlayerSkillController : MonoBehaviour
{
    /// <summary>ownerId dùng khi lưu cooldown của player vào PlayerSaveData.skillCooldowns.</summary>
    public const string CooldownOwnerId = "PLAYER";

    private const string UnarmedWeaponId = "Punch";

    [Header("Skill Config")]
    [SerializeField] private SkillConfig skillConfig;

    [Header("Các slot input của player")]
    [SerializeField] private PlayerSkillSlot[] skillSlots;

    [Header("Owner")]
    [SerializeField] private GameObject ownerOverride;

    // Đánh thường đã tách sang PlayerBasicAttackController. Giữ lại 4 field cũ (ẩn) để scene không mất dữ liệu:
    // PlayerBasicAttackController sao chép phím/chuột/nút UI từ đây một lần khi được thêm vào. Có thể xóa sau khi đã chuyển.
#pragma warning disable CS0414 // field cũ chỉ để giữ dữ liệu scene
    [SerializeField, HideInInspector] private bool autoBasicAttack = true;
#pragma warning restore CS0414
    [SerializeField, HideInInspector] private string basicAttackPcKey = "";
    [SerializeField, HideInInspector] private MouseButtonSelect basicAttackMouseButton = MouseButtonSelect.Left;
    [SerializeField, HideInInspector] private Button basicAttackUiButton;

    private GameObject ownerRoot;
    private IFacingProvider facingProvider;
    private PlayerController playerController;
    private PlayerSkillSlot[] skillSlotTemplates;
    private bool skillSlotTemplatesCaptured;
    private IHealth health;
    private string lastWeaponForContexts;
    private readonly HashSet<Button> boundUiButtons = new HashSet<Button>();

    // Cooldown thuộc về skillId: lần dùng gần nhất của từng skill (Time.time).
    private readonly Dictionary<string, float> lastUsedBySkill =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private bool cooldownsRestored;

    private void Awake()
    {
        CaptureConfiguredSkillTemplates();
        EnsureSkillConfig();

        playerController = GetComponentInParent<PlayerController>();
        ownerRoot = ownerOverride != null
            ? ownerOverride
            : playerController != null ? playerController.gameObject : transform.root.gameObject;
        facingProvider = GetComponentInParent<IFacingProvider>();
        health = GetComponentInParent<IHealth>();

        if (facingProvider == null)
            Debug.LogWarning($"[PlayerSkillController] Không tìm thấy IFacingProvider trên '{name}'. Hướng mặc định là (1,0).", this);
        if (health == null)
            Debug.LogWarning($"[PlayerSkillController] Không tìm thấy IHealth trên '{name}'.", this);
        if (skillConfig == null)
            Debug.LogWarning("[PlayerSkillController] Không tìm thấy SkillConfig. Hãy gán Inspector hoặc đặt ở Resources/SkillConfig.", this);

        RefreshWeaponContexts(true);
        BindUiButtons();
    }

    private void Start()
    {
        // Đồng bộ lại ở Start để controller chạy trước/sau PlayerProgressionService đều nhận đúng
        // skill đã trang bị và cooldown đã lưu.
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress != null && progress.Data != null) SyncEquippedSkills(progress.Data, null);
    }

    /// <summary>Ưu tiên: field Inspector -> SkillConfig của PlayerProgressionService -> Resources/SkillConfig.</summary>
    private SkillConfig EnsureSkillConfig()
    {
        if (skillConfig != null) return skillConfig;
        if (PlayerProgressionService.Instance != null) skillConfig = PlayerProgressionService.Instance.SkillConfigAsset;
        if (skillConfig == null) skillConfig = Resources.Load<SkillConfig>("SkillConfig");
        return skillConfig;
    }

    private void CaptureConfiguredSkillTemplates()
    {
        if (skillSlotTemplatesCaptured) return;
        skillSlotTemplates = skillSlots != null ? (PlayerSkillSlot[])skillSlots.Clone() : new PlayerSkillSlot[0];
        skillSlotTemplatesCaptured = true;
    }

    /// <summary>
    /// Dựng lại danh sách slot từ skill đã trang bị trong save.
    /// equipped.skillName có thể là skillId (chuẩn mới) hoặc tên hiển thị cũ; SkillConfig.FindEntry xử lý cả hai.
    /// Tham số table chỉ giữ để tương thích chữ ký cũ, không còn được dùng.
    /// </summary>
    public void SyncEquippedSkills(PlayerSaveData data, SkillTable table)
    {
        if (data == null) return;
        CaptureConfiguredSkillTemplates();
        EnsureSkillConfig();

        List<PlayerSkillSlot> runtimeSlots = new List<PlayerSkillSlot>();
        foreach (PlayerSkillSlot template in skillSlotTemplates)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.skillId)
                || !string.IsNullOrWhiteSpace(template.requiredUnlockSkill))
                continue;
            if (IsBasicAttackSkill(template.skillId))
            {
                Debug.LogWarning($"[PlayerSkillController] '{template.skillId}' là skill đánh thường (BasicAttack): không nhận ở controller này. Hãy dùng PlayerBasicAttackController.", this);
                continue;
            }
            runtimeSlots.Add(CopySlot(template));
        }

        if (data.equippedSkills != null && skillConfig != null)
        {
            string weaponId = GetCurrentWeaponName();
            float attackPower = PlayerProgressionService.Instance != null
                ? PlayerProgressionService.Instance.GetAttackPowerExact() : 0f;

            for (int slotIndex = 0; slotIndex < PlayerSaveData.EquippedSkillCapacity; slotIndex++)
            {
                EquippedSkillData equipped = data.equippedSkills.Find(entry => entry != null && entry.slotIndex == slotIndex);
                if (equipped == null) continue;

                SkillConfigEntry configEntry = skillConfig.FindEntry(equipped.skillName);
                if (configEntry == null || string.IsNullOrWhiteSpace(configEntry.skillId)) continue;
                if (configEntry.type == SkillType.BasicAttack
                    || configEntry.type == SkillType.Passive
                    || configEntry.type == SkillType.PassiveBuff)
                    continue; // BasicAttack do PlayerBasicAttackController lo; passive không dùng input slot.
                if (PlayerProgressionService.Instance != null
                    && !PlayerProgressionService.Instance.IsWeaponRequirementMet(configEntry))
                    continue; // thiếu vũ khí yêu cầu: service sẽ gỡ khỏi loadout

                string skillId = configEntry.skillId.Trim();
                PlayerSkillSlot slot = FindTemplate(skillId);
                if (slot == null) slot = new PlayerSkillSlot();
                slot.skillId = skillId;
                slot.actionId = configEntry.actionId;
                slot.requiredUnlockSkill = skillId;
                slot.pcKey = equipped.inputKey;
                slot.mouseButton = MouseButtonSelect.None;
                slot.pcKeyInvalid = false;
                slot.damage = attackPower > 0f ? attackPower : slot.damage;

                SkillContextConfig contextConfig = skillConfig.GetContextConfig(skillId, weaponId);
                if (contextConfig != null) CopyContextToSlot(contextConfig, slot);
                runtimeSlots.Add(slot);
            }
        }

        skillSlots = runtimeSlots.ToArray();
        RefreshWeaponContexts(true);
        BindUiButtons();

        if (!cooldownsRestored) RestoreCooldowns(data);
        else ApplySlotCooldownState();
    }

    private PlayerSkillSlot FindTemplate(string skillId)
    {
        if (skillSlotTemplates == null) return null;
        foreach (PlayerSkillSlot template in skillSlotTemplates)
        {
            if (template != null && string.Equals(template.skillId, skillId, StringComparison.OrdinalIgnoreCase))
                return CopySlot(template);
        }
        return null;
    }

    private static PlayerSkillSlot CopySlot(PlayerSkillSlot source)
    {
        return new PlayerSkillSlot
        {
            skillId = source.skillId,
            actionId = source.actionId,
            requiredUnlockSkill = source.requiredUnlockSkill,
            cooldown = source.cooldown,
            pcKey = source.pcKey,
            uiButton = source.uiButton,
            mouseButton = source.mouseButton,
            damage = source.damage,
            skillSize = source.skillSize,
            range = source.range,
            speed = source.speed,
            duration = source.duration,
            stunTime = source.stunTime,
            chargeTime = source.chargeTime,
            endTime = source.endTime,
            debug = source.debug,
            canPierce = source.canPierce,
            dotConfig = source.dotConfig,
            lastUsedTime = -999f,
            pcKeyInvalid = false
        };
    }

    private static void CopyContextToSlot(SkillContextConfig source, PlayerSkillSlot target)
    {
        target.cooldown = source.cooldown;
        target.skillSize = source.skillSize;
        target.range = source.range;
        target.speed = source.speed;
        target.duration = source.duration;
        target.stunTime = source.stunTime;
        target.chargeTime = source.chargeTime;
        target.endTime = source.endTime;
        target.debug = source.debug;
        target.canPierce = source.canPierce;
        target.dotConfig = source.dotConfig;
    }

    /// <summary>
    /// Cập nhật context của mọi slot khi vũ khí đổi (range/size/cooldown/DOT/animation có thể khác theo vũ khí).
    /// force = true bỏ qua kiểm tra "vũ khí không đổi".
    /// </summary>
    private void RefreshWeaponContexts(bool force)
    {
        if (skillConfig == null) return;
        string weaponName = GetCurrentWeaponName();
        if (!force && string.Equals(lastWeaponForContexts, weaponName, StringComparison.OrdinalIgnoreCase)) return;
        lastWeaponForContexts = weaponName;

        if (skillSlots == null) return;
        foreach (PlayerSkillSlot configuredSlot in skillSlots)
        {
            if (configuredSlot == null || string.IsNullOrWhiteSpace(configuredSlot.skillId)) continue;
            SkillContextConfig weaponContext = skillConfig.GetContextConfig(configuredSlot.skillId, weaponName);
            if (weaponContext != null) CopyContextToSlot(weaponContext, configuredSlot);
        }
    }

    private bool IsBasicAttackSkill(string skillId)
    {
        SkillConfigEntry entry = skillConfig != null ? skillConfig.GetEntryById(skillId) : null;
        return entry != null && entry.type == SkillType.BasicAttack;
    }

    /// <summary>
    /// Cấu hình đánh thường cũ (trước khi tách PlayerBasicAttackController). Chỉ để PlayerBasicAttackController
    /// sao chép một lần khi được thêm vào Player; không dùng cho gameplay.
    /// </summary>
    public void GetLegacyBasicAttackInput(out string key, out MouseButtonSelect mouse, out Button button)
    {
        key = basicAttackPcKey;
        mouse = basicAttackMouseButton;
        button = basicAttackUiButton;
    }

    private void BindUiButtons()
    {
        if (skillSlots == null) return;
        for (int i = 0; i < skillSlots.Length; i++)
        {
            PlayerSkillSlot slot = skillSlots[i];
            if (slot == null || slot.uiButton == null || !boundUiButtons.Add(slot.uiButton)) continue;
            Button button = slot.uiButton;
            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, () => HandleUiPointerDown(button));
            AddTrigger(trigger, EventTriggerType.PointerUp, () => HandleUiPointerUp(button));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => HandleUiPointerUp(button));
        }
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private void HandleUiPointerDown(Button button)
    {
        int index = FindSlotIndexByButton(button);
        if (index < 0) return;
        skillSlots[index].uiHeld = true;
        TryActivateSlot(index);
    }

    private void HandleUiPointerUp(Button button)
    {
        int index = FindSlotIndexByButton(button);
        if (index >= 0) skillSlots[index].uiHeld = false;
    }

    private int FindSlotIndexByButton(Button button)
    {
        if (skillSlots == null || button == null) return -1;
        for (int i = 0; i < skillSlots.Length; i++)
            if (skillSlots[i] != null && skillSlots[i].uiButton == button) return i;
        return -1;
    }

    private void Update()
    {
        if (GameplayPause.IsPaused) return;
        RefreshWeaponContexts(false);
        if (skillSlots == null) return;
        for (int i = 0; i < skillSlots.Length; i++) CheckPcInput(i);
    }

    private void CheckPcInput(int index)
    {
        PlayerSkillSlot slot = skillSlots[index];
        if (slot == null) return;
        bool keyTriggered = !string.IsNullOrEmpty(slot.pcKey) && !slot.pcKeyInvalid && TryGetKeyDown(slot);
        bool mouseTriggered = IsMouseButtonDown(slot.mouseButton);
        if (keyTriggered || mouseTriggered) TryActivateSlot(index);
    }

    private bool TryGetKeyDown(PlayerSkillSlot slot)
    {
        try { return Input.GetKeyDown(slot.pcKey); }
        catch (ArgumentException)
        {
            Debug.LogWarning($"[PlayerSkillController] Tên phím '{slot.pcKey}' của skill '{slot.skillId}' không hợp lệ. Bỏ qua phím này.", this);
            slot.pcKeyInvalid = true;
            return false;
        }
    }

    private bool IsMouseButtonDown(MouseButtonSelect select)
    {
        switch (select)
        {
            case MouseButtonSelect.Left: return Input.GetMouseButtonDown(0);
            case MouseButtonSelect.Right: return Input.GetMouseButtonDown(1);
            case MouseButtonSelect.Middle: return Input.GetMouseButtonDown(2);
            default: return false;
        }
    }

    public void TryActivateSlot(int index)
    {
        if (!CanActivate(index, out PlayerSkillSlot slot)) return;
        float chargeTime = GetChargeTime(slot);
        if (chargeTime > 0f) StartCoroutine(ChargeAndFire(slot, chargeTime));
        else FireSlot(slot);
    }

    private float GetChargeTime(PlayerSkillSlot slot)
    {
        SkillContextConfig context = skillConfig != null
            ? skillConfig.GetContextConfig(slot.skillId, GetCurrentWeaponName()) : null;
        return context != null ? context.chargeTime : slot.chargeTime;
    }

    private bool CanActivate(int index, out PlayerSkillSlot slot)
    {
        slot = null;
        if (GameplayPause.IsPaused || skillSlots == null || index < 0 || index >= skillSlots.Length) return false;
        if (playerController != null && playerController.IsActionLocked) return false;
        if (health != null && health.IsDead) return false;

        slot = skillSlots[index];
        if (slot == null || slot.isCharging) return false;
        if (!string.IsNullOrWhiteSpace(slot.requiredUnlockSkill)
            && (PlayerProgressionService.Instance == null
                || !PlayerProgressionService.Instance.HasUnlockedSkill(slot.requiredUnlockSkill))) return false;
        if (string.IsNullOrWhiteSpace(slot.skillId))
        {
            Debug.LogWarning($"[PlayerSkillController] skillId rỗng tại slot {index}.", this);
            return false;
        }
        if (Time.time - slot.lastUsedTime < slot.cooldown) return false;
        SkillConfigEntry entry = skillConfig != null ? skillConfig.GetEntryById(slot.skillId) : null;
        if (entry != null && (entry.type == SkillType.Passive || entry.type == SkillType.PassiveBuff))
        {
            Debug.LogWarning($"[PlayerSkillController] '{slot.skillId}' là passive, không được gắn vào active input slot.", this);
            return false;
        }

        if (entry != null && entry.type == SkillType.BasicAttack)
        {
            Debug.LogWarning($"[PlayerSkillController] '{slot.skillId}' là skill đánh thường: do PlayerBasicAttackController xử lý, không dùng ở controller này.", this);
            return false;
        }

        // Điều kiện vũ khí: kiểm tra ngay trước khi dùng (PlayerProgressionService cũng tự gỡ skill khỏi loadout khi mất vũ khí).
        if (entry != null
            && PlayerProgressionService.Instance != null
            && !PlayerProgressionService.Instance.IsWeaponRequirementMet(entry))
            return false;

        bool needsAction = entry == null || entry.type != SkillType.BuffAction || entry.executeActionAfterBuff;
        if (needsAction && SkillActions.Instance == null)
        {
            Debug.LogWarning("[PlayerSkillController] Chưa có SkillActions trong scene.", this);
            return false;
        }
        return true;
    }

    private IEnumerator ChargeAndFire(PlayerSkillSlot slot, float chargeTime)
    {
        slot.isCharging = true;
        bool completed = false;
        SkillContextConfig config = skillConfig != null ? skillConfig.GetContextConfig(slot.skillId, GetCurrentWeaponName()) : null;
        string animationName = !string.IsNullOrWhiteSpace(config?.animationName) ? config.animationName.Trim() : slot.skillId;
        yield return SkillAnimationPlayer.ChargeRoutine(
            ownerRoot,
            chargeTime,
            animationName,
            () => facingProvider != null ? facingProvider.FacingDirection : Vector2.right,
            () => IsSlotHeld(slot),
            () => health != null && health.IsDead,
            result => completed = result);

        slot.isCharging = false;
        if (completed) FireSlot(slot);
    }

    private bool IsSlotHeld(PlayerSkillSlot slot)
    {
        if (slot.uiHeld) return true;
        if (slot.mouseButton != MouseButtonSelect.None && Input.GetMouseButton((int)slot.mouseButton - 1)) return true;
        if (!string.IsNullOrEmpty(slot.pcKey) && !slot.pcKeyInvalid)
        {
            try { return Input.GetKey(slot.pcKey); }
            catch (ArgumentException) { slot.pcKeyInvalid = true; }
        }
        return false;
    }

    private void FireSlot(PlayerSkillSlot slot)
    {
        SkillConfigEntry configured = skillConfig != null ? skillConfig.FindEntry(slot.skillId) : null;
        if (configured != null && configured.needsInspectorSetup)
        {
            Debug.LogWarning("[PlayerSkillController] " + slot.skillId + ": " + configured.setupNotes, this);
            return;
        }
        MarkUsed(slot);
        Vector2 direction = facingProvider != null ? facingProvider.FacingDirection : Vector2.right;
        string weaponName = GetCurrentWeaponName();
        SkillConfigEntry entry = skillConfig != null ? skillConfig.GetEntryById(slot.skillId) : null;

        SkillContextConfig animationConfig = skillConfig != null ? skillConfig.GetContextConfig(slot.skillId, weaponName) : null;
        string animationName = !string.IsNullOrWhiteSpace(animationConfig?.animationName) ? animationConfig.animationName.Trim() : slot.skillId;
        CharacterAnimation animation = ownerRoot.GetComponentInChildren<CharacterAnimation>();
        if (animation != null)
        {
            animation.SetFacing(direction);
            if (!animation.IsPlayingSkill(animationName)) animation.PlayAnimationSkill(animationName);
            animation.BeginSkillCast();
        }

        // BuffAction áp dụng modifier theo duration; không cần class thực thi riêng nếu chỉ là buff chỉ số.
        if (entry != null && entry.type == SkillType.BuffAction)
        {
            SelfBuff buffController = ownerRoot.GetComponentInChildren<SelfBuff>(true);
            if (buffController == null)
            {
                Debug.LogWarning($"[PlayerSkillController] Skill '{slot.skillId}' cần SelfBuff trên Player.", this);
                return;
            }
            if (!buffController.ApplyBuff(slot.skillId, entry.buffConfig))
                Debug.LogWarning($"[PlayerSkillController] Không áp dụng được buff '{slot.skillId}'. Kiểm tra buffConfig/modifier.", this);

            if (!entry.executeActionAfterBuff) return;
        }

        string actionId = ResolveActionId(slot);
        if (string.IsNullOrWhiteSpace(actionId))
        {
            Debug.LogWarning($"[PlayerSkillController] Skill '{slot.skillId}' không có actionId.", this);
            return;
        }

        SkillContextConfig config = skillConfig != null
            ? skillConfig.GetContextConfig(slot.skillId, weaponName) : null;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        float attackPower = progress != null ? progress.GetAttackPowerExact() : slot.damage;
        float damageMultiplier = config != null ? config.damageMultiplier : 1f;

        SkillContext context = new SkillContext
        {
            IsHeld = () => IsSlotHeld(slot),
            Caster = ownerRoot,
            CasterType = SkillCasterType.Player,
            Position = ownerRoot.transform.position,
            Direction = direction,
            Damage = attackPower * damageMultiplier,
            Size = config != null ? config.skillSize : slot.skillSize,
            Range = config != null ? config.range : slot.range,
            Speed = config != null ? config.speed : slot.speed,
            Duration = config != null ? config.duration : slot.duration,
            StunTime = config != null ? config.stunTime : slot.stunTime,
            Debug = config != null ? config.debug : slot.debug,
            ChargeTime = config != null ? config.chargeTime : slot.chargeTime,
            EndTime = config != null ? config.endTime : slot.endTime,
            ActionDamageOverrides = config != null ? config.actionDamageOverrides : null,
            BreaksGuard = config != null && config.breaksGuard,
            BypassesMeleeResistance = config != null && config.bypassesMeleeResistance,
            Modules = config != null ? config.modules : null,
            EffectAnimation = config != null ? config.effectAnimation : null,
            CanPierce = config != null ? config.canPierce : slot.canPierce,
            DotConfig = config != null ? config.dotConfig : slot.dotConfig,
            AnimationName = config?.animationName,
            SkillId = slot.skillId,
            ActionId = actionId,
            WeaponId = weaponName
        };

        SkillActions.Instance.Activate(actionId, context);
    }


    private string ResolveActionId(PlayerSkillSlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.actionId)) return slot.actionId;
        SkillConfigEntry entry = skillConfig != null ? skillConfig.GetEntryById(slot.skillId) : null;
        if (entry != null && !string.IsNullOrWhiteSpace(entry.actionId)) return entry.actionId;

        // Tương thích slot cũ: skillName cũ từng chính là actionId.
        return slot.skillId;
    }

    private string GetCurrentWeaponName()
    {
        return PlayerProgressionService.Instance != null
            ? PlayerProgressionService.Instance.GetCurrentWeaponId()
            : UnarmedWeaponId;
    }

    // ---------------------------------------------------------------- Cooldown (theo skillId)

    private void MarkUsed(PlayerSkillSlot slot)
    {
        slot.lastUsedTime = Time.time;
        if (!string.IsNullOrWhiteSpace(slot.skillId)) lastUsedBySkill[slot.skillId] = Time.time;
    }

    private float GetLastUsed(string skillId)
    {
        return !string.IsNullOrWhiteSpace(skillId) && lastUsedBySkill.TryGetValue(skillId, out float lastUsed)
            ? lastUsed : -999f;
    }

    private void ApplySlotCooldownState()
    {
        if (skillSlots == null) return;
        foreach (PlayerSkillSlot slot in skillSlots)
            if (slot != null) slot.lastUsedTime = GetLastUsed(slot.skillId);
    }

    /// <summary>Thời gian hồi chiêu (giây) của skill: ưu tiên slot hiện tại, sau đó SkillConfig theo vũ khí đang cầm.</summary>
    public float GetCooldownDuration(string skillIdOrName)
    {
        string skillId = ResolveSkillId(skillIdOrName);
        if (skillSlots != null)
        {
            foreach (PlayerSkillSlot slot in skillSlots)
                if (slot != null && string.Equals(slot.skillId, skillId, StringComparison.OrdinalIgnoreCase))
                    return slot.cooldown;
        }

        SkillContextConfig config = skillConfig != null
            ? skillConfig.GetContextConfig(skillId, GetCurrentWeaponName()) : null;
        return config != null ? config.cooldown : 0f;
    }

    /// <summary>Số giây hồi chiêu còn lại của skill (0 = sẵn sàng). Dùng cho UI thanh kỹ năng.</summary>
    public float GetRemainingCooldown(string skillIdOrName)
    {
        string skillId = ResolveSkillId(skillIdOrName);
        float duration = GetCooldownDuration(skillId);
        return Mathf.Clamp(duration - (Time.time - GetLastUsed(skillId)), 0f, duration);
    }

    private string ResolveSkillId(string skillIdOrName)
    {
        if (string.IsNullOrWhiteSpace(skillIdOrName)) return string.Empty;
        SkillConfigEntry entry = skillConfig != null ? skillConfig.FindEntry(skillIdOrName) : null;
        return entry != null && !string.IsNullOrWhiteSpace(entry.skillId) ? entry.skillId.Trim() : skillIdOrName.Trim();
    }

    /// <summary>Ghi cooldown còn lại vào save. Chưa nạp từ save thì bỏ qua để không ghi đè save bằng trạng thái rỗng.</summary>
    public void CaptureCooldowns(PlayerSaveData data)
    {
        if (data == null || !cooldownsRestored) return;
        if (data.skillCooldowns == null) data.skillCooldowns = new List<SkillCooldownSnapshot>();
        data.skillCooldowns.RemoveAll(entry => entry != null && entry.ownerId == CooldownOwnerId);

        foreach (KeyValuePair<string, float> pair in lastUsedBySkill)
        {
            float duration = GetCooldownDuration(pair.Key);
            float remaining = Mathf.Min(duration, duration - (Time.time - pair.Value));
            if (remaining <= 0.01f) continue;
            data.skillCooldowns.Add(new SkillCooldownSnapshot
            {
                ownerId = CooldownOwnerId,
                skillId = pair.Key,
                remainingSeconds = remaining
            });
        }
    }

    /// <summary>Nạp cooldown đã lưu. Snapshot theo skillId; snapshot cũ theo actionId đã bị PlayerProgressionService dọn.</summary>
    public void RestoreCooldowns(PlayerSaveData data)
    {
        lastUsedBySkill.Clear();
        if (data != null && data.skillCooldowns != null)
        {
            foreach (SkillCooldownSnapshot saved in data.skillCooldowns)
            {
                if (saved == null || saved.ownerId != CooldownOwnerId || string.IsNullOrWhiteSpace(saved.skillId)) continue;
                float remaining = Mathf.Max(0f, saved.remainingSeconds);
                if (remaining <= 0f) continue;

                float duration = Mathf.Max(GetCooldownDuration(saved.skillId), remaining);
                lastUsedBySkill[saved.skillId] = Time.time - (duration - remaining);
            }
        }

        cooldownsRestored = true;
        ApplySlotCooldownState();
    }

    private void OnDisable()
    {
        if (skillSlots == null) return;
        bool wasCharging = false;
        foreach (PlayerSkillSlot slot in skillSlots)
        {
            if (slot == null || !slot.isCharging) continue;
            slot.isCharging = false;
            wasCharging = true;
        }
        if (wasCharging)
        {
            StopAllCoroutines();
            if (ownerRoot != null) ownerRoot.GetComponentInChildren<CharacterAnimation>()?.CancelSkillAnimation();
            if (playerController != null) playerController.LockMovement(false);
        }
    }

#if ENABLE_INPUT_SYSTEM
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.performed || skillSlots == null) return;
        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (MatchesControl(skillSlots[i], context.control))
            {
                TryActivateSlot(i);
                return;
            }
        }
    }

    private bool MatchesControl(PlayerSkillSlot slot, InputControl control)
    {
        if (slot == null || control == null) return false;
        if (control.device is Mouse)
        {
            switch (slot.mouseButton)
            {
                case MouseButtonSelect.Left: return control.name == "leftButton";
                case MouseButtonSelect.Right: return control.name == "rightButton";
                case MouseButtonSelect.Middle: return control.name == "middleButton";
                default: return false;
            }
        }

        if (control.device is Keyboard && !string.IsNullOrEmpty(slot.pcKey))
        {
            string slotKey = slot.pcKey.Replace(" ", "").ToLowerInvariant();
            string controlName = control.name.Replace(" ", "").ToLowerInvariant();
            return controlName == slotKey;
        }
        return false;
    }
#endif
}

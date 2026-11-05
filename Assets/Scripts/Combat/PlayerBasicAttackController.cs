using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Điều khiển đòn ĐÁNH THƯỜNG của Player (đánh thường bằng tay, bằng dao...). Controller này CHỈ nhận skill type BasicAttack.
/// - Tự chọn đòn theo vũ khí đang cầm (SkillConfigEntry.weaponId khớp đúng, hoặc Weapon Requirement khớp loại vũ khí,
///   hoặc đòn tay không) và TỰ ĐỔI ngay khi người chơi đổi / gỡ / mất vũ khí.
/// - Không nằm trong 3 ô kỹ năng và không cần mở khóa. Skill chủ động và buff do PlayerSkillController quản lý.
/// - Hồi chiêu tính riêng theo skillId (đánh thường tay không và bằng dao không dùng chung) và được lưu vào save.
/// Gắn cạnh PlayerSkillController (hoặc trên Player). Nếu quên gắn, PlayerProgressionService tự thêm khi bật Auto Attach Skill Components.
/// </summary>
public class PlayerBasicAttackController : MonoBehaviour
{
    /// <summary>ownerId dùng khi lưu cooldown đánh thường vào PlayerSaveData.skillCooldowns (tách khỏi skill chủ động).</summary>
    public const string CooldownOwnerId = "PLAYER_BASIC";

    private const string UnarmedWeaponId = "Punch";

    [Header("Skill Config")]
    [Tooltip("Để trống: tự lấy SkillConfig của PlayerProgressionService (hoặc Resources/SkillConfig).")]
    [SerializeField] private SkillConfig skillConfig;

    [Header("Owner")]
    [SerializeField] private GameObject ownerOverride;

    [Header("Input đánh thường")]
    [SerializeField] private MouseButtonSelect mouseButton = MouseButtonSelect.Left;
    [Tooltip("Phím tuỳ chọn (ví dụ j). Để trống nếu chỉ dùng chuột / nút UI.")]
    [SerializeField] private string pcKey = "";
    [Tooltip("Nút UI cho mobile. Giữ nút để tích chiêu nếu đòn có Charge Time.")]
    [SerializeField] private Button uiButton;

    [Header("Đòn đánh thường hiện tại (tự đổi theo vũ khí - chỉ để xem)")]
    [SerializeField] private string currentWeaponId;
    [SerializeField] private string currentSkillId;
    [SerializeField] private string currentActionId;

    // true sau khi đã sao chép cấu hình phím/chuột/nút cũ từ PlayerSkillController (xem ImportLegacyInputIfNeeded).
    [SerializeField, HideInInspector] private bool inputImported;

    private GameObject ownerRoot;
    private IFacingProvider facingProvider;
    private PlayerController playerController;
    private IHealth health;
    private SkillConfigEntry currentEntry;

    private readonly Dictionary<string, float> lastUsedBySkill =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private bool cooldownsRestored;
    private bool isCharging;
    private bool uiHeld;
    private bool uiButtonBound;
    private bool pcKeyInvalid;
    private bool warnedNoBasicAttack;
    private int lastTriggerFrame = -1;

    /// <summary>skillId của đòn đánh thường đang dùng (đổi theo vũ khí). Rỗng nếu chưa có.</summary>
    public string CurrentSkillId => currentSkillId;

    /// <summary>Entry SkillConfig của đòn đánh thường đang dùng (luôn là type BasicAttack, hoặc null).</summary>
    public SkillConfigEntry CurrentEntry => currentEntry;

    /// <summary>Phát ra khi đòn đánh thường đổi (do đổi vũ khí). Tham số: skillId mới ("" nếu không có đòn nào).</summary>
    public event Action<string> BasicAttackChanged;

    private void Awake()
    {
        ImportLegacyInputIfNeeded();

        playerController = FindOnOwner<PlayerController>();
        facingProvider = FindOnOwner<IFacingProvider>();
        health = FindOnOwner<IHealth>();
        ownerRoot = ownerOverride != null
            ? ownerOverride
            : playerController != null ? playerController.gameObject : transform.root.gameObject;

        if (facingProvider == null)
            Debug.LogWarning($"[PlayerBasicAttackController] Không tìm thấy IFacingProvider trên '{name}'. Hướng mặc định là (1,0).", this);
        if (EnsureSkillConfig() == null)
            Debug.LogWarning("[PlayerBasicAttackController] Không tìm thấy SkillConfig. Hãy gán Inspector hoặc đặt ở Resources/SkillConfig.", this);

        RefreshForWeapon(true);
        BindUiButton();
    }

    private void Start()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress != null && progress.Data != null && !cooldownsRestored) RestoreCooldowns(progress.Data);
        RefreshForWeapon(true);
    }

    /// <summary>Tìm component trên chủ sở hữu: từ chính nó đi lên cha, rồi đi xuống con, rồi toàn bộ cây của root.</summary>
    private T FindOnOwner<T>() where T : class
    {
        T found = GetComponentInParent<T>();
        if (found != null) return found;
        found = GetComponentInChildren<T>(true);
        if (found != null) return found;
        return transform.root.GetComponentInChildren<T>(true);
    }

    /// <summary>Ưu tiên: Inspector -> SkillConfig của PlayerProgressionService -> Resources/SkillConfig.</summary>
    private SkillConfig EnsureSkillConfig()
    {
        if (skillConfig != null) return skillConfig;
        if (PlayerProgressionService.Instance != null) skillConfig = PlayerProgressionService.Instance.SkillConfigAsset;
        if (skillConfig == null) skillConfig = Resources.Load<SkillConfig>("SkillConfig");
        return skillConfig;
    }

    // ------------------------------------------------------------------ Tự đổi đòn theo vũ khí

    private string GetCurrentWeaponName()
    {
        return PlayerProgressionService.Instance != null
            ? PlayerProgressionService.Instance.GetCurrentWeaponId()
            : UnarmedWeaponId;
    }

    private WeaponType GetCurrentWeaponType()
    {
        return PlayerProgressionService.Instance != null
            ? PlayerProgressionService.Instance.GetCurrentWeaponType()
            : WeaponType.Unarmed;
    }

    /// <summary>Chọn lại đòn đánh thường theo vũ khí đang cầm. force = true bỏ qua kiểm tra "vũ khí không đổi".</summary>
    private void RefreshForWeapon(bool force)
    {
        if (EnsureSkillConfig() == null) return;

        string weaponId = GetCurrentWeaponName();
        if (!force && string.Equals(currentWeaponId, weaponId, StringComparison.OrdinalIgnoreCase)) return;
        currentWeaponId = weaponId;

        SkillConfigEntry entry = skillConfig.GetBasicAttackForWeapon(weaponId, GetCurrentWeaponType());
        if (entry != null && entry.type != SkillType.BasicAttack)
        {
            // Controller này chỉ nhận skill type BasicAttack.
            Debug.LogWarning($"[PlayerBasicAttackController] '{entry.skillId}' không phải BasicAttack nên bị bỏ qua.", this);
            entry = null;
        }

        if (entry == null)
        {
            if (!warnedNoBasicAttack)
            {
                warnedNoBasicAttack = true;
                Debug.LogWarning($"[PlayerBasicAttackController] Không có skill BasicAttack cho vũ khí '{weaponId}' (kể cả đòn tay không). Player sẽ không đánh thường được.", this);
            }
            SetCurrent(null);
            return;
        }

        warnedNoBasicAttack = false;
        SetCurrent(entry);
    }

    private void SetCurrent(SkillConfigEntry entry)
    {
        string newSkillId = entry != null && !string.IsNullOrWhiteSpace(entry.skillId) ? entry.skillId.Trim() : string.Empty;
        bool changed = !string.Equals(currentSkillId, newSkillId, StringComparison.OrdinalIgnoreCase);

        currentEntry = entry;
        currentSkillId = newSkillId;
        currentActionId = entry != null ? entry.actionId : string.Empty;
        if (changed) BasicAttackChanged?.Invoke(newSkillId);
    }

    // ------------------------------------------------------------------ Input

    private void Update()
    {
        RefreshForWeapon(false); // đổi vũ khí (kể cả khi đang mở inventory) -> đổi đòn ngay
        if (GameplayPause.IsPaused) return;

        bool keyTriggered = !string.IsNullOrEmpty(pcKey) && !pcKeyInvalid && TryGetKeyDown();
        bool mouseTriggered = IsMouseButtonDown(mouseButton);
        if (keyTriggered || mouseTriggered) TryActivate();
    }

    private bool TryGetKeyDown()
    {
        try { return Input.GetKeyDown(pcKey); }
        catch (ArgumentException)
        {
            Debug.LogWarning($"[PlayerBasicAttackController] Tên phím '{pcKey}' không hợp lệ. Bỏ qua phím này.", this);
            pcKeyInvalid = true;
            return false;
        }
    }

    private static bool IsMouseButtonDown(MouseButtonSelect select)
    {
        switch (select)
        {
            case MouseButtonSelect.Left: return Input.GetMouseButtonDown(0);
            case MouseButtonSelect.Right: return Input.GetMouseButtonDown(1);
            case MouseButtonSelect.Middle: return Input.GetMouseButtonDown(2);
            default: return false;
        }
    }

    private bool IsHeld()
    {
        if (uiHeld) return true;
        if (mouseButton != MouseButtonSelect.None && Input.GetMouseButton((int)mouseButton - 1)) return true;
        if (!string.IsNullOrEmpty(pcKey) && !pcKeyInvalid)
        {
            try { return Input.GetKey(pcKey); }
            catch (ArgumentException) { pcKeyInvalid = true; }
        }
        return false;
    }

    private void BindUiButton()
    {
        if (uiButton == null || uiButtonBound) return;
        uiButtonBound = true;

        EventTrigger trigger = uiButton.GetComponent<EventTrigger>();
        if (trigger == null) trigger = uiButton.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerDown, HandleUiPointerDown);
        AddTrigger(trigger, EventTriggerType.PointerUp, HandleUiPointerUp);
        AddTrigger(trigger, EventTriggerType.PointerExit, HandleUiPointerUp);
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private void HandleUiPointerDown()
    {
        if (!isActiveAndEnabled) return;
        uiHeld = true;
        TryActivate();
    }

    private void HandleUiPointerUp()
    {
        uiHeld = false;
    }

    // ------------------------------------------------------------------ Thực thi đòn

    /// <summary>Đánh thường bằng đòn của vũ khí đang cầm (nếu đủ điều kiện). Trả về true nếu đòn được bắt đầu.</summary>
    public bool TryActivate()
    {
        if (!CanActivate()) return false;
        lastTriggerFrame = Time.frameCount;

        SkillConfigEntry entry = currentEntry;
        string weaponId = currentWeaponId;
        SkillContextConfig config = skillConfig.GetContextConfig(entry.skillId, weaponId);
        float chargeTime = config != null ? config.chargeTime : 0f;
        if (chargeTime > 0f) StartCoroutine(ChargeAndFire(entry, weaponId, chargeTime));
        else Fire(entry, weaponId);
        return true;
    }

    private bool CanActivate()
    {
        if (lastTriggerFrame == Time.frameCount) return false; // Update và OnAttack cùng frame không bắn 2 lần
        if (GameplayPause.IsPaused || isCharging) return false;
        if (currentEntry == null)
        {
            RefreshForWeapon(true);
            if (currentEntry == null) return false;
        }
        if (playerController != null && playerController.IsActionLocked) return false;
        if (health != null && health.IsDead) return false;
        if (string.IsNullOrWhiteSpace(currentEntry.actionId))
        {
            Debug.LogWarning($"[PlayerBasicAttackController] BasicAttack '{currentEntry.skillId}' chưa có actionId.", this);
            return false;
        }
        if (SkillActions.Instance == null)
        {
            Debug.LogWarning("[PlayerBasicAttackController] Chưa có SkillActions trong scene.", this);
            return false;
        }

        return Time.time - GetLastUsed(currentEntry.skillId) >= GetCooldownDuration(currentEntry.skillId);
    }

    private IEnumerator ChargeAndFire(SkillConfigEntry entry, string weaponId, float chargeTime)
    {
        isCharging = true;
        bool completed = false;
        string actionId = entry.actionId;
        SkillContext chargeContext = BuildContext(entry, weaponId);
        if (SkillActions.Instance != null) SkillActions.Instance.PlayChargeSfx(chargeContext);

        SkillAnimationSet chargeSet = skillConfig.GetAnimationSet(entry.skillId, weaponId);
        yield return SkillAnimationPlayer.ChargeRoutine(
            ownerRoot,
            chargeTime,
            chargeSet,
            () => facingProvider != null ? facingProvider.FacingDirection : Vector2.right,
            IsHeld,
            () => health != null && health.IsDead,
            result => completed = result);

        isCharging = false;
        if (!completed && SkillActions.Instance != null) SkillActions.Instance.StopChargeSfx(actionId);
        if (completed) Fire(entry, weaponId);
    }

    private void Fire(SkillConfigEntry entry, string weaponId)
    {
        MarkUsed(entry.skillId);

        SkillContext context = BuildContext(entry, weaponId);
        if (context == null)
        {
            Debug.LogWarning($"[PlayerBasicAttackController] Không dựng được context cho '{entry.skillId}'.", this);
            return;
        }

        context.Caster = ownerRoot;
        context.CasterType = SkillCasterType.Player;
        context.Position = ownerRoot.transform.position;
        context.Direction = facingProvider != null ? facingProvider.FacingDirection : Vector2.right;
        SkillActions.Instance.Activate(entry.actionId, context);
    }

    private SkillContext BuildContext(SkillConfigEntry entry, string weaponId)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress != null) return progress.GetSkillContext(entry.skillId, weaponId);

        // Không có PlayerProgressionService (scene test): dựng context tối thiểu từ SkillConfig.
        SkillContextConfig config = skillConfig.GetContextConfig(entry.skillId, weaponId);
        if (config == null) return null;
        return new SkillContext
        {
            Damage = 10f * config.damageMultiplier,
            Size = config.skillSize,
            Range = config.range,
            Speed = config.speed,
            Duration = config.duration,
            StunTime = config.stunTime,
            ChargeTime = config.chargeTime,
            EndTime = config.endTime,
            ChargeSfx = config.chargeSfx,
            CastSfx = config.castSfx,
            HitSfx = config.hitSfx,
            SfxVolume = config.sfxVolume,
            CutChargeSfxToChargeTime = config.cutChargeSfxToChargeTime,
            CanPierce = config.canPierce,
            Debug = config.debug,
            DotConfig = config.dotConfig != null ? config.dotConfig : new DameDotConfig(),
            AnimationSet = skillConfig.GetAnimationSet(entry.skillId, weaponId),
            SkillId = entry.skillId,
            ActionId = entry.actionId,
            WeaponId = weaponId
        };
    }

    // ------------------------------------------------------------------ Cooldown (theo skillId)

    private void MarkUsed(string skillId)
    {
        if (!string.IsNullOrWhiteSpace(skillId)) lastUsedBySkill[skillId] = Time.time;
    }

    private float GetLastUsed(string skillId)
    {
        return !string.IsNullOrWhiteSpace(skillId) && lastUsedBySkill.TryGetValue(skillId, out float lastUsed)
            ? lastUsed : -999f;
    }

    /// <summary>Thời gian hồi chiêu (giây) của một đòn đánh thường theo vũ khí đang cầm.</summary>
    public float GetCooldownDuration(string skillId)
    {
        if (skillConfig == null || string.IsNullOrWhiteSpace(skillId)) return 0f;
        SkillContextConfig config = skillConfig.GetContextConfig(skillId, currentWeaponId);
        return config != null ? config.cooldown : 0f;
    }

    /// <summary>Số giây hồi chiêu còn lại của đòn đánh thường hiện tại (0 = sẵn sàng). Dùng cho UI.</summary>
    public float GetRemainingCooldown()
    {
        if (string.IsNullOrWhiteSpace(currentSkillId)) return 0f;
        float duration = GetCooldownDuration(currentSkillId);
        return Mathf.Clamp(duration - (Time.time - GetLastUsed(currentSkillId)), 0f, duration);
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

    public void RestoreCooldowns(PlayerSaveData data)
    {
        EnsureSkillConfig();
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
    }

    // ------------------------------------------------------------------ Chuyển cấu hình input cũ

    /// <summary>
    /// Sao chép phím / chuột / nút UI đánh thường từ PlayerSkillController (cấu hình cũ, trước khi tách controller) một lần.
    /// Gọi tự động khi thêm component trong Editor (Reset) hoặc khi service tự thêm lúc chạy.
    /// </summary>
    private void ImportLegacyInputIfNeeded()
    {
        if (inputImported) return;
        inputImported = true;

        PlayerSkillController legacy = FindOnOwner<PlayerSkillController>();
        if (legacy == null) return;
        legacy.GetLegacyBasicAttackInput(out string key, out MouseButtonSelect mouse, out Button button);
        pcKey = key ?? string.Empty;
        mouseButton = mouse;
        uiButton = button;
    }

    private void Reset()
    {
        inputImported = false;
        ImportLegacyInputIfNeeded();
    }

    private void OnDisable()
    {
        uiHeld = false;
        if (!isCharging) return;

        isCharging = false;
        if (SkillActions.Instance != null) SkillActions.Instance.StopChargeSfx(currentActionId);
        if (playerController != null) playerController.LockMovement(false);
    }

#if ENABLE_INPUT_SYSTEM
    /// <summary>Gắn vào Player Input (action Attack) nếu dùng Input System; bắn khi chuột/phím khớp cấu hình ở trên.</summary>
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (MatchesControl(context.control)) TryActivate();
    }

    private bool MatchesControl(InputControl control)
    {
        if (control == null) return false;
        if (control.device is Mouse)
        {
            switch (mouseButton)
            {
                case MouseButtonSelect.Left: return control.name == "leftButton";
                case MouseButtonSelect.Right: return control.name == "rightButton";
                case MouseButtonSelect.Middle: return control.name == "middleButton";
                default: return false;
            }
        }

        if (control.device is Keyboard && !string.IsNullOrEmpty(pcKey))
        {
            string slotKey = pcKey.Replace(" ", "").ToLowerInvariant();
            string controlName = control.name.Replace(" ", "").ToLowerInvariant();
            return controlName == slotKey;
        }
        return false;
    }
#endif
}

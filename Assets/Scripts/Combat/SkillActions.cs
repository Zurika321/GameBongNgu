using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SkillRegistration
{
    [Tooltip("Phải khớp actionId trong SkillConfigEntry. Không phải skillId.")]
    public string skillName;
    public Sprite[] skillSprites;
}

/// <summary>
/// Registry class thực thi. Nhiều skillId có thể cùng gọi một actionId, ví dụ
/// BasicAttack_Dagger và BasicAttack_Punch đều gọi NormalAttack.
/// </summary>
public class SkillActions : MonoBehaviour
{
    [Header("Action registry - nhập actionId, không nhập skillId")]
    [SerializeField] private SkillRegistration[] skillRegistrations =
    {
        new SkillRegistration { skillName = "BasicSlash" },
        new SkillRegistration { skillName = "DodgeRoll" },
        new SkillRegistration { skillName = "RatBleedBite" },
        new SkillRegistration { skillName = "HoundRush" },
        new SkillRegistration { skillName = "SpiderPoisonBite" },
        new SkillRegistration { skillName = "ArcAttack" },
        new SkillRegistration { skillName = "NormalAttack" },
        new SkillRegistration { skillName = "FlyAttack" },
        new SkillRegistration { skillName = "Fireball" },
        new SkillRegistration { skillName = "DashAttack2" }
    };

    public static SkillActions Instance { get; private set; }

    private readonly Dictionary<string, ISkill> skillMap = new Dictionary<string, ISkill>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SkillRegistration> registrationMap = new Dictionary<string, SkillRegistration>(StringComparer.OrdinalIgnoreCase);
    private AudioSource sfxSource;
    private AudioSource chargeSource;
    private string chargingSkillName;
    private Coroutine chargeCutRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
        SetupAudio();
        RegisterSkills();
    }

    private void SetupAudio()
    {
        sfxSource = CreateSfxSource();
        chargeSource = CreateSfxSource();
    }

    private AudioSource CreateSfxSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        VolumeManager.RegisterSFXSource(source);
        return source;
    }

    private void RegisterSkills()
    {
        skillMap.Clear();
        registrationMap.Clear();
        if (skillRegistrations == null) return;

        foreach (SkillRegistration registration in skillRegistrations)
        {
            if (registration == null || string.IsNullOrWhiteSpace(registration.skillName))
            {
                Debug.LogWarning("[SkillActions] Bỏ qua registration không có actionId.", this);
                continue;
            }

            string actionId = registration.skillName.Trim();
            if (skillMap.ContainsKey(actionId))
            {
                Debug.LogWarning($"[SkillActions] actionId '{actionId}' bị đăng ký trùng.", this);
                continue;
            }

            ISkill skill = CreateSkill(actionId);
            if (skill == null)
            {
                Debug.LogWarning($"[SkillActions] Không có class thực thi cho actionId '{actionId}'.", this);
                continue;
            }

            if (skill is ISkillSpriteConfigurable configurable)
                configurable.SetSkillSprites(registration.skillSprites);
            else if (registration.skillSprites != null && registration.skillSprites.Length > 0)
                Debug.LogWarning($"[SkillActions] '{actionId}' không nhận cấu hình sprite.", this);

            skillMap.Add(actionId, skill);
            registrationMap.Add(actionId, registration);
        }
    }

    private ISkill CreateSkill(string actionId)
    {
        switch (actionId)
        {
            case "BasicSlash": return new BasicMeleeSkill();
            case "DodgeRoll": return new DodgeRollSkill();
            case "RatBleedBite": return new RatBleedBiteSkill();
            case "HoundRush": return new HoundRushSkill();
            case "SpiderPoisonBite": return new SpiderPoisonBiteSkill();
            case "ArcAttack": return GetOrAddSkill<ArcAttackSkill>();
            case "NormalAttack": return GetOrAddSkill<NormalAttackSkill>();
            case "FlyAttack": return GetOrAddSkill<FlyAttackSkill>();
            case "Fireball": return GetOrAddSkill<FireballAttackSkill>();
            case "DashAttack2": return new DashAttackSkill2();
            default: return null;
        }
    }

    private T GetOrAddSkill<T>() where T : MonoBehaviour, ISkill
    {
        T skill = GetComponent<T>();
        return skill != null ? skill : gameObject.AddComponent<T>();
    }

    public bool HasRegisteredSkill(string actionId)
    {
        return !string.IsNullOrWhiteSpace(actionId) && skillMap.ContainsKey(actionId.Trim());
    }

    /// <summary>
    /// Danh sách actionId đã khai báo trong Inspector (không cần Play). SkillConfigValidator dùng để
    /// kiểm tra actionId trong SkillConfig đã được đăng ký.
    /// </summary>
    public List<string> GetConfiguredActionIds()
    {
        List<string> ids = new List<string>();
        if (skillRegistrations == null) return ids;
        foreach (SkillRegistration registration in skillRegistrations)
        {
            if (registration != null && !string.IsNullOrWhiteSpace(registration.skillName))
                ids.Add(registration.skillName.Trim());
        }
        return ids;
    }

    public void PlayChargeSfx(string actionId)
    {
        // Compatibility no-op: charge audio now belongs to SkillContext and is played from the skill execution path.
    }

    public void PlayChargeSfx(SkillContext context)
    {
        if (GameplayPause.IsPaused || context == null || chargeSource == null || context.ChargeSfx == null) return;

        float cutDuration = 0f;
        if (context.CutChargeSfxToChargeTime && context.ChargeTime > 0f)
            cutDuration = Mathf.Min(context.ChargeTime, context.ChargeSfx.length);

        chargeSource.Stop();
        if (chargeCutRoutine != null)
        {
            StopCoroutine(chargeCutRoutine);
            chargeCutRoutine = null;
        }

        chargingSkillName = context.ActionId;
        chargeSource.clip = context.ChargeSfx;
        chargeSource.volume = Mathf.Clamp01(context.SfxVolume);
        chargeSource.pitch = 1f;
        chargeSource.loop = false;
        chargeSource.Play();

        if (cutDuration > 0.05f)
            chargeCutRoutine = StartCoroutine(TrimChargeSfxAfterSeconds(cutDuration));
    }

    public void StopChargeSfx(string actionId)
    {
        if (chargeSource == null || !string.Equals(chargingSkillName, actionId, StringComparison.OrdinalIgnoreCase)) return;
        if (chargeCutRoutine != null)
        {
            StopCoroutine(chargeCutRoutine);
            chargeCutRoutine = null;
        }
        chargeSource.Stop();
        chargeSource.clip = null;
        chargingSkillName = null;
    }

    public void PlayCastSfx(string actionId)
    {
        // Compatibility no-op: the actual cast audio is driven from SkillContext.
    }

    public void PlayCastSfx(SkillContext context)
    {
        if (GameplayPause.IsPaused || context == null || sfxSource == null || context.CastSfx == null) return;
        sfxSource.PlayOneShot(context.CastSfx, Mathf.Clamp01(context.SfxVolume));
    }

    public void PlayHitSfx(string actionId)
    {
        // Compatibility no-op: the actual hit audio is handled by the skill instance that triggers the hit.
    }

    public void PlayHitSfx(SkillContext context)
    {
        if (GameplayPause.IsPaused || context == null || sfxSource == null || context.HitSfx == null) return;
        sfxSource.PlayOneShot(context.HitSfx, Mathf.Clamp01(context.SfxVolume));
    }

    private System.Collections.IEnumerator TrimChargeSfxAfterSeconds(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (chargeSource != null && string.IsNullOrEmpty(chargingSkillName) == false)
            chargeSource.Stop();
        chargeSource.clip = null;
        chargingSkillName = null;
        chargeCutRoutine = null;
    }

    private bool TryGetRegistration(string actionId, out SkillRegistration registration)
    {
        registration = null;
        return !string.IsNullOrWhiteSpace(actionId) && registrationMap.TryGetValue(actionId.Trim(), out registration);
    }

    /// <summary>actionId chọn class; context chứa thông số của skillId cụ thể.</summary>
    public void Activate(string actionId, SkillContext context)
    {
        if (GameplayPause.IsPaused) return;
        if (string.IsNullOrWhiteSpace(actionId))
        {
            Debug.LogWarning("[SkillActions] actionId bị trống.", this);
            return;
        }
        if (context == null)
        {
            Debug.LogWarning($"[SkillActions] SkillContext null khi gọi '{actionId}'.", this);
            return;
        }

        actionId = actionId.Trim();
        if (!skillMap.TryGetValue(actionId, out ISkill skill))
        {
            Debug.LogWarning($"[SkillActions] Không tìm thấy actionId '{actionId}'.", this);
            return;
        }

        StopChargeSfx(actionId);
        PlayCastSfx(context);
        skill.Execute(context);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (sfxSource != null) VolumeManager.UnregisterSFXSource(sfxSource);
        if (chargeSource != null) VolumeManager.UnregisterSFXSource(chargeSource);
        skillMap.Clear();
        registrationMap.Clear();
    }
}

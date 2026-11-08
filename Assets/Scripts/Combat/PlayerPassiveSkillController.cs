using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Thông tin một passive của Player, hiển thị trong Inspector khi đang Play (tự cập nhật, không cần sửa tay).</summary>
[Serializable]
public class PassiveSkillInfo
{
    public string skillId;
    public string displayName;
    [Tooltip("Đang có hiệu lực: đã mở khóa và thỏa điều kiện vũ khí.")]
    public bool active;
    [Tooltip("Lý do chưa có hiệu lực (ví dụ thiếu vũ khí yêu cầu).")]
    public string note;
}

/// <summary>
/// Nơi xem và quản lý skill Passive/PassiveBuff của Player.
/// Controller TỰ hỏi PlayerProgressionService: lấy mọi passive đã mở khóa trong save, kiểm tra điều kiện vũ khí,
/// rồi chỉ bật/tắt phần chênh lệch (không làm mất các buff theo trigger đang chạy).
/// Khi đang Play, danh sách "Passive Skills" bên dưới luôn phản ánh đúng dữ liệu của Player.
/// Passive không hiện trong Inventory; mở/chỉnh passive qua LvConfig (mở khóa) và SkillConfig (Type = Passive/PassiveBuff).
/// </summary>
public class PlayerPassiveSkillController : MonoBehaviour
{
    [SerializeField] private SkillConfig skillConfig;
    [SerializeField] private GameObject ownerOverride;

    [Header("Passive của Player (tự lấy từ PlayerProgressionService - chỉ để xem)")]
    [SerializeField] private List<PassiveSkillInfo> passiveSkills = new List<PassiveSkillInfo>();

    [Header("Test")]
    [Tooltip("Các skillId passive luôn bật thêm vào danh sách passive đã mở khóa (test nhanh, không cần mở khóa).")]
    [SerializeField] private List<string> initialPassiveSkillIds = new List<string>();

    private readonly Dictionary<string, SkillConfigEntry> activePassives =
        new Dictionary<string, SkillConfigEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IPassiveSkill> customPassiveInstances =
        new Dictionary<string, IPassiveSkill>(StringComparer.OrdinalIgnoreCase);
    // Danh sách yêu cầu gần nhất, để bật lại đúng passive khi component bị disable rồi enable.
    private readonly List<string> desiredPassiveIds = new List<string>();

    private GameObject ownerRoot;
    private IStatModifierReceiver statReceiver;
    private SelfBuff buffController;
    private int lastSeenStateVersion = -1;
    private int lastSeenUnlockedCount = -1;

    /// <summary>Mọi passive Player đang có trong save (kể cả cái tạm thời chưa có hiệu lực vì thiếu vũ khí).</summary>
    public IReadOnlyList<PassiveSkillInfo> PassiveSkills => passiveSkills;

    private void Awake()
    {
        // Cùng quy tắc xác định owner với PlayerSkillController để IsOwner khớp đúng Player.
        PlayerController playerController = GetComponentInParent<PlayerController>();
        ownerRoot = ownerOverride != null
            ? ownerOverride
            : playerController != null ? playerController.gameObject : transform.root.gameObject;
        statReceiver = FindStatReceiver();
        buffController = ownerRoot.GetComponentInChildren<SelfBuff>(true);
        EnsureSkillConfig();
    }

    private void OnEnable()
    {
        SkillCombatEvents.HitLanded += HandleHitLanded;
        SkillCombatEvents.Killed += HandleKilled;
        SkillCombatEvents.DamageTaken += HandleDamageTaken;

        if (PlayerProgressionService.Instance != null) RefreshFromProgression();
        else ApplyDesiredPassives();
    }

    private void Start()
    {
        RefreshFromProgression();
    }

    private void Update()
    {
        // Tự phát hiện thay đổi: lên level mở khóa passive mới, đổi vũ khí làm passive có/không đủ điều kiện, load save...
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null) return;

        int unlockedCount = progress.Data.unlockedSkills != null ? progress.Data.unlockedSkills.Count : 0;
        if (progress.SkillStateVersion != lastSeenStateVersion || unlockedCount != lastSeenUnlockedCount)
            RefreshFromProgression();
    }

    /// <summary>Ưu tiên: Inspector -> SkillConfig của PlayerProgressionService -> Resources/SkillConfig.</summary>
    private SkillConfig EnsureSkillConfig()
    {
        if (skillConfig != null) return skillConfig;
        if (PlayerProgressionService.Instance != null) skillConfig = PlayerProgressionService.Instance.SkillConfigAsset;
        if (skillConfig == null) skillConfig = Resources.Load<SkillConfig>("SkillConfig");
        return skillConfig;
    }

    /// <summary>
    /// Hỏi PlayerProgressionService: passive nào đã mở khóa, cái nào đang thỏa điều kiện vũ khí;
    /// cập nhật danh sách xem được trong Inspector và bật/tắt passive tương ứng.
    /// </summary>
    public void RefreshFromProgression()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null) return;

        lastSeenStateVersion = progress.SkillStateVersion;
        lastSeenUnlockedCount = progress.Data.unlockedSkills != null ? progress.Data.unlockedSkills.Count : 0;

        List<string> usable = new List<string>();
        passiveSkills.Clear();
        foreach (string skillId in progress.GetUnlockedPassiveSkillIds())
        {
            SkillConfigEntry entry = progress.ResolveSkillEntry(skillId);
            bool weaponOk = progress.IsWeaponRequirementMet(entry) && !entry.needsInspectorSetup;
            passiveSkills.Add(new PassiveSkillInfo
            {
                skillId = skillId,
                displayName = progress.GetSkillDisplayName(skillId),
                active = weaponOk,
                note = entry.needsInspectorSetup ? entry.setupNotes : weaponOk ? string.Empty : "Cần cầm: " + progress.GetWeaponRequirementText(skillId)
            });
            if (weaponOk) usable.Add(skillId);
        }

        SetActivePassives(usable);
    }

    /// <summary>
    /// Đặt danh sách passive cần bật (skillId hoặc tên hiển thị). Chỉ bật/tắt phần chênh lệch.
    /// Dùng khi muốn điều khiển tay; bình thường RefreshFromProgression đã tự gọi.
    /// </summary>
    public void SetActivePassives(IEnumerable<string> skillIds)
    {
        desiredPassiveIds.Clear();
        if (skillIds != null)
        {
            foreach (string skillId in skillIds)
                if (!string.IsNullOrWhiteSpace(skillId)) desiredPassiveIds.Add(skillId.Trim());
        }
        ApplyDesiredPassives();
    }

    private void ApplyDesiredPassives()
    {
        if (!isActiveAndEnabled || EnsureSkillConfig() == null) return;

        HashSet<string> wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in desiredPassiveIds) wanted.Add(ResolveSkillId(id));
        if (initialPassiveSkillIds != null)
        {
            foreach (string id in initialPassiveSkillIds)
                if (!string.IsNullOrWhiteSpace(id)) wanted.Add(ResolveSkillId(id));
        }

        foreach (string activeId in new List<string>(activePassives.Keys))
            if (!wanted.Contains(activeId)) DeactivatePassive(activeId);
        foreach (string id in wanted) ActivatePassive(id);
    }

    private string ResolveSkillId(string skillIdOrName)
    {
        SkillConfigEntry entry = skillConfig != null ? skillConfig.FindEntry(skillIdOrName) : null;
        return entry != null && !string.IsNullOrWhiteSpace(entry.skillId) ? entry.skillId.Trim() : skillIdOrName.Trim();
    }

    public bool IsPassiveActive(string skillIdOrName)
    {
        return !string.IsNullOrWhiteSpace(skillIdOrName) && activePassives.ContainsKey(ResolveSkillId(skillIdOrName));
    }

    public bool ActivatePassive(string skillId)
    {
        if (EnsureSkillConfig() == null || string.IsNullOrWhiteSpace(skillId)) return false;
        SkillConfigEntry entry = skillConfig.FindEntry(skillId);
        if (entry == null)
        {
            Debug.LogWarning($"[PlayerPassiveSkillController] Không tìm thấy skillId '{skillId}'.", this);
            return false;
        }

        if (entry.type != SkillType.Passive && entry.type != SkillType.PassiveBuff)
        {
            Debug.LogWarning($"[PlayerPassiveSkillController] '{skillId}' không phải Passive/PassiveBuff.", this);
            return false;
        }

        if (entry.needsInspectorSetup) return false;
        if (activePassives.ContainsKey(entry.skillId)) return true;
        activePassives.Add(entry.skillId, entry);

        if (entry.type == SkillType.PassiveBuff || entry.passiveConfig == null
            || entry.passiveConfig.trigger == PassiveTriggerType.Always)
        {
            ApplyPermanentPassive(entry);
        }

        IPassiveSkill custom = FindCustomPassive(entry.actionId);
        if (custom != null)
        {
            customPassiveInstances[entry.skillId] = custom;
            custom.OnApply(new PassiveSkillRuntimeContext(ownerRoot, entry, statReceiver));
        }
        return true;
    }

    public void DeactivatePassive(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId) || !activePassives.TryGetValue(skillId, out SkillConfigEntry entry)) return;
        RemovePassiveEffects(entry);
        activePassives.Remove(skillId);
    }

    private void ApplyPermanentPassive(SkillConfigEntry entry)
    {
        if (statReceiver == null) statReceiver = FindStatReceiver();
        if (statReceiver == null)
        {
            Debug.LogWarning("[PlayerPassiveSkillController] Không tìm thấy IStatModifierReceiver để áp dụng passive buff.", this);
            return;
        }

        IList<SkillStatModifier> modifiers = entry.type == SkillType.PassiveBuff
            ? entry.buffConfig?.modifiers
            : entry.passiveConfig?.modifiers;
        if (modifiers == null) return;

        string sourceId = "PASSIVE:" + entry.skillId;
        statReceiver.RemoveModifiers(sourceId);
        foreach (SkillStatModifier modifier in modifiers)
        {
            if (modifier == null) continue;
            statReceiver.AddModifier(sourceId, modifier.stat, modifier.modifierType, modifier.value);
        }
    }

    private void HandleHitLanded(GameObject attacker, GameObject target, SkillContext context)
    {
        if (!IsOwner(attacker)) return;
        TriggerPassives(PassiveTriggerType.OnHit);
    }

    private void HandleKilled(GameObject killer, GameObject target)
    {
        if (!IsOwner(killer)) return;
        TriggerPassives(PassiveTriggerType.OnKill);
    }

    private void HandleDamageTaken(GameObject receiver, float amount)
    {
        if (!IsOwner(receiver)) return;
        TriggerPassives(PassiveTriggerType.OnDamageTaken);
    }

    private void TriggerPassives(PassiveTriggerType trigger)
    {
        foreach (SkillConfigEntry entry in activePassives.Values)
        {
            if (entry == null || entry.type != SkillType.Passive || entry.passiveConfig == null
                || entry.passiveConfig.trigger != trigger) continue;

            PassiveSkillConfig config = entry.passiveConfig;
            if (UnityEngine.Random.value > Mathf.Clamp01(config.chance)) continue;
            if (config.modifiers == null || config.modifiers.Count == 0) continue;

            if (buffController == null) buffController = ownerRoot.GetComponentInChildren<SelfBuff>(true);
            if (buffController != null)
                buffController.ApplyModifiers("PASSIVE_TRIGGER:" + entry.skillId, config.duration, config.modifiers);
            else
                Debug.LogWarning($"[PlayerPassiveSkillController] Passive '{entry.skillId}' cần SelfBuff để quản lý thời hạn.", this);
        }
    }

    private bool IsOwner(GameObject candidate)
    {
        if (candidate == null || ownerRoot == null) return false;
        return candidate == ownerRoot || candidate.transform.IsChildOf(ownerRoot.transform);
    }

    private IPassiveSkill FindCustomPassive(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId)) return null;
        MonoBehaviour[] components = ownerRoot.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour component in components)
        {
            if (component is IPassiveSkill passive
                && string.Equals(component.GetType().Name, actionId, StringComparison.OrdinalIgnoreCase))
                return passive;
        }
        return null;
    }

    private IStatModifierReceiver FindStatReceiver()
    {
        MonoBehaviour[] components = ownerRoot != null
            ? ownerRoot.GetComponentsInChildren<MonoBehaviour>(true)
            : GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour component in components)
            if (component is IStatModifierReceiver receiver) return receiver;

        return PlayerProgressionService.Instance != null ? PlayerProgressionService.Instance.StatStore : null;
    }

    private void RemovePassiveEffects(SkillConfigEntry entry)
    {
        if (entry == null) return;
        statReceiver?.RemoveModifiers("PASSIVE:" + entry.skillId);
        buffController?.RemoveBuff("PASSIVE_TRIGGER:" + entry.skillId);

        if (customPassiveInstances.TryGetValue(entry.skillId, out IPassiveSkill custom))
        {
            custom.OnRemove(new PassiveSkillRuntimeContext(ownerRoot, entry, statReceiver));
            customPassiveInstances.Remove(entry.skillId);
        }
    }

    public void ClearPassives()
    {
        foreach (SkillConfigEntry entry in new List<SkillConfigEntry>(activePassives.Values))
            RemovePassiveEffects(entry);
        activePassives.Clear();
        customPassiveInstances.Clear();
    }

    private void OnDisable()
    {
        SkillCombatEvents.HitLanded -= HandleHitLanded;
        SkillCombatEvents.Killed -= HandleKilled;
        SkillCombatEvents.DamageTaken -= HandleDamageTaken;
        ClearPassives(); // desiredPassiveIds được giữ lại để OnEnable bật lại.
        lastSeenStateVersion = -1;
    }
}

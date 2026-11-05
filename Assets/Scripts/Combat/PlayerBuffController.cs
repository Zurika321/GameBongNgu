using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý buff có thời hạn và gỡ modifier khi hết thời gian.
/// - Cùng một sourceId được áp lại: làm mới thời gian (không cộng dồn).
/// - Khác sourceId: cộng dồn theo công thức của RuntimeStatModifierStore.
/// </summary>
public class PlayerBuffController : MonoBehaviour
{
    private const string BuffPrefix = "BUFF:";

    private readonly Dictionary<string, Coroutine> runningTimers = new Dictionary<string, Coroutine>();
    private readonly Dictionary<string, float> endTimes = new Dictionary<string, float>();
    private readonly HashSet<string> activeSources = new HashSet<string>();
    private IStatModifierReceiver statReceiver;

    private void Awake()
    {
        statReceiver = FindStatReceiver();
        if (statReceiver == null)
            Debug.LogWarning("[PlayerBuffController] Chưa thấy IStatModifierReceiver. PlayerProgressionService sẽ tự thêm RuntimeStatModifierStore; nếu không, hãy gắn thủ công lên Player.", this);
    }

    /// <summary>Áp buff của một skill (BuffAction). sourceId nội bộ = "BUFF:" + skillId.</summary>
    public bool ApplyBuff(string skillId, SkillBuffConfig config)
    {
        if (config == null || config.modifiers == null || config.modifiers.Count == 0) return false;
        return ApplyModifiers(BuffPrefix + skillId, config.duration, config.modifiers);
    }

    public bool ApplyModifiers(string sourceId, float duration, IList<SkillStatModifier> modifiers)
    {
        if (statReceiver == null) statReceiver = FindStatReceiver();
        if (statReceiver == null || string.IsNullOrWhiteSpace(sourceId) || modifiers == null || modifiers.Count == 0)
            return false;

        RemoveBuff(sourceId);
        foreach (SkillStatModifier modifier in modifiers)
        {
            if (modifier == null) continue;
            statReceiver.AddModifier(sourceId, modifier.stat, modifier.modifierType, modifier.value);
        }

        activeSources.Add(sourceId);
        if (duration > 0f)
        {
            endTimes[sourceId] = Time.time + duration;
            runningTimers[sourceId] = StartCoroutine(RemoveAfter(sourceId, duration));
        }
        return true;
    }

    public void RemoveBuff(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return;
        if (runningTimers.TryGetValue(sourceId, out Coroutine timer) && timer != null)
            StopCoroutine(timer);
        runningTimers.Remove(sourceId);
        endTimes.Remove(sourceId);
        activeSources.Remove(sourceId);
        if (statReceiver == null) statReceiver = FindStatReceiver();
        statReceiver?.RemoveModifiers(sourceId);
    }

    /// <summary>Buff (theo sourceId nội bộ) đang có hiệu lực không.</summary>
    public bool IsBuffActive(string sourceId)
    {
        return !string.IsNullOrWhiteSpace(sourceId) && activeSources.Contains(sourceId);
    }

    /// <summary>Số giây còn lại của buff. 0 nếu không có hoặc buff vô hạn (duration = 0).</summary>
    public float GetRemainingTime(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || !endTimes.TryGetValue(sourceId, out float endTime)) return 0f;
        return Mathf.Max(0f, endTime - Time.time);
    }

    /// <summary>Tiện ích cho UI: buff của một BuffAction skill đang chạy không.</summary>
    public bool IsSkillBuffActive(string skillId) { return IsBuffActive(BuffPrefix + skillId); }

    /// <summary>Tiện ích cho UI: số giây còn lại của buff BuffAction.</summary>
    public float GetSkillBuffRemainingTime(string skillId) { return GetRemainingTime(BuffPrefix + skillId); }

    private IEnumerator RemoveAfter(string sourceId, float duration)
    {
        yield return new WaitForSeconds(duration);
        runningTimers.Remove(sourceId);
        endTimes.Remove(sourceId);
        activeSources.Remove(sourceId);
        statReceiver?.RemoveModifiers(sourceId);
    }

    private IStatModifierReceiver FindStatReceiver()
    {
        MonoBehaviour[] components = GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour component in components)
            if (component is IStatModifierReceiver receiver) return receiver;

        // Store có thể nằm ở nhánh khác của Player: hỏi PlayerProgressionService.
        return PlayerProgressionService.Instance != null ? PlayerProgressionService.Instance.StatStore : null;
    }

    private void OnDisable()
    {
        foreach (string source in activeSources)
            statReceiver?.RemoveModifiers(source);
        foreach (Coroutine timer in runningTimers.Values)
            if (timer != null) StopCoroutine(timer);
        runningTimers.Clear();
        endTimes.Clear();
        activeSources.Clear();
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded history of executed actions. ClearHistory at session boundaries if desired.</summary>
public sealed class ReplayAction : MonoBehaviour, ISkill
{
    private sealed class Entry { public GameObject caster; public string actionId; public SkillContext context; }
    private static readonly List<Entry> history = new List<Entry>();
    private static bool replaying;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ClearHistory() { history.Clear(); replaying = false; }
    public static void Record(string actionId, SkillContext context)
    {
        if (replaying || context?.Caster == null || actionId == nameof(ReplayAction)) return;
        history.RemoveAll(entry => entry.caster == null);
        history.Add(new Entry { caster = context.Caster, actionId = actionId, context = context.CopyFor(context.Caster) });
        if (history.Count > 128) history.RemoveAt(0);
    }
    public void Execute(SkillContext context)
    {
        if (context?.Caster == null || SkillActions.Instance == null || replaying) return;
        ReplayConfig config = context.Modules?.replay ?? new ReplayConfig();
        List<Entry> candidates = history.FindAll(entry => entry.caster != null && entry.caster != context.Caster
            && (context.Target == null || entry.caster == context.Target)
            && Vector2.Distance(entry.caster.transform.position, context.Position) <= config.range);
        if (candidates.Count == 0) { Debug.LogWarning("[ReplayAction] No recorded action in range.", this); return; }
        Entry selected = candidates[candidates.Count - 1];
        if (config.selection == ReplaySelection.Random) selected = candidates[Random.Range(0, candidates.Count)];
        else if (config.selection == ReplaySelection.Strongest)
            foreach (Entry item in candidates) if (item.context.Damage > selected.context.Damage) selected = item;
        SkillContext replay = selected.context.CopyFor(context.Caster);
        replay.Direction = context.Direction; replay.Target = context.Target; replay.CasterType = context.CasterType;
        replay.Damage = Mathf.Max(0f, selected.context.Damage * Mathf.Clamp01(config.damageFraction));
        replaying = true;
        try { SkillActions.Instance.Activate(selected.actionId, replay); }
        finally { replaying = false; }
    }
}

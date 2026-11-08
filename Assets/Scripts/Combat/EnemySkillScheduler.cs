using System.Collections.Generic;
using UnityEngine;

/// <summary>Selects configured skills against the ZoneSee target; no runtime detector objects.</summary>
public sealed class EnemySkillScheduler : MonoBehaviour
{
    [SerializeField] private EnemySkillController skills;
    [SerializeField] private EnemyController movement;
    private bool inCombat;
    private float combatElapsed;
    private int lastPhase;
    private readonly HashSet<int> phaseCasts = new HashSet<int>();
    private readonly List<int> order = new List<int>();
    private void Awake()
    {
        if (skills == null) skills = GetComponent<EnemySkillController>();
        if (movement == null) movement = GetComponent<EnemyController>();
    }
    private void Start()
    {
        if (skills == null || movement == null) { Debug.LogError("[EnemySkillScheduler] Gán Skills và Movement.", this); enabled = false; return; }
        for (int i = 0; i < skills.Slots.Length; i++) if (skills.Slots[i] != null) order.Add(i);
        order.Sort((a, b) => skills.Slots[b].priority.CompareTo(skills.Slots[a].priority));
    }
    private void Update()
    {
        if (GameplayPause.IsPaused || skills == null || movement == null || skills.IsDead) return;
        Transform target = movement.Target;
        IHealth targetHealth = target != null ? target.GetComponentInParent<IHealth>() : null;
        if (target == null || (targetHealth != null && targetHealth.IsDead))
        {
            if (inCombat) skills.EndCombat();
            inCombat = false; return;
        }
        if (!inCombat) { inCombat = true; combatElapsed = 0f; phaseCasts.Clear(); lastPhase = skills.Phase; skills.BeginCombat(); }
        combatElapsed += Time.deltaTime;
        if (lastPhase != skills.Phase) { phaseCasts.Clear(); lastPhase = skills.Phase; }
        if (skills.IsBusy || movement.IsActionLocked) return;
        foreach (int index in order)
        {
            var slot = skills.Slots[index];
            if (slot.trigger == EnemySkillTrigger.Death || combatElapsed < slot.initialDelay) continue;
            if (slot.trigger == EnemySkillTrigger.CombatStart && slot.used) continue;
            if (slot.trigger == EnemySkillTrigger.PhaseEnter && phaseCasts.Contains(index)) continue;
            Vector2 delta = (Vector2)(target.position - skills.Owner.transform.position) - slot.detectionOffset;
            if (delta.magnitude < slot.minimumDistance) continue;
            if (slot.shape == DetectionShape.Circle ? delta.sqrMagnitude > slot.detectionSize.x * slot.detectionSize.x : Mathf.Abs(delta.x) > slot.detectionSize.x * 0.5f || Mathf.Abs(delta.y) > slot.detectionSize.y * 0.5f) continue;
            if (!skills.TryCast(index, target.gameObject)) continue;
            if (slot.trigger == EnemySkillTrigger.PhaseEnter) phaseCasts.Add(index);
            break;
        }
    }
    private void OnDisable() { if (inCombat && skills != null) skills.EndCombat(); inCombat = false; }
}

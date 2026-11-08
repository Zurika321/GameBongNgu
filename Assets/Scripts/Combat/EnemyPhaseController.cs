using UnityEngine;
using UnityEngine.Events;

/// <summary>Monotonic HP transitions; unspecified story/HP gates remain manual in Inspector.</summary>
public sealed class EnemyPhaseController : MonoBehaviour
{
    [SerializeField] private EnemySkillController skills;
    [SerializeField] private HealthController health;
    [SerializeField] private UnityEvent<int> onPhaseChanged = new UnityEvent<int>();
    private void Awake()
    {
        if (skills == null) skills = GetComponent<EnemySkillController>();
        if (health == null) health = GetComponent<HealthController>();
    }
    private void Update()
    {
        if (GameplayPause.IsPaused || health == null || health.IsDead || skills?.CombatConfig?.phases == null) return;
        float fraction = health.CurrentHealth / Mathf.Max(1f, health.MaxHealth);
        foreach (var rule in skills.CombatConfig.phases)
        {
            if (rule == null || rule.manualOnly || rule.phase <= skills.Phase || fraction >= rule.healthBelow) continue;
            if (!string.IsNullOrWhiteSpace(rule.requiredProgressFlag) && !(PlayerProgressionService.Instance?.Data?.HasFlag(rule.requiredProgressFlag) ?? false)) continue;
            SetPhase(rule.phase);
        }
    }
    public void SetPhase(int phase)
    {
        if (skills == null || phase <= skills.Phase) return;
        skills.SetPhase(phase); onPhaseChanged.Invoke(phase);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Spawn only the configured prefab. No scene UI or placeholder objects are created.</summary>
public sealed class SummonEntity : MonoBehaviour, ISkill
{
    [SerializeField] private UnityEvent<GameObject> onSummoned = new UnityEvent<GameObject>();
    private readonly Dictionary<GameObject, List<GameObject>> alive = new Dictionary<GameObject, List<GameObject>>();
    public void Execute(SkillContext context)
    {
        SummonConfig config = context?.Modules?.summon;
        if (context?.Caster == null || config?.prefab == null) { Debug.LogWarning("[SummonEntity] Configure Modules/Summon/Prefab.", this); return; }
        GameObject ownerId = context.Caster;
        if (!alive.TryGetValue(ownerId, out List<GameObject> list)) alive[ownerId] = list = new List<GameObject>();
        list.RemoveAll(item => item == null || (item.GetComponent<IHealth>()?.IsDead ?? false));
        int count = Mathf.Min(Mathf.Max(0, config.count), Mathf.Max(0, config.maximumAlive - list.Count));
        IHealth ownerHealth = context.Caster.GetComponent<IHealth>();
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / Mathf.Max(1, count);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * config.radius;
            GameObject entity = Instantiate(config.prefab, context.Position + offset, Quaternion.identity);
            HealthController health = entity.GetComponentInChildren<HealthController>();
            if (health != null && ownerHealth != null && config.ownerHealthFraction > 0f)
            {
                float hp = Mathf.Max(1f, ownerHealth.MaxHealth * config.ownerHealthFraction);
                health.ConfigureHealth(hp, hp);
                entity.GetComponent<EnemySkillController>()?.NotifySpawnHealthConfigured();
            }
            // Combat spawns inherit a real target and their owner's configured ATK; clones have no summon slots.
            entity.GetComponent<EnemyController>()?.SetTarget(context.Target != null ? context.Target.transform : null);
            var summonedSkills = entity.GetComponent<EnemySkillController>();
            if (summonedSkills?.CombatConfig != null && summonedSkills.CombatConfig.needsAttack)
                summonedSkills.InheritAttack(context.Damage);
            list.Add(entity);
            if (config.lifetime > 0f) Destroy(entity, config.lifetime);
            onSummoned.Invoke(entity);
        }
    }
}

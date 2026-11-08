using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime contact status area. This is a skill effect, never a scene/UI construction fallback.</summary>
public sealed class SkillActionEffectArea : MonoBehaviour
{
    private SkillContext context;
    private bool follow;
    private float interval;
    private Vector2 offset;
    private readonly Dictionary<GameObject, float> next = new Dictionary<GameObject, float>();
    public void Init(SkillContext context, bool follow, float interval)
    {
        this.context = context; this.follow = follow; this.interval = Mathf.Max(0.01f, interval);
        offset = (Vector2)transform.position - (Vector2)context.Caster.transform.position;
    }
    private void Update()
    {
        if (context?.Caster == null) { Destroy(gameObject); return; }
        if (follow) transform.position = (Vector2)context.Caster.transform.position + offset;
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (GameplayPause.IsPaused || context?.Caster == null || !other.CompareTag(context.ResolveTargetTag())) return;
        HealthController health = other.GetComponentInParent<HealthController>();
        GameObject target = health != null ? health.gameObject : other.gameObject;
        if (target == context.Caster || (next.TryGetValue(target, out float at) && Time.time < at)) return;
        next[target] = Time.time + interval;
        ApplyStatus.Apply(target, context.Modules.status);
        if (context.DotConfig != null) DotOnHit.ApplyOrRefresh(target, context.Caster, context.DotConfig);
    }
}

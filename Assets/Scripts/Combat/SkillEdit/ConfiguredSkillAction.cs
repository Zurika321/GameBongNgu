using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Runs immutable catalog recipes with per-caster state. Never stores gameplay numbers in a switch.</summary>
public sealed class ConfiguredSkillAction : ISkill
{
    private readonly SkillActionDefinition definition;
    private readonly SkillActions host;
    private readonly Dictionary<GameObject, RunState> active = new Dictionary<GameObject, RunState>();
    private readonly Dictionary<GameObject, int> consecutiveHits = new Dictionary<GameObject, int>();
    private sealed class Pending { public int step, iteration; public float at; public SkillContext context; }
    private sealed class RunState
    {
        public SkillContext context;
        public bool cancelled, anyHit;
        public Action<GameObject, float> damageHandler;
        public float elapsed, waitUntil;
        public int pendingChallenges;
        public readonly List<ChallengeMeter> challenges = new List<ChallengeMeter>();
        public readonly List<Pending> queue = new List<Pending>();
        public readonly List<GameObject> objects = new List<GameObject>();
        public readonly HashSet<string> hitIterations = new HashSet<string>();
        public readonly List<(SelfBuff receiver, string source)> temporaryBuffs = new List<(SelfBuff, string)>();
    }
    public ConfiguredSkillAction(SkillActionDefinition definition, SkillActions host) { this.definition = definition; this.host = host; }
    public bool IsRunning(GameObject caster) => caster != null && active.TryGetValue(caster, out RunState state) && !state.cancelled;
    public void Cancel(GameObject caster) { if (caster != null && active.TryGetValue(caster, out RunState state)) state.cancelled = true; }
    public void Shutdown()
    {
        foreach (RunState state in new List<RunState>(active.Values))
        {
            state.cancelled = true;
            if (state.damageHandler != null) SkillCombatEvents.DamageTaken -= state.damageHandler;
            foreach (var obj in state.objects) if (obj != null) UnityEngine.Object.Destroy(obj);
            foreach (var challenge in state.challenges) if (challenge != null && challenge.IsOwnedBy(state)) challenge.Cancel();
            foreach (var buff in state.temporaryBuffs) if (buff.receiver != null) buff.receiver.RemoveBuff(buff.source);
            if (definition.lockMovement) Lock(state.context.Caster, false);
        }
        active.Clear();
    }
    public void Execute(SkillContext context)
    {
        if (context?.Caster == null || !definition.enabled) return;
        foreach (var stale in new List<GameObject>(consecutiveHits.Keys)) if (stale == null) consecutiveHits.Remove(stale);
        if (definition.requiresHold && context.IsHeld == null) { Debug.LogWarning("[SkillActions] " + definition.actionId + " requires a held-input predicate.", context.Caster); return; }
        if (active.TryGetValue(context.Caster, out RunState old)) old.cancelled = true;
        var state = new RunState { context = context.CopyFor(context.Caster) };
        state.context.IsHeld = context.IsHeld;
        active[context.Caster] = state;
        host.StartCoroutine(Run(state));
    }
    private IEnumerator Run(RunState state)
    {
        GameObject caster = state.context.Caster;
        IHealth health = caster.GetComponent<IHealth>();
        float initialHp = health != null ? health.CurrentHealth : 0f;
        Vector3 initialPosition = caster.transform.position;
        if (definition.condition != null && definition.condition.kind != SkillConditionKind.Always && !ConditionGate.Evaluate(state.context, definition.condition))
        { active.Remove(caster); yield break; }
        bool playerWasLocked = caster.GetComponent<PlayerController>()?.IsActionLocked ?? false;
        bool enemyWasLocked = caster.GetComponent<EnemyController>()?.IsActionLocked ?? false;
        Action<GameObject, float> onDamaged = (target, damage) => { if (definition.cancelOnDamage && target == caster && damage > 0f) state.cancelled = true; };
        state.damageHandler = onDamaged;
        SkillCombatEvents.DamageTaken += onDamaged;
        if (definition.lockMovement) Lock(caster, true);
        try
        {
            for (int i = 0; i < definition.steps.Count; i++)
                if (definition.steps[i].trigger == ActionTrigger.Timeline) Enqueue(state, i, 0, definition.steps[i].delay, state.context);
            while (!state.cancelled && caster != null && caster.activeInHierarchy)
            {
                if (!definition.allowDeadCaster && health != null && health.IsDead) break;
                if (definition.requiresHold && !state.context.IsHeld()) break;
                if (definition.cancelOnMove && Vector3.Distance(caster.transform.position, initialPosition) > definition.moveTolerance) break;
                if (definition.cancelOnDamage && health != null && health.CurrentHealth < initialHp) break;
                if (definition.maintainCondition && !ConditionGate.Evaluate(state.context, definition.condition)) break;
                if (!GameplayPause.IsPaused)
                {
                    // Process a snapshot: callbacks may enqueue further work in the same frame.
                    var due = state.queue.FindAll(item => item.at <= state.elapsed);
                    foreach (var item in due)
                    {
                        state.queue.Remove(item);
                        SkillActionStep step = definition.steps[item.step];
                        if (step.requirePreviousHit && item.iteration > 0 && !state.hitIterations.Contains(item.step + ":" + (item.iteration - 1))) { state.cancelled = true; break; }
                        ExecuteStep(state, item);
                        if (state.cancelled) break;
                        if (step.repeatCount == 0 || item.iteration + 1 < step.repeatCount)
                            Enqueue(state, item.step, item.iteration + 1, item.at + step.interval, item.context);
                    }
                    state.elapsed += Time.deltaTime;
                }
                state.objects.RemoveAll(obj => obj == null);
                if (state.queue.Count == 0 && state.objects.Count == 0 && state.pendingChallenges == 0 && state.elapsed > state.waitUntil && !definition.maintainCondition) break;
                yield return null;
            }
        }
        finally
        {
            state.cancelled = true;
            SkillCombatEvents.DamageTaken -= onDamaged;
            foreach (var obj in state.objects) if (obj != null) UnityEngine.Object.Destroy(obj);
            foreach (var challenge in state.challenges) if (challenge != null && challenge.IsOwnedBy(state)) challenge.Cancel();
            foreach (var buff in state.temporaryBuffs) if (buff.receiver != null) buff.receiver.RemoveBuff(buff.source);
            if (caster != null)
            {
                consecutiveHits[caster] = state.anyHit ? (consecutiveHits.TryGetValue(caster, out int count) ? count : 0) + 1 : 0;
                if (definition.lockMovement && !playerWasLocked && !enemyWasLocked) Lock(caster, false);
                if (active.TryGetValue(caster, out var current) && current == state) active.Remove(caster);
            }
        }
    }
    private static void Lock(GameObject caster, bool locked)
    {
        if (caster == null) return;
        caster.GetComponent<PlayerController>()?.LockMovement(locked);
        caster.GetComponent<EnemyController>()?.LockMovement(locked);
    }
    private static void Enqueue(RunState state, int step, int iteration, float at, SkillContext context)
    { state.queue.Add(new Pending { step = step, iteration = iteration, at = at, context = context }); }
    private void Trigger(RunState state, ActionTrigger trigger, int source, int iteration, SkillContext context)
    {
        if (state.cancelled) return;
        for (int i = 0; i < definition.steps.Count; i++)
        {
            SkillActionStep step = definition.steps[i];
            if (step.trigger == trigger && step.sourceStep == source && (step.sourceIteration < 0 || step.sourceIteration == iteration))
                Enqueue(state, i, 0, state.elapsed + step.delay, context);
        }
    }
    private void ExecuteStep(RunState state, Pending pending)
    {
        SkillActionStep step = definition.steps[pending.step];
        if (step.chance <= 0f || (step.chance < 1f && UnityEngine.Random.value >= step.chance)) return;
        int count = consecutiveHits.TryGetValue(state.context.Caster, out int previous) ? previous : 0;
        if (step.minimumConsecutiveHits > count + 1) return;
        SkillContext context = pending.context.CopyFor(state.context.Caster);
        context.DamageDealt = pending.context.DamageDealt;
        context.Target = step.target == ActionTarget.Caster ? context.Caster
            : step.target == ActionTarget.Opponent ? state.context.Target : pending.context.Target;
        context.Modules = step.useSkillModuleSettings && state.context.Modules != null ? state.context.Modules : step.settings;
        context.Damage = SkillActionMath.Damage(state.context.Damage, step, pending.iteration);
        var damageOverride = state.context.ActionDamageOverrides?.Find(item => item != null && item.stepIndex == pending.step);
        if (step.fixedDamage < 0f && damageOverride?.damagePercents != null && damageOverride.damagePercents.Length > 0)
            context.Damage = Mathf.Max(0f, state.context.Damage * damageOverride.damagePercents[Mathf.Clamp(pending.iteration, 0, damageOverride.damagePercents.Length - 1)] / 100f);
        context.DamageKind = step.damageKind;
        context.ArmorPenetration = step.armorPenetration;
        context.BreaksGuard = step.breaksGuard || state.context.BreaksGuard;
        context.BypassesMeleeResistance = step.bypassesMeleeResistance || state.context.BypassesMeleeResistance;
        context.Position = step.trigger == ActionTrigger.ProjectileEnd ? pending.context.Position : (Vector2)context.Caster.transform.position;
        context.Duration = step.lifetime;
        context.Size = step.size * (1f + step.sizeGrowthPerRepeat * pending.iteration);
        context.Range = step.forwardOffset;
        context.Speed = step.speed;
        context.CanPierce = step.canPierce;
        context.DotConfig = null;
        context.StunTime = 0f;
        switch (step.module)
        {
            case ActionModule.DamageZone:
            case ActionModule.Projectile:
                SpawnHitbox(state, pending, context, step); break;
            case ActionModule.DotOnHit:
                DameDotConfig dot = JsonUtility.FromJson<DameDotConfig>(JsonUtility.ToJson(step.dot));
                dot.activationType = DotActivationType.Immediate;
                dot.damagePerTick = context.Damage;
                if (context.Target != null) DotOnHit.ApplyOrRefresh(context.Target, context.Caster, dot);
                break;
            case ActionModule.EffectArea:
                GameObject area = ZoneHitboxSpawner.Spawn(context.Caster, context.Position + context.Direction.normalized * step.forwardOffset,
                    step.shape, context.Size, step.lifetime, context.ResolveTargetTag(), context.Debug, null,
                    Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg, true, step.coneAngle);
                area.AddComponent<SkillActionEffectArea>().Init(context, step.followCaster, step.interval);
                state.objects.Add(area); SkillEffectPlayer.Attach(area, context); break;
            case ActionModule.Status:
                if (step.settings.status.kind == SkillStatusKind.Curse && step.useSkillModuleSettings && context.Modules.status.kind != SkillStatusKind.Curse)
                { Debug.LogWarning("[SkillActions] " + definition.actionId + " needs an authored Curse status in Modules/Status.", context.Caster); break; }
                ApplyStatus.Apply(context.Target, context.Modules.status); break;
            case ActionModule.Buff:
                SelfBuff receiver = context.Caster.GetComponentInChildren<SelfBuff>(true);
                if (receiver == null) { Debug.LogWarning("[SkillActions] " + definition.actionId + " needs SelfBuff on its caster.", context.Caster); break; }
                string source = definition.actionId + ":" + pending.step + ":" + pending.iteration;
                receiver.ApplyModifiers(source, context.Modules.selfBuff.duration, context.Modules.selfBuff.modifiers);
                if (step.clearBuffWhenCancelled) state.temporaryBuffs.Add((receiver, source));
                break;
            case ActionModule.Summon:
                context.Damage = state.context.Damage; // summon inherits actual owner ATK, not the empty damage step.
                if (context.Modules.summon.prefab == null && state.context.Modules?.summon?.prefab != null)
                {
                    context.Modules = JsonUtility.FromJson<SkillModuleConfig>(JsonUtility.ToJson(context.Modules));
                    context.Modules.summon.prefab = state.context.Modules.summon.prefab;
                }
                host.GetModule<SummonEntity>().Execute(context); break;
            case ActionModule.Move: CasterMove.Perform(context); break;
            case ActionModule.Resource: ModifyResource.Apply(context, context.Modules.resource, context.Target); break;
            case ActionModule.Defense:
                context.OnDefenseCallback = attacker =>
                {
                    var hit = state.context.CopyFor(state.context.Caster); hit.Target = attacker;
                    if (attacker != null) hit.Direction = ((Vector2)(attacker.transform.position - context.Caster.transform.position)).normalized;
                    Trigger(state, ActionTrigger.Defended, pending.step, pending.iteration, hit);
                };
                host.GetModule<DefenseWindow>().Execute(context);
                state.waitUntil = Mathf.Max(state.waitUntil, state.elapsed + context.Modules.defense.duration);
                break;
            case ActionModule.Screen:
                if (context.Target != null && context.Target.GetComponentInChildren<ScreenEffect>(true) != null) host.GetModule<ScreenEffect>().Execute(context);
                else Debug.LogWarning("[SkillActions] " + definition.actionId + " needs a ScreenEffect scene receiver on its target.", context.Caster);
                break;
            case ActionModule.Challenge:
                ChallengeMeter meter = context.Target != null ? context.Target.GetComponentInChildren<ChallengeMeter>(true) : null;
                if (meter == null) { Debug.LogWarning("[SkillActions] " + definition.actionId + " needs a ChallengeMeter scene receiver on its target.", context.Caster); break; }
                if (context.Modules.challenge == null || context.Modules.challenge.mode != step.settings.challenge.mode)
                { Debug.LogWarning("[SkillActions] " + definition.actionId + " needs the recipe challenge mode configured in Modules/Challenge.", context.Caster); break; }
                state.pendingChallenges++;
                context.OnChallengeCompleted = success =>
                {
                    state.pendingChallenges = Mathf.Max(0, state.pendingChallenges - 1);
                    if (!success) Trigger(state, ActionTrigger.ChallengeFailed, pending.step, pending.iteration, context);
                };
                state.challenges.Add(meter);
                meter.Play(context, state);
                state.waitUntil = Mathf.Max(state.waitUntil, state.elapsed + context.Modules.challenge.duration + Time.deltaTime);
                break;
            case ActionModule.Replay: host.GetModule<ReplayAction>().Execute(context); break;
            case ActionModule.Condition:
                if (step.useSkillModuleSettings && step.settings.condition.kind == SkillConditionKind.HitCount && context.Modules.condition.kind == SkillConditionKind.Always)
                { Debug.LogWarning("[SkillActions] " + definition.actionId + " needs Modules/Condition hit count settings.", context.Caster); state.cancelled = true; break; }
                if (!ConditionGate.Evaluate(context, context.Modules.condition)) state.cancelled = true;
                break;
        }
    }
    private void SpawnHitbox(RunState state, Pending pending, SkillContext context, SkillActionStep step)
    {
        Vector2 direction = context.Direction.sqrMagnitude > 0f ? context.Direction.normalized : Vector2.right;
        Vector2 position = context.Position + direction * step.forwardOffset;
        var victims = new HashSet<GameObject>();
        Action<GameObject, Vector2> onHit = (target, hitPosition) =>
        {
            if (state.cancelled || step.telegraph || context.Caster == null) return;
            HealthController health = target.GetComponentInParent<HealthController>();
            target = health != null ? health.gameObject : target;
            if (!victims.Add(target)) return;
            var hit = context.CopyFor(context.Caster); hit.Target = target;
            hit.DamageDealt = SkillActionDamage.Deal(hit, target);
            if (hit.DamageDealt <= 0f) return;
            state.anyHit = true;
            state.hitIterations.Add(pending.step + ":" + pending.iteration);
            if (step.knockback > 0f) target.GetComponentInParent<Rigidbody2D>()?.AddForce(direction * step.knockback, ForceMode2D.Impulse);
            Trigger(state, ActionTrigger.Hit, pending.step, pending.iteration, hit);
        };
        GameObject hitbox;
        if (step.module == ActionModule.Projectile)
        {
            float lifetime = step.travelDistance > 0f && step.speed > 0f ? step.travelDistance / step.speed : step.lifetime;
            hitbox = ProjectileHitboxSpawner.Spawn(context.Caster, position, direction, step.speed, lifetime, step.shape, context.Size,
                context.ResolveTargetTag(), step.canPierce, context.Debug, onHit, finalPosition =>
                {
                    var end = context.CopyFor(context.Caster); end.Position = finalPosition;
                    Trigger(state, ActionTrigger.ProjectileEnd, pending.step, pending.iteration, end);
                });
        }
        else hitbox = ZoneHitboxSpawner.Spawn(context.Caster, position, step.shape, context.Size, Mathf.Max(Time.fixedDeltaTime, step.lifetime),
            context.ResolveTargetTag(), context.Debug, onHit, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, true, step.coneAngle);
        state.objects.Add(hitbox);
        if (!step.telegraph) SkillEffectPlayer.Attach(hitbox, context);
    }
}

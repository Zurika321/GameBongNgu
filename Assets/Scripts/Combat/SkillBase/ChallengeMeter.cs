using UnityEngine;
using UnityEngine.Events;

/// <summary>QTE logic without creating UI. Connect AddInput to an existing button/input action.</summary>
public sealed class ChallengeMeter : MonoBehaviour, ISkill
{
    [SerializeField] private UnityEvent<float> onProgress = new UnityEvent<float>();
    [SerializeField] private UnityEvent onSucceeded = new UnityEvent();
    [SerializeField] private UnityEvent onFailed = new UnityEvent();
    private ChallengeConfig config;
    private System.Action<bool> completed;
    private object owner;
    public bool IsOwnedBy(object token) => ReferenceEquals(owner, token);
    private float value, elapsed, belowTime;
    public bool IsRunning { get; private set; }
    public float Value => value;
    public void Execute(SkillContext context) => Play(context);
    public void Play(SkillContext context, object ownerToken = null)
    {
        Begin(context?.Modules?.challenge);
        owner = ownerToken;
        completed = IsRunning ? context?.OnChallengeCompleted : null;
        if (!IsRunning && context?.Modules?.challenge != null) context.OnChallengeCompleted?.Invoke(true);
    }
    public void Begin(ChallengeConfig settings)
    {
        Cancel(); config = settings;
        if (config == null) return;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (!string.IsNullOrWhiteSpace(config.immunityFlag) && progress?.Data != null && progress.Data.HasFlag(config.immunityFlag))
        {
            if (config.consumeImmunity) progress.RemoveFlag(config.immunityFlag);
            onSucceeded.Invoke(); return;
        }
        value = Mathf.Clamp01(config.initialValue); elapsed = belowTime = 0f; IsRunning = true; onProgress.Invoke(value);
    }
    public void AddInput() => AddProgress(config != null ? config.gainPerInput : 0f);
    public void AddProgress(float amount)
    {
        if (!IsRunning || GameplayPause.IsPaused) return;
        value = Mathf.Clamp01(value + amount); onProgress.Invoke(value);
        if (config.mode == ChallengeMode.RepeatedInput && value >= config.requiredValue) Finish(true);
    }
    private void Update()
    {
        if (!IsRunning || GameplayPause.IsPaused) return;
        elapsed += Time.deltaTime; value = Mathf.Clamp01(value - config.drainPerSecond * Time.deltaTime); onProgress.Invoke(value);
        if (config.mode == ChallengeMode.MaintainMeter)
        {
            belowTime = value < config.requiredValue ? belowTime + Time.deltaTime : 0f;
            if (belowTime >= config.failureGraceSeconds && value < config.requiredValue) { Finish(false); return; }
        }
        if (config.duration > 0f && elapsed >= config.duration) Finish(config.mode == ChallengeMode.MaintainMeter && value >= config.requiredValue);
    }
    private void Finish(bool success) { if (!IsRunning) return; IsRunning = false; var callback = completed; completed = null; callback?.Invoke(success); if (success) onSucceeded.Invoke(); else onFailed.Invoke(); }
    public void Cancel() { var callback = completed; IsRunning = false; completed = null; owner = null; callback?.Invoke(true); }
    private void OnDisable() => Cancel();
}

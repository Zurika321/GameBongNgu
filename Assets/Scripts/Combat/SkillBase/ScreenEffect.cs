using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>References existing scene UI only. Shader/light adapters can subscribe to onStrengthChanged.</summary>
public sealed class ScreenEffect : MonoBehaviour, ISkill
{
    [SerializeField] private CanvasGroup overlay;
    [SerializeField] private UnityEvent<float> onStrengthChanged = new UnityEvent<float>();
    private Coroutine running;
    private float originalAlpha;
    private bool captured;
    public void Execute(SkillContext context)
    {
        ScreenEffect receiver = context?.Target != null ? context.Target.GetComponentInChildren<ScreenEffect>(true) : this;
        if (receiver == null) { Debug.LogWarning("[ScreenEffect] Configure a scene receiver on the target.", this); return; }
        receiver.Begin(context?.Modules?.screen);
    }
    public void Begin(ScreenEffectConfig config)
    {
        if (config == null) return;
        if (!captured) { originalAlpha = overlay != null ? overlay.alpha : 0f; captured = true; }
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Run(config));
    }
    private IEnumerator Run(ScreenEffectConfig config)
    {
        yield return Fade(overlay != null ? overlay.alpha : 0f, config.strength, config.fadeSeconds);
        yield return new WaitForSeconds(Mathf.Max(0f, config.duration));
        yield return Fade(config.strength, originalAlpha, config.fadeSeconds);
        running = null;
    }
    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            SetStrength(Mathf.Lerp(from, to, elapsed / duration));
            if (!GameplayPause.IsPaused) elapsed += Time.deltaTime;
            yield return null;
        }
        SetStrength(to);
    }
    private void SetStrength(float value) { if (overlay != null) overlay.alpha = value; onStrengthChanged.Invoke(value); }
    private void OnDisable()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        if (captured) SetStrength(originalAlpha);
        captured = false;
    }
}

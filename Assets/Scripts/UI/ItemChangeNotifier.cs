using System.Collections;
using TMPro;
using UnityEngine;

public sealed class ItemChangeNotifier : MonoBehaviour
{
    private const float DefaultHoldDuration = 1.25f;
    private const float DefaultFadeDuration = 0.8f;

    private TMP_Text label;
    private Coroutine activeRoutine;
    private Color baseColor;

    public static void Show(TMP_Text target, string itemName, int amount, bool added)
    {
        if (target == null || string.IsNullOrWhiteSpace(itemName) || amount <= 0) return;

        ItemChangeNotifier notifier = target.GetComponent<ItemChangeNotifier>();
        if (notifier == null) notifier = target.gameObject.AddComponent<ItemChangeNotifier>();
        notifier.ShowMessage(itemName, amount, added);
    }

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
        if (label == null)
        {
            Debug.LogError("[ItemChangeNotifier] This component needs a TMP_Text on the same GameObject.", this);
            enabled = false;
            return;
        }

        baseColor = label.color;
        if (baseColor.a <= 0f) baseColor.a = 1f;
    }

    private void ShowMessage(string itemName, int amount, bool added)
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label == null) return;

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        label.text = (added ? "+" : "-") + amount + " " + itemName;
        SetAlpha(1f);
        activeRoutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        yield return new WaitForSecondsRealtime(DefaultHoldDuration);

        float elapsed = 0f;
        while (elapsed < DefaultFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / DefaultFadeDuration));
            yield return null;
        }

        SetAlpha(0f);
        label.text = string.Empty;
        activeRoutine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (label == null) return;
        Color color = baseColor;
        color.a *= alpha;
        label.color = color;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this to each UI object whose displayed text should change with the selected language.
/// Supports legacy UI Text and TextMeshPro (including TextMeshProUGUI).
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalizedText : MonoBehaviour
{
    [SerializeField] private string translationKey;
    [SerializeField] private bool warnIfNoTextComponent = true;

    private TMP_Text tmpText;
    private Text legacyText;

    private void Awake() => CacheTextComponent();

    private void OnEnable()
    {
        CacheTextComponent();
        transition.OnLanguageChanged += HandleLanguageChanged;
        RefreshText();
    }

    private void OnDisable() => transition.OnLanguageChanged -= HandleLanguageChanged;

    private void OnValidate()
    {
        if (!Application.isPlaying)
            CacheTextComponent();
    }

    private void Reset() => CacheTextComponent();

    private void CacheTextComponent()
    {
        if (tmpText == null) tmpText = GetComponent<TMP_Text>();
        if (legacyText == null) legacyText = GetComponent<Text>();
    }

    private void HandleLanguageChanged(string _) => RefreshText();

    public void RefreshText()
    {
        if (string.IsNullOrWhiteSpace(translationKey)) return;

        string value = transition.dich(translationKey, transition.CurrentLanguage);
        if (tmpText != null) tmpText.text = value;
        else if (legacyText != null) legacyText.text = value;
        else if (warnIfNoTextComponent)
            Debug.LogWarning($"[LocalizedText] '{name}' chưa có TMP_Text hoặc UI Text.", this);
    }

    /// <summary>Set a new key at runtime and refresh this UI element.</summary>
    public void SetKey(string newKey)
    {
        translationKey = newKey;
        RefreshText();
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Changes the selected language and colors this button according to the active language.
/// Assign a language code and button in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class LanguageButton : MonoBehaviour
{
    [SerializeField] private string languageCode = "vn";
    [SerializeField] private Button button;
    [SerializeField] private Color activeColor = new Color(0.15f, 0.54f, 0.91f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.47f, 0.47f, 0.47f, 1f);

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        transition.OnLanguageChanged += HandleLanguageChanged;
        RefreshAppearance();
    }

    private void OnDisable()
    {
        transition.OnLanguageChanged -= HandleLanguageChanged;
    }

    public void ChuyenNgonNgu() => transition.DoiNgonNgu(languageCode);
    public void ChuyenNgonNgu(string code) => transition.DoiNgonNgu(code);

    private void HandleLanguageChanged(string _) => RefreshAppearance();

    private void RefreshAppearance()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (button == null)
            return;

        bool isCurrentLanguage = !string.IsNullOrWhiteSpace(languageCode)
            && string.Equals(transition.CurrentLanguage, languageCode.Trim(), StringComparison.OrdinalIgnoreCase);
        Color normalColor = isCurrentLanguage ? activeColor : inactiveColor;

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.15f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Singleton localization manager. Put one instance in the first scene.
/// Persists across scene loads. LocalizedText components subscribe to OnLanguageChanged
/// and refresh themselves whenever the language changes.
/// </summary>
public sealed class transition : MonoBehaviour
{
    private const string LanguagePrefsKey = "Game.Language";
    private static transition instance;

    private static readonly Dictionary<string, Dictionary<string, string>> LanguageCache =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MissingLanguageWarnings =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MissingKeyWarnings =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [SerializeField] private string defaultLanguage = "vn";
    [SerializeField] private bool logLanguageChanges;

    private string currentLanguage = "vn";

    /// <summary>Raised after a language switch; active localized UI updates immediately.</summary>
    public static event Action<string> OnLanguageChanged;

    public static string CurrentLanguage => instance != null ? instance.currentLanguage : "vn";
    public static bool IsReady => instance != null;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // DontDestroyOnLoad only works for a root GameObject.
        if (transform.parent != null)
            transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        string savedLanguage = PlayerPrefs.GetString(LanguagePrefsKey, defaultLanguage);
        currentLanguage = NormalizeLanguageCode(savedLanguage);
        if (string.IsNullOrEmpty(currentLanguage))
            currentLanguage = NormalizeLanguageCode(defaultLanguage);
        if (string.IsNullOrEmpty(currentLanguage))
            currentLanguage = "vn";

        // Load now so a missing language is reported early. UI components also refresh in OnEnable.
        LoadLanguage(currentLanguage);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            OnLanguageChanged = null;
        }
    }

    /// <summary>Translate a key in a specific language, e.g. transition.dich("hi", "vn").</summary>
    public static string dich(string key, string language)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        string code = NormalizeLanguageCode(language);
        if (string.IsNullOrEmpty(code))
            return key;

        Dictionary<string, string> translations = LoadLanguage(code);
        if (translations.TryGetValue(key, out string translated))
            return translated;

        string warningId = code + "|" + key;
        if (MissingKeyWarnings.Add(warningId))
            Debug.LogWarning($"[transition] Thiếu key '{key}' trong ngón ngữ '{code}'. Tạm hiển thị key gốc.");
        return key;
    }

    /// <summary>Switch language globally. Every active LocalizedText in loaded scenes updates immediately.</summary>
    public static void DoiNgonNgu(string language)
    {
        if (instance == null)
        {
            Debug.LogError("[transition] Chưa có GameObject chứa component transition trong scene đầu tiên.");
            return;
        }

        string code = NormalizeLanguageCode(language);
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[transition] Mã ngôn ngữ không hợp lệ.");
            return;
        }

        instance.currentLanguage = code;
        PlayerPrefs.SetString(LanguagePrefsKey, code);
        LanguageCache.Remove(code);
        LoadLanguage(code);
        OnLanguageChanged?.Invoke(code);

        if (instance.logLanguageChanges)
            Debug.Log($"[transition] ÄÃ£ chuyá»ƒn ngÃ´n ngá»¯ sang '{code}'.");
    }

    // English alias for scripts that use English naming.
    public static void SetLanguage(string language) => DoiNgonNgu(language);

    private static string NormalizeLanguageCode(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        string code = language.Trim().ToLowerInvariant();
        // Allow language codes such as vn, en, pt-br, zh-hans; reject path characters.
        foreach (char c in code)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                return null;
        }
        return code;
    }

    private static Dictionary<string, string> LoadLanguage(string languageCode)
    {
        if (LanguageCache.TryGetValue(languageCode, out Dictionary<string, string> cached))
            return cached;

        string json = null;

        if (string.IsNullOrEmpty(json))
        {
            TextAsset asset = Resources.Load<TextAsset>("transitions/" + languageCode);
            if (asset != null)
                json = asset.text;
        }

        if (string.IsNullOrEmpty(json))
        {
            if (MissingLanguageWarnings.Add(languageCode))
                Debug.LogWarning($"[transition] KhÃ´ng tÃ¬m tháº¥y {languageCode}.json. HÃ£y Ä‘áº·t file á»Ÿ Assets/Resources/transitions/.");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            Dictionary<string, string> parsed = ParseFlatJson(json);
            LanguageCache[languageCode] = parsed;
            return parsed;
        }
        catch (FormatException e)
        {
            Debug.LogError($"[transition] JSON '{languageCode}.json' khÃ´ng há»£p lá»‡: {e.Message}");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    // Parses a flat JSON object of string-to-string pairs without requiring an external JSON library.
    private static Dictionary<string, string> ParseFlatJson(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        SkipWhitespace(json, ref i);
        if (i < json.Length && json[i] == '\uFEFF') i++;
        SkipWhitespace(json, ref i);
        Expect(json, ref i, '{');
        SkipWhitespace(json, ref i);
        if (TryConsume(json, ref i, '}')) return result;

        while (i < json.Length)
        {
            SkipWhitespace(json, ref i);
            string key = ReadJsonString(json, ref i);
            SkipWhitespace(json, ref i);
            Expect(json, ref i, ':');
            SkipWhitespace(json, ref i);
            string value = ReadJsonString(json, ref i);
            result[key] = value;
            SkipWhitespace(json, ref i);
            if (TryConsume(json, ref i, '}')) break;
            Expect(json, ref i, ',');
        }

        SkipWhitespace(json, ref i);
        if (i != json.Length) throw new FormatException($"CÃ³ dá»¯ liá»‡u thá»«a sau dáº¥u '}}' táº¡i vá»‹ trÃ­ {i}.");
        return result;
    }

    private static string ReadJsonString(string json, ref int i)
    {
        Expect(json, ref i, '"');
        var b = new StringBuilder();
        while (i < json.Length)
        {
            char c = json[i++];
            if (c == '"') return b.ToString();
            if (c < 0x20) throw new FormatException("Chuá»—i cÃ³ kÃ½ tá»± Ä‘iá»u khiá»ƒn khÃ´ng há»£p lá»‡.");
            if (c != '\\') { b.Append(c); continue; }
            if (i >= json.Length) throw new FormatException("Thiáº¿u kÃ½ tá»± sau dáº¥u escape.");

            char e = json[i++];
            switch (e)
            {
                case '"': b.Append('"'); break;
                case '\\': b.Append('\\'); break;
                case '/': b.Append('/'); break;
                case 'b': b.Append('\b'); break;
                case 'f': b.Append('\f'); break;
                case 'n': b.Append('\n'); break;
                case 'r': b.Append('\r'); break;
                case 't': b.Append('\t'); break;
                case 'u':
                    if (i + 4 > json.Length) throw new FormatException("MÃ£ Unicode escape khÃ´ng Ä‘á»§ 4 kÃ½ tá»±.");
                    string hex = json.Substring(i, 4);
                    if (!ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out ushort cp))
                        throw new FormatException("MÃ£ Unicode escape khÃ´ng há»£p lá»‡.");
                    b.Append((char)cp);
                    i += 4;
                    break;
                default: throw new FormatException($"Escape '\\{e}' khÃ´ng há»£p lá»‡.");
            }
        }
        throw new FormatException("Chuá»—i JSON chÆ°a Ä‘Æ°á»£c Ä‘Ã³ng báº±ng dáº¥u ngoáº·c kÃ©p.");
    }

    private static void SkipWhitespace(string json, ref int i)
    {
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
    }

    private static void Expect(string json, ref int i, char expected)
    {
        if (i >= json.Length || json[i] != expected)
            throw new FormatException($"Mong Ä‘á»£i '{expected}' táº¡i vá»‹ trÃ­ {i}.");
        i++;
    }

    private static bool TryConsume(string json, ref int i, char c)
    {
        if (i < json.Length && json[i] == c) { i++; return true; }
        return false;
    }
}

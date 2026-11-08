using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
public sealed partial class GameIdRegistry {
    private static void AddScalarValues(string text, string names, List<string> destination,
        bool sessionOnly = false, string prefix = null)
    {
        MatchCollection matches = Regex.Matches(text,
            @"(?m)^[ \t]*(?:-[ \t]*)?(?:" + names + @"):[ \t]*(?<value>[^\r\n]*)");
        foreach (Match match in matches)
        {
            string value = ReadYamlValue(match.Groups["value"].Value);
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (sessionOnly && !Regex.IsMatch(value, @"^[0-9]+(?:\.[0-9]+|[A-Z][A-Z0-9]*)?$")) continue;
            AddUnique(destination, (prefix ?? string.Empty) + value);
        }
    }

    private static string ReadYamlValue(string value)
    {
        value = value.Trim();
        if (value.Length == 0 || value == "[]" || value == "{}" || value == "null" || value == "~") return string.Empty;
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            string inner = value.Substring(1, value.Length - 2);
            try { return Regex.Unescape(inner); }
            catch (ArgumentException) { return inner; }
        }
        if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
            return value.Substring(1, value.Length - 2).Replace("''", "'");
        return value;
    }
    private static void AddUnique(List<string> list, string value)
    {
        if (list == null || string.IsNullOrWhiteSpace(value)) return;
        value = value.Trim();
        if (!list.Contains(value)) list.Add(value);
    }
}

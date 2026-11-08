using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "GameIdRegistry", menuName = "Bóng Ngủ/Flag & ID Registry")]
public sealed partial class GameIdRegistry : ScriptableObject
{
    [Header("Flags được dùng trong scene, thoại và code")]
    public List<string> flags = new List<string>();

    [Header("Item ID đồng bộ từ JSON và tham chiếu trong project")]
    public List<string> itemIds = new List<string>();

    [Header("Session ID đồng bộ từ JSON và nội dung thoại")]
    public List<string> sessionIds = new List<string>();

    [Header("Skill ID / tên skill dùng trong hệ thống unlock, equip và skill registry")]
    public List<string> skillIds = new List<string>();

    [Header("Weapon ID / tên vũ khí dùng trong hệ thống equipment và damage data")]
    public List<string> weaponIds = new List<string>();

    [Header("Music ID / tên nhạc nền dùng trong hệ thống âm thanh")]
    public List<string> musicIds = new List<string>();

    [Header("SFX ID / tên hiệu ứng âm thanh dùng trong hệ thống âm thanh")]
    public List<string> sfxIds = new List<string>();

    public List<string> GetList(RegistryIdKind type)
    {
        switch (type)
        {
            case RegistryIdType.Flag: return flags ?? (flags = new List<string>());
            case RegistryIdType.Item: return itemIds ?? (itemIds = new List<string>());
            case RegistryIdType.Session: return sessionIds ?? (sessionIds = new List<string>());
            case RegistryIdType.Skill: return skillIds ?? (skillIds = new List<string>());
            case RegistryIdType.Weapon: return weaponIds ?? (weaponIds = new List<string>());
            case RegistryIdType.Music: return musicIds ?? (musicIds = new List<string>());
            case RegistryIdType.Sfx: return sfxIds ?? (sfxIds = new List<string>());
            default: return GetEventList(type);
        }
    }

#if UNITY_EDITOR
    [Serializable] private sealed class ItemJsonTable { public ItemJsonRow[] items; }
    [Serializable] private sealed class ItemJsonRow { public string name; }
    [Serializable] private sealed class SessionJsonTable { public SessionJsonRow[] sessions; }
    [Serializable] private sealed class SessionJsonRow { public string id; }

    public void SyncFromProject()
    {
        flags ??= new List<string>();
        itemIds ??= new List<string>();
        sessionIds ??= new List<string>();
        skillIds ??= new List<string>();
        weaponIds ??= new List<string>();
        musicIds ??= new List<string>();
        sfxIds ??= new List<string>();
        for (int zone = 1; zone <= 5; zone++) CollectEventIdsFromZoneScene(zone);

        bool loadedItemJson = false;
        bool loadedSessionJson = false;
        TextAsset itemJson = Resources.Load<TextAsset>("Data/Item");
        TextAsset sessionJson = Resources.Load<TextAsset>("Data/Session");

        if (itemJson != null)
        {
            ItemJsonTable table = JsonUtility.FromJson<ItemJsonTable>(itemJson.text);
            if (table != null && table.items != null)
            {
                foreach (ItemJsonRow row in table.items)
                    if (row != null) AddUnique(itemIds, row.name);
                loadedItemJson = true;
            }
        }

        if (sessionJson != null)
        {
            SessionJsonTable table = JsonUtility.FromJson<SessionJsonTable>(sessionJson.text);
            if (table != null && table.sessions != null)
            {
                foreach (SessionJsonRow row in table.sessions)
                    if (row != null) AddUnique(sessionIds, row.id);
                loadedSessionJson = true;
            }
        }

        skillIds.Clear();

        foreach (ItemDefinition definition in Resources.LoadAll<ItemDefinition>("ItemDefinitions"))
        {
            if (definition == null) continue;
            if (!string.IsNullOrWhiteSpace(definition.itemName))
            {
                AddUnique(itemIds, definition.itemName);
                if (definition.category == ItemCategory.Weapon)
                    AddUnique(weaponIds, definition.itemName);
            }
        }

        string actionScriptPath = AssetDatabase.FindAssets("SkillActions t:MonoScript")
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .FirstOrDefault(path => Path.GetFileName(path) == "SkillActions.cs");
        string actionGuid = string.IsNullOrEmpty(actionScriptPath) ? null : AssetDatabase.AssetPathToGUID(actionScriptPath);

        string[] files = Directory.GetFiles(Application.dataPath, "*.unity", SearchOption.AllDirectories);
        string[] prefabs = Directory.GetFiles(Application.dataPath, "*.prefab", SearchOption.AllDirectories);
        string[] assets = Directory.GetFiles(Application.dataPath, "*.asset", SearchOption.AllDirectories);
        foreach (string path in files)
            CollectSerializedIds(File.ReadAllText(path), actionGuid);
        foreach (string path in prefabs)
            CollectSerializedIds(File.ReadAllText(path), actionGuid);
        foreach (string path in assets)
            CollectSerializedIds(File.ReadAllText(path), actionGuid);

        string[] scripts = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        foreach (string path in scripts)
        {
            string source = File.ReadAllText(path);
            MatchCollection matches = Regex.Matches(source,
                "(?:SetFlag|RemoveFlag|HasFlag)\\s*\\(\\s*\"(?<id>[^\"]+)\"\\s*\\)");
            foreach (Match match in matches)
                AddUnique(flags, Regex.Unescape(match.Groups["id"].Value));
        }

        if (sessionIds != null)
        {
            foreach (string sessionId in new List<string>(sessionIds))
                AddUnique(flags, "Session" + sessionId + "Complete");
        }

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GameIdRegistry] Registry: {flags.Count} flags, {itemIds.Count} items, {sessionIds.Count} sessions, {skillIds.Count} skills, {weaponIds.Count} weapons. "
            + (loadedItemJson ? "Item.json đã đọc; " : "Không đọc được Item.json; ")
            + (loadedSessionJson ? "Session.json đã đọc; " : "Không đọc được Session.json; ")
            + "Skill IDs synchronized from SkillActions registrations.");
    }

    private void CollectSerializedIds(string text, string actionGuid)
    {
        AddScalarValues(text, "requiredFlag|openedFlag", flags);
        AddArrayValues(text, "flagsToSet|flagsToRemove", flags);
        AddScalarValues(text, "itemName|requiredItemName|itemToAdd|itemToRemove|readableItemToOpen", itemIds);
        AddScalarValues(text, "weaponName|equippedItemName", weaponIds);
        if (!string.IsNullOrEmpty(actionGuid))
            foreach (string component in Regex.Split(text, @"(?m)^--- !u!"))
                if (component.Contains("guid: " + actionGuid + ","))
                {
                    AddScalarValues(component, "skillName", skillIds);
                    Match catalogReference = Regex.Match(component, @"actionCatalog:\s*\{[^}]*guid:\s*(?<guid>[a-fA-F0-9]+)");
                    if (catalogReference.Success)
                    {
                        SkillActionCatalog catalog = AssetDatabase.LoadAssetAtPath<SkillActionCatalog>(AssetDatabase.GUIDToAssetPath(catalogReference.Groups["guid"].Value));
                        if (catalog?.actions != null)
                            foreach (var action in catalog.actions)
                                if (action != null && action.enabled) AddUnique(skillIds, action.actionId);
                    }
                }
        AddScalarValues(text, "completeSessionId|nextSessionId|sessionId", sessionIds, true);
        AddScalarValues(text, "eventId", flags, false, "StoryEventCompleted:");
    }

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

    private static void AddArrayValues(string text, string names, List<string> destination)
    {
        string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        Regex header = new Regex(@"^(?<indent>[ \t]*)(?:" + names + @"):[ \t]*(?:\[\])?[ \t]*$");
        for (int i = 0; i < lines.Length; i++)
        {
            Match match = header.Match(lines[i]);
            if (!match.Success) continue;
            string prefix = match.Groups["indent"].Value + "-";
            for (int j = i + 1; j < lines.Length && lines[j].StartsWith(prefix, StringComparison.Ordinal); j++)
            {
                string value = ReadYamlValue(lines[j].Substring(prefix.Length).Trim());
                AddUnique(destination, value);
            }
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
#endif
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

[Serializable]
public sealed class StoryEventZoneRegistry
{
    [HideInInspector] public string scenePath;
    [Tooltip("Để trống khi scene chỉ có một director. Nếu có nhiều, nhập đúng tên GameObject nhận event mới.")]
    public string directorObjectName;
    public List<string> eventIds = new List<string>();
}

public sealed partial class GameIdRegistry
{
    [Header("Story Event ID theo zone")]
    public StoryEventZoneRegistry zone1Events = new StoryEventZoneRegistry();
    public StoryEventZoneRegistry zone2Events = new StoryEventZoneRegistry();
    public StoryEventZoneRegistry zone3Events = new StoryEventZoneRegistry();
    public StoryEventZoneRegistry zone4Events = new StoryEventZoneRegistry();
    public StoryEventZoneRegistry zone5Events = new StoryEventZoneRegistry();

    public StoryEventZoneRegistry GetEventZone(int zone)
    {
        switch (zone)
        {
            case 1: return zone1Events ?? (zone1Events = new StoryEventZoneRegistry());
            case 2: return zone2Events ?? (zone2Events = new StoryEventZoneRegistry());
            case 3: return zone3Events ?? (zone3Events = new StoryEventZoneRegistry());
            case 4: return zone4Events ?? (zone4Events = new StoryEventZoneRegistry());
            case 5: return zone5Events ?? (zone5Events = new StoryEventZoneRegistry());
            default: throw new ArgumentOutOfRangeException(nameof(zone));
        }
    }

    public RegistryIdKind GetEventKindForScene(string scenePath)
    {
        if (!string.IsNullOrEmpty(scenePath))
            for (int zone = 1; zone <= 5; zone++)
                if (string.Equals(GetEventZone(zone).scenePath, scenePath, StringComparison.OrdinalIgnoreCase))
                    return (RegistryIdKind)((int)RegistryIdKind.EventZone1 + zone - 1);
        return RegistryIdKind.EventScene;
    }

    private List<string> GetEventList(RegistryIdKind kind)
    {
        if (kind >= RegistryIdKind.EventZone1 && kind <= RegistryIdKind.EventZone5)
        {
            var zone = GetEventZone((int)kind - (int)RegistryIdKind.EventZone1 + 1);
            return zone.eventIds ?? (zone.eventIds = new List<string>());
        }
        if (kind == RegistryIdKind.EventScene)
            return Enumerable.Range(1, 5).SelectMany(zone => GetEventZone(zone).eventIds ?? new List<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        return new List<string>();
    }

    public static string[] MissingStoryEventIds(IEnumerable<string> registered, IEnumerable<string> existing)
    {
        var known = new HashSet<string>(existing ?? Array.Empty<string>(), StringComparer.Ordinal);
        return (registered ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Where(id => known.Add(id)).ToArray();
    }

#if UNITY_EDITOR
    public int CollectEventIdsFromZoneScene(int zone)
    {
        var data = GetEventZone(zone);
        if (string.IsNullOrWhiteSpace(data.scenePath) || !File.Exists(data.scenePath)) return 0;
        Undo.RecordObject(this, "Collect Zone Story Event IDs");
        data.eventIds ??= new List<string>();
        int previous = data.eventIds.Count;
        Scene loaded = SceneManager.GetSceneByPath(data.scenePath);
        if (loaded.IsValid() && loaded.isLoaded)
        {
            foreach (var root in loaded.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.String &&
                            (property.name == "eventId" || property.name == "eventIdOnCleared" || property.name == "onOpenedEventId"))
                            AddUnique(data.eventIds, property.stringValue);
                }
        }
        else AddScalarValues(File.ReadAllText(data.scenePath), "eventId|eventIdOnCleared|onOpenedEventId", data.eventIds);
        EditorUtility.SetDirty(this);
        return data.eventIds.Count - previous;
    }

    // Only the chosen scene is searched. Newly opened scenes are saved and closed;
    // already open scenes retain Undo and are left dirty for the user's normal save.
    public int SyncStoryEventsInZone(int zone)
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("[GameIdRegistry] Dừng Play Mode trước khi đồng bộ StoryEvent.", this); return 0; }
        var data = GetEventZone(zone);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(data.scenePath) == null)
        { Debug.LogError("[GameIdRegistry] Gán Scene cho Zone " + zone + " trước khi đồng bộ.", this); return 0; }
        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(data.scenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(data.scenePath, OpenSceneMode.Additive);
        bool changed = false;
        try
        {
            var directors = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<StoryEventDirector>(true)).ToArray();
            var targets = string.IsNullOrWhiteSpace(data.directorObjectName) ? directors
                : directors.Where(item => item.name == data.directorObjectName.Trim()).ToArray();
            if (targets.Length != 1)
            { Debug.LogError("[GameIdRegistry] Zone " + zone + ": tìm thấy " + directors.Length + " director; gán Director Object Name để chọn đúng một nơi nhận event mới. Không thay đổi event.", this); return 0; }
            var director = targets[0];
            var existing = directors.SelectMany(item => item.EventDefinitions).Where(entry => entry != null).Select(entry => entry.eventId);
            string[] missing = MissingStoryEventIds(data.eventIds, existing);
            if (missing.Length == 0) { Debug.Log("[GameIdRegistry] Zone " + zone + ": không có event thiếu.", director); return 0; }
            Undo.RecordObject(director, "Add missing Zone Story Events");
            var entries = director.EventDefinitions.ToList();
            foreach (string id in missing) entries.Add(new StoryEventDefinition { eventId = id });
            director.ConfigureEvents(entries.ToArray());
            PrefabUtility.RecordPrefabInstancePropertyModifications(director);
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(scene);
            changed = true;
            if (opened && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Không lưu được scene " + data.scenePath + "; scene được giữ mở để lưu tay.");
            Debug.Log("[GameIdRegistry] Zone " + zone + ": thêm " + missing.Length + " event rỗng; giữ nguyên event đã có." + (opened ? " Đã lưu scene." : " Ctrl+S để lưu scene."), director);
            return missing.Length;
        }
        finally
        {
            if (opened && (!changed || !scene.isDirty)) EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }
#endif
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class UniqueIdUtility
{
    public static string CreateId(string sceneName) => $"{sceneName}_{Guid.NewGuid():N}";

    public static bool EnsureIds(MonoBehaviour component, bool recordUndo = false)
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return false;

        if (component == null || !component.gameObject.scene.IsValid()
            || PrefabUtility.IsPartOfPrefabAsset(component)) return false;

        bool changed = false;
        for (Type type = component.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(string)
                    || !Attribute.IsDefined(field, typeof(UniqueIdAttribute))) continue;

                string value = field.GetValue(component) as string;
                if (!string.IsNullOrWhiteSpace(value) && !HasEarlierDuplicate(component, field, value)) continue;

                if (recordUndo) Undo.RecordObject(component, "Assign Unique ID");
                field.SetValue(component, CreateId(component.gameObject.scene.name));
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
        return changed;
    }

    private static bool HasEarlierDuplicate(MonoBehaviour component, FieldInfo field, string value)
    {
        MonoBehaviour[] candidates = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        foreach (MonoBehaviour other in candidates)
        {
            if (other == null || other == component
                || other.gameObject.scene != component.gameObject.scene
                || !ComesBeforeInHierarchy(other, component)) continue;

            FieldInfo matchingField = other.GetType().GetField(field.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (matchingField == null || matchingField.FieldType != typeof(string)
                || !Attribute.IsDefined(matchingField, typeof(UniqueIdAttribute))) continue;

            if (string.Equals(matchingField.GetValue(other) as string, value, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool ComesBeforeInHierarchy(MonoBehaviour candidate, MonoBehaviour component)
    {
        List<int> candidatePath = GetSiblingPath(candidate.transform);
        List<int> componentPath = GetSiblingPath(component.transform);
        int sharedDepth = Math.Min(candidatePath.Count, componentPath.Count);

        for (int i = 0; i < sharedDepth; i++)
        {
            if (candidatePath[i] != componentPath[i])
                return candidatePath[i] < componentPath[i];
        }

        if (candidatePath.Count != componentPath.Count)
            return candidatePath.Count < componentPath.Count;

        MonoBehaviour[] components = candidate.GetComponents<MonoBehaviour>();
        int candidateIndex = Array.IndexOf(components, candidate);
        int componentIndex = Array.IndexOf(components, component);
        return candidateIndex >= 0 && componentIndex >= 0 && candidateIndex < componentIndex;
    }

    private static List<int> GetSiblingPath(Transform target)
    {
        List<int> path = new List<int>();
        while (target != null)
        {
            path.Add(target.GetSiblingIndex());
            target = target.parent;
        }

        path.Reverse();
        return path;
    }
}
#endif
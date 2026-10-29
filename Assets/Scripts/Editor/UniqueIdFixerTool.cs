#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UniqueIdFixerTool
{
    [MenuItem("Tools/Bóng Ngủ/Cấp và sửa ID cho các Scene đang mở")]
    public static void FixLoadedScenes()
    {
        MonoBehaviour[] components = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int fixedCount = 0;
        foreach (MonoBehaviour component in components)
        {
            if (component == null || !component.gameObject.scene.IsValid()) continue;
            if (!UniqueIdUtility.EnsureIds(component, true)) continue;
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            fixedCount++;
        }

        Debug.Log(fixedCount > 0
            ? $"Đã cấp hoặc sửa ID cho {fixedCount} component trong các Scene đang mở. Hãy lưu Scene để giữ thay đổi."
            : "Không có ID trống hoặc trùng trong các Scene đang mở.");
    }
}
#endif
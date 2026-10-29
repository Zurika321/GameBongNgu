#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameIdRegistry))]
public sealed class GameIdRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(8f);
        if (GUILayout.Button("Đồng bộ ID từ JSON và tham chiếu project"))
            ((GameIdRegistry)target).SyncFromProject();
    }
}
#endif
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameIdRegistry))]
public sealed class GameIdRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "zone1Events", "zone2Events", "zone3Events", "zone4Events", "zone5Events");
        serializedObject.ApplyModifiedProperties();
        GUILayout.Space(8f);
        if (GUILayout.Button("Đồng bộ ID từ JSON và tham chiếu project"))
            ((GameIdRegistry)target).SyncFromProject();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Story Event — Zone 1 đến Zone 5", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Đăng ký Event ID theo zone. Đồng bộ StoryEvent chỉ thêm event rỗng còn thiếu, giữ nguyên nội dung đã có. Scene đang mở được đánh dấu cần lưu; scene chưa mở được mở riêng, lưu rồi đóng.", MessageType.Info);
        for (int zone = 1; zone <= 5; zone++)
        {
            serializedObject.Update();
            SerializedProperty entry = serializedObject.FindProperty("zone" + zone + "Events");
            SerializedProperty scenePath = entry.FindPropertyRelative("scenePath");
            SerializedProperty ids = entry.FindPropertyRelative("eventIds");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Zone " + zone, EditorStyles.boldLabel);
            SceneAsset current = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath.stringValue);
            EditorGUI.BeginChangeCheck();
            SceneAsset selected = (SceneAsset)EditorGUILayout.ObjectField("Scene", current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck()) scenePath.stringValue = selected != null ? AssetDatabase.GetAssetPath(selected) : string.Empty;
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("directorObjectName"), new GUIContent("Director Object Name"));
            EditorGUILayout.PropertyField(ids, new GUIContent("Event IDs"), true);
            serializedObject.ApplyModifiedProperties();
            var registry = (GameIdRegistry)target;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Đọc Event ID từ scene Zone " + zone))
                {
                    registry.CollectEventIdsFromZoneScene(zone);
                    AssetDatabase.SaveAssetIfDirty(registry);
                }
                if (GUILayout.Button("Đồng bộ StoryEvent Zone " + zone + " — thêm event rỗng"))
                {
                    registry.SyncStoryEventsInZone(zone);
                    AssetDatabase.SaveAssetIfDirty(registry);
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}
#endif

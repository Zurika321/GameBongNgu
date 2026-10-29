#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomPropertyDrawer(typeof(UniqueIdAttribute))]
public sealed class UniqueIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect valueRect = new Rect(position.x, position.y, position.width - 66f, position.height);
        Rect resetRect = new Rect(position.xMax - 62f, position.y, 62f, position.height);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = false;
        EditorGUI.PropertyField(valueRect, property, label, true);
        GUI.enabled = previousEnabled;

        if (GUI.Button(resetRect, "Reset")
            && EditorUtility.DisplayDialog("Cấp lại ID?",
                "Dữ liệu save đang gắn với ID hiện tại sẽ không tự chuyển sang ID mới. Bạn có chắc muốn cấp lại?",
                "Cấp lại", "Hủy"))
        {
            Component component = property.serializedObject.targetObject as Component;
            if (component != null && component.gameObject.scene.IsValid())
            {
                Undo.RecordObject(component, "Reset Unique ID");
                property.stringValue = UniqueIdUtility.CreateId(component.gameObject.scene.name);
                property.serializedObject.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
        }

        EditorGUI.EndProperty();
    }
}
#endif
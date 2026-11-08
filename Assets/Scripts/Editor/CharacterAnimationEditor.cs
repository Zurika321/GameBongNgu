using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterAnimation))]
public sealed class CharacterAnimationEditor : Editor
{
    private SerializedProperty unityJson;
    private SerializedProperty pixelsPerUnitOverride;
    private SerializedProperty useUnscaledTime;
    private SerializedProperty audioBindings;
    private SerializedProperty groupSelections;
    private SerializedProperty logAnimationWarnings;
    private SerializedProperty weaponGroupName;
    private SerializedProperty weaponVariantBindings;
    private string animationNameError;

    private void OnEnable()
    {
        unityJson = serializedObject.FindProperty("unityJsonAsset");
        pixelsPerUnitOverride = serializedObject.FindProperty("pixelsPerUnitOverride");
        useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
        audioBindings = serializedObject.FindProperty("audioBindings");
        groupSelections = serializedObject.FindProperty("groupSelections");
        logAnimationWarnings = serializedObject.FindProperty("logAnimationWarnings");
        weaponGroupName = serializedObject.FindProperty("weaponGroupName");
        weaponVariantBindings = serializedObject.FindProperty("weaponVariantBindings");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Bone Animator Studio", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(unityJson, new GUIContent("Unity JSON", "Kéo file <model>_unity.json vào đây."));

        CharacterAnimation component = (CharacterAnimation)target;
        bool jsonValid = component.GetInspectorJsonStatus(out string status);
        if (jsonValid)
            EditorGUILayout.HelpBox(status, MessageType.Info);
        else
            EditorGUILayout.HelpBox(status, unityJson.objectReferenceValue == null ? MessageType.None : MessageType.Warning);

        if (GUILayout.Button("↻ Đồng bộ Animation / SFX / GroupBlock"))
        {
            component.RefreshAudioBindings();
            serializedObject.Update();
        }

        EditorGUILayout.PropertyField(pixelsPerUnitOverride);
        EditorGUILayout.PropertyField(useUnscaledTime);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Tên animation skill", EditorStyles.boldLabel);
        if (jsonValid)
        {
            EditorGUILayout.HelpBox("Đổi tên gọi animation skill tại đây cho khớp skillId. Tên thuộc AnimState và Default được khóa. JSON gốc và các SFX đã gán được giữ nguyên. Nếu cần dùng Attack cho một skill, chọn Animation Name trong Context của SkillConfig.", MessageType.Info);
            foreach (CharacterAnimationNameInfo definition in component.GetAnimationNameDefinitions())
            {
                using (new EditorGUI.DisabledScope(definition.locked))
                {
                    EditorGUI.BeginChangeCheck();
                    string renamed = EditorGUILayout.DelayedTextField(new GUIContent(definition.sourceName,
                        definition.locked ? "Tên mặc định được khóa." : "Tên dùng khi gọi PlayAnimationSkill; Enter để lưu."), definition.animationName);
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                        Undo.RecordObject(component, "Rename skill animation");
                        if (component.SetSkillAnimationName(definition.sourceName, renamed, out animationNameError))
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                            EditorUtility.SetDirty(component);
                        }
                        serializedObject.Update();
                    }
                }
            }
            if (!string.IsNullOrEmpty(animationNameError)) EditorGUILayout.HelpBox(animationNameError, MessageType.Warning);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Animation SFX Registry", EditorStyles.boldLabel);
        if (!jsonValid)
        {
            EditorGUILayout.HelpBox("Danh sách animation và SFX chỉ hiển thị sau khi Unity JSON hợp lệ.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Danh sách animation được đồng bộ từ JSON. ID âm thanh dùng RegistryIdType.Sfx và được gọi qua VolumeManager.PlaySFX().", MessageType.Info);
            for (int i = 0; i < audioBindings.arraySize; i++)
            {
                SerializedProperty item = audioBindings.GetArrayElementAtIndex(i);
                SerializedProperty name = item.FindPropertyRelative("animationName");
                SerializedProperty isCombat = item.FindPropertyRelative("isCombat");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(string.IsNullOrEmpty(name.stringValue) ? "Animation (chưa có tên)" : name.stringValue, EditorStyles.boldLabel);
                if (isCombat.boolValue)
                {
                    SerializedProperty castStart = item.FindPropertyRelative("castStartFrame");
                    SerializedProperty castEnd = item.FindPropertyRelative("castEndFrame");
                    if (castStart != null) EditorGUILayout.PropertyField(castStart, new GUIContent("Cast Start Frame"));
                    if (castEnd != null) EditorGUILayout.PropertyField(castEnd, new GUIContent("Cast End Frame"));
                    EditorGUILayout.Space(3);
                    DrawRegistryField(item, "chargeSfxId", "Charge SFX");
                    DrawRegistryField(item, "castSfxId", "Cast SFX");
                    DrawRegistryField(item, "endSfxId", "End SFX");
                }
                else
                {
                    DrawRegistryField(item, "sfxId", "Animation SFX");
                }
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("GroupBlock — biến thể theo JSON", EditorStyles.boldLabel);
        if (jsonValid && groupSelections != null)
        {
            CharacterAnimationGroupInfo[] groups = component.GetGroupVariantDefinitions();
            if (groups.Length == 0) EditorGUILayout.HelpBox("JSON này không có GroupBlock (tương thích phiên bản cũ).", MessageType.None);
            foreach (CharacterAnimationGroupInfo group in groups)
            {
                SerializedProperty item = FindGroupSelection(group.name);
                if (item == null)
                {
                    EditorGUILayout.HelpBox("Cần đồng bộ lại group '" + group.name + "'.", MessageType.Warning);
                    continue;
                }
                SerializedProperty selected = item.FindPropertyRelative("variantName");
                if (selected == null) continue;
                string[] options = new string[group.variants.Length + 1];
                options[0] = "None — ẩn block của nhóm";
                int selectedIndex = 0;
                for (int i = 0; i < group.variants.Length; i++)
                {
                    options[i + 1] = group.variants[i];
                    if (selected.stringValue == group.variants[i]) selectedIndex = i + 1;
                }
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup(new GUIContent(group.name, "Biến thể GroupBlock; None = không có block nhóm này."), selectedIndex, options);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(component, "Change GroupBlock " + group.name);
                    selected.stringValue = newIndex == 0 ? string.Empty : group.variants[newIndex - 1];
                    serializedObject.ApplyModifiedProperties();
                    component.RefreshGroupVariantVisibility();
                    EditorUtility.SetDirty(component);
                    serializedObject.Update();
                }
            }
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Lựa chọn được lưu vào component. Renderer sẽ cập nhật khi Play Mode; Scene view ở Edit Mode chưa dựng rig preview tự động.", MessageType.Info);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Đồng bộ vũ khí từ Inventory", EditorStyles.boldLabel);
            string[] weaponGroups = new string[groups.Length + 1];
            weaponGroups[0] = "Tự nhận theo tên biến thể vũ khí";
            int weaponGroupIndex = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                weaponGroups[i + 1] = groups[i].name;
                if (groups[i].name == weaponGroupName.stringValue) weaponGroupIndex = i + 1;
            }
            if (!string.IsNullOrEmpty(weaponGroupName.stringValue) && weaponGroupIndex == 0)
                EditorGUILayout.HelpBox("Weapon Group không còn có trong JSON: " + weaponGroupName.stringValue, MessageType.Warning);
            EditorGUI.BeginChangeCheck();
            int chosenGroup = EditorGUILayout.Popup("Weapon Group", weaponGroupIndex, weaponGroups);
            if (EditorGUI.EndChangeCheck()) weaponGroupName.stringValue = chosenGroup == 0 ? string.Empty : groups[chosenGroup - 1].name;
            EditorGUILayout.PropertyField(weaponVariantBindings, new GUIContent("Weapon Variant Bindings", "Chỉ gán khi tên biến thể khác tên/ID vật phẩm. Bỏ trang bị sẽ chọn None."), true);
        }
        else if (!jsonValid)
            EditorGUILayout.HelpBox("GroupBlock sẽ có khi Unity JSON hợp lệ.", MessageType.None);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(logAnimationWarnings);
        serializedObject.ApplyModifiedProperties();
    }

    private SerializedProperty FindGroupSelection(string name)
    {
        if (groupSelections == null) return null;
        for (int i = 0; i < groupSelections.arraySize; i++)
        {
            SerializedProperty item = groupSelections.GetArrayElementAtIndex(i);
            SerializedProperty groupName = item.FindPropertyRelative("groupName");
            if (groupName != null && groupName.stringValue == name) return item;
        }
        return null;
    }

    private static void DrawRegistryField(SerializedProperty parent, string childName, string label)
    {
        SerializedProperty child = parent.FindPropertyRelative(childName);
        if (child != null)
            EditorGUILayout.PropertyField(child, new GUIContent(label), true);
    }
}

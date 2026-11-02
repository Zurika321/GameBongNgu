using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LvConfig))]
public sealed class LvConfigEditor : Editor
{
    private SerializedProperty levelsProperty;

    private void OnEnable()
    {
        levelsProperty = serializedObject.FindProperty("levels");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lv Config", EditorStyles.boldLabel);

        if (levelsProperty == null)
        {
            EditorGUILayout.HelpBox("Không tìm thấy property levels.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        for (int i = 0; i < levelsProperty.arraySize; i++)
        {
            DrawLevelEntry(levelsProperty.GetArrayElementAtIndex(i));
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Thêm level"))
        {
            levelsProperty.arraySize++;
            SerializedProperty newEntry = levelsProperty.GetArrayElementAtIndex(levelsProperty.arraySize - 1);
            newEntry.FindPropertyRelative("level").intValue = levelsProperty.arraySize;
            newEntry.FindPropertyRelative("expRequired").intValue = 0;
            newEntry.FindPropertyRelative("maxHp").intValue = 60;
            newEntry.FindPropertyRelative("atk").intValue = 8;
            newEntry.FindPropertyRelative("def").intValue = 3;
            newEntry.FindPropertyRelative("maxRareAmmo").intValue = 2;
            newEntry.FindPropertyRelative("skills").ClearArray();
        }

        if (levelsProperty.arraySize > 0 && GUILayout.Button("Xóa level cuối"))
        {
            levelsProperty.DeleteArrayElementAtIndex(levelsProperty.arraySize - 1);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Tạo default từ Level.json"))
        {
            LvConfig config = target as LvConfig;
            if (config != null)
            {
                bool loaded = config.LoadDefaultFromJson();
                if (!loaded)
                    config.EnsureDefaultEntries();
                serializedObject.Update();
                levelsProperty = serializedObject.FindProperty("levels");
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLevelEntry(SerializedProperty entryProperty)
    {
        SerializedProperty levelProperty = entryProperty.FindPropertyRelative("level");
        SerializedProperty expProperty = entryProperty.FindPropertyRelative("expRequired");
        SerializedProperty hpProperty = entryProperty.FindPropertyRelative("maxHp");
        SerializedProperty atkProperty = entryProperty.FindPropertyRelative("atk");
        SerializedProperty defProperty = entryProperty.FindPropertyRelative("def");
        SerializedProperty ammoProperty = entryProperty.FindPropertyRelative("maxRareAmmo");
        SerializedProperty skillsProperty = entryProperty.FindPropertyRelative("skills");

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Level " + Mathf.Max(1, levelProperty.intValue), EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int levelValue = EditorGUILayout.IntField("Level", Mathf.Max(1, levelProperty.intValue));
        if (EditorGUI.EndChangeCheck())
            levelProperty.intValue = Mathf.Max(1, levelValue);

        EditorGUILayout.PropertyField(expProperty, new GUIContent("XP required"));
        EditorGUILayout.PropertyField(hpProperty, new GUIContent("HP"));
        EditorGUILayout.PropertyField(atkProperty, new GUIContent("ATK"));
        EditorGUILayout.PropertyField(defProperty, new GUIContent("DEF"));
        EditorGUILayout.PropertyField(ammoProperty, new GUIContent("Ammo"));

        if (skillsProperty != null)
        {
            EditorGUILayout.LabelField("Skills unlock", EditorStyles.miniBoldLabel);
            DrawSkillList(skillsProperty);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawSkillList(SerializedProperty skillsProperty)
    {
        for (int i = 0; i < skillsProperty.arraySize; i++)
        {
            SerializedProperty skillProp = skillsProperty.GetArrayElementAtIndex(i);
            string current = skillProp.stringValue;
            string[] options = GetSkillOptions();
            if (!string.IsNullOrWhiteSpace(current) && !System.Array.Exists(options, x => x == current))
            {
                options = new string[] { current }.AddRangeToArray(options);
            }

            int selectedIndex = System.Array.FindIndex(options, x => x == current);
            if (selectedIndex < 0) selectedIndex = 0;

            int newIndex = EditorGUILayout.Popup("Skill " + (i + 1), selectedIndex, options);
            skillProp.stringValue = options[newIndex];
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Thêm skill"))
            skillsProperty.arraySize++;
        if (skillsProperty.arraySize > 0 && GUILayout.Button("Xóa skill"))
            skillsProperty.arraySize--;
        EditorGUILayout.EndHorizontal();
    }

    private static string[] GetSkillOptions()
    {
        List<string> result = new List<string>();

        GameIdRegistry registry = Resources.Load<GameIdRegistry>("GameIdRegistry");
        if (registry == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:GameIdRegistry");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                registry = AssetDatabase.LoadAssetAtPath<GameIdRegistry>(path);
                if (registry != null)
                    break;
            }
        }

        if (registry != null)
        {
            foreach (string skill in registry.GetList(RegistryIdType.Skill))
            {
                if (string.IsNullOrWhiteSpace(skill))
                    continue;

                string trimmed = skill.Trim();
                if (!result.Contains(trimmed))
                    result.Add(trimmed);
            }
        }

        if (result.Count == 0)
        {
            string[] fallback = { "BasicSlash", "DodgeRoll", "NormalAttack", "Fireball", "FlyAttack", "DashAttack2", "ArcAttack" };
            foreach (string skill in fallback)
            {
                if (!result.Contains(skill))
                    result.Add(skill);
            }
        }

        return result.ToArray();
    }
}

public static class StringArrayExtensions
{
    public static string[] AddRangeToArray(this string[] source, string[] add)
    {
        List<string> merged = new List<string>();
        if (source != null)
            merged.AddRange(source);
        if (add != null)
            merged.AddRange(add);
        return merged.ToArray();
    }
}


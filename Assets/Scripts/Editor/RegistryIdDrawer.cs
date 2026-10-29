#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomPropertyDrawer(typeof(RegistryIdAttribute))]
public sealed class RegistryIdDrawer : PropertyDrawer
{
    private static GameIdRegistry cachedRegistry;
    private const float LineHeight = 18f;
    private const float Spacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!IsStringArray(property) || !property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        return LineHeight + Spacing + (property.arraySize + 1) * (LineHeight + Spacing);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            DrawString(position, property, label, RegistryIdTypeFor(attribute));
            return;
        }

        if (IsStringArray(property))
        {
            DrawStringArray(position, property, label, RegistryIdTypeFor(attribute));
            return;
        }

        EditorGUI.LabelField(position, label.text, "RegistryId chỉ hỗ trợ string hoặc string[].");
    }

    private static RegistryIdType RegistryIdTypeFor(PropertyAttribute propertyAttribute)
        => ((RegistryIdAttribute)propertyAttribute).IdType;

    private static bool IsStringArray(SerializedProperty property)
    {
        return property.isArray && property.propertyType == SerializedPropertyType.Generic;
    }

    private static GameIdRegistry GetRegistry()
    {
        if (cachedRegistry != null) return cachedRegistry;
        cachedRegistry = Resources.Load<GameIdRegistry>("GameIdRegistry");
        if (cachedRegistry != null) return cachedRegistry;

        string[] guids = AssetDatabase.FindAssets("t:GameIdRegistry");
        if (guids.Length > 0)
            cachedRegistry = AssetDatabase.LoadAssetAtPath<GameIdRegistry>(AssetDatabase.GUIDToAssetPath(guids[0]));
        return cachedRegistry;
    }

    private static void DrawString(Rect position, SerializedProperty property, GUIContent label, RegistryIdType type)
    {
        GameIdRegistry registry = GetRegistry();
        if (registry == null)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        List<string> values = registry.GetList(type);
        string current = property.stringValue;
        bool missing = !string.IsNullOrWhiteSpace(current) && !values.Contains(current);
        EditorGUI.BeginProperty(position, label, property);
        Rect field = EditorGUI.PrefixLabel(position, label);
        DrawSelector(field, current, missing, values, registry, property, -1, type);
        EditorGUI.EndProperty();
    }

    private static void DrawStringArray(Rect position, SerializedProperty property, GUIContent label, RegistryIdType type)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect header = new Rect(position.x, position.y, position.width, LineHeight);
        property.isExpanded = EditorGUI.Foldout(header, property.isExpanded,
            new GUIContent($"{label.text} ({property.arraySize})"), true);

        if (property.isExpanded)
        {
            GameIdRegistry registry = GetRegistry();
            List<string> values = registry != null ? registry.GetList(type) : null;
            float y = position.y + LineHeight + Spacing;
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                Rect row = new Rect(position.x + 14f, y, position.width - 14f, LineHeight);
                Rect nameRect = new Rect(row.x, row.y, 88f, row.height);
                Rect removeRect = new Rect(row.xMax - 25f, row.y, 25f, row.height);
                Rect valueRect = new Rect(nameRect.xMax + 2f, row.y, removeRect.x - nameRect.xMax - 5f, row.height);
                EditorGUI.LabelField(nameRect, $"Element {i}");

                if (registry == null)
                    EditorGUI.PropertyField(valueRect, element, GUIContent.none);
                else
                {
                    string current = element.stringValue;
                    bool missing = !string.IsNullOrWhiteSpace(current) && !values.Contains(current);
                    DrawSelector(valueRect, current, missing, values, registry, property, i, type);
                }

                if (GUI.Button(removeRect, "−"))
                {
                    property.DeleteArrayElementAtIndex(i);
                    property.serializedObject.ApplyModifiedProperties();
                    break;
                }
                y += LineHeight + Spacing;
            }

            Rect addRect = new Rect(position.x + 14f, y, position.width - 14f, LineHeight);
            if (GUI.Button(addRect, "+ Thêm ID"))
            {
                int index = property.arraySize;
                property.InsertArrayElementAtIndex(index);
                property.GetArrayElementAtIndex(index).stringValue = string.Empty;
                property.serializedObject.ApplyModifiedProperties();
            }
        }
        EditorGUI.EndProperty();
    }

    private static void DrawSelector(Rect fieldRect, string current, bool missing, List<string> values,
        GameIdRegistry registry, SerializedProperty property, int arrayIndex, RegistryIdType type)
    {
        Rect selectRect = new Rect(fieldRect.x, fieldRect.y, Mathf.Max(30f, fieldRect.width - 25f), fieldRect.height);
        Rect pingRect = new Rect(fieldRect.xMax - 22f, fieldRect.y, 22f, fieldRect.height);
        Color oldColor = GUI.backgroundColor;
        if (missing) GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);

        string text = string.IsNullOrEmpty(current)
            ? $"-- Chọn {type} --"
            : missing ? $"⚠ [Không có trong Registry] {current}" : current;
        if (EditorGUI.DropdownButton(selectRect, new GUIContent(text), FocusType.Keyboard))
            ShowDropdown(selectRect, values, property, arrayIndex);
        GUI.backgroundColor = oldColor;

        if (GUI.Button(pingRect, new GUIContent("⚙", "Mở GameIdRegistry")))
        {
            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);
        }
    }


    private static void ShowDropdown(Rect anchor, List<string> values, SerializedProperty property, int arrayIndex)
    {
        SerializedObject serializedObject = property.serializedObject;
        string propertyPath = property.propertyPath;
        var dropdown = new SearchableIdDropdown(new AdvancedDropdownState(), values, selected =>
        {
            serializedObject.Update();
            SerializedProperty target = serializedObject.FindProperty(propertyPath);
            if (target == null) return;
            if (arrayIndex >= 0) target.GetArrayElementAtIndex(arrayIndex).stringValue = selected;
            else target.stringValue = selected;
            serializedObject.ApplyModifiedProperties();
        });
        dropdown.Show(anchor);
    }

    private sealed class SearchableIdDropdown : AdvancedDropdown
    {
        private readonly List<string> values;
        private readonly System.Action<string> onSelected;

        public SearchableIdDropdown(AdvancedDropdownState state, List<string> values, System.Action<string> onSelected)
            : base(state)
        {
            this.values = values ?? new List<string>();
            this.onSelected = onSelected;
            minimumSize = new Vector2(280f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Tìm kiếm & chọn ID");
            root.AddChild(new IdItem("(Trống / bỏ chọn)", string.Empty));
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value)) root.AddChild(new IdItem(value, value));
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is IdItem selected) onSelected?.Invoke(selected.Value);
        }

        private sealed class IdItem : AdvancedDropdownItem
        {
            public string Value { get; }
            public IdItem(string name, string value) : base(name) => Value = value;
        }
    }
}
#endif
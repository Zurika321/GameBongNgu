#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Đặt trong thư mục Editor/
[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public sealed class SubclassSelectorDrawer : PropertyDrawer
{
    private const float Spacing = 2f;
    private static readonly Dictionary<Type, List<Type>> TypeCacheByBase = new Dictionary<Type, List<Type>>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
            return EditorGUI.GetPropertyHeight(property, label, true);

        float line = EditorGUIUtility.singleLineHeight;
        float height = line; // header
        bool hasValue = property.managedReferenceValue != null;

        if (property.isExpanded || !hasValue)
            height += Spacing + line; // dòng dropdown

        if (property.isExpanded && hasValue)
        {
            foreach (SerializedProperty child in GetChildren(property))
                height += Spacing + EditorGUI.GetPropertyHeight(child, true);
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        object value = property.managedReferenceValue;
        bool hasValue = value != null;
        float y = position.y;

        // 1) Header: "Element 0: Add Item"
        string typeLabel = hasValue ? PrettyName(value.GetType()) : "None";
        Rect header = new Rect(position.x, y, position.width, line);
        if (hasValue)
        {
            property.isExpanded = EditorGUI.Foldout(header, property.isExpanded,
                $"{label.text}: {typeLabel}", true);
        }
        else
        {
            EditorGUI.LabelField(header, label.text, "None (chọn loại bên dưới)");
        }
        y += line + Spacing;

        // 2) Dòng dropdown riêng
        if (property.isExpanded || !hasValue)
        {
            EditorGUI.indentLevel++;
            Rect row = new Rect(position.x, y, position.width, line);
            Rect field = EditorGUI.PrefixLabel(row, new GUIContent("Type"));
            EditorGUI.indentLevel--;

            if (EditorGUI.DropdownButton(field, new GUIContent(typeLabel), FocusType.Keyboard))
                ShowMenu(field, property);
            y += line + Spacing;
        }

        // 3) Field con thụt vào
        if (property.isExpanded && hasValue)
        {
            EditorGUI.indentLevel++;
            foreach (SerializedProperty child in GetChildren(property))
            {
                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
                y += h + Spacing;
            }
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    // ---------- Menu chọn type ----------

    private static void ShowMenu(Rect anchor, SerializedProperty property)
    {
        Type baseType = ResolveBaseType(property);
        if (baseType == null)
        {
            Debug.LogError($"[SubclassSelector] Không xác định được kiểu gốc: {property.managedReferenceFieldTypename}");
            return;
        }

        Type currentType = property.managedReferenceValue?.GetType();
        SerializedObject so = property.serializedObject;
        string path = property.propertyPath;

        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("None"), currentType == null, () => Assign(so, path, null));
        menu.AddSeparator(string.Empty);

        foreach (Type type in GetImplementations(baseType))
        {
            Type captured = type;
            menu.AddItem(new GUIContent(PrettyName(type)), type == currentType, () =>
            {
                if (captured == currentType) return;
                Assign(so, path, captured);
            });
        }
        menu.DropDown(anchor);
    }

    private static void Assign(SerializedObject so, string path, Type type)
    {
        so.Update();
        SerializedProperty target = so.FindProperty(path);
        if (target == null) return;
        target.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
        target.isExpanded = type != null;
        so.ApplyModifiedProperties();
    }

    // ---------- Helpers ----------

    private static IEnumerable<SerializedProperty> GetChildren(SerializedProperty parent)
    {
        SerializedProperty iterator = parent.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enter = true;
        while (iterator.NextVisible(enter) && !SerializedProperty.EqualContents(iterator, end))
        {
            enter = false;
            yield return iterator.Copy();
        }
    }

    private static List<Type> GetImplementations(Type baseType)
    {
        if (TypeCacheByBase.TryGetValue(baseType, out List<Type> cached)) return cached;

        List<Type> result = TypeCache.GetTypesDerivedFrom(baseType)
            .Where(t => t.IsClass
                        && !t.IsAbstract
                        && !t.IsGenericType
                        && !typeof(UnityEngine.Object).IsAssignableFrom(t)
                        && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(PrettyName)
            .ToList();
        TypeCacheByBase[baseType] = result;
        return result;
    }

    // managedReferenceFieldTypename có dạng "Assembly-CSharp IStoryAction"
    private static Type ResolveBaseType(SerializedProperty property)
    {
        string full = property.managedReferenceFieldTypename;
        if (string.IsNullOrEmpty(full)) return null;

        int space = full.IndexOf(' ');
        string assemblyName = space > 0 ? full.Substring(0, space) : string.Empty;
        string typeName = space > 0 ? full.Substring(space + 1) : full;

        Type type = Type.GetType($"{typeName}, {assemblyName}");
        if (type != null) return type;

        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(typeName))
            .FirstOrDefault(t => t != null);
    }

    // "AddItemStoryAction" -> "Add Item", "HasFlagStoryCondition" -> "Has Flag"
    private static string PrettyName(Type type)
    {
        string name = type.Name;
        foreach (string suffix in new[] { "StoryAction", "StoryCondition" })
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - suffix.Length);
                break;
            }
        }
        return ObjectNames.NicifyVariableName(name);
    }
}
#endif

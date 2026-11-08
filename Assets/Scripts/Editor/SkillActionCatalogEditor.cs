using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillActionCatalog))]
public sealed class SkillActionCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SkillActionCatalog catalog = (SkillActionCatalog)target;
        EditorGUILayout.HelpBox("Recipes from Skill Action. Configure missing prefab/UI references in the skill context or recipe. Legacy action IDs remain registered separately.", MessageType.Info);
        if (GUILayout.Button("Validate action catalog"))
        {
            var errors = catalog.Validate();
            if (errors.Count == 0) Debug.Log("[SkillActionCatalog] Valid: " + catalog.actions.Count + " actions.", catalog);
            else foreach (string error in errors) Debug.LogError("[SkillActionCatalog] " + error, catalog);
        }
        DrawDefaultInspector();
    }
}

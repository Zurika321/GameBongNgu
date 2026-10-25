using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LocalizationSetupTool : EditorWindow
{
    private const string SourceFolder = "Assets/ForAIAgentEdit";
    private const string ScriptFolder = "Assets/Scripts/Localization";
    private const string ManagerPrefabPath = "Assets/Prefabs/Systems/LocalizationManager.prefab";
    private const string VietnameseJson = "{\n  \"new_game\": \"Tr\\u00F2 ch\\u01A1i m\\u1EDBi\",\n  \"continue\": \"Ti\\u1EBFp t\\u1EE5c\",\n  \"settings\": \"C\\u00E0i \\u0111\\u1EB7t\",\n  \"close\": \"\\u0110\\u00F3ng [ESC]\",\n  \"back\": \"Quay l\\u1EA1i\",\n  \"quit\": \"Tho\\u00E1t\"\n}\n";
    private const string EnglishJson = "{\n  \"new_game\": \"New Game\",\n  \"continue\": \"Continue\",\n  \"settings\": \"Settings\",\n  \"close\": \"Close [ESC]\",\n  \"back\": \"Back\",\n  \"quit\": \"Quit\"\n}\n";

    [SerializeField] private string defaultLanguage = "vn";
    [SerializeField] private string languageCode = "vn";
    [SerializeField] private string translationKey = "new_game";

    [MenuItem("Tools/Localization/Setup Tool")]
    private static void OpenWindow()
    {
        GetWindow<LocalizationSetupTool>("Localization Setup");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Install the three localization scripts, create starter VN/EN JSON files, add a persistent manager to a scene or prefab, and configure the selected UI object.",
            MessageType.Info);

        defaultLanguage = EditorGUILayout.TextField("Default Language", defaultLanguage);
        languageCode = EditorGUILayout.TextField("Selected Button Language", languageCode);
        translationKey = EditorGUILayout.TextField("Selected Text Key", translationKey);

        EditorGUILayout.Space();
        if (GUILayout.Button("Install Scripts + Create Missing Language JSON"))
        {
            InstallScripts();
            CreateMissingLanguageFiles();
        }

        if (GUILayout.Button("Create / Ensure Manager in Loaded Scene"))
            EnsureManagerInLoadedScenes();

        if (GUILayout.Button("Create / Update Localization Manager Prefab"))
            CreateOrUpdateManagerPrefab();

        EditorGUILayout.Space();
        if (GUILayout.Button("Add LanguageButton to Selected Button and Connect OnClick"))
            ConfigureSelectedLanguageButton();

        if (GUILayout.Button("Add LocalizedText to Selected Text and Set Key"))
            ConfigureSelectedLocalizedText();

        EditorGUILayout.HelpBox(
            "Select a Button or Text/TMP object in the Hierarchy or Project window before configuring it. Prefab assets are edited and saved in place. Language JSON files are created only when missing.",
            MessageType.None);
    }

    private static void InstallScripts()
    {
        if (!AssetDatabase.IsValidFolder(ScriptFolder))
            AssetDatabase.CreateFolder("Assets/Scripts", "Localization");

        string[] fileNames = { "LanguageButton.cs", "LocalizedText.cs", "transition.cs" };
        foreach (string fileName in fileNames)
        {
            string sourcePath = SourceFolder + "/" + fileName;
            string targetPath = ScriptFolder + "/" + fileName;
            bool sourceExists = AssetDatabase.LoadAssetAtPath<MonoScript>(sourcePath) != null;
            bool targetExists = AssetDatabase.LoadAssetAtPath<MonoScript>(targetPath) != null;

            if (sourceExists && targetExists)
            {
                Debug.LogError($"[LocalizationSetupTool] Both source and installed copies exist for '{fileName}'. Remove the duplicate manually before compiling.");
                continue;
            }

            if (sourceExists)
            {
                string error = AssetDatabase.MoveAsset(sourcePath, targetPath);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError($"[LocalizationSetupTool] Could not move '{sourcePath}': {error}");
                else
                    Debug.Log($"[LocalizationSetupTool] Installed '{targetPath}'.");
            }
            else if (!targetExists)
            {
                Debug.LogError($"[LocalizationSetupTool] Could not find '{sourcePath}' or '{targetPath}'.");
            }
        }

        AssetDatabase.Refresh();
    }

    private static void CreateMissingLanguageFiles()
    {
        string resourceLanguageFolder = "Assets/Resources/transitions";
        Directory.CreateDirectory(resourceLanguageFolder);

        WriteIfMissing(Path.Combine(resourceLanguageFolder, "vn.json"), VietnameseJson);
        WriteIfMissing(Path.Combine(resourceLanguageFolder, "en.json"), EnglishJson);
        AssetDatabase.Refresh();
    }

    private static void WriteIfMissing(string path, string contents)
    {
        if (File.Exists(path)) return;
        File.WriteAllText(path, contents, new System.Text.UTF8Encoding(false));
        Debug.Log($"[LocalizationSetupTool] Created '{path}'. Add or edit translation keys there.");
    }

    private void EnsureManagerInLoadedScenes()
    {
        List<transition> managers = FindSceneManagers();
        transition manager = managers.Count > 0 ? managers[0] : null;

        if (manager == null)
        {
            GameObject managerObject = new GameObject("LocalizationManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Create Localization Manager");
            manager = Undo.AddComponent<transition>(managerObject);
            SetStringProperty(manager, "defaultLanguage", defaultLanguage);
            EditorSceneManager.MarkSceneDirty(managerObject.scene);
        }
        else
        {
            SetStringProperty(manager, "defaultLanguage", defaultLanguage);
        }
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        for (int i = 1; i < managers.Count; i++)
            Undo.DestroyObjectImmediate(managers[i]);

        Selection.activeGameObject = manager.gameObject;
        EditorGUIUtility.PingObject(manager.gameObject);
        Debug.Log("[LocalizationSetupTool] Exactly one transition manager remains in the currently loaded scenes. It persists across scene loads at runtime.");
    }

    private static List<transition> FindSceneManagers()
    {
        var found = new List<transition>();
        foreach (transition manager in Resources.FindObjectsOfTypeAll<transition>())
        {
            if (manager == null) continue;
            Scene scene = manager.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded && !EditorUtility.IsPersistent(manager))
                found.Add(manager);
        }
        return found;
    }

    private void CreateOrUpdateManagerPrefab()
    {
        EnsureAssetFolder("Assets/Prefabs/Systems");
        GameObject prefabRoot = null;
        bool loadedPrefab = File.Exists(ManagerPrefabPath);

        try
        {
            if (loadedPrefab)
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(ManagerPrefabPath);
            }
            else
            {
                prefabRoot = new GameObject("LocalizationManager");
            }

            transition manager = prefabRoot.GetComponent<transition>();
            if (manager == null) manager = prefabRoot.AddComponent<transition>();
            SetStringProperty(manager, "defaultLanguage", defaultLanguage);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ManagerPrefabPath);
            Debug.Log($"[LocalizationSetupTool] Saved '{ManagerPrefabPath}'. Drag it into the first loaded scene.");
        }
        finally
        {
            if (prefabRoot != null)
            {
                if (loadedPrefab) PrefabUtility.UnloadPrefabContents(prefabRoot);
                else DestroyImmediate(prefabRoot);
            }
        }
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private void ConfigureSelectedLanguageButton()
    {
        ApplyToSelectedObject("Configure Language Button", (target, isPrefab) =>
        {
            Button button = target.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("[LocalizationSetupTool] The selected object needs a Button component.", target);
                return false;
            }

            LanguageButton languageButton = target.GetComponent<LanguageButton>();
            if (languageButton == null)
                languageButton = AddComponent<LanguageButton>(target, isPrefab);
            SetStringProperty(languageButton, "languageCode", languageCode);

            bool alreadyConnected = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == languageButton
                    && button.onClick.GetPersistentMethodName(i) == nameof(LanguageButton.ChuyenNgonNgu))
                {
                    alreadyConnected = true;
                    break;
                }
            }

            if (!alreadyConnected)
                UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(languageButton.ChuyenNgonNgu));

            EditorUtility.SetDirty(button);
            EditorUtility.SetDirty(languageButton);
            return true;
        });
    }

    private void ConfigureSelectedLocalizedText()
    {
        ApplyToSelectedObject("Configure Localized Text", (target, isPrefab) =>
        {
            if (target.GetComponent<TMP_Text>() == null && target.GetComponent<Text>() == null)
            {
                Debug.LogError("[LocalizationSetupTool] The selected object needs TMP_Text or legacy UI Text.", target);
                return false;
            }

            LocalizedText localizedText = target.GetComponent<LocalizedText>();
            if (localizedText == null)
                localizedText = AddComponent<LocalizedText>(target, isPrefab);
            SetStringProperty(localizedText, "translationKey", translationKey);
            EditorUtility.SetDirty(localizedText);
            return true;
        });
    }

    private static void ApplyToSelectedObject(string undoName, Func<GameObject, bool, bool> configure)
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("[LocalizationSetupTool] Select a UI GameObject in the Hierarchy or Project window first.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(selected);
        if (PrefabUtility.IsPartOfPrefabAsset(selected) && !string.IsNullOrEmpty(assetPath))
        {
            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                string childPath = selected.transform == selected.transform.root
                    ? string.Empty
                    : AnimationUtility.CalculateTransformPath(selected.transform, selected.transform.root);
                GameObject target = string.IsNullOrEmpty(childPath) ? prefabRoot : prefabRoot.transform.Find(childPath)?.gameObject;
                if (target == null)
                {
                    Debug.LogError("[LocalizationSetupTool] Could not find the selected object inside its prefab asset.");
                    return;
                }

                if (configure(target, true))
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                    Debug.Log($"[LocalizationSetupTool] Updated prefab '{assetPath}'.");
                }
            }
            finally
            {
                if (prefabRoot != null) PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            return;
        }

        Undo.RecordObject(selected, undoName);
        if (!configure(selected, false)) return;
        EditorSceneManager.MarkSceneDirty(selected.scene);
        Debug.Log($"[LocalizationSetupTool] Updated scene object '{selected.name}'.");
    }

    private static T AddComponent<T>(GameObject target, bool isPrefab) where T : Component
    {
        return isPrefab ? target.AddComponent<T>() : Undo.AddComponent<T>(target);
    }

    private static void SetStringProperty(UnityEngine.Object target, string propertyName, string value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"[LocalizationSetupTool] Serialized field '{propertyName}' was not found on '{target.name}'.", target);
            return;
        }

        property.stringValue = value ?? string.Empty;
        serialized.ApplyModifiedProperties();
    }
}
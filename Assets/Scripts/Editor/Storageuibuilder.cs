#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Đặt file này vào Assets/Editor/.
///
///   Tools/Storage UI/1. Create Prefab            -> tạo Assets/Prefabs/StorageWindowUI.prefab
///   Tools/Storage UI/2. Setup For Selected Chest -> chọn rương (có InventoryStorage) rồi bấm:
///                                                   đặt UI vào scene (nếu chưa có) + gán biến "window" cho rương.
///
/// Trong prefab có object "SlotTemplate" (đã TẮT enable). Đó là ô mẫu: bạn gán sprite khung,
/// chỉnh viền chọn, font... ở đó, các ô trong game sẽ clone y hệt.
/// </summary>
public static class StorageUIBuilder
{
    const string PrefabFolder = "Assets/Prefabs";
    const string PrefabPath = PrefabFolder + "/StorageWindowUI.prefab";

    static readonly Vector2 SlotSize = new Vector2(88f, 88f);
    static readonly Vector2 SlotSpacing = new Vector2(6f, 6f);
    const int Columns = 6;
    const int Rows = 4;

    // ------------------------------------------------------------
    [MenuItem("Tools/Storage UI/1. Create Prefab")]
    public static void CreatePrefab()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        GameObject root = BuildUI();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log("[StorageUIBuilder] Đã tạo prefab: " + PrefabPath);
    }

    static void EnsureInteractionPromptInPrefab()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            StorageWindowUI window = root.GetComponent<StorageWindowUI>();
            if (window == null) return;

            SerializedObject serializedWindow = new SerializedObject(window);
            SerializedProperty promptProperty = serializedWindow.FindProperty("interactionPromptText");
            if (promptProperty == null || promptProperty.objectReferenceValue != null) return;

            Transform promptTransform = root.transform.Find("ChestInteractionPrompt");
            TMP_Text prompt = promptTransform != null ? promptTransform.GetComponent<TMP_Text>() : null;
            if (prompt == null)
            {
                prompt = NewText("ChestInteractionPrompt", root.transform,
                    "Press 'E' to interact", 30, TextAlignmentOptions.Center);
                Anchor(prompt.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0, 145), new Vector2(600, 64));
                prompt.color = Color.white;
            }

            prompt.gameObject.SetActive(false);
            promptProperty.objectReferenceValue = prompt;
            serializedWindow.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.Refresh();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Storage UI/2. Setup For Selected Chest")]
    public static void SetupForSelected()
    {
        InventoryStorage storage = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<InventoryStorage>()
            : null;

        if (storage == null)
        {
            EditorUtility.DisplayDialog("Storage UI",
                "Hãy chọn object có component InventoryStorage trong Hierarchy trước.", "OK");
            return;
        }

        StorageWindowUI window = FindWindowInScene();
        if (window == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                CreatePrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            GameObject ui = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(ui, "Create Storage Window");
            window = ui.GetComponent<StorageWindowUI>();
            EnsureEventSystem();
        }

        SerializedObject so = new SerializedObject(storage);
        SerializedProperty p = so.FindProperty("window");
        if (p != null) p.objectReferenceValue = window;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(storage);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(storage.gameObject.scene);
        Selection.activeGameObject = window.gameObject;
        Debug.Log("[StorageUIBuilder] Đã gán StorageWindowUI cho " + storage.name
            + ". Nhớ kéo nút Menu vào 'Hide While Open' của StorageWindowUI.");
    }

    [MenuItem("Tools/Storage UI/3. Add Window To CoreCombatDemo")]
    public static void AddWindowToMainGameplayScene()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Storage UI", "Dừng Play Mode trước khi chỉnh scene.", "OK");
            return;
        }

        EnsureInteractionPromptInPrefab();

        const string scenePath = "Assets/Scenes/Zones/CoreCombatDemo.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        Scene previousActiveScene = SceneManager.GetActiveScene();
        if (openedHere)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        StorageWindowUI existing = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<StorageWindowUI>(true))
            .FirstOrDefault();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog("Storage UI", "CoreCombatDemo đã có StorageWindowUI.", "OK");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Storage UI", "Không tìm thấy prefab " + PrefabPath + ". Hãy tạo prefab trước.", "OK");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            EditorUtility.DisplayDialog("Storage UI", "Không thể thêm prefab vào CoreCombatDemo.", "OK");
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Add Storage Window To CoreCombatDemo");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);
        Selection.activeGameObject = instance;
        Debug.Log("[StorageUIBuilder] Đã thêm StorageWindowUI vào CoreCombatDemo. Các chest trong map sẽ tự kết nối khi map được nạp Additive.");
    }

    static StorageWindowUI FindWindowInScene()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<StorageWindowUI>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<StorageWindowUI>(true);
#endif
    }

    static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        bool has = Object.FindFirstObjectByType<EventSystem>() != null;
#else
        bool has = Object.FindObjectOfType<EventSystem>() != null;
#endif
        if (has) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
    }

    // ------------------------------------------------------------
    // Dựng cây UI
    // ------------------------------------------------------------
    static GameObject BuildUI()
    {
        GameObject root = new GameObject("StorageWindowUI", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        StorageWindowUI window = root.AddComponent<StorageWindowUI>();
        TMP_Text interactionPrompt = NewText("ChestInteractionPrompt", root.transform,
            "Press 'E' to interact", 30, TextAlignmentOptions.Center);
        Anchor(interactionPrompt.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 145), new Vector2(600, 64));
        interactionPrompt.color = Color.white;
        interactionPrompt.gameObject.SetActive(false);

        // ---- Panel ----
        float gridW = Columns * SlotSize.x + (Columns - 1) * SlotSpacing.x;   // 558
        float gridH = Rows * SlotSize.y + (Rows - 1) * SlotSpacing.y;         // 370

        GameObject panel = NewUI("Panel", root.transform);
        panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.96f);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        Center(panelRt, new Vector2(gridW * 2 + 120f, 720f));

        TMP_Text playerTitle = NewText("PlayerTitle", panel.transform, "Túi đồ", 34, TextAlignmentOptions.Left);
        Anchor(playerTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -20), new Vector2(gridW, 50));

        TMP_Text chestTitle = NewText("ChestTitle", panel.transform, "Rương", 34, TextAlignmentOptions.Left);
        Anchor(chestTitle.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -20), new Vector2(gridW, 50));

        RectTransform playerGrid = NewGrid("PlayerGrid", panel.transform, new Vector2(0, 1), new Vector2(40, -80), new Vector2(gridW, gridH));
        RectTransform chestGrid = NewGrid("ChestGrid", panel.transform, new Vector2(1, 1), new Vector2(-40, -80), new Vector2(gridW, gridH));

        Button storeAll = NewButton("StoreAllButton", panel.transform, "Bỏ tất cả vào rương",
            new Color(0.25f, 0.4f, 0.7f), new Vector2(0, 1), new Vector2(40, -465), new Vector2(gridW, 55), 26);
        Button takeAll = NewButton("TakeAllButton", panel.transform, "Lấy tất cả",
            new Color(0.2f, 0.55f, 0.3f), new Vector2(1, 1), new Vector2(-40, -465), new Vector2(gridW, 55), 26);
        Button close = NewButton("CloseButton", panel.transform, "X",
            new Color(0.7f, 0.2f, 0.2f), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(44, 44), 24);

        TMP_Text message = NewText("MessageText", panel.transform, string.Empty, 24, TextAlignmentOptions.Center);
        message.color = new Color(1f, 0.85f, 0.4f);
        Anchor(message.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -530), new Vector2(gridW * 2, 34));

        TMP_Text info = NewText("InfoText", panel.transform,
            "Click ô để xem thông tin.", 24, TextAlignmentOptions.TopLeft);
        info.richText = true;
        info.overflowMode = TextOverflowModes.Ellipsis;
        Anchor(info.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(gridW * 2, 110));

        // ---- Ô mẫu (TẮT enable) ----
        StorageSlotUI template = BuildSlotTemplate(panel.transform);
        template.gameObject.SetActive(false);

        // ---- Gán biến cho StorageWindowUI ----
        SerializedObject so = new SerializedObject(window);
        Ref(so, "panel", panel);
        Ref(so, "playerGridRoot", playerGrid);
        Ref(so, "chestGridRoot", chestGrid);
        Ref(so, "playerTitleText", playerTitle);
        Ref(so, "chestTitleText", chestTitle);
        Ref(so, "infoText", info);
        Ref(so, "messageText", message);
        Ref(so, "takeAllButton", takeAll);
        Ref(so, "storeAllButton", storeAll);
        Ref(so, "closeButton", close);
        Ref(so, "slotTemplate", template);
        Ref(so, "interactionPromptText", interactionPrompt);
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    /// <summary>Ô mẫu: khung + icon + tên + số lượng + nhãn trang bị + viền chọn + nút Lấy/Bỏ ra.</summary>
    static StorageSlotUI BuildSlotTemplate(Transform parent)
    {
        GameObject slotGo = NewUI("SlotTemplate", parent);
        Image frame = slotGo.AddComponent<Image>();          // <- gán sprite khung ô ở đây
        frame.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        Center(slotGo.GetComponent<RectTransform>(), SlotSize);
        StorageSlotUI slot = slotGo.AddComponent<StorageSlotUI>();

        // Icon vật phẩm
        GameObject iconGo = NewUI("Icon", slotGo.transform);
        Image icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Stretch(iconGo.GetComponent<RectTransform>(), new Vector2(10, 40), new Vector2(-10, -10));

        // Tên
        TMP_Text nameText = NewText("NameText", slotGo.transform, string.Empty, 11, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 7;
        nameText.fontSizeMax = 11;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform nr = nameText.rectTransform;
        nr.anchorMin = new Vector2(0, 0);
        nr.anchorMax = new Vector2(1, 0);
        nr.offsetMin = new Vector2(2, 22);
        nr.offsetMax = new Vector2(-2, 40);

        // Số lượng
        TMP_Text amount = NewText("AmountText", slotGo.transform, string.Empty, 15, TextAlignmentOptions.TopRight);
        amount.fontStyle = FontStyles.Bold;
        Anchor(amount.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -3), new Vector2(46, 20));

        // Nhãn đang trang bị
        GameObject badge = NewUI("EquippedBadge", slotGo.transform);
        Image badgeImg = badge.AddComponent<Image>();
        badgeImg.color = new Color(0.2f, 0.7f, 0.3f, 1f);
        badgeImg.raycastTarget = false;
        Anchor(badge.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(3, -3), new Vector2(22, 18));
        TMP_Text badgeText = NewText("Text", badge.transform, "E", 13, TextAlignmentOptions.Center);
        badgeText.fontStyle = FontStyles.Bold;
        Stretch(badgeText.rectTransform, Vector2.zero, Vector2.zero);
        badge.SetActive(false);

        // Nút Lấy / Bỏ ra
        GameObject btnGo = NewUI("ActionButton", slotGo.transform);
        Image btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.45f, 0.6f, 1f);
        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        RectTransform br = btnGo.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0, 0);
        br.anchorMax = new Vector2(1, 0);
        br.offsetMin = new Vector2(4, 3);
        br.offsetMax = new Vector2(-4, 21);
        TMP_Text btnLabel = NewText("Label", btnGo.transform, "Lấy", 12, TextAlignmentOptions.Center);
        Stretch(btnLabel.rectTransform, Vector2.zero, Vector2.zero);

        // Viền khi được chọn (4 thanh mỏng, vẽ trên cùng). Có thể thay bằng 1 ảnh viền riêng.
        GameObject border = NewUI("SelectedBorder", slotGo.transform);
        Stretch(border.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        Color borderColor = new Color(1f, 0.85f, 0.2f, 1f);
        const float t = 3f;
        Bar(border.transform, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, t), borderColor);
        Bar(border.transform, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, t), borderColor);
        Bar(border.transform, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(t, 0), borderColor);
        Bar(border.transform, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(t, 0), borderColor);
        border.SetActive(false);

        // Gán biến cho StorageSlotUI
        SerializedObject so = new SerializedObject(slot);
        Ref(so, "frameImage", frame);
        Ref(so, "iconImage", icon);
        Ref(so, "nameText", nameText);
        Ref(so, "amountText", amount);
        Ref(so, "equippedBadge", badge);
        Ref(so, "selectedBorder", border);
        Ref(so, "actionButton", btn);
        Ref(so, "actionLabel", btnLabel);
        so.ApplyModifiedPropertiesWithoutUndo();

        return slot;
    }

    // ------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------
    static void Ref(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("[StorageUIBuilder] Không thấy biến: " + field); return; }
        p.objectReferenceValue = value;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static TMP_Text NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static Button NewButton(string name, Transform parent, string label, Color color,
        Vector2 anchor, Vector2 pos, Vector2 size, float fontSize)
    {
        GameObject go = NewUI(name, parent);
        Image img = go.AddComponent<Image>();
        img.color = color;
        Button b = go.AddComponent<Button>();
        b.targetGraphic = img;
        Anchor(go.GetComponent<RectTransform>(), anchor, anchor, pos, size);

        TMP_Text t = NewText("Label", go.transform, label, fontSize, TextAlignmentOptions.Center);
        Stretch(t.rectTransform, Vector2.zero, Vector2.zero);
        return b;
    }

    static RectTransform NewGrid(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = NewUI(name, parent);
        go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
        RectTransform rt = go.GetComponent<RectTransform>();
        Anchor(rt, anchor, anchor, pos, size);

        GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = SlotSize;
        grid.spacing = SlotSpacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        grid.childAlignment = TextAnchor.UpperLeft;
        return rt;
    }

    static void Bar(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 sizeDelta, Color color)
    {
        GameObject go = NewUI(name, parent);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
    }

    static void Center(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = aMin;     // pivot trùng neo cho dễ đặt vị trí
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
#endif

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class DemoPauseMenu : MonoBehaviour
{
    [Header("Scene UI")]
    [SerializeField] private Button openMenuButton;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button closeMenuButton;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InventoryGridUI inventoryGridUI;
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("World map")]
    [SerializeField] private MapZoneGraphic mapGraphic;
    [SerializeField] private Transform mapTarget;
    [SerializeField, Min(1f)] private float mapCameraScale = 3f;
    [SerializeField] private bool mapEnabled = true;

    private enum OverlayMode { None, Settings, Inventory }
    private OverlayMode mode;
    private bool menuButtonInitiallyInteractable;
    private float mapRefreshTimer;
    private Vector2 lastMapPlayerPosition;
    private bool hasMapPlayerPosition;
    private PlayerStatusHUD playerStatusHUD;
    private MiniMapCameraView miniMapCameraView;
    private Camera mainCamera;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        openMenuButton.onClick.AddListener(OpenSettings);
        menuButtonInitiallyInteractable = openMenuButton.interactable;
        closeMenuButton.onClick.AddListener(CloseMenu);
        EnsureLanguageManager();

        inventoryPanel.SetActive(false);
        settingsPanel.SetActive(false);
        mapPanel.SetActive(mapEnabled);
        if (mapEnabled)
        {
            ConfigureMapOverlay();
            RefreshMap();
        }
        playerStatusHUD = PlayerStatusHUD.Create(GetCanvas());
        if (playerStatusHUD != null) playerStatusHUD.SetVisible(false);
        menuRoot.SetActive(false);
        mode = OverlayMode.None;
    }

    private void Update()
    {
        if (openMenuButton != null)
            openMenuButton.interactable = menuButtonInitiallyInteractable && !StorageWindowUI.AnyOpen;

        if (!StorageWindowUI.AnyOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (mode == OverlayMode.Settings) CloseMenu();
                else OpenSettings();
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (mode == OverlayMode.Inventory) CloseMenu();
                else OpenInventory();
            }
        }

        if (!mapEnabled) return;

        mapRefreshTimer += Time.unscaledDeltaTime;
        if (mapRefreshTimer >= 0.1f)
        {
            mapRefreshTimer = 0f;
            if (mapTarget == null) mapTarget = FindPlayer();
            if (mapTarget != null && (!hasMapPlayerPosition ||
                ((Vector2)mapTarget.position - lastMapPlayerPosition).sqrMagnitude >= 0.0025f))
                RefreshMap();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (mode == OverlayMode.None) return;
        mode = OverlayMode.None;
        GameplayPause.Resume();
    }

    private bool HasRequiredReferences()
    {

        System.Collections.Generic.List<string> missing = new System.Collections.Generic.List<string>();
        if (openMenuButton == null) missing.Add("Open Menu Button");
        if (menuRoot == null) missing.Add("Paused Menu Overlay");
        if (closeMenuButton == null) missing.Add("Close Menu Button");
        if (inventoryPanel == null) missing.Add("Inventory Content");
        if (mapPanel == null) missing.Add("Map Content");
        if (settingsPanel == null) missing.Add("Settings Content");
        if (mapGraphic == null) missing.Add("Map Graphic");

        if (missing.Count > 0)
        {
            Debug.LogError("[DemoPauseMenu] Could not find required UI references in scene '" +
                gameObject.scene.name + "': " + string.Join(", ", missing) +
                ". Ensure the prebuilt menu UI is in this scene and object names match.", this);
            return false;
        }
        return true;
    }

    private Canvas GetCanvas()
    {
        Canvas canvas = menuRoot.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        return canvas;
    }

    private void ConfigureMapOverlay()
    {
        mapPanel.SetActive(true);

        Image panelImage = mapPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.enabled = false;
            panelImage.raycastTarget = false;
        }

        Transform viewport = mapPanel.transform.Find("Map Viewport");
        if (viewport == null)
        {
            GameObject viewportObject = new GameObject("Map Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport = viewportObject.transform;
            viewport.SetParent(mapPanel.transform, false);
        }
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        RectMask2D viewportMask = viewport.GetComponent<RectMask2D>();
        if (viewportMask == null) viewportMask = viewport.gameObject.AddComponent<RectMask2D>();
        viewportMask.padding = Vector4.zero;
        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.enabled = false;
            viewportImage.raycastTarget = false;
        }

        Transform imageTransform = viewport.Find("Map Camera Image");
        if (imageTransform == null)
        {
            GameObject imageObject = new GameObject("Map Camera Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageTransform = imageObject.transform;
            imageTransform.SetParent(viewport, false);
        }
        RectTransform imageRect = imageTransform.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        imageTransform.SetAsFirstSibling();

        mapGraphic.transform.SetParent(viewport, false);
        RectTransform graphicRect = mapGraphic.rectTransform;
        graphicRect.anchorMin = Vector2.zero;
        graphicRect.anchorMax = Vector2.one;
        graphicRect.offsetMin = Vector2.zero;
        graphicRect.offsetMax = Vector2.zero;
        mapGraphic.transform.SetAsLastSibling();
        mapGraphic.raycastTarget = false;

        RawImage mapImage = imageTransform.GetComponent<RawImage>();
        miniMapCameraView = mapPanel.GetComponent<MiniMapCameraView>();
        if (miniMapCameraView == null) miniMapCameraView = mapPanel.AddComponent<MiniMapCameraView>();
        mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();
        miniMapCameraView.Configure(mainCamera, mapTarget != null ? mapTarget : FindPlayer(), mapImage, mapCameraScale);
    }

    private void RefreshMap()
    {
        if (!mapEnabled || mapGraphic == null) return;
        if (mapTarget == null) mapTarget = FindPlayer();
        if (miniMapCameraView != null) miniMapCameraView.SetTarget(mapTarget);
        float radius = mainCamera != null && mainCamera.orthographic
            ? mainCamera.orthographicSize * mapCameraScale : 32f;
        if (mapTarget != null)
        {
            lastMapPlayerPosition = mapTarget.position;
            hasMapPlayerPosition = true;
        }
        mapGraphic.Refresh(mapTarget, radius);
    }
    private Transform FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (!mapEnabled) return;
        mapTarget = FindPlayer();
        if (mapGraphic != null) mapGraphic.InvalidateCache();
        if (miniMapCameraView != null) miniMapCameraView.SetTarget(mapTarget);
        RefreshMap();
    }

    private void EnsureLanguageManager()
    {
        if (transition.IsReady) return;
        transition existing = FindFirstObjectByType<transition>(FindObjectsInactive.Include);
        if (existing == null) new GameObject("Language Manager").AddComponent<transition>();
    }

    private void ShowOverlay(OverlayMode nextMode)
    {
        if (StorageWindowUI.AnyOpen) return;
        if (mode == OverlayMode.None) GameplayPause.Pause();

        mode = nextMode;
        menuRoot.SetActive(true);
        inventoryPanel.SetActive(nextMode == OverlayMode.Inventory);
        if (playerStatusHUD != null) playerStatusHUD.SetVisible(nextMode == OverlayMode.Inventory);
        settingsPanel.SetActive(nextMode == OverlayMode.Settings);
        if (nextMode == OverlayMode.Inventory && inventoryGridUI != null)
            inventoryGridUI.RefreshInventory();
    }

    private void OpenSettings()
    {
        ShowOverlay(OverlayMode.Settings);
    }

    public void OpenInventory()
    {
        ShowOverlay(OverlayMode.Inventory);
    }

    private void CloseMenu()
    {
        if (mode == OverlayMode.None) return;
        mode = OverlayMode.None;
        menuRoot.SetActive(false);
        inventoryPanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (playerStatusHUD != null) playerStatusHUD.SetVisible(false);
        GameplayPause.Resume();
    }
}









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
    [SerializeField] private MiniMapCameraView miniMapCameraView;
    [SerializeField] private Transform mapTarget;
    [SerializeField, Min(1f)] private float mapCameraScale = 3f;
    [SerializeField] private bool mapEnabled = true;

    private enum OverlayMode { None, Settings, Inventory }
    private OverlayMode mode;
    private bool menuButtonInitiallyInteractable;
    private PlayerStatusHUD playerStatusHUD;
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

        inventoryPanel.SetActive(false);
        settingsPanel.SetActive(false);
        mapPanel.SetActive(false);

        playerStatusHUD = PlayerStatusHUD.Create(GetCanvas());
        if (playerStatusHUD != null) playerStatusHUD.SetVisible(false);
        menuRoot.SetActive(false);
        mode = OverlayMode.None;
    }

    private void Update()
    {
        if (openMenuButton != null)
            openMenuButton.interactable = menuButtonInitiallyInteractable && !StorageWindowUI.AnyOpen;

        if (StorageWindowUI.AnyOpen) return;

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (mapPanel != null) mapPanel.SetActive(false);
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
        if (mapEnabled && mapGraphic == null) missing.Add("Map Graphic");
        if (mapEnabled && miniMapCameraView == null) missing.Add("MiniMap Camera View");

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
        if (!mapEnabled || miniMapCameraView == null) return;

        Image panelImage = mapPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.enabled = false;
            panelImage.raycastTarget = false;
        }

        if (mapTarget == null) mapTarget = FindPlayer();
        mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();

        miniMapCameraView.Configure(mainCamera, mapTarget, mapCameraScale);
    }

    private void RefreshMap()
    {
        if (!mapEnabled || mode != OverlayMode.Inventory || mapGraphic == null) return;
        if (mapTarget == null) mapTarget = FindPlayer();
        if (miniMapCameraView != null) miniMapCameraView.SetTarget(mapTarget);

        float radius = mainCamera != null && mainCamera.orthographic
            ? mainCamera.orthographicSize * mapCameraScale : 32f;
        mapGraphic.Refresh(mapTarget, radius);
    }

    private Transform FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (!mapEnabled || mode != OverlayMode.Inventory) return;

        mapTarget = FindPlayer();
        if (mapGraphic != null) mapGraphic.InvalidateCache();
        ConfigureMapOverlay();
        RefreshMap();
    }

    private void ShowOverlay(OverlayMode nextMode)
    {
        if (StorageWindowUI.AnyOpen) return;
        if (mode == OverlayMode.None) GameplayPause.Pause();

        mode = nextMode;
        menuRoot.SetActive(true);
        inventoryPanel.SetActive(nextMode == OverlayMode.Inventory);
        settingsPanel.SetActive(nextMode == OverlayMode.Settings);
        mapPanel.SetActive(mapEnabled && nextMode == OverlayMode.Inventory);
        if (playerStatusHUD != null) playerStatusHUD.SetVisible(nextMode == OverlayMode.Inventory);

        if (nextMode == OverlayMode.Inventory)
        {
            if (mapEnabled)
            {
                ConfigureMapOverlay();
                RefreshMap();
            }
            if (inventoryGridUI != null) inventoryGridUI.RefreshInventory();
        }
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
        mapPanel.SetActive(false);
        menuRoot.SetActive(false);
        inventoryPanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (playerStatusHUD != null) playerStatusHUD.SetVisible(false);
        GameplayPause.Resume();
    }
}

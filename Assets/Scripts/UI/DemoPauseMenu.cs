using UnityEngine;
using UnityEngine.UI;

public class DemoPauseMenu : MonoBehaviour
{
    [Header("Scene UI (create and edit in the Hierarchy)")]
    [SerializeField] private Button openMenuButton;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button closeMenuButton;
    [SerializeField] private Button inventoryTabButton;
    [SerializeField] private Button mapTabButton;
    [SerializeField] private Button settingsTabButton;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InventoryGridUI inventoryGridUI;
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("World map UI")]
    [SerializeField] private MapZoneGraphic mapGraphic;
    [SerializeField] private Transform mapTarget;
    [SerializeField, Min(1f)] private float mapWorldRadius = 8f;

    [Header("Settings UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;

    private bool menuOpen;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        openMenuButton.onClick.AddListener(OpenMenu);
        closeMenuButton.onClick.AddListener(CloseMenu);
        inventoryTabButton.onClick.AddListener(() => SetTab(MenuTab.Inventory));
        mapTabButton.onClick.AddListener(() => SetTab(MenuTab.Map));
        settingsTabButton.onClick.AddListener(() => SetTab(MenuTab.Settings));

        masterVolumeSlider.onValueChanged.AddListener(value => AudioListener.volume = value);
        fullscreenToggle.onValueChanged.AddListener(value => Screen.fullScreen = value);
        masterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        menuRoot.SetActive(false);
    }

    private void Update()
    {
        if (menuOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMenu();
        }
    }

    private void OnDisable()
    {
        if (!menuOpen) return;

        menuOpen = false;
        GameplayPause.Resume();
    }

    private bool HasRequiredReferences()
    {
        bool valid = openMenuButton != null
            && menuRoot != null
            && closeMenuButton != null
            && inventoryTabButton != null
            && mapTabButton != null
            && settingsTabButton != null
            && inventoryPanel != null
            && mapPanel != null
            && settingsPanel != null
            && mapGraphic != null
            && masterVolumeSlider != null
            && fullscreenToggle != null;

        if (!valid)
        {
            Debug.LogError("[DemoPauseMenu] Assign the prebuilt menu UI references in the Inspector.", this);
        }

        return valid;
    }

    private void OpenMenu()
    {
        if (menuOpen) return;

        menuOpen = true;
        GameplayPause.Pause();
        menuRoot.SetActive(true);
        SetTab(MenuTab.Inventory);
    }

    private void CloseMenu()
    {
        if (!menuOpen) return;

        menuOpen = false;
        menuRoot.SetActive(false);
        GameplayPause.Resume();
    }

    public void OpenInventory()
    {
        if (!menuOpen)
        {
            menuOpen = true;
            GameplayPause.Pause();
            menuRoot.SetActive(true);
        }

        SetTab(MenuTab.Inventory);
    }

    private void SetTab(MenuTab tab)
    {
        inventoryPanel.SetActive(tab == MenuTab.Inventory);
        mapPanel.SetActive(tab == MenuTab.Map);
        settingsPanel.SetActive(tab == MenuTab.Settings);

        if (tab == MenuTab.Inventory)
        {
            RefreshInventoryPanel();
        }
        else if (tab == MenuTab.Map)
        {
            Transform target = mapTarget != null ? mapTarget : FindPlayer();
            Canvas.ForceUpdateCanvases();
            mapGraphic.Refresh(target, mapWorldRadius);
        }
    }

    private void RefreshInventoryPanel()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null) return;

        if (inventoryGridUI != null) inventoryGridUI.RefreshInventory();
    }

    private Transform FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private enum MenuTab
    {
        Inventory,
        Map,
        Settings
    }
}

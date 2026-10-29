using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Text saveStatusText;
    [SerializeField] private string gameSceneName = "CoreCombatDemo";
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsOverlay;
    [SerializeField] private Button closeSettingsButton;

    private void Start()
    {
        bool hasSave = DataManager.HasSavedGame();
        if (continueButton != null) continueButton.interactable = hasSave;
        if (saveStatusText != null)
            saveStatusText.text = hasSave ? "\u0110\u00E3 t\u00ECm th\u1EA5y d\u1EEF li\u1EC7u h\u00E0nh tr\u00ECnh." : "Ch\u01B0a c\u00F3 d\u1EEF li\u1EC7u. H\u00E3y b\u1EAFt \u0111\u1EA7u h\u00E0nh tr\u00ECnh m\u1EDBi.";

        if (newGameButton == null || continueButton == null)
        {
            Debug.LogError("[MainMenuController] Assign New Game and Continue buttons.", this);
            enabled = false;
            return;
        }

        if (settingsOverlay != null) settingsOverlay.SetActive(false);
        newGameButton.onClick.AddListener(StartNewGame);
        continueButton.onClick.AddListener(ContinueGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettings);
    }

    private void Update()
    {
        if (settingsOverlay != null && settingsOverlay.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseSettings();
    }

    public void OpenSettings()
    {
        if (settingsOverlay != null) settingsOverlay.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsOverlay != null) settingsOverlay.SetActive(false);
    }

    public void StartNewGame()
    {
        PlayerProgressionService.BeginNewGame();
        LoadGame(gameSceneName, false);
    }

    public void ContinueGame()
    {
        if (!DataManager.HasSavedGame())
        {
            Debug.LogWarning("[MainMenuController] No saved game is available to continue.", this);
            if (continueButton != null) continueButton.interactable = false;
            return;
        }

        if (!DataManager.TryGetData(Tables.Player, out PlayerSaveData save) || save == null)
        {
            Debug.LogWarning("[MainMenuController] The saved game could not be read.", this);
            if (continueButton != null) continueButton.interactable = false;
            return;
        }
        if (!string.IsNullOrWhiteSpace(save.languageCode) && transition.IsReady)
            transition.SetLanguage(save.languageCode);
        string savedZone = save != null ? save.currentZone : "";
        Debug.Log($"[MainMenuController] Continue: loading '{gameSceneName}'; map will be selected from currentZone '{savedZone}'.", this);
        LoadGame(gameSceneName, true);
    }

    private void LoadGame(string sceneName, bool isContinue)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[MainMenuController] Scene '{sceneName}' is not in Build Settings.", this);
            return;
        }

        if (isContinue) PlayerProgressionService.BeginContinueRestore();
        SceneManager.LoadScene(sceneName);
    }
}

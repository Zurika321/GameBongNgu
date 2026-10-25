using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Text saveStatusText;
    [SerializeField] private string gameSceneName = "CoreCombatDemo";

    private void Start()
    {
        bool hasSave = DataManager.HasSavedGame();
        if (continueButton != null) continueButton.interactable = hasSave;
        if (saveStatusText != null)
            saveStatusText.text = hasSave ? "Đã tìm thấy dữ liệu hành trình." : "Chưa có dữ liệu. Hãy bắt đầu hành trình mới.";

        if (newGameButton == null || continueButton == null)
        {
            Debug.LogError("[MainMenuController] Assign New Game and Continue buttons.", this);
            enabled = false;
            return;
        }

        newGameButton.onClick.AddListener(StartNewGame);
        continueButton.onClick.AddListener(ContinueGame);
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

        DataManager.LoadData(Tables.Player);
        PlayerSaveData save = DataManager.GetData<PlayerSaveData>(Tables.Player);
        // CoreCombatDemo is the persistent root. PlayerProgressionService reads
        // currentZone after CoreCombatDemo loads and adds the matching map scene.
        string savedZone = save != null ? save.currentZone : "";
        // string savedScene = save != null ? save.currentScene : "";
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

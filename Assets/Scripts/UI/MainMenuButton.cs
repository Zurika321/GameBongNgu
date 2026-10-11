using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuButton : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";
    public void GoToMainMenu()
    {
        GameplayPause.Resume();
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }
}



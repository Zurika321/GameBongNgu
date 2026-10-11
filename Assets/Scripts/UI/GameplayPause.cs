using UnityEngine;

public static class GameplayPause
{
    public static bool IsPaused { get; private set; }

    private static float previousTimeScale = 1f;

    public static void Pause()
    {
        if (IsPaused) return;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsPaused = true;
    }

    public static void Resume()
    {
        if (!IsPaused) return;

        Time.timeScale = previousTimeScale;
        IsPaused = false;
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Centralizes scene names and common loads. Extend to support fades/loading screens.
/// </summary>
public static class SceneRouter
{
    public const string BootScene = "00_Boot";
    public const string MenuScene = "01_Menu";
    public const string GameplayScene = "02_Gameplay";

    public static void Load(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public static void LoadMenu()
    {
        Load(MenuScene);
    }

    public static void LoadGameplay(WorldDefinition world)
    {
        if (world) WorldSelection.Select(world);
        Load(GameplayScene);
    }

    public static void Reload()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}


using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple global selection for which WorldDefinition to play.
/// Lives across scenes only as static memory for now.
/// </summary>
public static class WorldSelection
{
    public static WorldDefinition Selected { get; private set; }

    public static bool HasSelection => Selected != null;

    public static void Select(WorldDefinition world)
    {
        Selected = world;
    }

    public static void Clear()
    {
        Selected = null;
    }

    /// <summary>
    /// Loads the gameplay scene using the selected world.
    /// If no world is selected, the gameplay scene will use whatever is configured in-scene.
    /// </summary>
    public static void StartGame(string gameplaySceneName = "02_Gameplay")
    {
        SceneManager.LoadScene(gameplaySceneName);
    }
}


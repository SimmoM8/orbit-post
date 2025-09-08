using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Minimal bootstrap that applies saved settings and routes to the Menu.
/// Place this on an object in 00_Boot.
/// </summary>
public class BootLoader : MonoBehaviour
{
    [Tooltip("Override next scene; empty uses SceneRouter.MenuScene")] public string nextSceneOverride = "";
    [Tooltip("Apply saved audio volumes on boot")] public bool applySavedAudio = true;

    void Awake()
    {
        // Keep Boot scene lean and instant.
        if (applySavedAudio)
        {
            float music = PlayerPrefs.GetFloat("musicVolume", 0.8f);
            float sfx = PlayerPrefs.GetFloat("sfxVolume", 0.8f);
            AudioListener.volume = Mathf.Clamp01(Mathf.Max(music, sfx));
        }

        var scene = string.IsNullOrEmpty(nextSceneOverride) ? SceneRouter.MenuScene : nextSceneOverride;
        SceneRouter.Load(scene);
    }
}


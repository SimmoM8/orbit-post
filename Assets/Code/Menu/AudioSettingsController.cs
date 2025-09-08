using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives music and sfx volume sliders. Persists values in PlayerPrefs and applies a simple default.
/// If you later add an AudioMixer, you can extend this to set mixer parameters.
/// </summary>
public class AudioSettingsController : MonoBehaviour
{
    [Header("UI")]
    public Slider musicSlider;
    public Slider sfxSlider;

    const string MusicKey = "musicVolume";
    const string SfxKey = "sfxVolume";

    void Start()
    {
        float music = PlayerPrefs.GetFloat(MusicKey, 0.8f);
        float sfx = PlayerPrefs.GetFloat(SfxKey, 0.8f);
        if (musicSlider)
        {
            musicSlider.value = music;
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }
        if (sfxSlider)
        {
            sfxSlider.value = sfx;
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        }
        ApplyVolumes(music, sfx);
    }

    public void SetMusicVolume(float v)
    {
        PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(v));
        ApplyVolumes(GetMusic(), GetSfx());
    }

    public void SetSfxVolume(float v)
    {
        PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(v));
        ApplyVolumes(GetMusic(), GetSfx());
    }

    float GetMusic() => PlayerPrefs.GetFloat(MusicKey, 0.8f);
    float GetSfx() => PlayerPrefs.GetFloat(SfxKey, 0.8f);

    void ApplyVolumes(float music, float sfx)
    {
        // Minimal default: drive master volume by the max of the two.
        AudioListener.volume = Mathf.Clamp01(Mathf.Max(music, sfx));
        // If/when you add an AudioMixer, set group parameters here.
    }
}


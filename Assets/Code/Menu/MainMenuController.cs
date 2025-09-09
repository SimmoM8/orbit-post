using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the launch menu flow: Home, Worlds, Settings, Highscores (stub).
/// Wire this up in the 01_Menu scene. Assign the panels and container fields in the Inspector.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject homePanel;
    public GameObject worldsPanel;
    public GameObject settingsPanel;
    public GameObject highscoresPanel; // placeholder for now

    [Header("Home Buttons")]
    public Button playButton;
    public Button highscoresButton;
    public Button settingsButton;

    [Header("Worlds List")]
    public WorldCatalog worldCatalog;
    public RectTransform worldListContainer; // parent to populate with world items
    public GameObject worldListItemPrefab;   // a simple Button with a Text label

    [Header("Common")]
    public Button backButtonWorlds;
    public Button backButtonSettings;
    public Button backButtonHighscores;

    readonly List<GameObject> _spawnedWorldItems = new();

    void Awake()
    {
        if (playButton) playButton.onClick.AddListener(ShowWorlds);
        if (highscoresButton) highscoresButton.onClick.AddListener(ShowHighscores);
        if (settingsButton) settingsButton.onClick.AddListener(ShowSettings);

        if (backButtonWorlds) backButtonWorlds.onClick.AddListener(ShowHome);
        if (backButtonSettings) backButtonSettings.onClick.AddListener(ShowHome);
        if (backButtonHighscores) backButtonHighscores.onClick.AddListener(ShowHome);
    }

    void Start()
    {
        ShowHome();
        BuildWorldsList();
    }

    void SetPanelActive(GameObject go, bool active)
    {
        if (go) go.SetActive(active);
    }

    public void ShowHome()
    {
        SetPanelActive(homePanel, true);
        SetPanelActive(worldsPanel, false);
        SetPanelActive(settingsPanel, false);
        SetPanelActive(highscoresPanel, false);
    }

    public void ShowWorlds()
    {
        SetPanelActive(homePanel, false);
        SetPanelActive(worldsPanel, true);
        SetPanelActive(settingsPanel, false);
        SetPanelActive(highscoresPanel, false);
        BuildWorldsList();
    }

    public void ShowSettings()
    {
        SetPanelActive(homePanel, false);
        SetPanelActive(worldsPanel, false);
        SetPanelActive(settingsPanel, true);
        SetPanelActive(highscoresPanel, false);
    }

    public void ShowHighscores()
    {
        SetPanelActive(homePanel, false);
        SetPanelActive(worldsPanel, false);
        SetPanelActive(settingsPanel, false);
        SetPanelActive(highscoresPanel, true);
    }

    void BuildWorldsList()
    {
        if (!worldListContainer || !worldListItemPrefab) return;

        // Clear previous
        for (int i = 0; i < _spawnedWorldItems.Count; i++)
        {
            if (_spawnedWorldItems[i]) Destroy(_spawnedWorldItems[i]);
        }
        _spawnedWorldItems.Clear();

        if (!worldCatalog || worldCatalog.worlds == null) return;

        foreach (var world in worldCatalog.worlds)
        {
            if (!world) continue;
            var go = Instantiate(worldListItemPrefab, worldListContainer);
            go.name = $"WorldItem_{world.displayName}";

            // Try to set label on either legacy Text or TMP
            string label = string.IsNullOrEmpty(world.displayName) ? world.name : world.displayName;
            var txt = go.GetComponentInChildren<Text>();
            if (txt) txt.text = label;
            else
            {
                var tmp = go.GetComponentInChildren<TMP_Text>();
                if (tmp) tmp.text = label;
            }

            // Hook up button
            var btn = go.GetComponent<Button>();
            if (btn)
            {
                var captured = world;
                btn.onClick.AddListener(() => OnChooseWorld(captured));
            }

            _spawnedWorldItems.Add(go);
        }
    }

    void OnChooseWorld(WorldDefinition world)
    {
        if (!world) return;
        WorldSelection.Select(world);
        WorldSelection.StartGame();
    }
}

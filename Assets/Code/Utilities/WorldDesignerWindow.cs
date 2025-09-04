#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

// World Designer: place planets visually without entering Play mode.
// Open via: Tools → Orbit Post → World Designer
public class WorldDesignerWindow : EditorWindow
{
    // Selected world asset
    [SerializeField] private WorldDefinition _world;

    // Scene GUI subscription
    private bool _sceneHooked;

    [SerializeField] private Vector2 _scroll;
    [SerializeField] private bool _lockPreview = true; // prevent selecting/moving preview instances
    [SerializeField] private bool _snapEnabled = false;
    [SerializeField] private float _snapSize = 0.5f;
    [SerializeField] private bool _showBounds = true;
    [SerializeField] private bool _showProceduralInfo = false;
    [SerializeField] private System.Collections.Generic.List<int> _selPlanets = new System.Collections.Generic.List<int>();
    [SerializeField] private System.Collections.Generic.List<int> _selPosts = new System.Collections.Generic.List<int>();
    [SerializeField] private bool _filterBySize = false;
    [SerializeField] private PlanetSize _filterSize = PlanetSize.Medium;
    [SerializeField] private bool _filterByType = false;
    [SerializeField] private PlanetType _filterType = PlanetType.Default;
    [SerializeField] private bool _postFilterByName = false;
    [SerializeField] private string _postFilterName = "";
    [SerializeField] private bool _postFilterByMinLevel = false;
    [SerializeField] private int _postFilterMinLevel = 1;
    [SerializeField] private bool _previewPosts = true;
    [SerializeField] private bool _previewPlanets = true;
    [SerializeField] private bool _previewIsolateSelection = false;
    [SerializeField] private bool _livePreview = false;

    // EditorPrefs keys for persistence
    private const string PrefSnapEnabledKey = "WorldDesigner_SnapEnabled";
    private const string PrefSnapSizeKey = "WorldDesigner_SnapSize";
    private const string PrefShowBoundsKey = "WorldDesigner_ShowBounds";
    private const string PrefPreviewPostsKey = "WorldDesigner_PreviewPosts";
    private const string PrefPreviewPlanetsKey = "WorldDesigner_PreviewPlanets";
    private const string PrefPreviewIsolateKey = "WorldDesigner_PreviewIsolate";
    private const string PrefLivePreviewKey = "WorldDesigner_LivePreview";

    // Handle colors by type (editor-only hinting)
    private static readonly Color _typeDefault = new Color(1f, 1f, 1f, 0.8f);
    private static readonly Color _typeFire    = new Color(1f, 0.45f, 0.15f, 0.8f);
    private static readonly Color _typeWater   = new Color(0.2f, 0.6f, 1f, 0.8f);
    private static readonly Color _typeEarth   = new Color(0.4f, 0.8f, 0.4f, 0.8f);
    private static readonly Color _postRing    = new Color(1f, 0.9f, 0.2f, 0.9f);

    // Preview parenting
    private const string PreviewRootName = "__WorldPreview__";

    [MenuItem("Tools/Orbit Post/World Designer")]
    public static void Open()
    {
        var win = GetWindow<WorldDesignerWindow>(false, "World Designer", true);
        win.Show();
    }

    private void OnEnable()
    {
        HookScene(true);
        // Load snap prefs
        if (EditorPrefs.HasKey(PrefSnapEnabledKey))
            _snapEnabled = EditorPrefs.GetBool(PrefSnapEnabledKey, _snapEnabled);
        if (EditorPrefs.HasKey(PrefSnapSizeKey))
            _snapSize = Mathf.Max(0.01f, EditorPrefs.GetFloat(PrefSnapSizeKey, _snapSize));
        if (EditorPrefs.HasKey(PrefShowBoundsKey))
            _showBounds = EditorPrefs.GetBool(PrefShowBoundsKey, _showBounds);
        if (EditorPrefs.HasKey(PrefPreviewPostsKey))
            _previewPosts = EditorPrefs.GetBool(PrefPreviewPostsKey, _previewPosts);
        if (EditorPrefs.HasKey(PrefPreviewPlanetsKey))
            _previewPlanets = EditorPrefs.GetBool(PrefPreviewPlanetsKey, _previewPlanets);
        if (EditorPrefs.HasKey(PrefPreviewIsolateKey))
            _previewIsolateSelection = EditorPrefs.GetBool(PrefPreviewIsolateKey, _previewIsolateSelection);
        if (EditorPrefs.HasKey(PrefLivePreviewKey))
            _livePreview = EditorPrefs.GetBool(PrefLivePreviewKey, _livePreview);
    }

    private void OnDisable()
    {
        HookScene(false);
        // Save snap prefs
        EditorPrefs.SetBool(PrefSnapEnabledKey, _snapEnabled);
        EditorPrefs.SetFloat(PrefSnapSizeKey, Mathf.Max(0.01f, _snapSize));
        EditorPrefs.SetBool(PrefShowBoundsKey, _showBounds);
        EditorPrefs.SetBool(PrefPreviewPostsKey, _previewPosts);
        EditorPrefs.SetBool(PrefPreviewPlanetsKey, _previewPlanets);
        EditorPrefs.SetBool(PrefPreviewIsolateKey, _previewIsolateSelection);
        EditorPrefs.SetBool(PrefLivePreviewKey, _livePreview);
    }

    private void HookScene(bool hook)
    {
        if (hook && !_sceneHooked)
        {
            SceneView.duringSceneGui += OnSceneGUI;
            _sceneHooked = true;
        }
        else if (!hook && _sceneHooked)
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _sceneHooked = false;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            _world = (WorldDefinition)EditorGUILayout.ObjectField("World Definition", _world, typeof(WorldDefinition), false);
            if (GUILayout.Button("Ping", GUILayout.Width(60)) && _world)
                EditorGUIUtility.PingObject(_world);
        }

        // Quick access: ping referenced prefabs/data assets
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!_world))
            {
                if (GUILayout.Button(new GUIContent("Ping Planet Library", "Ping the assigned PlanetPrefabLibrary asset."), GUILayout.Width(160)))
                {
                    if (_world && _world.planetPrefabs) EditorGUIUtility.PingObject(_world.planetPrefabs);
                }
                if (GUILayout.Button(new GUIContent("Ping Post Prefab", "Ping the assigned Post prefab asset."), GUILayout.Width(140)))
                {
                    if (_world && _world.postPrefab) EditorGUIUtility.PingObject(_world.postPrefab);
                }
            }
        }

        if (!_world)
        {
            EditorGUILayout.HelpBox("Assign a WorldDefinition asset to begin.", MessageType.Info);
            return;
        }

        if (_world.planetPrefabs == null)
        {
            EditorGUILayout.HelpBox("WorldDefinition is missing a Planet Prefab Library.", MessageType.Warning);
        }

        // Procedural settings quick reference (read-only)
        _showProceduralInfo = EditorGUILayout.Foldout(_showProceduralInfo, "Procedural Settings (read-only)");
        if (_showProceduralInfo)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Mode", _world.mode.ToString());
                EditorGUILayout.LabelField("Seed", _world.seed.ToString());
                EditorGUILayout.LabelField("Post Count", $"{_world.postCountRange.x}–{_world.postCountRange.y}");
                EditorGUILayout.LabelField("Planet Count", $"{_world.planetCountRange.x}–{_world.planetCountRange.y}");
                EditorGUILayout.LabelField("Min Post Spacing", _world.minPostSpacing.ToString("0.###"));
                EditorGUILayout.LabelField("Edge Padding (Posts)", _world.edgePaddingPosts.ToString("0.###"));
                EditorGUILayout.LabelField("Min Planet Spacing", _world.minPlanetSpacing.ToString("0.###"));
                EditorGUILayout.LabelField("Min Dist From Post", _world.minDistanceFromPost.ToString("0.###"));
                EditorGUILayout.LabelField("Bounds (±x, ±y)", $"±{_world.halfExtents.x:0.###}, ±{_world.halfExtents.y:0.###}");
                EditorGUILayout.HelpBox("Procedural placement enforces min spacings; posts also keep edge padding + their radius away from edges.", MessageType.None);
            }
        }

        // Begin scrollable content (so large worlds are manageable)
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        // Authored posts list (read/write)
        EditorGUILayout.Space();
        DrawPostsList();

        EditorGUILayout.Space();
        DrawPlanetsList();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Planet"))
            {
                AddPlanet();
            }

            if (GUILayout.Button("Sort by X"))
            {
                SortPlanetsByX();
            }
        }

        // Snap options
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            bool prevSnapEnabled = _snapEnabled;
            float prevSnapSize = _snapSize;

            _snapEnabled = EditorGUILayout.Toggle(new GUIContent("Snap To Grid", "If enabled, dragging planets snaps their position to a grid."), _snapEnabled, GUILayout.Width(140));
            using (new EditorGUI.DisabledScope(!_snapEnabled))
            {
                _snapSize = EditorGUILayout.FloatField(new GUIContent("Snap Size", "Grid size used for snapping."), Mathf.Max(0.01f, _snapSize));
            }

            // Persist if changed
            if (_snapEnabled != prevSnapEnabled)
                EditorPrefs.SetBool(PrefSnapEnabledKey, _snapEnabled);
            if (!Mathf.Approximately(_snapSize, prevSnapSize))
                EditorPrefs.SetFloat(PrefSnapSizeKey, Mathf.Max(0.01f, _snapSize));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Snap All Now", "Round all authored planet positions to the current grid size."), GUILayout.Width(140)))
            {
                SnapAllToGrid();
            }
        }

        EditorGUILayout.Space();
        _lockPreview = EditorGUILayout.Toggle(new GUIContent("Lock Preview Instances", "If enabled, Preview World spawns non-editable instances that cannot be selected/moved and won't be saved."), _lockPreview);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview World"))
                PreviewWorld();
            if (GUILayout.Button("Clear Preview"))
                ClearPreview();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPostsList()
    {
        if (_world.authoredPosts == null)
            _world.authoredPosts = new WorldDefinition.AuthoredPost[0];

        EditorGUILayout.LabelField("Posts", EditorStyles.boldLabel);

        // Filter bar for posts
        using (new EditorGUILayout.HorizontalScope())
        {
            _postFilterByName = EditorGUILayout.ToggleLeft("Name contains", _postFilterByName, GUILayout.Width(110));
            using (new EditorGUI.DisabledScope(!_postFilterByName))
            {
                _postFilterName = EditorGUILayout.TextField(_postFilterName, GUILayout.MinWidth(100));
            }
            GUILayout.Space(8);
            _postFilterByMinLevel = EditorGUILayout.ToggleLeft("Min Level", _postFilterByMinLevel, GUILayout.Width(90));
            using (new EditorGUI.DisabledScope(!_postFilterByMinLevel))
            {
                _postFilterMinLevel = Mathf.Max(1, EditorGUILayout.IntField(_postFilterMinLevel, GUILayout.Width(60)));
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear Filters", GUILayout.Width(110)))
            {
                _postFilterByName = false; _postFilterByMinLevel = false; _postFilterName = string.Empty; _postFilterMinLevel = 1;
            }
        }

        // Bulk actions row for posts
        using (new EditorGUILayout.HorizontalScope())
        {
            int total = _world.authoredPosts.Length;
            _selPosts.RemoveAll(i => i < 0 || i >= total);
            EditorGUILayout.LabelField($"Selected: {_selPosts.Count}", GUILayout.Width(100));
            if (GUILayout.Button("All", GUILayout.Width(40)))
                _selPosts = Enumerable.Range(0, total).ToList();
            if (GUILayout.Button("None", GUILayout.Width(50)))
                _selPosts.Clear();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_selPosts.Count == 0))
            {
                if (GUILayout.Button("Duplicate Selected", GUILayout.Width(150))) DuplicateSelectedPosts();
                if (GUILayout.Button("Delete Selected", GUILayout.Width(130))) DeleteSelectedPosts();
                if (GUILayout.Button("Snap Selected", GUILayout.Width(130))) SnapSelectedPosts();
            }
        }

        EditorGUI.BeginChangeCheck();

        int removeAt = -1;
        int duplicateAt = -1;
        for (int i = 0; i < _world.authoredPosts.Length; i++)
        {
            var ap = _world.authoredPosts[i];
            if (_postFilterByName && !string.IsNullOrEmpty(_postFilterName))
            {
                var name = ap.displayName ?? string.Empty;
                if (name.IndexOf(_postFilterName, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            }
            if (_postFilterByMinLevel && ap.startLevel < _postFilterMinLevel) continue;
            EditorGUILayout.BeginVertical("box");

            using (new EditorGUILayout.HorizontalScope())
            {
                bool sel = _selPosts.Contains(i);
                bool selNew = GUILayout.Toggle(sel, GUIContent.none, GUILayout.Width(18));
                if (selNew != sel)
                {
                    if (selNew) _selPosts.Add(i); else _selPosts.Remove(i);
                }
                EditorGUILayout.LabelField($"#{i}", GUILayout.Width(28));
                ap.position = EditorGUILayout.Vector2Field("Position", ap.position);
                GUILayout.Label($"L{Mathf.Max(1, ap.startLevel)}", EditorStyles.miniBoldLabel, GUILayout.Width(28));
                if (GUILayout.Button("◉", GUILayout.Width(24))) FocusSceneOn(ap.position);
                if (GUILayout.Button("⧉", GUILayout.Width(24))) duplicateAt = i;
                if (GUILayout.Button("×", GUILayout.Width(24))) removeAt = i;
            }

            ap.startLevel = Mathf.Max(1, EditorGUILayout.IntField("Start Level", ap.startLevel));
            ap.displayName = EditorGUILayout.TextField("Display Name", ap.displayName);
            ap.influenceRadius = EditorGUILayout.FloatField("Influence Radius", ap.influenceRadius);
            ap.initialRequestMaterial = (PackageType)EditorGUILayout.ObjectField("Initial Request Material", ap.initialRequestMaterial, typeof(PackageType), false);
            ap.initialRequestAmount = Mathf.Max(1, EditorGUILayout.IntField("Initial Request Amount", ap.initialRequestAmount));

            _world.authoredPosts[i] = ap;
            EditorGUILayout.EndVertical();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Post"))
            {
                Undo.RecordObject(_world, "Add Post");
                var list = _world.authoredPosts?.ToList() ?? new System.Collections.Generic.List<WorldDefinition.AuthoredPost>();
                list.Add(new WorldDefinition.AuthoredPost
                {
                    position = Vector2.zero,
                    startLevel = 1,
                    displayName = "Post",
                    influenceRadius = 7f,
                    initialRequestMaterial = null,
                    initialRequestAmount = 3
                });
                _world.authoredPosts = list.ToArray();
                EditorUtility.SetDirty(_world);
                Repaint();
                SceneView.RepaintAll();
                if (_livePreview) PreviewWorld();
            }
        }

        if (duplicateAt >= 0)
        {
            Undo.RecordObject(_world, "Duplicate Post");
            var list = _world.authoredPosts?.ToList() ?? new System.Collections.Generic.List<WorldDefinition.AuthoredPost>();
            var src = list[duplicateAt];
            var dup = src;
            float dx = (_snapEnabled ? Mathf.Max(0.01f, _snapSize) : 0.5f);
            dup.position += new Vector2(dx, 0f);
            // clamp to bounds
            dup.position.x = Mathf.Clamp(dup.position.x, -_world.halfExtents.x, _world.halfExtents.x);
            dup.position.y = Mathf.Clamp(dup.position.y, -_world.halfExtents.y, _world.halfExtents.y);
            list.Insert(duplicateAt + 1, dup);
            _world.authoredPosts = list.ToArray();
            EditorUtility.SetDirty(_world);
            if (_livePreview) PreviewWorld();
        }

        if (removeAt >= 0)
        {
            Undo.RecordObject(_world, "Remove Post");
            _world.authoredPosts = _world.authoredPosts.Where((_, idx) => idx != removeAt).ToArray();
            EditorUtility.SetDirty(_world);
            if (_livePreview) PreviewWorld();
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_world, "Edit Posts");
            EditorUtility.SetDirty(_world);
            Repaint();
            SceneView.RepaintAll();
            if (_livePreview) PreviewWorld();
        }
    }

    private void SnapAllToGrid()
    {
        if (_world == null || _world.authoredPlanets == null || _world.authoredPlanets.Length == 0) return;
        float s = Mathf.Max(0.01f, _snapSize);
        float hxB = _world.halfExtents.x;
        float hyB = _world.halfExtents.y;
        Undo.RecordObject(_world, "Snap All Planets To Grid");
        for (int i = 0; i < _world.authoredPlanets.Length; i++)
        {
            var ap = _world.authoredPlanets[i];
            // snap
            ap.position.x = Mathf.Round(ap.position.x / s) * s;
            ap.position.y = Mathf.Round(ap.position.y / s) * s;
            // radius-aware clamp
            float r = GetRadiusForSize(ap.size);
            float availX = Mathf.Max(0f, hxB - r);
            float availY = Mathf.Max(0f, hyB - r);
            ap.position.x = Mathf.Clamp(ap.position.x, -availX, availX);
            ap.position.y = Mathf.Clamp(ap.position.y, -availY, availY);
            _world.authoredPlanets[i] = ap;
        }
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void DuplicateSelectedPosts()
    {
        if (_world == null || _world.authoredPosts == null || _world.authoredPosts.Length == 0) return;
        var sel = _selPosts.Distinct().OrderBy(i => i).ToList();
        if (sel.Count == 0) return;
        Undo.RecordObject(_world, "Duplicate Selected Posts");
        var list = _world.authoredPosts.ToList();
        float dx = (_snapEnabled ? Mathf.Max(0.01f, _snapSize) : 0.5f);
        int offset = 0;
        foreach (var idx in sel)
        {
            if (idx + offset < 0 || idx + offset >= list.Count) continue;
            var src = list[idx + offset];
            var dup = src;
            dup.position += new Vector2(dx, 0f);
            dup.position.x = Mathf.Clamp(dup.position.x, -_world.halfExtents.x, _world.halfExtents.x);
            dup.position.y = Mathf.Clamp(dup.position.y, -_world.halfExtents.y, _world.halfExtents.y);
            list.Insert(idx + offset + 1, dup);
            offset++;
        }
        _world.authoredPosts = list.ToArray();
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void DeleteSelectedPosts()
    {
        if (_world == null || _world.authoredPosts == null || _world.authoredPosts.Length == 0) return;
        var sel = new System.Collections.Generic.HashSet<int>(_selPosts.Where(i => i >= 0 && i < _world.authoredPosts.Length));
        if (sel.Count == 0) return;
        Undo.RecordObject(_world, "Delete Selected Posts");
        _world.authoredPosts = _world.authoredPosts.Where((_, idx) => !sel.Contains(idx)).ToArray();
        _selPosts.Clear();
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void SnapSelectedPosts()
    {
        if (_world == null || _world.authoredPosts == null || _world.authoredPosts.Length == 0) return;
        var sel = new System.Collections.Generic.HashSet<int>(_selPosts.Where(i => i >= 0 && i < _world.authoredPosts.Length));
        if (sel.Count == 0) return;
        float s = Mathf.Max(0.01f, _snapSize);
        float hxB = _world.halfExtents.x;
        float hyB = _world.halfExtents.y;
        Undo.RecordObject(_world, "Snap Selected Posts To Grid");
        for (int i = 0; i < _world.authoredPosts.Length; i++)
        {
            if (!sel.Contains(i)) continue;
            var ap = _world.authoredPosts[i];
            ap.position.x = Mathf.Round(ap.position.x / s) * s;
            ap.position.y = Mathf.Round(ap.position.y / s) * s;
            ap.position.x = Mathf.Clamp(ap.position.x, -hxB, hxB);
            ap.position.y = Mathf.Clamp(ap.position.y, -hyB, hyB);
            _world.authoredPosts[i] = ap;
        }
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void DuplicateSelectedPlanets()
    {
        if (_world == null || _world.authoredPlanets == null || _world.authoredPlanets.Length == 0) return;
        var sel = _selPlanets.Distinct().OrderBy(i => i).ToList();
        if (sel.Count == 0) return;
        Undo.RecordObject(_world, "Duplicate Selected Planets");
        var list = _world.authoredPlanets.ToList();
        float dx = (_snapEnabled ? Mathf.Max(0.01f, _snapSize) : 0.5f);
        int offset = 0;
        foreach (var idx in sel)
        {
            if (idx + offset < 0 || idx + offset >= list.Count) continue;
            var src = list[idx + offset];
            var dup = src;
            dup.position += new Vector2(dx, 0f);
            // radius-aware clamp
            float r = GetRadiusForSize(dup.size);
            float availX = Mathf.Max(0f, _world.halfExtents.x - r);
            float availY = Mathf.Max(0f, _world.halfExtents.y - r);
            dup.position.x = Mathf.Clamp(dup.position.x, -availX, availX);
            dup.position.y = Mathf.Clamp(dup.position.y, -availY, availY);
            list.Insert(idx + offset + 1, dup);
            offset++;
        }
        _world.authoredPlanets = list.ToArray();
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void DeleteSelectedPlanets()
    {
        if (_world == null || _world.authoredPlanets == null || _world.authoredPlanets.Length == 0) return;
        var sel = new System.Collections.Generic.HashSet<int>(_selPlanets.Where(i => i >= 0 && i < _world.authoredPlanets.Length));
        if (sel.Count == 0) return;
        Undo.RecordObject(_world, "Delete Selected Planets");
        _world.authoredPlanets = _world.authoredPlanets.Where((_, idx) => !sel.Contains(idx)).ToArray();
        _selPlanets.Clear();
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void SnapSelectedPlanets()
    {
        if (_world == null || _world.authoredPlanets == null || _world.authoredPlanets.Length == 0) return;
        var sel = new System.Collections.Generic.HashSet<int>(_selPlanets.Where(i => i >= 0 && i < _world.authoredPlanets.Length));
        if (sel.Count == 0) return;
        float s = Mathf.Max(0.01f, _snapSize);
        float hxB = _world.halfExtents.x;
        float hyB = _world.halfExtents.y;
        Undo.RecordObject(_world, "Snap Selected Planets To Grid");
        for (int i = 0; i < _world.authoredPlanets.Length; i++)
        {
            if (!sel.Contains(i)) continue;
            var ap = _world.authoredPlanets[i];
            ap.position.x = Mathf.Round(ap.position.x / s) * s;
            ap.position.y = Mathf.Round(ap.position.y / s) * s;
            float r = GetRadiusForSize(ap.size);
            float availX = Mathf.Max(0f, hxB - r);
            float availY = Mathf.Max(0f, hyB - r);
            ap.position.x = Mathf.Clamp(ap.position.x, -availX, availX);
            ap.position.y = Mathf.Clamp(ap.position.y, -availY, availY);
            _world.authoredPlanets[i] = ap;
        }
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void DrawPlanetsList()
    {
        if (_world.authoredPlanets == null)
            _world.authoredPlanets = new WorldDefinition.AuthoredPlanet[0];

        EditorGUILayout.LabelField("Planets", EditorStyles.boldLabel);

        // Filter bar
        using (new EditorGUILayout.HorizontalScope())
        {
            _filterBySize = EditorGUILayout.ToggleLeft("Filter Size", _filterBySize, GUILayout.Width(90));
            using (new EditorGUI.DisabledScope(!_filterBySize))
            {
                _filterSize = (PlanetSize)EditorGUILayout.EnumPopup(_filterSize, GUILayout.Width(110));
            }
            GUILayout.Space(8);
            _filterByType = EditorGUILayout.ToggleLeft("Filter Type", _filterByType, GUILayout.Width(90));
            using (new EditorGUI.DisabledScope(!_filterByType))
            {
                _filterType = (PlanetType)EditorGUILayout.EnumPopup(_filterType, GUILayout.Width(110));
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear Filters", GUILayout.Width(110)))
            {
                _filterBySize = false; _filterByType = false;
            }
        }

        // Bulk actions row for planets
        using (new EditorGUILayout.HorizontalScope())
        {
            int total = _world.authoredPlanets.Length;
            _selPlanets.RemoveAll(i => i < 0 || i >= total);
            EditorGUILayout.LabelField($"Selected: {_selPlanets.Count}", GUILayout.Width(100));
            if (GUILayout.Button("All", GUILayout.Width(40)))
                _selPlanets = Enumerable.Range(0, total).ToList();
            if (GUILayout.Button("None", GUILayout.Width(50)))
                _selPlanets.Clear();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_selPlanets.Count == 0))
            {
                if (GUILayout.Button("Duplicate Selected", GUILayout.Width(150))) DuplicateSelectedPlanets();
                if (GUILayout.Button("Delete Selected", GUILayout.Width(130))) DeleteSelectedPlanets();
                if (GUILayout.Button("Snap Selected", GUILayout.Width(130))) SnapSelectedPlanets();
            }
        }
        EditorGUI.BeginChangeCheck();

        int removeAt = -1;
        int duplicateAt = -1;
        for (int i = 0; i < _world.authoredPlanets.Length; i++)
        {
            var ap = _world.authoredPlanets[i];
            if (_filterBySize && ap.size != _filterSize) continue;
            if (_filterByType && ap.type != _filterType) continue;
            EditorGUILayout.BeginVertical("box");

            using (new EditorGUILayout.HorizontalScope())
            {
                bool sel = _selPlanets.Contains(i);
                bool selNew = GUILayout.Toggle(sel, GUIContent.none, GUILayout.Width(18));
                if (selNew != sel)
                {
                    if (selNew) _selPlanets.Add(i); else _selPlanets.Remove(i);
                }
                EditorGUILayout.LabelField($"#{i}", GUILayout.Width(28));
                ap.position = EditorGUILayout.Vector2Field("Position", ap.position);
                // quick badge: show size/type compactly
                string badge = $"{ap.size}/{ap.type}";
                GUILayout.Label(badge, EditorStyles.miniLabel, GUILayout.Width(120));
                if (GUILayout.Button("◉", GUILayout.Width(24))) FocusSceneOn(ap.position);
                if (GUILayout.Button("⧉", GUILayout.Width(24))) duplicateAt = i;
                if (GUILayout.Button("×", GUILayout.Width(24))) removeAt = i;
            }

            ap.size  = (PlanetSize)EditorGUILayout.EnumPopup("Size", ap.size);
            ap.type  = (PlanetType)EditorGUILayout.EnumPopup("Type", ap.type);

            // Quick size presets
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Size Presets", GUILayout.Width(85));
                if (GUILayout.Button("+Small", EditorStyles.miniButton, GUILayout.Width(60))) ap.size = PlanetSize.Small;
                if (GUILayout.Button("+Medium", EditorStyles.miniButton, GUILayout.Width(70))) ap.size = PlanetSize.Medium;
                if (GUILayout.Button("+Large", EditorStyles.miniButton, GUILayout.Width(60))) ap.size = PlanetSize.Large;
            }

            // Quick type swatches
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Type", GUILayout.Width(85));
                var types = new PlanetType[] { PlanetType.Default, PlanetType.Fire, PlanetType.Water, PlanetType.Earth };
                foreach (var t in types)
                {
                    var prevBg = GUI.backgroundColor;
                    var col = TypeColor(t); col.a = 1f;
                    GUI.backgroundColor = col;
                    string label = t.ToString();
                    if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(64)))
                    {
                        ap.type = t;
                    }
                    GUI.backgroundColor = prevBg;
                }
            }
            ap.mass  = EditorGUILayout.FloatField(new GUIContent("Mass", "Independent mass per planet; gravity ignores 0."), ap.mass);
            ap.profile = (PlanetProfile)EditorGUILayout.ObjectField("Profile (optional)", ap.profile, typeof(PlanetProfile), false);

            // Read radius from prefab library for this size and show it read-only
            float radius = GetRadiusForSize(ap.size);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.FloatField("Radius (from prefab)", radius);

            _world.authoredPlanets[i] = ap;
            EditorGUILayout.EndVertical();
        }

        if (duplicateAt >= 0)
        {
            Undo.RecordObject(_world, "Duplicate Planet");
            var list = _world.authoredPlanets?.ToList() ?? new System.Collections.Generic.List<WorldDefinition.AuthoredPlanet>();
            var src = list[duplicateAt];
            var dup = src;
            // nudge position slightly to avoid perfect overlap
            float dx = (_snapEnabled ? Mathf.Max(0.01f, _snapSize) : 0.5f);
            dup.position += new Vector2(dx, 0f);
            // clamp to bounds
            dup.position.x = Mathf.Clamp(dup.position.x, -_world.halfExtents.x, _world.halfExtents.x);
            dup.position.y = Mathf.Clamp(dup.position.y, -_world.halfExtents.y, _world.halfExtents.y);
            list.Insert(duplicateAt + 1, dup);
            _world.authoredPlanets = list.ToArray();
            EditorUtility.SetDirty(_world);
            if (_livePreview) PreviewWorld();
        }

        if (removeAt >= 0)
        {
            Undo.RecordObject(_world, "Remove Planet");
            _world.authoredPlanets = _world.authoredPlanets.Where((_, idx) => idx != removeAt).ToArray();
            EditorUtility.SetDirty(_world);
            if (_livePreview) PreviewWorld();
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_world, "Edit Planets");
            EditorUtility.SetDirty(_world);
            Repaint();
            SceneView.RepaintAll();
            if (_livePreview) PreviewWorld();
        }
    }

    private void AddPlanet()
    {
        Undo.RecordObject(_world, "Add Planet");
        var list = _world.authoredPlanets?.ToList() ?? new System.Collections.Generic.List<WorldDefinition.AuthoredPlanet>();
        list.Add(new WorldDefinition.AuthoredPlanet
        {
            position = Vector2.zero,
            size = PlanetSize.Medium,
            type = PlanetType.Default,
            mass = 0f,
            profile = null
        });
        _world.authoredPlanets = list.ToArray();
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void SortPlanetsByX()
    {
        if (_world.authoredPlanets == null || _world.authoredPlanets.Length == 0) return;
        Undo.RecordObject(_world, "Sort Planets");
        _world.authoredPlanets = _world.authoredPlanets.OrderBy(p => p.position.x).ToArray();
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void OnSceneGUI(SceneView sv)
    {
        if (!_world || _world.authoredPlanets == null) return;

        // Scene overlay: small toolbar for grid, snap size, and bounds toggle
        Handles.BeginGUI();
        {
            var r = new Rect(12, 12, 250, 64);
            GUILayout.BeginArea(r, GUIContent.none, GUI.skin.box);
            GUILayout.Label("World Designer");
            using (new EditorGUILayout.HorizontalScope())
            {
                bool prevSnap = _snapEnabled;
                float prevSnapSize = _snapSize;
                bool prevShowBounds = _showBounds;

                _snapEnabled = GUILayout.Toggle(_snapEnabled, new GUIContent("Grid", "Toggle snap-to-grid and grid overlay"), GUILayout.Width(60));
                using (new EditorGUI.DisabledScope(!_snapEnabled))
                {
                    GUILayout.Label("Size", GUILayout.Width(32));
                    string snapStr = GUILayout.TextField(_snapSize.ToString("0.##"), GUILayout.Width(48));
                    if (float.TryParse(snapStr, out float parsed))
                        _snapSize = Mathf.Max(0.01f, parsed);
                }
                _showBounds = GUILayout.Toggle(_showBounds, new GUIContent("Bounds", "Toggle world bounds overlay"), GUILayout.Width(70));

                if (GUILayout.Button(new GUIContent("Frame", "Center and zoom the Scene view to fit the world bounds"), GUILayout.Width(64)))
                {
                    FrameWorldBounds();
                }

                if (_snapEnabled != prevSnap) EditorPrefs.SetBool(PrefSnapEnabledKey, _snapEnabled);
                if (!Mathf.Approximately(_snapSize, prevSnapSize)) EditorPrefs.SetFloat(PrefSnapSizeKey, Mathf.Max(0.01f, _snapSize));
                if (_showBounds != prevShowBounds) EditorPrefs.SetBool(PrefShowBoundsKey, _showBounds);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool prevPosts = _previewPosts;
                bool prevPlanets = _previewPlanets;
                bool prevIso = _previewIsolateSelection;
                _previewPosts = GUILayout.Toggle(_previewPosts, new GUIContent("Posts", "Include Posts in Preview"), GUILayout.Width(64));
                _previewPlanets = GUILayout.Toggle(_previewPlanets, new GUIContent("Planets", "Include Planets in Preview"), GUILayout.Width(72));
                _previewIsolateSelection = GUILayout.Toggle(_previewIsolateSelection, new GUIContent("Isolate", "Preview only selected rows"), GUILayout.Width(80));
                if (_previewPosts != prevPosts)
                {
                    EditorPrefs.SetBool(PrefPreviewPostsKey, _previewPosts);
                    if (_livePreview) PreviewWorld();
                }
                if (_previewPlanets != prevPlanets)
                {
                    EditorPrefs.SetBool(PrefPreviewPlanetsKey, _previewPlanets);
                    if (_livePreview) PreviewWorld();
                }
                if (_previewIsolateSelection != prevIso)
                {
                    EditorPrefs.SetBool(PrefPreviewIsolateKey, _previewIsolateSelection);
                    if (_livePreview) PreviewWorld();
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_world))
                {
                    if (GUILayout.Button(new GUIContent("Preview", "Spawn a preview of the authored world"), GUILayout.Width(80)))
                        PreviewWorld();
                    if (GUILayout.Button(new GUIContent("Clear", "Remove the preview root from the scene"), GUILayout.Width(80)))
                        ClearPreview();
                    bool prevLive = _livePreview;
                    _livePreview = GUILayout.Toggle(_livePreview, new GUIContent("Live", "Auto-refresh preview on data change"), GUILayout.Width(60));
                    if (_livePreview != prevLive)
                    {
                        EditorPrefs.SetBool(PrefLivePreviewKey, _livePreview);
                        if (_livePreview) PreviewWorld();
                    }
                }
            }
            GUILayout.EndArea();
        }
        Handles.EndGUI();

        // Draw world bounds (centered at 0,0) for visual guidance
        if (_showBounds)
        {
            var hx = _world.halfExtents.x;
            var hy = _world.halfExtents.y;
            var verts = new Vector3[]
            {
                new Vector3(-hx, -hy, 0),
                new Vector3(-hx,  hy, 0),
                new Vector3( hx,  hy, 0),
                new Vector3( hx, -hy, 0),
            };
            var fill = new Color(0.2f, 0.8f, 1f, 0.05f);
            var outline = new Color(0.2f, 0.8f, 1f, 0.9f);
            Handles.DrawSolidRectangleWithOutline(verts, fill, outline);
        }

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        // Grid overlay when snapping is enabled
        if (_snapEnabled)
        {
            float s = Mathf.Max(0.01f, _snapSize);
            float hx = _world.halfExtents.x;
            float hy = _world.halfExtents.y;

            // Safety: avoid excessive lines for tiny snap sizes
            int maxLines = 1200;
            int vCount = Mathf.FloorToInt((hx * 2f) / s) + 1;
            int hCount = Mathf.FloorToInt((hy * 2f) / s) + 1;
            if (vCount + hCount <= maxLines)
            {
                var gridCol = new Color(0.2f, 0.8f, 1f, 0.08f);
                var axisCol = new Color(0.2f, 0.8f, 1f, 0.25f);

                int minXi = Mathf.CeilToInt(-hx / s);
                int maxXi = Mathf.FloorToInt(hx / s);
                for (int xi = minXi; xi <= maxXi; xi++)
                {
                    float x = xi * s;
                    Handles.color = Mathf.Abs(x) < 1e-4f ? axisCol : gridCol;
                    Handles.DrawLine(new Vector3(x, -hy, 0f), new Vector3(x, hy, 0f));
                }

                int minYi = Mathf.CeilToInt(-hy / s);
                int maxYi = Mathf.FloorToInt(hy / s);
                for (int yi = minYi; yi <= maxYi; yi++)
                {
                    float y = yi * s;
                    Handles.color = Mathf.Abs(y) < 1e-4f ? axisCol : gridCol;
                    Handles.DrawLine(new Vector3(-hx, y, 0f), new Vector3(hx, y, 0f));
                }
            }
        }

        // Posts: influence ring + position handle
        if (_world.authoredPosts != null)
        {
            for (int i = 0; i < _world.authoredPosts.Length; i++)
            {
                var ap = _world.authoredPosts[i];

                // Draw influence radius
                if (ap.influenceRadius > 0f)
                {
                    Handles.color = _postRing;
                    Handles.DrawWireDisc((Vector3)ap.position, Vector3.forward, ap.influenceRadius);
                }

                // Position handle (2D) with optional snapping and bounds clamp
                EditorGUI.BeginChangeCheck();
                Vector3 p3 = Handles.FreeMoveHandle((Vector3)ap.position, 0.12f, Vector3.zero, Handles.DotHandleCap);
                if (_snapEnabled)
                {
                    float s = Mathf.Max(0.01f, _snapSize);
                    p3.x = Mathf.Round(p3.x / s) * s;
                    p3.y = Mathf.Round(p3.y / s) * s;
                }
                float hxP = _world.halfExtents.x;
                float hyP = _world.halfExtents.y;
                Vector2 clampedP = new Vector2(
                    Mathf.Clamp(p3.x, -hxP, hxP),
                    Mathf.Clamp(p3.y, -hyP, hyP)
                );
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_world, "Move Post");
                    ap.position = clampedP;
                    _world.authoredPosts[i] = ap;
                    EditorUtility.SetDirty(_world);
                    if (_livePreview) PreviewWorld();
                }

                // Label
                Handles.BeginGUI();
                var guiPtP = HandleUtility.WorldToGUIPoint((Vector3)ap.position + new Vector3(0, 0.4f, 0));
                var rectP = new Rect(guiPtP.x - 80, guiPtP.y - 28, 160, 24);
                GUI.Box(rectP, GUIContent.none);
                GUILayout.BeginArea(rectP);
                GUILayout.Label($"{(string.IsNullOrEmpty(ap.displayName) ? "Post" : ap.displayName)}  L{Mathf.Max(1, ap.startLevel)}", EditorStyles.miniLabel);
                GUILayout.EndArea();
                Handles.EndGUI();
            }
        }

        for (int i = 0; i < _world.authoredPlanets.Length; i++)
        {
            var ap = _world.authoredPlanets[i];
            float radius = Mathf.Max(0.01f, GetRadiusForSize(ap.size));

            // Color by type for readability
            Handles.color = TypeColor(ap.type);

            // Draw disc for visual radius
            Handles.DrawWireDisc((Vector3)ap.position, Vector3.forward, radius);

            // Position handle (2D)
            EditorGUI.BeginChangeCheck();
            Vector3 pos3 = Handles.FreeMoveHandle((Vector3)ap.position, 0.12f, Vector3.zero, Handles.DotHandleCap);
            // Optional snap to grid
            if (_snapEnabled)
            {
                float s = Mathf.Max(0.01f, _snapSize);
                pos3.x = Mathf.Round(pos3.x / s) * s;
                pos3.y = Mathf.Round(pos3.y / s) * s;
            }
            // Clamp to world bounds accounting for planet radius (keep whole disc inside)
            var hxB = _world.halfExtents.x;
            var hyB = _world.halfExtents.y;
            float availX = Mathf.Max(0f, hxB - radius);
            float availY = Mathf.Max(0f, hyB - radius);
            Vector2 clamped = new Vector2(
                Mathf.Clamp(pos3.x, -availX, availX),
                Mathf.Clamp(pos3.y, -availY, availY)
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_world, "Move Planet");
                ap.position = clamped;
                _world.authoredPlanets[i] = ap;
                EditorUtility.SetDirty(_world);
                if (_livePreview) PreviewWorld();
            }

            // Label
            Handles.BeginGUI();
            var guiPt = HandleUtility.WorldToGUIPoint((Vector3)ap.position + new Vector3(0, radius + 0.2f, 0));
            var rect = new Rect(guiPt.x - 80, guiPt.y - 36, 160, 32);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(rect);
            GUILayout.Label($"{ap.size} / {ap.type}\nMass: {ap.mass}", EditorStyles.miniLabel);
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }

    private Color TypeColor(PlanetType t)
    {
        switch (t)
        {
            case PlanetType.Fire:  return _typeFire;
            case PlanetType.Water: return _typeWater;
            case PlanetType.Earth: return _typeEarth;
            default:               return _typeDefault;
        }
    }

    private float GetRadiusForSize(PlanetSize size)
    {
        if (_world == null || _world.planetPrefabs == null) return 1f;
        if (_world.planetPrefabs.TryGetRadius(size, out float r))
            return Mathf.Max(0.01f, r);
        return 1f;
    }

    private static void SetHideAndPicking(GameObject go, bool hide, bool lockPicking)
    {
        if (!go) return;
        // Always ensure previews are not saved to scene or build
        var baseFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        if (hide)
            go.hideFlags = baseFlags | HideFlags.NotEditable | HideFlags.HideInHierarchy;
        else
            go.hideFlags = baseFlags; // visible & editable

        // Disable/enable picking in Scene view if supported
#if UNITY_EDITOR
#if UNITY_2019_1_OR_NEWER
        try
        {
            var svm = UnityEditor.SceneVisibilityManager.instance;
            svm.DisablePicking(go, lockPicking);
        }
        catch { /* older Unity versions may not have SceneVisibilityManager */ }
#endif
#endif

        foreach (Transform t in go.transform)
            SetHideAndPicking(t.gameObject, hide, lockPicking);
    }

    private void PreviewWorld()
    {
        if (!_world)
        {
            EditorUtility.DisplayDialog("Preview World", "Assign a WorldDefinition asset first.", "OK");
            return;
        }

        var root = GameObject.Find(PreviewRootName);
        if (root) ClearPreview();
        root = new GameObject(PreviewRootName);

        // Ensure the preview root never gets saved
        root.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        // Spawn POSTS via runtime rules (if prefab exists)
        if (_previewPosts && _world.authoredPosts != null && _world.postPrefab)
        {
            var selected = _previewIsolateSelection ? new System.Collections.Generic.HashSet<int>(_selPosts) : null;
            for (int i = 0; i < _world.authoredPosts.Length; i++)
            {
                if (selected != null && !selected.Contains(i)) continue;
                var post = _world.authoredPosts[i];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(_world.postPrefab);
                go.transform.SetParent(root.transform);
                go.transform.position = post.position;
                go.transform.localScale = Vector3.one;

                var node = go.GetComponent<DeliveryNode>();
                if (node)
                {
                    WorldSpawnHelpers.ApplyAuthoredPost(node, post);
                }
            }
        }

        // Spawn PLANETS via the same rules as runtime builder (size prefab + type + profile + mass)
        if (_previewPlanets && _world.authoredPlanets != null && _world.planetPrefabs != null)
        {
            var selected = _previewIsolateSelection ? new System.Collections.Generic.HashSet<int>(_selPlanets) : null;
            for (int i = 0; i < _world.authoredPlanets.Length; i++)
            {
                if (selected != null && !selected.Contains(i)) continue;
                var ap = _world.authoredPlanets[i];
                if (!_world.planetPrefabs.TryGet(ap.size, out var prefab) || !prefab) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(root.transform);
                go.transform.position = ap.position;
                go.transform.localScale = Vector3.one;

                var planet = go.GetComponent<Planet>();
                if (planet)
                {
                    WorldSpawnHelpers.ApplyAuthoredPlanet(planet, ap);
                }
            }
        }

        SetHideAndPicking(root, _lockPreview, _lockPreview);

        Selection.activeObject = root;
    }

    private void ClearPreview()
    {
        var root = GameObject.Find(PreviewRootName);
        if (!root) return;

        // Re-enable picking in case it was disabled
#if UNITY_EDITOR
#if UNITY_2019_1_OR_NEWER
        try
        {
            var svm = UnityEditor.SceneVisibilityManager.instance;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t) svm.DisablePicking(t.gameObject, false);
            }
        }
        catch { }
#endif
#endif

        Undo.DestroyObjectImmediate(root);
    }

    private void FocusSceneOn(Vector2 position)
    {
        // Ping the world asset for quick locate
        if (_world) EditorGUIUtility.PingObject(_world);
        // Move SceneView pivot to the target position
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            Vector3 pivot = new Vector3(position.x, position.y, 0f);
            sv.LookAt(pivot);
            sv.Repaint();
        }
    }

    private void FrameWorldBounds()
    {
        if (!_world) return;
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;
        float hx = Mathf.Max(0.01f, _world.halfExtents.x);
        float hy = Mathf.Max(0.01f, _world.halfExtents.y);
        float maxExtent = Mathf.Max(hx, hy);
        // Ensure orthographic for 2D layout and frame with a small margin
        sv.orthographic = true;
        sv.pivot = Vector3.zero;
        sv.size = maxExtent + 2f;
        sv.Repaint();
    }
}
#endif

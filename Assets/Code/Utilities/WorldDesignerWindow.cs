#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Text;

// World Designer: place planets visually without entering Play mode.
// Open via: Tools → Orbit Post → World Designer
public class WorldDesignerWindow : EditorWindow
{
    private enum PlanetGroupBy { None, Size, Type }
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
    [SerializeField] private PlanetGroupBy _groupBy = PlanetGroupBy.None;
    [SerializeField] private bool[] _sizeFoldouts;
    [SerializeField] private bool[] _typeFoldouts;
    [SerializeField] private bool _postFilterByName = false;
    [SerializeField] private string _postFilterName = "";
    [SerializeField] private bool _postFilterByMinLevel = false;
    [SerializeField] private int _postFilterMinLevel = 1;
    [SerializeField] private bool _previewPosts = true;
    [SerializeField] private bool _previewPlanets = true;
    [SerializeField] private bool _previewIsolateSelection = false;
    [SerializeField] private bool _livePreview = false;
    [SerializeField] private bool _alignSnapEnabled = true;
    [SerializeField] private float _alignSnapThreshold = 0.25f;
    [SerializeField] private bool _boxSelectEnabled = true;
    [SerializeField] private bool _boxSelectPlanets = true;
    [SerializeField] private bool _boxSelectPosts = true;
    private bool _boxSelecting;
    private Vector2 _boxStartGui, _boxEndGui;
    [SerializeField] private float _nudgeStep = 1f;
    private Rect _overlayRect;
    [SerializeField] private bool _showPlanetInfo = true;
    [SerializeField] private bool _showPostInfo = true;
    [SerializeField] private bool _showInfo = true; // master toggle
    // Group drag state
    private bool _draggingPlanets;
    private bool _draggingPosts;
    private int _dragAnchorPlanetIndex = -1;
    private int _dragAnchorPostIndex = -1;
    private Vector2 _dragAnchorPlanetStart;
    private Vector2 _dragAnchorPostStart;
    private System.Collections.Generic.Dictionary<int, Vector2> _dragStartPlanets;
    private System.Collections.Generic.Dictionary<int, Vector2> _dragStartPosts;
    [SerializeField] private bool _foldPosts = true;
    [SerializeField] private bool _foldPlanets = true;
    [SerializeField] private bool[] _postItemFoldouts;
    [SerializeField] private bool[] _planetItemFoldouts;

    // Batch Edit (Planets)
    [SerializeField] private bool _batchPlanetApplySize = false;
    [SerializeField] private PlanetSize _batchPlanetSize = PlanetSize.Medium;
    [SerializeField] private bool _batchPlanetApplyType = false;
    [SerializeField] private PlanetType _batchPlanetType = PlanetType.Default;
    [SerializeField] private bool _batchPlanetApplyMass = false;
    [SerializeField] private float _batchPlanetMass = 20f;
    [SerializeField] private bool _batchPlanetApplyProfile = false;
    [SerializeField] private PlanetProfile _batchPlanetProfile = null;

    // Batch Edit (Posts)
    [SerializeField] private bool _batchPostApplyLevel = false;
    [SerializeField] private int _batchPostLevel = 1;
    [SerializeField] private bool _batchPostApplyInfluence = false;
    [SerializeField] private float _batchPostInfluence = 7f;
    [SerializeField] private bool _batchPostApplyMaterial = false;
    [SerializeField] private PackageType _batchPostMaterial = null;
    [SerializeField] private bool _batchPostApplyAmount = false;
    [SerializeField] private int _batchPostAmount = 3;

    // EditorPrefs keys for persistence
    private const string PrefSnapEnabledKey = "WorldDesigner_SnapEnabled";
    private const string PrefSnapSizeKey = "WorldDesigner_SnapSize";
    private const string PrefShowBoundsKey = "WorldDesigner_ShowBounds";
    private const string PrefPreviewPostsKey = "WorldDesigner_PreviewPosts";
    private const string PrefPreviewPlanetsKey = "WorldDesigner_PreviewPlanets";
    private const string PrefPreviewIsolateKey = "WorldDesigner_PreviewIsolate";
    private const string PrefLivePreviewKey = "WorldDesigner_LivePreview";
    private const string PrefGroupByKey = "WorldDesigner_GroupBy";
    private const string PrefFoldPostsKey = "WorldDesigner_FoldPosts";
    private const string PrefFoldPlanetsKey = "WorldDesigner_FoldPlanets";
    private const string PrefPostSelectionKey = "WorldDesigner_PostSelection";
    private const string PrefPlanetSelectionKey = "WorldDesigner_PlanetSelection";
    private const string PrefAlignEnabledKey = "WorldDesigner_AlignEnabled";
    private const string PrefAlignThresholdKey = "WorldDesigner_AlignThreshold";
    private const string PrefBoxEnabledKey = "WorldDesigner_BoxEnabled";
    private const string PrefBoxPlanetsKey = "WorldDesigner_BoxPlanets";
    private const string PrefBoxPostsKey = "WorldDesigner_BoxPosts";
    private const string PrefShowPlanetInfoKey = "WorldDesigner_ShowPlanetInfo";
    private const string PrefShowPostInfoKey = "WorldDesigner_ShowPostInfo";
    private const string PrefShowInfoKey = "WorldDesigner_ShowInfo";

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
        if (EditorPrefs.HasKey(PrefGroupByKey))
            _groupBy = (PlanetGroupBy)EditorPrefs.GetInt(PrefGroupByKey, (int)_groupBy);
        if (EditorPrefs.HasKey(PrefFoldPostsKey))
            _foldPosts = EditorPrefs.GetBool(PrefFoldPostsKey, _foldPosts);
        if (EditorPrefs.HasKey(PrefFoldPlanetsKey))
            _foldPlanets = EditorPrefs.GetBool(PrefFoldPlanetsKey, _foldPlanets);
        if (EditorPrefs.HasKey(PrefAlignEnabledKey))
            _alignSnapEnabled = EditorPrefs.GetBool(PrefAlignEnabledKey, _alignSnapEnabled);
        if (EditorPrefs.HasKey(PrefAlignThresholdKey))
            _alignSnapThreshold = Mathf.Max(0.001f, EditorPrefs.GetFloat(PrefAlignThresholdKey, _alignSnapThreshold));
        if (EditorPrefs.HasKey(PrefBoxEnabledKey))
            _boxSelectEnabled = EditorPrefs.GetBool(PrefBoxEnabledKey, _boxSelectEnabled);
        if (EditorPrefs.HasKey(PrefBoxPlanetsKey))
            _boxSelectPlanets = EditorPrefs.GetBool(PrefBoxPlanetsKey, _boxSelectPlanets);
        if (EditorPrefs.HasKey(PrefBoxPostsKey))
            _boxSelectPosts = EditorPrefs.GetBool(PrefBoxPostsKey, _boxSelectPosts);
        if (EditorPrefs.HasKey(PrefShowPlanetInfoKey))
            _showPlanetInfo = EditorPrefs.GetBool(PrefShowPlanetInfoKey, _showPlanetInfo);
        if (EditorPrefs.HasKey(PrefShowPostInfoKey))
            _showPostInfo = EditorPrefs.GetBool(PrefShowPostInfoKey, _showPostInfo);
        if (EditorPrefs.HasKey(PrefShowInfoKey))
            _showInfo = EditorPrefs.GetBool(PrefShowInfoKey, _showInfo);

        // Attempt to load per-item foldouts for current world (if any)
        TryLoadItemFoldouts();
        TryLoadSelections();
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
        EditorPrefs.SetInt(PrefGroupByKey, (int)_groupBy);
        EditorPrefs.SetBool(PrefFoldPostsKey, _foldPosts);
        EditorPrefs.SetBool(PrefFoldPlanetsKey, _foldPlanets);
        EditorPrefs.SetBool(PrefAlignEnabledKey, _alignSnapEnabled);
        EditorPrefs.SetFloat(PrefAlignThresholdKey, Mathf.Max(0.001f, _alignSnapThreshold));
        EditorPrefs.SetBool(PrefBoxEnabledKey, _boxSelectEnabled);
        EditorPrefs.SetBool(PrefBoxPlanetsKey, _boxSelectPlanets);
        EditorPrefs.SetBool(PrefBoxPostsKey, _boxSelectPosts);
        EditorPrefs.SetBool(PrefShowPlanetInfoKey, _showPlanetInfo);
        EditorPrefs.SetBool(PrefShowPostInfoKey, _showPostInfo);
        EditorPrefs.SetBool(PrefShowInfoKey, _showInfo);

        // Save per-item foldouts for current world
        SaveItemFoldouts();
        SaveSelections();
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

        // Batch Edit Panel
        DrawBatchEditPanel();

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

    private void DrawBatchEditPanel()
    {
        EditorGUILayout.LabelField("Batch Edit", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope("box"))
        {
            // Planets column
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Planets", EditorStyles.miniBoldLabel);
                _batchPlanetApplySize = EditorGUILayout.ToggleLeft("Apply Size", _batchPlanetApplySize);
                using (new EditorGUI.DisabledScope(!_batchPlanetApplySize))
                {
                    _batchPlanetSize = (PlanetSize)EditorGUILayout.EnumPopup("Size", _batchPlanetSize);
                }
                _batchPlanetApplyType = EditorGUILayout.ToggleLeft("Apply Type", _batchPlanetApplyType);
                using (new EditorGUI.DisabledScope(!_batchPlanetApplyType))
                {
                    _batchPlanetType = (PlanetType)EditorGUILayout.EnumPopup("Type", _batchPlanetType);
                }
                _batchPlanetApplyMass = EditorGUILayout.ToggleLeft("Apply Mass", _batchPlanetApplyMass);
                using (new EditorGUI.DisabledScope(!_batchPlanetApplyMass))
                {
                    _batchPlanetMass = EditorGUILayout.FloatField("Mass", _batchPlanetMass);
                }
                _batchPlanetApplyProfile = EditorGUILayout.ToggleLeft("Apply Profile", _batchPlanetApplyProfile);
                using (new EditorGUI.DisabledScope(!_batchPlanetApplyProfile))
                {
                    _batchPlanetProfile = (PlanetProfile)EditorGUILayout.ObjectField("Profile", _batchPlanetProfile, typeof(PlanetProfile), false);
                }
                using (new EditorGUI.DisabledScope(_selPlanets == null || _selPlanets.Count == 0))
                {
                    if (GUILayout.Button($"Apply to {_selPlanets.Count} selected planet(s)"))
                    {
                        ApplyBatchToSelectedPlanets();
                    }
                }
            }

            GUILayout.Space(12);

            // Posts column
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Posts", EditorStyles.miniBoldLabel);
                _batchPostApplyLevel = EditorGUILayout.ToggleLeft("Apply Level", _batchPostApplyLevel);
                using (new EditorGUI.DisabledScope(!_batchPostApplyLevel))
                {
                    _batchPostLevel = Mathf.Max(1, EditorGUILayout.IntField("Level", _batchPostLevel));
                }
                _batchPostApplyInfluence = EditorGUILayout.ToggleLeft("Apply Influence Radius", _batchPostApplyInfluence);
                using (new EditorGUI.DisabledScope(!_batchPostApplyInfluence))
                {
                    _batchPostInfluence = EditorGUILayout.FloatField("Influence", _batchPostInfluence);
                }
                _batchPostApplyMaterial = EditorGUILayout.ToggleLeft("Apply Request Material", _batchPostApplyMaterial);
                using (new EditorGUI.DisabledScope(!_batchPostApplyMaterial))
                {
                    _batchPostMaterial = (PackageType)EditorGUILayout.ObjectField("Material", _batchPostMaterial, typeof(PackageType), false);
                }
                _batchPostApplyAmount = EditorGUILayout.ToggleLeft("Apply Request Amount", _batchPostApplyAmount);
                using (new EditorGUI.DisabledScope(!_batchPostApplyAmount))
                {
                    _batchPostAmount = Mathf.Max(1, EditorGUILayout.IntField("Amount", _batchPostAmount));
                }
                using (new EditorGUI.DisabledScope(_selPosts == null || _selPosts.Count == 0))
                {
                    if (GUILayout.Button($"Apply to {_selPosts.Count} selected post(s)"))
                    {
                        ApplyBatchToSelectedPosts();
                    }
                }
            }
        }
    }

    private void ApplyBatchToSelectedPlanets()
    {
        if (_world == null || _world.authoredPlanets == null || _selPlanets == null || _selPlanets.Count == 0) return;
        var indices = _selPlanets.Where(i => i >= 0 && i < _world.authoredPlanets.Length).Distinct().ToList();
        if (indices.Count == 0) return;
        Undo.RecordObject(_world, "Batch Edit Planets");
        for (int k = 0; k < indices.Count; k++)
        {
            int i = indices[k];
            var ap = _world.authoredPlanets[i];
            if (_batchPlanetApplySize) ap.size = _batchPlanetSize;
            if (_batchPlanetApplyType) ap.type = _batchPlanetType;
            if (_batchPlanetApplyMass) ap.mass = _batchPlanetMass;
            if (_batchPlanetApplyProfile) ap.profile = _batchPlanetProfile;
            _world.authoredPlanets[i] = ap;
        }
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void ApplyBatchToSelectedPosts()
    {
        if (_world == null || _world.authoredPosts == null || _selPosts == null || _selPosts.Count == 0) return;
        var indices = _selPosts.Where(i => i >= 0 && i < _world.authoredPosts.Length).Distinct().ToList();
        if (indices.Count == 0) return;
        Undo.RecordObject(_world, "Batch Edit Posts");
        for (int k = 0; k < indices.Count; k++)
        {
            int i = indices[k];
            var ap = _world.authoredPosts[i];
            if (_batchPostApplyLevel) ap.startLevel = Mathf.Max(1, _batchPostLevel);
            if (_batchPostApplyInfluence) ap.influenceRadius = _batchPostInfluence;
            if (_batchPostApplyMaterial) ap.initialRequestMaterial = _batchPostMaterial;
            if (_batchPostApplyAmount) ap.initialRequestAmount = Mathf.Max(1, _batchPostAmount);
            _world.authoredPosts[i] = ap;
        }
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
        if (_livePreview) PreviewWorld();
    }

    private void DrawPostsList()
    {
        if (_world.authoredPosts == null)
            _world.authoredPosts = new WorldDefinition.AuthoredPost[0];

        // Section foldout
        using (new EditorGUILayout.HorizontalScope())
        {
            bool newFold = EditorGUILayout.Foldout(_foldPosts, "Posts", true);
            if (newFold != _foldPosts) { _foldPosts = newFold; EditorPrefs.SetBool(PrefFoldPostsKey, _foldPosts); }
        }
        if (!_foldPosts) return;

        EditorGUILayout.Space();

        // Ensure per-item foldout states sized
        if (_postItemFoldouts == null || _postItemFoldouts.Length != _world.authoredPosts.Length)
        {
            var old = _postItemFoldouts;
            _postItemFoldouts = new bool[_world.authoredPosts.Length];
            for (int i = 0; i < _postItemFoldouts.Length; i++) _postItemFoldouts[i] = true;
        }

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
            if (GUILayout.Button("Expand All", GUILayout.Width(90))) { SetAllPostFoldouts(true); }
            if (GUILayout.Button("Collapse All", GUILayout.Width(100))) { SetAllPostFoldouts(false); }
            if (GUILayout.Button("All", GUILayout.Width(40)))
            {
                _selPosts = Enumerable.Range(0, total).ToList();
                SavePostSelection();
            }
            if (GUILayout.Button("None", GUILayout.Width(50)))
            {
                _selPosts.Clear();
                SavePostSelection();
            }
            // Align/Distribute
            using (new EditorGUI.DisabledScope(_selPosts.Count < 2))
            {
                if (GUILayout.Button("Align X", GUILayout.Width(80))) AlignSelectedPostsX();
                if (GUILayout.Button("Align Y", GUILayout.Width(80))) AlignSelectedPostsY();
                if (GUILayout.Button("Distribute X", GUILayout.Width(100))) DistributeSelectedPostsX();
                if (GUILayout.Button("Distribute Y", GUILayout.Width(100))) DistributeSelectedPostsY();
            }
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
                // Per-item foldout toggle
                bool open = i < _postItemFoldouts.Length ? _postItemFoldouts[i] : true;
                string arrow = open ? "▼" : "▶";
                if (GUILayout.Button(arrow, GUILayout.Width(18)))
                {
                    if (i < _postItemFoldouts.Length)
                    {
                        _postItemFoldouts[i] = !open;
                        SaveItemFoldoutsPosts();
                    }
                    open = !open;
                }
                bool sel = _selPosts.Contains(i);
                bool selNew = GUILayout.Toggle(sel, GUIContent.none, GUILayout.Width(18));
                if (selNew != sel)
                {
                    if (selNew) _selPosts.Add(i); else _selPosts.Remove(i);
                    SavePostSelection();
                }
                EditorGUILayout.LabelField($"#{i}", GUILayout.Width(28));
                ap.position = EditorGUILayout.Vector2Field("Position", ap.position);
                GUILayout.Label($"L{Mathf.Max(1, ap.startLevel)}", EditorStyles.miniBoldLabel, GUILayout.Width(28));
                // Validation badges
                DrawPostBadges(i, ap);
                if (GUILayout.Button("◉", GUILayout.Width(24))) FocusSceneOn(ap.position);
                if (GUILayout.Button("⧉", GUILayout.Width(24))) duplicateAt = i;
                if (GUILayout.Button("×", GUILayout.Width(24))) removeAt = i;
            }
            // Details foldout body
            if (i < _postItemFoldouts.Length && _postItemFoldouts[i])
            {
                ap.startLevel = Mathf.Max(1, EditorGUILayout.IntField("Start Level", ap.startLevel));
                ap.displayName = EditorGUILayout.TextField("Display Name", ap.displayName);
                ap.influenceRadius = EditorGUILayout.FloatField("Influence Radius", ap.influenceRadius);
                ap.initialRequestMaterial = (PackageType)EditorGUILayout.ObjectField("Initial Request Material", ap.initialRequestMaterial, typeof(PackageType), false);
                ap.initialRequestAmount = Mathf.Max(1, EditorGUILayout.IntField("Initial Request Amount", ap.initialRequestAmount));
            }

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
                // Expand foldouts for new item and save
                _postItemFoldouts = (_postItemFoldouts ?? new bool[0]).Concat(new[] { true }).ToArray();
                SaveItemFoldoutsPosts();
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
            // Insert corresponding foldout state
            if (_postItemFoldouts != null && _postItemFoldouts.Length >= duplicateAt + 1)
            {
                var f = _postItemFoldouts.ToList();
                f.Insert(duplicateAt + 1, true);
                _postItemFoldouts = f.ToArray();
                SaveItemFoldoutsPosts();
            }
            EditorUtility.SetDirty(_world);
            if (_livePreview) PreviewWorld();
        }

        if (removeAt >= 0)
        {
            Undo.RecordObject(_world, "Remove Post");
            _world.authoredPosts = _world.authoredPosts.Where((_, idx) => idx != removeAt).ToArray();
            if (_postItemFoldouts != null && _postItemFoldouts.Length > removeAt)
            {
                _postItemFoldouts = _postItemFoldouts.Where((_, idx) => idx != removeAt).ToArray();
                SaveItemFoldoutsPosts();
            }
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

    private void AlignSelectedPostsX()
    {
        var sel = _selPosts.Where(i => i >= 0 && i < (_world.authoredPosts?.Length ?? 0)).Distinct().ToList();
        if (sel.Count < 2) return;
        Undo.RecordObject(_world, "Align Posts X");
        float avg = sel.Average(i => _world.authoredPosts[i].position.x);
        for (int k = 0; k < sel.Count; k++)
        {
            var ap = _world.authoredPosts[sel[k]];
            ap.position.x = Mathf.Clamp(avg, -_world.halfExtents.x, _world.halfExtents.x);
            _world.authoredPosts[sel[k]] = ap;
        }
        EditorUtility.SetDirty(_world); Repaint(); SceneView.RepaintAll(); if (_livePreview) PreviewWorld();
    }

    private void AlignSelectedPostsY()
    {
        var sel = _selPosts.Where(i => i >= 0 && i < (_world.authoredPosts?.Length ?? 0)).Distinct().ToList();
        if (sel.Count < 2) return;
        Undo.RecordObject(_world, "Align Posts Y");
        float avg = sel.Average(i => _world.authoredPosts[i].position.y);
        for (int k = 0; k < sel.Count; k++)
        {
            var ap = _world.authoredPosts[sel[k]];
            ap.position.y = Mathf.Clamp(avg, -_world.halfExtents.y, _world.halfExtents.y);
            _world.authoredPosts[sel[k]] = ap;
        }
        EditorUtility.SetDirty(_world); Repaint(); SceneView.RepaintAll(); if (_livePreview) PreviewWorld();
    }

    private void DistributeSelectedPostsX()
    {
        var sel = _selPosts.Where(i => i >= 0 && i < (_world.authoredPosts?.Length ?? 0)).Distinct().OrderBy(i => _world.authoredPosts[i].position.x).ToList();
        if (sel.Count < 3) { AlignSelectedPostsX(); return; }
        Undo.RecordObject(_world, "Distribute Posts X");
        float min = _world.authoredPosts[sel.First()].position.x;
        float max = _world.authoredPosts[sel.Last()].position.x;
        if (Mathf.Approximately(min, max)) { AlignSelectedPostsX(); return; }
        for (int idx = 1; idx < sel.Count - 1; idx++)
        {
            float t = (float)idx / (sel.Count - 1);
            float x = Mathf.Lerp(min, max, t);
            var ap = _world.authoredPosts[sel[idx]];
            ap.position.x = Mathf.Clamp(x, -_world.halfExtents.x, _world.halfExtents.x);
            _world.authoredPosts[sel[idx]] = ap;
        }
        EditorUtility.SetDirty(_world); Repaint(); SceneView.RepaintAll(); if (_livePreview) PreviewWorld();
    }

    private void DistributeSelectedPostsY()
    {
        var sel = _selPosts.Where(i => i >= 0 && i < (_world.authoredPosts?.Length ?? 0)).Distinct().OrderBy(i => _world.authoredPosts[i].position.y).ToList();
        if (sel.Count < 3) { AlignSelectedPostsY(); return; }
        Undo.RecordObject(_world, "Distribute Posts Y");
        float min = _world.authoredPosts[sel.First()].position.y;
        float max = _world.authoredPosts[sel.Last()].position.y;
        if (Mathf.Approximately(min, max)) { AlignSelectedPostsY(); return; }
        for (int idx = 1; idx < sel.Count - 1; idx++)
        {
            float t = (float)idx / (sel.Count - 1);
            float y = Mathf.Lerp(min, max, t);
            var ap = _world.authoredPosts[sel[idx]];
            ap.position.y = Mathf.Clamp(y, -_world.halfExtents.y, _world.halfExtents.y);
            _world.authoredPosts[sel[idx]] = ap;
        }
        EditorUtility.SetDirty(_world); Repaint(); SceneView.RepaintAll(); if (_livePreview) PreviewWorld();
    }

    private void AlignSelectedPlanetsX()
    {
        var sel = _selPlanets.Where(i => i >= 0 && i < (_world.authoredPlanets?.Length ?? 0)).Distinct().ToList();
        if (sel.Count < 2) return;
        Undo.RecordObject(_world, "Align Planets X");
        float avg = sel.Average(i => _world.authoredPlanets[i].position.x);
        for (int k = 0; k < sel.Count; k++)
        {
            var ap = _world.authoredPlanets[sel[k]];
            float r = GetRadiusForSize(ap.size);
            float availX = Mathf.Max(0f, _world.halfExtents.x - r);
            ap.position.x = Mathf.Clamp(avg, -availX, availX);
            _world.authoredPlanets[sel[k]] = ap;
        }
        EditorUtility.SetDirty(_world); Repaint(); SceneView.RepaintAll(); if (_livePreview) PreviewWorld();
    }

    private void AlignSelectedPlanetsY()
    {
        var sel = _selPlanets.Where(i => i >= 0 && i < (_world.authoredPlanets?.Length ?? 0)).Distinct().ToList();
        if (sel.Count < 2) return;
        Undo.RecordObject(_world, "Align Planets Y");
        float avg = sel.Average(i => _world.authoredPlanets[i].position.y);
        for (int k = 0; k < sel.Count; k++)
        {
            var ap = _world.authoredPlanets[sel[k]];
            float r = GetRadiusForSize(ap.size);
            float availY = Mathf.Max(0f, _world.halfExtents.y - r);
            ap.position.y = Mathf.Clamp(avg, -availY, availY);
            _world.authoredPlanets[sel[k]] = ap;
        }
        EditorUtility.SetDirty(_world); Repaint(); SceneView.RepaintAll(); if (_livePreview) PreviewWorld();
    }

    private void DistributeSelectedPlanetsX()
    {
        var sel = _selPlanets.Where(i => i >= 0 && i < (_world.authoredPlanets?.Length ?? 0)).Distinct().OrderBy(i => _world.authoredPlanets[i].position.x).ToList();
        if (sel.Count < 3) { AlignSelectedPlanetsX(); return; }
        Undo.RecordObject(_world, "Distribute Planets X");
        float min = _world.authoredPlanets[sel.First()].position.x;
        float max = _world.authoredPlanets[sel.Last()].position.x;
        if (Mathf.Approximately(min, max)) { AlignSelectedPlanetsX(); return; }
        for (int idx = 1; idx < sel.Count - 1; idx++)
        {
            float t = (float)idx / (sel.Count - 1);
            float x = Mathf.Lerp(min, max, t);
            var ap = _world.authoredPlanets[sel[idx]];
            float r = GetRadiusForSize(ap.size);
            float availX = Mathf.Max(0f, _world.halfExtents.x - r);
            ap.position.x = Mathf.Clamp(x, -availX, availX);
            _world.authoredPlanets[sel[idx]] = ap;
        }
        EditorUtility.SetDirty(_world); Repaint(); SceneView.RepaintAll(); if (_livePreview) PreviewWorld();
    }

    private void DistributeSelectedPlanetsY()
    {
        var sel = _selPlanets.Where(i => i >= 0 && i < (_world.authoredPlanets?.Length ?? 0)).Distinct().OrderBy(i => _world.authoredPlanets[i].position.y).ToList();
        if (sel.Count < 3) { AlignSelectedPlanetsY(); return; }
        Undo.RecordObject(_world, "Distribute Planets Y");
        float min = _world.authoredPlanets[sel.First()].position.y;
        float max = _world.authoredPlanets[sel.Last()].position.y;
        if (Mathf.Approximately(min, max)) { AlignSelectedPlanetsY(); return; }
        for (int idx = 1; idx < sel.Count - 1; idx++)
        {
            float t = (float)idx / (sel.Count - 1);
            float y = Mathf.Lerp(min, max, t);
            var ap = _world.authoredPlanets[sel[idx]];
            float r = GetRadiusForSize(ap.size);
            float availY = Mathf.Max(0f, _world.halfExtents.y - r);
            ap.position.y = Mathf.Clamp(y, -availY, availY);
            _world.authoredPlanets[sel[idx]] = ap;
        }
        EditorUtility.SetDirty(_world); Repaint(); SceneView.RepaintAll(); if (_livePreview) PreviewWorld();
    }

    private void DrawPlanetsList()
    {
        if (_world.authoredPlanets == null)
            _world.authoredPlanets = new WorldDefinition.AuthoredPlanet[0];

        var planetOverlap = ComputePlanetOverlaps();

        // Section foldout
        using (new EditorGUILayout.HorizontalScope())
        {
            bool newFold = EditorGUILayout.Foldout(_foldPlanets, "Planets", true);
            if (newFold != _foldPlanets) { _foldPlanets = newFold; EditorPrefs.SetBool(PrefFoldPlanetsKey, _foldPlanets); }
        }
        if (!_foldPlanets) return;

        // Ensure per-item foldout array sized
        if (_planetItemFoldouts == null || _planetItemFoldouts.Length != _world.authoredPlanets.Length)
        {
            _planetItemFoldouts = new bool[_world.authoredPlanets.Length];
            for (int i = 0; i < _planetItemFoldouts.Length; i++) _planetItemFoldouts[i] = true;
        }

        // Ensure foldout arrays are sized and default to expanded
        var sizeValues = (PlanetSize[])System.Enum.GetValues(typeof(PlanetSize));
        var typeValues = (PlanetType[])System.Enum.GetValues(typeof(PlanetType));
        if (_sizeFoldouts == null || _sizeFoldouts.Length != sizeValues.Length)
        {
            _sizeFoldouts = new bool[sizeValues.Length];
            for (int i = 0; i < _sizeFoldouts.Length; i++) _sizeFoldouts[i] = true;
        }
        if (_typeFoldouts == null || _typeFoldouts.Length != typeValues.Length)
        {
            _typeFoldouts = new bool[typeValues.Length];
            for (int i = 0; i < _typeFoldouts.Length; i++) _typeFoldouts[i] = true;
        }

        // Grouping selector
        using (new EditorGUILayout.VerticalScope("box"))
        {
            var prevGroup = _groupBy;
            _groupBy = (PlanetGroupBy)EditorGUILayout.EnumPopup("Group By", _groupBy);
            if (_groupBy != prevGroup) EditorPrefs.SetInt(PrefGroupByKey, (int)_groupBy);
        }

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
            if (GUILayout.Button("Expand All", GUILayout.Width(90))) { SetAllPlanetFoldouts(true); }
            if (GUILayout.Button("Collapse All", GUILayout.Width(100))) { SetAllPlanetFoldouts(false); }
            if (GUILayout.Button("All", GUILayout.Width(40)))
            {
                _selPlanets = Enumerable.Range(0, total).ToList();
                SavePlanetSelection();
            }
            if (GUILayout.Button("None", GUILayout.Width(50)))
            {
                _selPlanets.Clear();
                SavePlanetSelection();
            }
            using (new EditorGUI.DisabledScope(_selPlanets.Count < 2))
            {
                if (GUILayout.Button("Align X", GUILayout.Width(80))) AlignSelectedPlanetsX();
                if (GUILayout.Button("Align Y", GUILayout.Width(80))) AlignSelectedPlanetsY();
                if (GUILayout.Button("Distribute X", GUILayout.Width(100))) DistributeSelectedPlanetsX();
                if (GUILayout.Button("Distribute Y", GUILayout.Width(100))) DistributeSelectedPlanetsY();
            }
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
        // Draw rows grouped by selection
        System.Action<int> DrawPlanetRow = (idx) =>
        {
            var ap = _world.authoredPlanets[idx];
            EditorGUILayout.BeginVertical("box");
            using (new EditorGUILayout.HorizontalScope())
            {
                bool open = (idx < _planetItemFoldouts.Length) ? _planetItemFoldouts[idx] : true;
                string arrow = open ? "▼" : "▶";
                if (GUILayout.Button(arrow, GUILayout.Width(18)))
                {
                    if (idx < _planetItemFoldouts.Length)
                    {
                        _planetItemFoldouts[idx] = !open;
                        SaveItemFoldoutsPlanets();
                    }
                    open = !open;
                }
                bool sel = _selPlanets.Contains(idx);
                bool selNew = GUILayout.Toggle(sel, GUIContent.none, GUILayout.Width(18));
                if (selNew != sel)
                {
                    if (selNew) _selPlanets.Add(idx); else _selPlanets.Remove(idx);
                    SavePlanetSelection();
                }
                EditorGUILayout.LabelField($"#{idx}", GUILayout.Width(28));
                ap.position = EditorGUILayout.Vector2Field("Position", ap.position);
                string badge = $"{ap.size}/{ap.type}";
                GUILayout.Label(badge, EditorStyles.miniLabel, GUILayout.Width(120));
                DrawPlanetBadges(idx, ap, planetOverlap);
                if (GUILayout.Button("◉", GUILayout.Width(24))) FocusSceneOn(ap.position);
                if (GUILayout.Button("⧉", GUILayout.Width(24))) duplicateAt = idx;
                if (GUILayout.Button("×", GUILayout.Width(24))) removeAt = idx;
            }
            if (idx < _planetItemFoldouts.Length && _planetItemFoldouts[idx])
            {
                ap.size  = (PlanetSize)EditorGUILayout.EnumPopup("Size", ap.size);
                ap.type  = (PlanetType)EditorGUILayout.EnumPopup("Type", ap.type);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Size Presets", GUILayout.Width(85));
                    if (GUILayout.Button("+Small", EditorStyles.miniButton, GUILayout.Width(60))) ap.size = PlanetSize.Small;
                    if (GUILayout.Button("+Medium", EditorStyles.miniButton, GUILayout.Width(70))) ap.size = PlanetSize.Medium;
                    if (GUILayout.Button("+Large", EditorStyles.miniButton, GUILayout.Width(60))) ap.size = PlanetSize.Large;
                }
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
                        if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(64))) ap.type = t;
                        GUI.backgroundColor = prevBg;
                    }
                }
                ap.mass  = EditorGUILayout.FloatField(new GUIContent("Mass", "Independent mass per planet; gravity ignores 0."), ap.mass);
                ap.profile = (PlanetProfile)EditorGUILayout.ObjectField("Profile (optional)", ap.profile, typeof(PlanetProfile), false);
                float radius = GetRadiusForSize(ap.size);
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.FloatField("Radius (from prefab)", radius);
            }
            _world.authoredPlanets[idx] = ap;
            EditorGUILayout.EndVertical();
        };

        if (_groupBy == PlanetGroupBy.None)
        {
            for (int i = 0; i < _world.authoredPlanets.Length; i++)
            {
                var ap = _world.authoredPlanets[i];
                if (_filterBySize && ap.size != _filterSize) continue;
                if (_filterByType && ap.type != _filterType) continue;
                DrawPlanetRow(i);
            }
        }
        else if (_groupBy == PlanetGroupBy.Size)
        {
            var sizeValuesLocal = (PlanetSize[])System.Enum.GetValues(typeof(PlanetSize));
            for (int gi = 0; gi < sizeValuesLocal.Length; gi++)
            {
                int count = 0;
                for (int i = 0; i < _world.authoredPlanets.Length; i++)
                {
                    var ap = _world.authoredPlanets[i];
                    if (ap.size != sizeValuesLocal[gi]) continue;
                    if (_filterBySize && ap.size != _filterSize) continue;
                    if (_filterByType && ap.type != _filterType) continue;
                    count++;
                }
                _sizeFoldouts[gi] = EditorGUILayout.Foldout(_sizeFoldouts[gi], $"Size: {sizeValuesLocal[gi]} ({count})");
                if (!_sizeFoldouts[gi]) continue;
                for (int i = 0; i < _world.authoredPlanets.Length; i++)
                {
                    var ap = _world.authoredPlanets[i];
                    if (ap.size != sizeValuesLocal[gi]) continue;
                    if (_filterBySize && ap.size != _filterSize) continue;
                    if (_filterByType && ap.type != _filterType) continue;
                    DrawPlanetRow(i);
                }
            }
        }
        else if (_groupBy == PlanetGroupBy.Type)
        {
            var typeValuesLocal = (PlanetType[])System.Enum.GetValues(typeof(PlanetType));
            for (int gi = 0; gi < typeValuesLocal.Length; gi++)
            {
                int count = 0;
                for (int i = 0; i < _world.authoredPlanets.Length; i++)
                {
                    var ap = _world.authoredPlanets[i];
                    if (ap.type != typeValuesLocal[gi]) continue;
                    if (_filterBySize && ap.size != _filterSize) continue;
                    if (_filterByType && ap.type != _filterType) continue;
                    count++;
                }
                _typeFoldouts[gi] = EditorGUILayout.Foldout(_typeFoldouts[gi], $"Type: {typeValuesLocal[gi]} ({count})");
                if (!_typeFoldouts[gi]) continue;
                for (int i = 0; i < _world.authoredPlanets.Length; i++)
                {
                    var ap = _world.authoredPlanets[i];
                    if (ap.type != typeValuesLocal[gi]) continue;
                    if (_filterBySize && ap.size != _filterSize) continue;
                    if (_filterByType && ap.type != _filterType) continue;
                    DrawPlanetRow(i);
                }
            }
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
            if (_planetItemFoldouts != null && _planetItemFoldouts.Length >= duplicateAt + 1)
            {
                var f = _planetItemFoldouts.ToList();
                f.Insert(duplicateAt + 1, true);
                _planetItemFoldouts = f.ToArray();
                SaveItemFoldoutsPlanets();
            }
            EditorUtility.SetDirty(_world);
            if (_livePreview) PreviewWorld();
        }

        if (removeAt >= 0)
        {
            Undo.RecordObject(_world, "Remove Planet");
            _world.authoredPlanets = _world.authoredPlanets.Where((_, idx) => idx != removeAt).ToArray();
            if (_planetItemFoldouts != null && _planetItemFoldouts.Length > removeAt)
            {
                _planetItemFoldouts = _planetItemFoldouts.Where((_, idx) => idx != removeAt).ToArray();
                SaveItemFoldoutsPlanets();
            }
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
        // Expand foldouts for new item and save
        _planetItemFoldouts = (_planetItemFoldouts ?? new bool[0]).Concat(new[] { true }).ToArray();
        SaveItemFoldoutsPlanets();
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

        HandleBoxSelect(sv);
        HandleClickSelection();
        HandleKeyboardNudge();

        // Reset group-drag state on mouse up
        var e = Event.current;
        if (e != null && e.rawType == EventType.MouseUp)
        {
            _draggingPlanets = false; _draggingPosts = false;
            _dragAnchorPlanetIndex = -1; _dragAnchorPostIndex = -1;
            _dragStartPlanets = null; _dragStartPosts = null;
        }

        // Scene overlay: small toolbar for grid, snap size, and bounds toggle
        Handles.BeginGUI();
        {
            // Expand overlay to avoid clipping and accidental deselects
            _overlayRect = new Rect(12, 12, 420, 220);
            GUILayout.BeginArea(_overlayRect, GUIContent.none, GUI.skin.box);
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
            using (new EditorGUILayout.HorizontalScope())
            {
                bool prevAlign = _alignSnapEnabled;
                float prevThresh = _alignSnapThreshold;
                _alignSnapEnabled = GUILayout.Toggle(_alignSnapEnabled, new GUIContent("Align", "Enable alignment snapping to X/Y of nearby items"), GUILayout.Width(64));
                GUILayout.Label("Thresh", GUILayout.Width(48));
                string tStr = GUILayout.TextField(_alignSnapThreshold.ToString("0.###"), GUILayout.Width(52));
                if (float.TryParse(tStr, out float tParsed))
                    _alignSnapThreshold = Mathf.Max(0.001f, tParsed);
                if (_alignSnapEnabled != prevAlign) EditorPrefs.SetBool(PrefAlignEnabledKey, _alignSnapEnabled);
                if (!Mathf.Approximately(_alignSnapThreshold, prevThresh)) EditorPrefs.SetFloat(PrefAlignThresholdKey, Mathf.Max(0.001f, _alignSnapThreshold));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool prevBox = _boxSelectEnabled;
                bool prevBP = _boxSelectPlanets;
                bool prevBL = _boxSelectPosts;
                _boxSelectEnabled = GUILayout.Toggle(_boxSelectEnabled, new GUIContent("Box", "Enable box selection (Shift+Drag)"), GUILayout.Width(54));
                _boxSelectPlanets = GUILayout.Toggle(_boxSelectPlanets, new GUIContent("Planets", "Include Planets in box select"), GUILayout.Width(72));
                _boxSelectPosts = GUILayout.Toggle(_boxSelectPosts, new GUIContent("Posts", "Include Posts in box select"), GUILayout.Width(64));
                if (_boxSelectEnabled != prevBox) EditorPrefs.SetBool(PrefBoxEnabledKey, _boxSelectEnabled);
                if (_boxSelectPlanets != prevBP) EditorPrefs.SetBool(PrefBoxPlanetsKey, _boxSelectPlanets);
                if (_boxSelectPosts != prevBL) EditorPrefs.SetBool(PrefBoxPostsKey, _boxSelectPosts);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool prevMaster = _showInfo;
                _showInfo = GUILayout.Toggle(_showInfo, new GUIContent("Info", "Toggle all info bubbles"), GUILayout.Width(56));
                if (_showInfo != prevMaster)
                {
                    _showPlanetInfo = _showInfo;
                    _showPostInfo = _showInfo;
                    EditorPrefs.SetBool(PrefShowPlanetInfoKey, _showPlanetInfo);
                    EditorPrefs.SetBool(PrefShowPostInfoKey, _showPostInfo);
                    EditorPrefs.SetBool(PrefShowInfoKey, _showInfo);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool prevPI = _showPlanetInfo;
                bool prevPoI = _showPostInfo;
                _showPlanetInfo = GUILayout.Toggle(_showPlanetInfo, new GUIContent("Planet Info", "Show info bubbles above planets"), GUILayout.Width(100));
                _showPostInfo = GUILayout.Toggle(_showPostInfo, new GUIContent("Post Info", "Show info bubbles above posts"), GUILayout.Width(90));
                if (_showPlanetInfo != prevPI) EditorPrefs.SetBool(PrefShowPlanetInfoKey, _showPlanetInfo);
                if (_showPostInfo != prevPoI) EditorPrefs.SetBool(PrefShowPostInfoKey, _showPostInfo);
                // Keep master in sync: true only if both are true
                _showInfo = _showPlanetInfo && _showPostInfo;
                EditorPrefs.SetBool(PrefShowInfoKey, _showInfo);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Move", GUILayout.Width(36));
                string stepStr = GUILayout.TextField(_nudgeStep.ToString("0.###"), GUILayout.Width(56));
                if (float.TryParse(stepStr, out float stepParsed)) _nudgeStep = Mathf.Max(0.001f, stepParsed);
                using (new EditorGUI.DisabledScope((_selPlanets?.Count ?? 0) + (_selPosts?.Count ?? 0) == 0))
                {
                    if (GUILayout.Button("←", GUILayout.Width(28))) NudgeSelection(new Vector2(-_nudgeStep, 0f));
                    if (GUILayout.Button("→", GUILayout.Width(28))) NudgeSelection(new Vector2(_nudgeStep, 0f));
                    if (GUILayout.Button("↑", GUILayout.Width(28))) NudgeSelection(new Vector2(0f, _nudgeStep));
                    if (GUILayout.Button("↓", GUILayout.Width(28))) NudgeSelection(new Vector2(0f, -_nudgeStep));
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Modifiers: Shift=Align • Alt=NoAlign • Ctrl/Cmd=Constrain Axis", EditorStyles.miniLabel);
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

        // Scene validation guides: overlaps and out-of-bounds hints
        DrawSceneValidationGuides();

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

                // Selection highlight for posts (small ring at center)
                bool postSelected = _selPosts != null && _selPosts.Contains(i);
                if (postSelected)
                {
                    Handles.color = new Color(1f, 0.95f, 0.2f, 0.95f);
                    float rr = Mathf.Max(0.25f, Mathf.Min(0.6f, ap.influenceRadius * 0.08f));
                    Handles.DrawWireDisc((Vector3)ap.position, Vector3.forward, rr);
                    Handles.DrawWireDisc((Vector3)ap.position, Vector3.forward, rr + 0.03f);
                }

            // Position handle (2D) with optional snapping and bounds clamp
            EditorGUI.BeginChangeCheck();
            Vector3 p3 = Handles.FreeMoveHandle((Vector3)ap.position, 0.12f, Vector3.zero, Handles.DotHandleCap);
            Vector2 oldPostPos = ap.position;
                // Modifiers
                var evt = Event.current;
                bool axisConstrain = evt != null && (evt.control || evt.command);
                bool gridActive = _snapEnabled; // could add Alt to disable if desired
                bool alignActive = _alignSnapEnabled;
                if (evt != null)
                {
                    if (evt.shift) alignActive = true;
                    if (evt.alt) alignActive = false;
                }
                if (gridActive)
                {
                    float s = Mathf.Max(0.01f, _snapSize);
                    p3.x = Mathf.Round(p3.x / s) * s;
                    p3.y = Mathf.Round(p3.y / s) * s;
                }
                // Axis constrain (lock axis by dominant delta)
                if (axisConstrain)
                {
                    Vector2 d = (Vector2)p3 - ap.position;
                    if (Mathf.Abs(d.x) >= Mathf.Abs(d.y)) p3.y = ap.position.y; else p3.x = ap.position.x;
                }
                // Alignment snap (to other posts' X/Y and world axes) + draw guides
                if (alignActive)
                {
                    float guideX, guideY;
                    ApplyAlignSnapPosts(i, ref p3, out guideX, out guideY);
                    DrawAlignGuides(guideX, guideY);
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
                    // Initialize group drag if needed
                    bool thisSelected = _selPosts != null && _selPosts.Contains(i);
                    if (!_draggingPosts && thisSelected && _selPosts != null && _selPosts.Count > 1)
                    {
                        _draggingPosts = true;
                        _dragAnchorPostIndex = i;
                        _dragAnchorPostStart = oldPostPos;
                        _dragStartPosts = new System.Collections.Generic.Dictionary<int, Vector2>(_selPosts.Count);
                        for (int si = 0; si < _selPosts.Count; si++)
                        {
                            int idx = _selPosts[si];
                            if (idx < 0 || idx >= _world.authoredPosts.Length) continue;
                            _dragStartPosts[idx] = _world.authoredPosts[idx].position;
                        }
                        // Also snapshot selected planets so drag can move both kinds together
                        if (_selPlanets != null && _selPlanets.Count > 0)
                        {
                            _dragStartPlanets = new System.Collections.Generic.Dictionary<int, Vector2>(_selPlanets.Count);
                            for (int sp = 0; sp < _selPlanets.Count; sp++)
                            {
                                int pidx = _selPlanets[sp];
                                if (pidx < 0 || pidx >= (_world.authoredPlanets?.Length ?? 0)) continue;
                                _dragStartPlanets[pidx] = _world.authoredPlanets[pidx].position;
                            }
                        }
                    }
                    // Compute delta and apply to all selected posts for multi-drag (anchor-based)
                    if (_draggingPosts && _dragAnchorPostIndex == i && _dragStartPosts != null)
                    {
                        Vector2 delta = clampedP - _dragAnchorPostStart;
                        var keys = _dragStartPosts.Keys.ToList();
                        for (int si = 0; si < keys.Count; si++)
                        {
                            int idx = keys[si];
                            if (idx == i) continue;
                            var startPos = _dragStartPosts[idx];
                            var np = startPos + delta;
                            np.x = Mathf.Clamp(np.x, -hxP, hxP);
                            np.y = Mathf.Clamp(np.y, -hyP, hyP);
                            var op = _world.authoredPosts[idx];
                            op.position = np;
                            _world.authoredPosts[idx] = op;
                        }
                        // Apply same delta to selected planets (radius-aware clamp)
                        if (_dragStartPlanets != null && _world.authoredPlanets != null)
                        {
                            var pkeys = _dragStartPlanets.Keys.ToList();
                            for (int pi = 0; pi < pkeys.Count; pi++)
                            {
                                int pidx = pkeys[pi];
                                var startPos = _dragStartPlanets[pidx];
                                var op = _world.authoredPlanets[pidx];
                                float rOther = Mathf.Max(0.01f, GetRadiusForSize(op.size));
                                float ax = Mathf.Max(0f, _world.halfExtents.x - rOther);
                                float ay = Mathf.Max(0f, _world.halfExtents.y - rOther);
                                var np = startPos + delta;
                                np.x = Mathf.Clamp(np.x, -ax, ax);
                                np.y = Mathf.Clamp(np.y, -ay, ay);
                                op.position = np;
                                _world.authoredPlanets[pidx] = op;
                            }
                        }
                    }
                    ap.position = clampedP;
                    _world.authoredPosts[i] = ap;
                    EditorUtility.SetDirty(_world);
                    if (_livePreview) PreviewWorld();
                }

                // Info bubble
                if (_showPostInfo)
                {
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
        }

        for (int i = 0; i < _world.authoredPlanets.Length; i++)
        {
            var ap = _world.authoredPlanets[i];
            float radius = Mathf.Max(0.01f, GetRadiusForSize(ap.size));

            // Color by type for readability
            Handles.color = TypeColor(ap.type);

            // Draw disc for visual radius
            Handles.DrawWireDisc((Vector3)ap.position, Vector3.forward, radius);

            // Selection highlight overlay for planets
            bool planetSelected = _selPlanets != null && _selPlanets.Contains(i);
            if (planetSelected)
            {
                var hi = new Color(0.2f, 1f, 1f, 0.95f);
                Handles.color = hi;
                Handles.DrawWireDisc((Vector3)ap.position, Vector3.forward, radius + 0.03f);
                Handles.DrawWireDisc((Vector3)ap.position, Vector3.forward, radius + 0.06f);
            }

            // Position handle (2D)
            EditorGUI.BeginChangeCheck();
            Vector3 pos3 = Handles.FreeMoveHandle((Vector3)ap.position, 0.12f, Vector3.zero, Handles.DotHandleCap);
            Vector2 oldPlanetPos = ap.position;
            // Optional snap to grid
            var evtP = Event.current;
            bool axisConstrainP = evtP != null && (evtP.control || evtP.command);
            bool gridActiveP = _snapEnabled;
            bool alignActiveP = _alignSnapEnabled;
            if (evtP != null)
            {
                if (evtP.shift) alignActiveP = true;
                if (evtP.alt) alignActiveP = false;
            }
            if (gridActiveP)
            {
                float s = Mathf.Max(0.01f, _snapSize);
                pos3.x = Mathf.Round(pos3.x / s) * s;
                pos3.y = Mathf.Round(pos3.y / s) * s;
            }
            // Axis constrain
            if (axisConstrainP)
            {
                Vector2 d = (Vector2)pos3 - ap.position;
                if (Mathf.Abs(d.x) >= Mathf.Abs(d.y)) pos3.y = ap.position.y; else pos3.x = ap.position.x;
            }
            // Alignment snap (to other planets' X/Y and world axes) + draw guides
            if (alignActiveP)
            {
                float guideX, guideY;
                ApplyAlignSnapPlanets(i, ref pos3, out guideX, out guideY);
                DrawAlignGuides(guideX, guideY);
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
                // Initialize group drag if needed
                bool thisSelected = _selPlanets != null && _selPlanets.Contains(i);
                if (!_draggingPlanets && thisSelected && _selPlanets != null && _selPlanets.Count > 1)
                {
                    _draggingPlanets = true;
                    _dragAnchorPlanetIndex = i;
                    _dragAnchorPlanetStart = oldPlanetPos;
                    _dragStartPlanets = new System.Collections.Generic.Dictionary<int, Vector2>(_selPlanets.Count);
                    for (int si = 0; si < _selPlanets.Count; si++)
                    {
                        int idx = _selPlanets[si];
                        if (idx < 0 || idx >= _world.authoredPlanets.Length) continue;
                        _dragStartPlanets[idx] = _world.authoredPlanets[idx].position;
                    }
                    // Also snapshot selected posts so drag can move both kinds together
                    if (_selPosts != null && _selPosts.Count > 0)
                    {
                        _dragStartPosts = new System.Collections.Generic.Dictionary<int, Vector2>(_selPosts.Count);
                        for (int sp = 0; sp < _selPosts.Count; sp++)
                        {
                            int sidx = _selPosts[sp];
                            if (sidx < 0 || sidx >= (_world.authoredPosts?.Length ?? 0)) continue;
                            _dragStartPosts[sidx] = _world.authoredPosts[sidx].position;
                        }
                    }
                }
                // Compute delta and apply to all selected planets for multi-drag (anchor-based)
                if (_draggingPlanets && _dragAnchorPlanetIndex == i && _dragStartPlanets != null)
                {
                    Vector2 delta = clamped - _dragAnchorPlanetStart;
                    var keys = _dragStartPlanets.Keys.ToList();
                    for (int si = 0; si < keys.Count; si++)
                    {
                        int idx = keys[si];
                        if (idx == i) continue;
                        var startPos = _dragStartPlanets[idx];
                        var op = _world.authoredPlanets[idx];
                        float rOther = Mathf.Max(0.01f, GetRadiusForSize(op.size));
                        float ax = Mathf.Max(0f, _world.halfExtents.x - rOther);
                        float ay = Mathf.Max(0f, _world.halfExtents.y - rOther);
                        var np = startPos + delta;
                        np.x = Mathf.Clamp(np.x, -ax, ax);
                        np.y = Mathf.Clamp(np.y, -ay, ay);
                        op.position = np;
                        _world.authoredPlanets[idx] = op;
                    }
                    // Apply same delta to selected posts
                    if (_dragStartPosts != null && _world.authoredPosts != null)
                    {
                        var skeys = _dragStartPosts.Keys.ToList();
                        float hxP = _world.halfExtents.x; float hyP = _world.halfExtents.y;
                        for (int si = 0; si < skeys.Count; si++)
                        {
                            int sidx = skeys[si];
                            var startPos = _dragStartPosts[sidx];
                            var op = _world.authoredPosts[sidx];
                            var np = startPos + delta;
                            np.x = Mathf.Clamp(np.x, -hxP, hxP);
                            np.y = Mathf.Clamp(np.y, -hyP, hyP);
                            op.position = np;
                            _world.authoredPosts[sidx] = op;
                        }
                    }
                }
                ap.position = clamped;
                _world.authoredPlanets[i] = ap;
                EditorUtility.SetDirty(_world);
                if (_livePreview) PreviewWorld();
            }

            // Info bubble
            if (_showPlanetInfo)
            {
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

    // --- Persisted foldout helpers ---
    private string GetWorldKeySuffix()
    {
        if (!_world) return "(no_world)";
        string path = AssetDatabase.GetAssetPath(_world);
        string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        if (!string.IsNullOrEmpty(guid)) return guid;
        return _world.name;
    }

    private void TryLoadItemFoldouts()
    {
        LoadItemFoldoutsPosts();
        LoadItemFoldoutsPlanets();
    }

    private void TryLoadSelections()
    {
        LoadPostSelection();
        LoadPlanetSelection();
    }

    private void LoadItemFoldoutsPosts()
    {
        if (_world == null) return;
        string key = $"WorldDesigner_PostFoldouts_{GetWorldKeySuffix()}";
        string data = EditorPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(data)) return;
        int n = _world.authoredPosts != null ? _world.authoredPosts.Length : 0;
        if (n <= 0) return;
        if (_postItemFoldouts == null || _postItemFoldouts.Length != n)
            _postItemFoldouts = new bool[n];
        for (int i = 0; i < n; i++)
        {
            _postItemFoldouts[i] = (i < data.Length) ? (data[i] == '1') : true;
        }
    }

    private void SaveItemFoldoutsPosts()
    {
        if (_world == null || _postItemFoldouts == null) return;
        var sb = new StringBuilder(_postItemFoldouts.Length);
        for (int i = 0; i < _postItemFoldouts.Length; i++) sb.Append(_postItemFoldouts[i] ? '1' : '0');
        string key = $"WorldDesigner_PostFoldouts_{GetWorldKeySuffix()}";
        EditorPrefs.SetString(key, sb.ToString());
    }

    private void LoadItemFoldoutsPlanets()
    {
        if (_world == null) return;
        string key = $"WorldDesigner_PlanetFoldouts_{GetWorldKeySuffix()}";
        string data = EditorPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(data)) return;
        int n = _world.authoredPlanets != null ? _world.authoredPlanets.Length : 0;
        if (n <= 0) return;
        if (_planetItemFoldouts == null || _planetItemFoldouts.Length != n)
            _planetItemFoldouts = new bool[n];
        for (int i = 0; i < n; i++)
        {
            _planetItemFoldouts[i] = (i < data.Length) ? (data[i] == '1') : true;
        }
    }

    private void SaveItemFoldoutsPlanets()
    {
        if (_world == null || _planetItemFoldouts == null) return;
        var sb = new StringBuilder(_planetItemFoldouts.Length);
        for (int i = 0; i < _planetItemFoldouts.Length; i++) sb.Append(_planetItemFoldouts[i] ? '1' : '0');
        string key = $"WorldDesigner_PlanetFoldouts_{GetWorldKeySuffix()}";
        EditorPrefs.SetString(key, sb.ToString());
    }

    private void SaveItemFoldouts()
    {
        SaveItemFoldoutsPosts();
        SaveItemFoldoutsPlanets();
    }

    private void SaveSelections()
    {
        SavePostSelection();
        SavePlanetSelection();
    }

    private void LoadPostSelection()
    {
        _selPosts.Clear();
        if (_world == null) return;
        string key = $"{PrefPostSelectionKey}_{GetWorldKeySuffix()}";
        string data = EditorPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(data)) return;
        var parts = data.Split(',');
        int count = _world.authoredPosts != null ? _world.authoredPosts.Length : 0;
        foreach (var p in parts)
        {
            if (int.TryParse(p, out int idx) && idx >= 0 && idx < count)
                _selPosts.Add(idx);
        }
    }

    private void SavePostSelection()
    {
        if (_world == null) return;
        string key = $"{PrefPostSelectionKey}_{GetWorldKeySuffix()}";
        var uniq = _selPosts.Distinct().OrderBy(i => i);
        string data = string.Join(",", uniq);
        EditorPrefs.SetString(key, data);
    }

    private void LoadPlanetSelection()
    {
        _selPlanets.Clear();
        if (_world == null) return;
        string key = $"{PrefPlanetSelectionKey}_{GetWorldKeySuffix()}";
        string data = EditorPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(data)) return;
        var parts = data.Split(',');
        int count = _world.authoredPlanets != null ? _world.authoredPlanets.Length : 0;
        foreach (var p in parts)
        {
            if (int.TryParse(p, out int idx) && idx >= 0 && idx < count)
                _selPlanets.Add(idx);
        }
    }

    private void SavePlanetSelection()
    {
        if (_world == null) return;
        string key = $"{PrefPlanetSelectionKey}_{GetWorldKeySuffix()}";
        var uniq = _selPlanets.Distinct().OrderBy(i => i);
        string data = string.Join(",", uniq);
        EditorPrefs.SetString(key, data);
    }

    private void SetAllPostFoldouts(bool open)
    {
        if (_world == null) return;
        int n = _world.authoredPosts != null ? _world.authoredPosts.Length : 0;
        if (_postItemFoldouts == null || _postItemFoldouts.Length != n) _postItemFoldouts = new bool[n];
        for (int i = 0; i < n; i++) _postItemFoldouts[i] = open;
        SaveItemFoldoutsPosts();
    }

    private void SetAllPlanetFoldouts(bool open)
    {
        if (_world == null) return;
        int n = _world.authoredPlanets != null ? _world.authoredPlanets.Length : 0;
        if (_planetItemFoldouts == null || _planetItemFoldouts.Length != n) _planetItemFoldouts = new bool[n];
        for (int i = 0; i < n; i++) _planetItemFoldouts[i] = open;
        SaveItemFoldoutsPlanets();
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

    // --- Alignment snap helpers ---
    private void ApplyAlignSnapPosts(int movingIndex, ref Vector3 pos, out float guideX, out float guideY)
    {
        guideX = float.NaN; guideY = float.NaN;
        if (_world == null || _world.authoredPosts == null) return;
        float bestDx = _alignSnapThreshold + 1f;
        float bestDy = _alignSnapThreshold + 1f;
        // Candidates: world axes
        float[] xCands = new float[] { 0f };
        float[] yCands = new float[] { 0f };
        // From other posts
        for (int i = 0; i < _world.authoredPosts.Length; i++)
        {
            if (i == movingIndex) continue;
            var p = _world.authoredPosts[i].position;
            CheckAlignCandidate(pos.x, p.x, ref bestDx, ref guideX);
            CheckAlignCandidate(pos.y, p.y, ref bestDy, ref guideY);
        }
        // World axes
        CheckAlignCandidate(pos.x, 0f, ref bestDx, ref guideX);
        CheckAlignCandidate(pos.y, 0f, ref bestDy, ref guideY);

        if (!float.IsNaN(guideX) && Mathf.Abs(pos.x - guideX) <= _alignSnapThreshold) pos.x = guideX;
        if (!float.IsNaN(guideY) && Mathf.Abs(pos.y - guideY) <= _alignSnapThreshold) pos.y = guideY;
    }

    private void ApplyAlignSnapPlanets(int movingIndex, ref Vector3 pos, out float guideX, out float guideY)
    {
        guideX = float.NaN; guideY = float.NaN;
        if (_world == null || _world.authoredPlanets == null) return;
        float bestDx = _alignSnapThreshold + 1f;
        float bestDy = _alignSnapThreshold + 1f;
        for (int i = 0; i < _world.authoredPlanets.Length; i++)
        {
            if (i == movingIndex) continue;
            var p = _world.authoredPlanets[i].position;
            CheckAlignCandidate(pos.x, p.x, ref bestDx, ref guideX);
            CheckAlignCandidate(pos.y, p.y, ref bestDy, ref guideY);
        }
        // World axes
        CheckAlignCandidate(pos.x, 0f, ref bestDx, ref guideX);
        CheckAlignCandidate(pos.y, 0f, ref bestDy, ref guideY);

        if (!float.IsNaN(guideX) && Mathf.Abs(pos.x - guideX) <= _alignSnapThreshold) pos.x = guideX;
        if (!float.IsNaN(guideY) && Mathf.Abs(pos.y - guideY) <= _alignSnapThreshold) pos.y = guideY;
    }

    private void CheckAlignCandidate(float current, float candidate, ref float bestD, ref float guide)
    {
        float d = Mathf.Abs(current - candidate);
        if (d <= _alignSnapThreshold && d < bestD)
        {
            bestD = d;
            guide = candidate;
        }
    }

    private void DrawAlignGuides(float guideX, float guideY)
    {
        float hx = _world.halfExtents.x;
        float hy = _world.halfExtents.y;
        if (!float.IsNaN(guideX))
        {
            Handles.color = new Color(1f, 0.2f, 0.8f, 0.8f);
            Handles.DrawLine(new Vector3(guideX, -hy, 0f), new Vector3(guideX, hy, 0f));
        }
        if (!float.IsNaN(guideY))
        {
            Handles.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Handles.DrawLine(new Vector3(-hx, guideY, 0f), new Vector3(hx, guideY, 0f));
        }
    }

    private void NudgeSelection(Vector2 delta)
    {
        if (_world == null) return;
        bool any = false;
        Undo.RecordObject(_world, "Nudge Selection");
        // Planets (radius-aware clamp)
        if (_selPlanets != null && _world.authoredPlanets != null)
        {
            for (int i = 0; i < _selPlanets.Count; i++)
            {
                int idx = _selPlanets[i];
                if (idx < 0 || idx >= _world.authoredPlanets.Length) continue;
                var ap = _world.authoredPlanets[idx];
                float r = Mathf.Max(0.01f, GetRadiusForSize(ap.size));
                float ax = Mathf.Max(0f, _world.halfExtents.x - r);
                float ay = Mathf.Max(0f, _world.halfExtents.y - r);
                var np = ap.position + delta;
                np.x = Mathf.Clamp(np.x, -ax, ax);
                np.y = Mathf.Clamp(np.y, -ay, ay);
                if (np != ap.position) { ap.position = np; _world.authoredPlanets[idx] = ap; any = true; }
            }
        }
        // Posts (bounds clamp)
        if (_selPosts != null && _world.authoredPosts != null)
        {
            for (int i = 0; i < _selPosts.Count; i++)
            {
                int idx = _selPosts[i];
                if (idx < 0 || idx >= _world.authoredPosts.Length) continue;
                var ap = _world.authoredPosts[idx];
                var np = ap.position + delta;
                np.x = Mathf.Clamp(np.x, -_world.halfExtents.x, _world.halfExtents.x);
                np.y = Mathf.Clamp(np.y, -_world.halfExtents.y, _world.halfExtents.y);
                if (np != ap.position) { ap.position = np; _world.authoredPosts[idx] = ap; any = true; }
            }
        }
        if (any)
        {
            EditorUtility.SetDirty(_world);
            Repaint();
            SceneView.RepaintAll();
            if (_livePreview) PreviewWorld();
        }
    }

    private void HandleKeyboardNudge()
    {
        var e = Event.current;
        if (e == null) return;
        if (e.type != EventType.KeyDown) return;

        // Respect SceneView arrow panning: only handle when a modifier is held
        bool hasModifier = e.shift || e.control || e.command || e.alt;
        if (!hasModifier) return;

        Vector2 delta = Vector2.zero;
        switch (e.keyCode)
        {
            case KeyCode.LeftArrow:  delta = new Vector2(-1f, 0f); break;
            case KeyCode.RightArrow: delta = new Vector2( 1f, 0f); break;
            case KeyCode.UpArrow:    delta = new Vector2( 0f, 1f); break;
            case KeyCode.DownArrow:  delta = new Vector2( 0f,-1f); break;
            default: return;
        }

        // Base step
        float step = Mathf.Max(0.001f, _nudgeStep);
        // Modifier scaling: Ctrl/Cmd = fast, Alt = fine
        if (e.control || e.command) step *= 5f;
        if (e.alt) step *= 0.2f;

        NudgeSelection(delta * step);
        e.Use();
    }

    // --- Validation helpers ---
    private void HandleClickSelection()
    {
        var e = Event.current;
        if (e == null) return;
        if (e.type != EventType.MouseDown || e.button != 0) return;
        // Ignore clicks inside overlay toolbar so it doesn't affect selection
        if (_overlayRect != Rect.zero && _overlayRect.Contains(e.mousePosition)) return;
        // If box-select is active (Shift+Drag starts it), don't process click selection here
        if (_boxSelectEnabled && e.shift && !e.alt) return;

        Vector2 gui = e.mousePosition;
        int hitPlanet = -1, hitPost = -1;
        float hitDist = float.MaxValue;
        const float pickRadius = 12f; // pixels

        // Planets
        if (_world.authoredPlanets != null)
        {
            for (int i = 0; i < _world.authoredPlanets.Length; i++)
            {
                var p = _world.authoredPlanets[i].position;
                Vector2 g = HandleUtility.WorldToGUIPoint(p);
                float d = Vector2.Distance(gui, g);
                if (d <= pickRadius && d < hitDist)
                {
                    hitPlanet = i; hitPost = -1; hitDist = d;
                }
            }
        }
        // Posts
        if (_world.authoredPosts != null)
        {
            for (int i = 0; i < _world.authoredPosts.Length; i++)
            {
                var p = _world.authoredPosts[i].position;
                Vector2 g = HandleUtility.WorldToGUIPoint(p);
                float d = Vector2.Distance(gui, g);
                if (d <= pickRadius && d < hitDist)
                {
                    hitPost = i; hitPlanet = -1; hitDist = d;
                }
            }
        }

        bool additive = e.control || e.command;
        bool subtractive = e.alt;
        bool changed = false;
        if (hitPlanet >= 0)
        {
            changed = true;
            if (subtractive)
                _selPlanets = _selPlanets.Where(id => id != hitPlanet).ToList();
            else if (additive)
            {
                if (!_selPlanets.Contains(hitPlanet)) _selPlanets.Add(hitPlanet);
            }
            else
            {
                // If already selected, keep current selection intact (do not collapse)
                if (!_selPlanets.Contains(hitPlanet))
                {
                    _selPlanets.Clear(); _selPlanets.Add(hitPlanet);
                    _selPosts.Clear();
                }
                else
                {
                    changed = false; // No change to selection
                }
            }
            _selPlanets = _selPlanets.Distinct().OrderBy(i => i).ToList();
            SavePlanetSelection();
        }
        else if (hitPost >= 0)
        {
            changed = true;
            if (subtractive)
                _selPosts = _selPosts.Where(id => id != hitPost).ToList();
            else if (additive)
            {
                if (!_selPosts.Contains(hitPost)) _selPosts.Add(hitPost);
            }
            else
            {
                // If already selected, keep current selection intact (do not collapse)
                if (!_selPosts.Contains(hitPost))
                {
                    _selPosts.Clear(); _selPosts.Add(hitPost);
                    _selPlanets.Clear();
                }
                else
                {
                    changed = false;
                }
            }
            _selPosts = _selPosts.Distinct().OrderBy(i => i).ToList();
            SavePostSelection();
        }
        else
        {
            // Clicked empty space: clear selection
            if (!additive && !subtractive)
            {
                if ((_selPlanets?.Count ?? 0) > 0 || (_selPosts?.Count ?? 0) > 0)
                {
                    _selPlanets.Clear(); _selPosts.Clear();
                    SavePlanetSelection(); SavePostSelection();
                    changed = true;
                }
            }
        }

        if (changed)
        {
            Repaint();
            SceneView.RepaintAll();
        }
        // Do not e.Use() so FreeMoveHandle can still receive the drag if any
    }
    private void HandleBoxSelect(SceneView sv)
    {
        if (!_boxSelectEnabled) return;
        var e = Event.current;
        if (e == null) return;
        // Start: Shift + LeftMouse on background (not on overlay UI)
        if (!_boxSelecting && e.type == EventType.MouseDown && e.button == 0 && e.shift && !e.alt && (_overlayRect == Rect.zero || !_overlayRect.Contains(e.mousePosition)))
        {
            _boxSelecting = true;
            _boxStartGui = e.mousePosition; _boxEndGui = _boxStartGui;
            e.Use();
        }
        else if (_boxSelecting && e.type == EventType.MouseDrag)
        {
            _boxEndGui = e.mousePosition;
            e.Use();
            sv.Repaint();
        }
        else if (_boxSelecting && (e.type == EventType.MouseUp || e.type == EventType.Ignore))
        {
            var rect = ToRect(_boxStartGui, _boxEndGui);
            bool add = e.control || e.command; // additive
            bool subtract = e.alt; // subtractive
            SelectWithinRect(rect, add, subtract);
            _boxSelecting = false;
            e.Use();
        }

        // Draw rectangle overlay
        if (_boxSelecting)
        {
            var rect = ToRect(_boxStartGui, _boxEndGui);
            Handles.BeginGUI();
            var fill = new Color(0.3f, 0.7f, 1f, 0.15f);
            var outline = new Color(0.3f, 0.7f, 1f, 0.85f);
            EditorGUI.DrawRect(rect, fill);
            Handles.color = outline;
            Handles.DrawAAPolyLine(2f,
                new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin),
                new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax), new Vector3(rect.xMin, rect.yMin));
            // Draw counts badge
            CountHitsInRect(rect, out int pCount, out int sCount);
            string label = "";
            if (_boxSelectPlanets) label += $"Planets: {pCount}";
            if (_boxSelectPosts) label += (label.Length > 0 ? "  •  " : "") + $"Posts: {sCount}";
            if (label.Length > 0)
            {
                var pad = 4f;
                var content = new GUIContent(label);
                Vector2 size = GUI.skin.box.CalcSize(content);
                var lb = new Rect(rect.xMax - size.x - 8f, rect.yMax + 6f, size.x + pad * 2f, size.y + pad);
                EditorGUI.DrawRect(lb, new Color(0f, 0f, 0f, 0.35f));
                GUI.Label(new Rect(lb.x + pad, lb.y + pad * 0.5f, lb.width - pad * 2f, lb.height - pad), content, EditorStyles.miniBoldLabel);
            }
            Handles.EndGUI();
        }
    }

    private static Rect ToRect(Vector2 a, Vector2 b)
    {
        float x = Mathf.Min(a.x, b.x);
        float y = Mathf.Min(a.y, b.y);
        float w = Mathf.Abs(a.x - b.x);
        float h = Mathf.Abs(a.y - b.y);
        return new Rect(x, y, w, h);
    }

    private void SelectWithinRect(Rect guiRect, bool additive, bool subtractive)
    {
        if (_world == null) return;
        // Planets
        if (_boxSelectPlanets && _world.authoredPlanets != null)
        {
            var hits = new System.Collections.Generic.List<int>();
            for (int i = 0; i < _world.authoredPlanets.Length; i++)
            {
                var p = _world.authoredPlanets[i].position;
                var gui = HandleUtility.WorldToGUIPoint(p);
                if (guiRect.Contains(gui)) hits.Add(i);
            }
            ApplySelection(ref _selPlanets, hits, additive, subtractive);
            SavePlanetSelection();
        }
        // Posts
        if (_boxSelectPosts && _world.authoredPosts != null)
        {
            var hits = new System.Collections.Generic.List<int>();
            for (int i = 0; i < _world.authoredPosts.Length; i++)
            {
                var p = _world.authoredPosts[i].position;
                var gui = HandleUtility.WorldToGUIPoint(p);
                if (guiRect.Contains(gui)) hits.Add(i);
            }
            ApplySelection(ref _selPosts, hits, additive, subtractive);
            SavePostSelection();
        }
        Repaint();
        SceneView.RepaintAll();
    }

    private void ApplySelection(ref System.Collections.Generic.List<int> selection, System.Collections.Generic.List<int> hits, bool additive, bool subtractive)
    {
        if (selection == null) selection = new System.Collections.Generic.List<int>();
        if (subtractive)
        {
            var set = new System.Collections.Generic.HashSet<int>(selection);
            for (int i = 0; i < hits.Count; i++) set.Remove(hits[i]);
            selection = set.ToList();
        }
        else if (additive)
        {
            var set = new System.Collections.Generic.HashSet<int>(selection);
            for (int i = 0; i < hits.Count; i++) set.Add(hits[i]);
            selection = set.OrderBy(i => i).ToList();
        }
        else
        {
            selection = hits;
        }
    }

    private void CountHitsInRect(Rect guiRect, out int planetCount, out int postCount)
    {
        planetCount = 0; postCount = 0;
        if (_world == null) return;
        if (_boxSelectPlanets && _world.authoredPlanets != null)
        {
            for (int i = 0; i < _world.authoredPlanets.Length; i++)
            {
                var p = _world.authoredPlanets[i].position;
                var gui = HandleUtility.WorldToGUIPoint(p);
                if (guiRect.Contains(gui)) planetCount++;
            }
        }
        if (_boxSelectPosts && _world.authoredPosts != null)
        {
            for (int i = 0; i < _world.authoredPosts.Length; i++)
            {
                var p = _world.authoredPosts[i].position;
                var gui = HandleUtility.WorldToGUIPoint(p);
                if (guiRect.Contains(gui)) postCount++;
            }
        }
    }
    private void DrawSceneValidationGuides()
    {
        if (_world == null) return;
        // Posts: overlaps and out-of-bounds
        if (_world.authoredPosts != null && _world.authoredPosts.Length > 0)
        {
            var postPairs = ComputePostOverlapPairs();
            Handles.color = new Color(1f, 0.3f, 0.3f, 0.85f);
            foreach (var pr in postPairs)
            {
                var a = _world.authoredPosts[pr.Item1].position;
                var b = _world.authoredPosts[pr.Item2].position;
                Handles.DrawLine(a, b);
            }
            float r = Mathf.Max(0f, GetPostPrefabRadius());
            for (int i = 0; i < _world.authoredPosts.Length; i++)
            {
                var ap = _world.authoredPosts[i];
                if (PostOutsideBounds(ap))
                {
                    // draw boundary ring at post radius (fallback small disc if unknown)
                    float rr = r > 0f ? r : 0.25f;
                    Handles.color = new Color(1f, 0.2f, 0.2f, 0.9f);
                    Handles.DrawWireDisc(ap.position, Vector3.forward, rr);
                }
            }
        }

        // Planets: overlaps and out-of-bounds
        if (_world.authoredPlanets != null && _world.authoredPlanets.Length > 0)
        {
            var planetPairs = ComputePlanetOverlapPairs();
            Handles.color = new Color(1f, 0.3f, 0.3f, 0.85f);
            foreach (var pr in planetPairs)
            {
                var a = _world.authoredPlanets[pr.Item1].position;
                var b = _world.authoredPlanets[pr.Item2].position;
                Handles.DrawLine(a, b);
            }
            for (int i = 0; i < _world.authoredPlanets.Length; i++)
            {
                var ap = _world.authoredPlanets[i];
                if (PlanetOutsideBounds(ap))
                {
                    float r = GetRadiusForSize(ap.size);
                    Handles.color = new Color(1f, 0.2f, 0.2f, 0.9f);
                    Handles.DrawWireDisc(ap.position, Vector3.forward, r);
                }
            }
        }
    }

    private float GetPostPrefabRadius()
    {
        if (_world == null || !_world.postPrefab) return 0f;
        var prefab = _world.postPrefab;
        var col = prefab.GetComponent<CircleCollider2D>();
        if (col) return Mathf.Max(0f, col.radius * Mathf.Max(prefab.transform.lossyScale.x, prefab.transform.lossyScale.y));
        var node = prefab.GetComponent<DeliveryNode>();
        if (node) return Mathf.Max(0f, node.radius * Mathf.Max(prefab.transform.lossyScale.x, prefab.transform.lossyScale.y));
        return 0f;
    }

    private System.Collections.Generic.HashSet<int> ComputePlanetOverlaps()
    {
        var set = new System.Collections.Generic.HashSet<int>();
        if (_world == null || _world.authoredPlanets == null) return set;
        for (int i = 0; i < _world.authoredPlanets.Length; i++)
        {
            var a = _world.authoredPlanets[i];
            float ra = GetRadiusForSize(a.size);
            for (int j = i + 1; j < _world.authoredPlanets.Length; j++)
            {
                var b = _world.authoredPlanets[j];
                float rb = GetRadiusForSize(b.size);
                float sum = ra + rb;
                if (Vector2.SqrMagnitude(a.position - b.position) < sum * sum)
                {
                    set.Add(i); set.Add(j);
                }
            }
        }
        return set;
    }

    private System.Collections.Generic.List<System.Tuple<int,int>> ComputePlanetOverlapPairs()
    {
        var list = new System.Collections.Generic.List<System.Tuple<int,int>>();
        if (_world == null || _world.authoredPlanets == null) return list;
        for (int i = 0; i < _world.authoredPlanets.Length; i++)
        {
            var a = _world.authoredPlanets[i];
            float ra = GetRadiusForSize(a.size);
            for (int j = i + 1; j < _world.authoredPlanets.Length; j++)
            {
                var b = _world.authoredPlanets[j];
                float rb = GetRadiusForSize(b.size);
                float sum = ra + rb;
                if (Vector2.SqrMagnitude(a.position - b.position) < sum * sum)
                    list.Add(System.Tuple.Create(i, j));
            }
        }
        return list;
    }

    private System.Collections.Generic.HashSet<int> ComputePostOverlaps()
    {
        var set = new System.Collections.Generic.HashSet<int>();
        if (_world == null || _world.authoredPosts == null) return set;
        float r = GetPostPrefabRadius();
        if (r <= 0f) return set; // cannot infer without a radius
        for (int i = 0; i < _world.authoredPosts.Length; i++)
        {
            var a = _world.authoredPosts[i];
            for (int j = i + 1; j < _world.authoredPosts.Length; j++)
            {
                var b = _world.authoredPosts[j];
                float sum = r + r;
                if (Vector2.SqrMagnitude(a.position - b.position) < sum * sum)
                {
                    set.Add(i); set.Add(j);
                }
            }
        }
        return set;
    }

    private System.Collections.Generic.List<System.Tuple<int,int>> ComputePostOverlapPairs()
    {
        var list = new System.Collections.Generic.List<System.Tuple<int,int>>();
        if (_world == null || _world.authoredPosts == null) return list;
        float r = GetPostPrefabRadius();
        if (r <= 0f) return list;
        for (int i = 0; i < _world.authoredPosts.Length; i++)
        {
            var a = _world.authoredPosts[i];
            for (int j = i + 1; j < _world.authoredPosts.Length; j++)
            {
                var b = _world.authoredPosts[j];
                float sum = r + r;
                if (Vector2.SqrMagnitude(a.position - b.position) < sum * sum)
                    list.Add(System.Tuple.Create(i, j));
            }
        }
        return list;
    }

    private bool PlanetOutsideBounds(WorldDefinition.AuthoredPlanet ap)
    {
        float r = GetRadiusForSize(ap.size);
        float availX = Mathf.Max(0f, _world.halfExtents.x - r);
        float availY = Mathf.Max(0f, _world.halfExtents.y - r);
        return Mathf.Abs(ap.position.x) > availX || Mathf.Abs(ap.position.y) > availY;
    }

    private bool PostOutsideBounds(WorldDefinition.AuthoredPost ap)
    {
        float r = GetPostPrefabRadius();
        float availX = Mathf.Max(0f, _world.halfExtents.x - r);
        float availY = Mathf.Max(0f, _world.halfExtents.y - r);
        return Mathf.Abs(ap.position.x) > availX || Mathf.Abs(ap.position.y) > availY;
    }

    private void DrawBadge(string text, Color bg, float width = 64f)
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = bg;
        GUILayout.Label(text, EditorStyles.miniButton, GUILayout.Width(width));
        GUI.backgroundColor = prev;
    }

    private void DrawPlanetBadges(int index, WorldDefinition.AuthoredPlanet ap, System.Collections.Generic.HashSet<int> overlaps)
    {
        // Missing prefab
        bool missingPrefab = (_world.planetPrefabs == null) || !_world.planetPrefabs.TryGet(ap.size, out var _);
        if (missingPrefab) DrawBadge("Prefab?", new Color(1f, 0.5f, 0.9f, 0.9f), 64f);
        // Outside bounds
        if (PlanetOutsideBounds(ap)) DrawBadge("Out of Bounds", new Color(1f, 0.4f, 0.3f, 0.9f), 96f);
        // Overlap
        if (overlaps != null && overlaps.Contains(index)) DrawBadge("Overlaps", new Color(1f, 0.75f, 0.2f, 0.9f), 72f);
    }

    private void DrawPostBadges(int index, WorldDefinition.AuthoredPost ap)
    {
        bool missingPrefab = (_world.postPrefab == null);
        if (missingPrefab) DrawBadge("Prefab?", new Color(1f, 0.5f, 0.9f, 0.9f), 64f);
        if (PostOutsideBounds(ap)) DrawBadge("Out of Bounds", new Color(1f, 0.4f, 0.3f, 0.9f), 96f);
        var overlaps = ComputePostOverlaps();
        if (overlaps.Contains(index)) DrawBadge("Overlaps", new Color(1f, 0.75f, 0.2f, 0.9f), 72f);
    }
}
#endif

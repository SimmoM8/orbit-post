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
    [SerializeField] private bool _showProceduralInfo = false;

    // EditorPrefs keys for persistence
    private const string PrefSnapEnabledKey = "WorldDesigner_SnapEnabled";
    private const string PrefSnapSizeKey = "WorldDesigner_SnapSize";

    // Handle colors by type (editor-only hinting)
    private static readonly Color _typeDefault = new Color(1f, 1f, 1f, 0.8f);
    private static readonly Color _typeFire    = new Color(1f, 0.45f, 0.15f, 0.8f);
    private static readonly Color _typeWater   = new Color(0.2f, 0.6f, 1f, 0.8f);
    private static readonly Color _typeEarth   = new Color(0.4f, 0.8f, 0.4f, 0.8f);

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
    }

    private void OnDisable()
    {
        HookScene(false);
        // Save snap prefs
        EditorPrefs.SetBool(PrefSnapEnabledKey, _snapEnabled);
        EditorPrefs.SetFloat(PrefSnapSizeKey, Mathf.Max(0.01f, _snapSize));
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
    }

    private void DrawPlanetsList()
    {
        if (_world.authoredPlanets == null)
            _world.authoredPlanets = new WorldDefinition.AuthoredPlanet[0];

        EditorGUILayout.LabelField("Planets", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        int removeAt = -1;
        int duplicateAt = -1;
        for (int i = 0; i < _world.authoredPlanets.Length; i++)
        {
            var ap = _world.authoredPlanets[i];
            EditorGUILayout.BeginVertical("box");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"#{i}", GUILayout.Width(28));
                ap.position = EditorGUILayout.Vector2Field("Position", ap.position);
                if (GUILayout.Button("⧉", GUILayout.Width(24))) duplicateAt = i;
                if (GUILayout.Button("×", GUILayout.Width(24))) removeAt = i;
            }

            ap.size  = (PlanetSize)EditorGUILayout.EnumPopup("Size", ap.size);
            ap.type  = (PlanetType)EditorGUILayout.EnumPopup("Type", ap.type);
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
        }

        if (removeAt >= 0)
        {
            Undo.RecordObject(_world, "Remove Planet");
            _world.authoredPlanets = _world.authoredPlanets.Where((_, idx) => idx != removeAt).ToArray();
            EditorUtility.SetDirty(_world);
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_world, "Edit Planets");
            EditorUtility.SetDirty(_world);
            Repaint();
            SceneView.RepaintAll();
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
    }

    private void SortPlanetsByX()
    {
        if (_world.authoredPlanets == null || _world.authoredPlanets.Length == 0) return;
        Undo.RecordObject(_world, "Sort Planets");
        _world.authoredPlanets = _world.authoredPlanets.OrderBy(p => p.position.x).ToArray();
        EditorUtility.SetDirty(_world);
        Repaint();
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sv)
    {
        if (!_world || _world.authoredPlanets == null) return;

        // Draw world bounds (centered at 0,0) for visual guidance
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
        if (!_world || _world.planetPrefabs == null)
        {
            EditorUtility.DisplayDialog("Preview World", "Assign a WorldDefinition with a Planet Prefab Library first.", "OK");
            return;
        }

        var root = GameObject.Find(PreviewRootName);
        if (root) ClearPreview();
        root = new GameObject(PreviewRootName);

        // Ensure the preview root never gets saved
        root.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        // Spawn via the same rules as runtime builder (size prefab + type + profile + mass)
        foreach (var ap in _world.authoredPlanets)
        {
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
}
#endif

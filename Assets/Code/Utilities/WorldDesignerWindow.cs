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
    }

    private void OnDisable()
    {
        HookScene(false);
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

    private void DrawPlanetsList()
    {
        if (_world.authoredPlanets == null)
            _world.authoredPlanets = new WorldDefinition.AuthoredPlanet[0];

        EditorGUILayout.LabelField("Planets", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        int removeAt = -1;
        for (int i = 0; i < _world.authoredPlanets.Length; i++)
        {
            var ap = _world.authoredPlanets[i];
            EditorGUILayout.BeginVertical("box");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"#{i}", GUILayout.Width(28));
                ap.position = EditorGUILayout.Vector2Field("Position", ap.position);
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

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
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
            var fmh_203_73_638905931160964810 = Quaternion.identity; Vector3 pos3 = Handles.FreeMoveHandle((Vector3)ap.position, 0.12f, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_world, "Move Planet");
                ap.position = (Vector2)pos3;
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
        if (_world.planetPrefabs.TryGet(size, out var prefab) && prefab)
        {
            var col = prefab.GetComponent<CircleCollider2D>();
            if (col) return Mathf.Max(0.01f, col.radius);
            var sr = prefab.GetComponentInChildren<SpriteRenderer>();
            if (sr) return Mathf.Max(sr.bounds.extents.x, sr.bounds.extents.y);
        }
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
                planet.planetType = ap.type;
                planet.ApplyTypeVisual();
                planet.profile = ap.profile;
                planet.mass = ap.mass;
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
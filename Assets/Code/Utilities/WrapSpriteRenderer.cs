using UnityEngine;

/// <summary>
/// Renders wrapped "ghost" images of a SpriteRenderer near world edges so objects remain visible
/// across toroidal boundaries. Useful for planets or any large sprites that should appear on
/// opposite edges before the camera/courier crosses.
///
/// Attach to a GameObject that has (or contains) a SpriteRenderer.
/// </summary>
public class WrapSpriteRenderer : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("If left null, the first SpriteRenderer on this GameObject or its children is used.")]
    public SpriteRenderer source;

    [Header("Activation")]
    [Tooltip("If true, only show ghosts when the sprite is near edges; otherwise always create and keep hidden when far.")]
    public bool enableEdgeCulling = true;

    [Header("Bounds")]
    [Tooltip("When true, uses WorldBuilder half-extents (center 0,0) if present; otherwise falls back to PackageSpawner area.")]
    public bool preferWorldBuilder = true;

    [Header("Appearance")]
    [Tooltip("Extra margin factor relative to the sprite's approximate radius before spawning ghosts.")]
    [Range(0f, 2f)] public float marginScale = 1.0f;

    const int GhostCount = 8; // +X, -X, +Y, -Y, and 4 corners
    SpriteRenderer[] ghosts = new SpriteRenderer[GhostCount];

    WorldBuilder wb;
    PackageSpawner sp;

    void Awake()
    {
        if (!source)
        {
            source = GetComponent<SpriteRenderer>();
            if (!source) source = GetComponentInChildren<SpriteRenderer>();
        }
    }

    void OnEnable()
    {
#if UNITY_2023_1_OR_NEWER
        wb = Object.FindFirstObjectByType<WorldBuilder>();
        sp = Object.FindFirstObjectByType<PackageSpawner>();
#else
        wb = Object.FindObjectOfType<WorldBuilder>();
        sp = Object.FindObjectOfType<PackageSpawner>();
#endif
        EnsureGhosts();
        SyncGhostStyles();
    }

    void OnDisable()
    {
        DestroyGhosts();
    }

    void LateUpdate()
    {
        if (!source) return;
        Vector2 center; Vector2 half; float w, h;
        if (!TryGetBounds(out center, out half)) return;
        w = Mathf.Max(1e-4f, half.x * 2f);
        h = Mathf.Max(1e-4f, half.y * 2f);

        // Approximate sprite radius in world units (prefer planet radius if available)
        float r = 1f;
        var planet = GetComponent<Planet>();
        if (planet) r = Mathf.Max(0.01f, planet.radius);
        else if (source && source.sprite)
        {
            var e = source.bounds.extents; r = Mathf.Max(0.01f, Mathf.Max(e.x, e.y));
        }
        float margin = r * marginScale;

        Vector3 pos = transform.position;
        Vector2 local = (Vector2)pos - center;
        bool nearLeft = (local.x - (-half.x)) < margin;
        bool nearRight = (half.x - local.x) < margin;
        bool nearBottom = (local.y - (-half.y)) < margin;
        bool nearTop = (half.y - local.y) < margin;

        // Determine which ghosts should be visible
        bool gRight = nearRight;
        bool gLeft = nearLeft;
        bool gTop = nearTop;
        bool gBottom = nearBottom;
        bool gTopRight = nearTop && nearRight;
        bool gTopLeft = nearTop && nearLeft;
        bool gBottomRight = nearBottom && nearRight;
        bool gBottomLeft = nearBottom && nearLeft;

        Vector3 scale = transform.lossyScale;
        Quaternion rot = transform.rotation;

        // Offsets (world space)
        Vector3 offRight = new Vector3(w, 0f, 0f);
        Vector3 offLeft = new Vector3(-w, 0f, 0f);
        Vector3 offTop = new Vector3(0f, h, 0f);
        Vector3 offBottom = new Vector3(0f, -h, 0f);

        // Apply
        SetGhost(0, pos + offRight, rot, scale, gRight);
        SetGhost(1, pos + offLeft, rot, scale, gLeft);
        SetGhost(2, pos + offTop, rot, scale, gTop);
        SetGhost(3, pos + offBottom, rot, scale, gBottom);
        SetGhost(4, pos + offTop + offRight, rot, scale, gTopRight);
        SetGhost(5, pos + offTop + offLeft, rot, scale, gTopLeft);
        SetGhost(6, pos + offBottom + offRight, rot, scale, gBottomRight);
        SetGhost(7, pos + offBottom + offLeft, rot, scale, gBottomLeft);

        // Keep style in sync if sprite/material/color changed
        SyncGhostStyles();

        if (!enableEdgeCulling)
        {
            // If culling disabled, just enable all ghosts (useful for debugging large zooms)
            for (int i = 0; i < GhostCount; i++) if (ghosts[i]) ghosts[i].enabled = true;
        }
    }

    bool TryGetBounds(out Vector2 center, out Vector2 half)
    {
        if (preferWorldBuilder && wb)
        {
            center = Vector2.zero;
            half = wb.WorldHalfExtents;
            return true;
        }
        if (sp)
        {
            center = sp.areaCenter;
            half = new Vector2(Mathf.Max(1f, sp.areaSize.x * 0.5f), Mathf.Max(1f, sp.areaSize.y * 0.5f));
            return true;
        }
        // Last resort, still draw with a default to avoid NREs
        center = Vector2.zero;
        half = new Vector2(60f, 60f);
        return true;
    }

    void EnsureGhosts()
    {
        for (int i = 0; i < GhostCount; i++)
        {
            if (ghosts[i]) continue;
            var go = new GameObject($"__WrapGhost_{i}");
            // Place under same parent so we can set world transforms directly
            go.transform.SetParent(transform.parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            ghosts[i] = sr;
        }
    }

    void DestroyGhosts()
    {
        for (int i = 0; i < GhostCount; i++)
        {
            if (ghosts[i])
            {
                Destroy(ghosts[i].gameObject);
                ghosts[i] = null;
            }
        }
    }

    void SyncGhostStyles()
    {
        if (!source) return;
        for (int i = 0; i < GhostCount; i++)
        {
            var g = ghosts[i]; if (!g) continue;
            g.sprite = source.sprite;
            g.color = source.color;
            g.flipX = source.flipX; g.flipY = source.flipY;
            g.sharedMaterial = source.sharedMaterial;
            g.sortingLayerID = source.sortingLayerID;
            g.sortingOrder = source.sortingOrder;
            g.drawMode = source.drawMode;
            g.maskInteraction = source.maskInteraction;
        }
    }

    void SetGhost(int idx, Vector3 worldPos, Quaternion rot, Vector3 worldScale, bool visible)
    {
        var g = ghosts[idx]; if (!g) return;
        g.transform.position = worldPos;
        g.transform.rotation = rot;
        g.transform.localScale = worldScale;
        g.enabled = visible;
    }
}


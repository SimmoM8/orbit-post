using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple screen-space minimap that visualizes planets, the courier, packages, and delivery nodes.
/// - Builds its own UI panel under a Canvas at runtime if no container is assigned.
/// - Maps world positions to the panel using PackageSpawner's areaCenter/areaSize when available,
///   otherwise auto-computes bounds from present gameplay objects.
/// - Renders minimal icons using a generated 1x1 white sprite tinted per type.
///
/// Attach this to any GameObject in a scene with a Canvas (ideally the HUD Canvas).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MinimapHUD : MonoBehaviour
{
    [Header("Build & Layout")]
    [Tooltip("Optional container; if null, a panel is created under this object.")]
    public RectTransform container;
    [Tooltip("Panel anchor position (pixels) from chosen corner.")]
    public Vector2 anchoredPos = new Vector2(-20f, 20f);
    [Tooltip("Minimap size in pixels.")]
    public Vector2 size = new Vector2(220f, 160f);
    [Tooltip("Where to anchor the minimap panel.")]
    public Corner corner = Corner.BottomRight;

    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    [Header("Style")]
    public Color frameColor = new Color(0f, 0f, 0f, 0.45f);
    public Color backgroundColor = new Color(1f, 1f, 1f, 0.06f);
    public float framePadding = 6f;

    [Header("Icon Sizes (px)")]
    public float courierSize = 10f;
    public float packageSize = 6f;
    public float nodeSize = 8f;
    public float planetMinSize = 8f;
    public float planetMaxSize = 24f;

    [Header("Colors")]
    public Color courierColor = new Color(1f, 1f, 1f, 1f);
    public Color packageColorDefault = new Color(0.9f, 0.9f, 0.9f, 1f);
    public Color nodeColor = new Color(1f, 0.95f, 0.35f, 1f);
    public Color planetColorDefault = new Color(0.7f, 0.85f, 1f, 1f);

    [Header("Discovery & Refresh")]
    [Tooltip("How often (seconds) to rescan scene for new/removed objects.")]
    public float rescanInterval = 0.5f;

    // Runtime references
    CourierController courier;
    PackageSpawner spawner;

    // Simple white sprite for drawing rectangles/circles
    static Sprite s_WhiteSprite;

    Image frameBg;
    RectTransform panel;

    abstract class Tracked
    {
        public Transform tf;
        public Image img;
        public abstract Vector2 WorldPos { get; }
        public abstract void UpdateStyle();
        public virtual bool IsValid => tf != null;
    }

    class TrackedCourier : Tracked
    {
        readonly MinimapHUD mm;
        public TrackedCourier(MinimapHUD m) { mm = m; }
        public override Vector2 WorldPos => tf ? (Vector2)tf.position : Vector2.zero;
        public override void UpdateStyle()
        {
            if (!img) return;
            img.color = mm.courierColor;
            var rt = img.rectTransform; rt.sizeDelta = Vector2.one * mm.courierSize;
        }
    }

    class TrackedPlanet : Tracked
    {
        readonly MinimapHUD mm;
        Planet planet;
        public TrackedPlanet(MinimapHUD m, Planet p) { mm = m; planet = p; tf = p ? p.transform : null; }
        public override Vector2 WorldPos => planet ? planet.WorldPos : Vector2.zero;
        public override void UpdateStyle()
        {
            if (!img || planet == null) return;
            // Size proportional to radius, clamped
            float d = Mathf.Clamp(planet.radius * 2f, mm.planetMinSize, mm.planetMaxSize);
            var rt = img.rectTransform; rt.sizeDelta = new Vector2(d, d);
            // Color: try sprite color if available; fallback
            var sr = planet.spriteRef ? planet.spriteRef : planet.GetComponentInChildren<SpriteRenderer>();
            img.color = sr ? sr.color : mm.planetColorDefault;
        }
    }

    class TrackedPackage : Tracked
    {
        readonly MinimapHUD mm;
        Package pkg;
        public TrackedPackage(MinimapHUD m, Package p) { mm = m; pkg = p; tf = p ? p.transform : null; }
        public override Vector2 WorldPos => pkg ? (Vector2)pkg.transform.position : Vector2.zero;
        public override void UpdateStyle()
        {
            if (!img || pkg == null) return;
            var rt = img.rectTransform; rt.sizeDelta = Vector2.one * mm.packageSize;
            if (pkg.packageType)
                img.color = pkg.packageType.materialColor;
            else
                img.color = mm.packageColorDefault;
        }
        public override bool IsValid => pkg != null && !pkg.IsPickedUp; // hide if carried/destroyed
    }

    class TrackedNode : Tracked
    {
        readonly MinimapHUD mm;
        DeliveryNode node;
        public TrackedNode(MinimapHUD m, DeliveryNode n) { mm = m; node = n; tf = n ? n.transform : null; }
        public override Vector2 WorldPos => node ? (Vector2)node.transform.position : Vector2.zero;
        public override void UpdateStyle()
        {
            if (!img) return;
            var rt = img.rectTransform; rt.sizeDelta = Vector2.one * mm.nodeSize;
            img.color = mm.nodeColor;
        }
    }

    readonly List<Tracked> tracked = new List<Tracked>();
    float rescanTimer = 0f;

    void Awake()
    {
        EnsureWhiteSprite();
    }

    void Start()
    {
        // Find common references
        FindRefs();

        if (!container)
            container = CreatePanel();

        // Background frame
        frameBg = CreateImage("Minimap_Frame", container, backgroundColor);
        var frt = frameBg.rectTransform;
        frt.anchorMin = new Vector2(0, 0);
        frt.anchorMax = new Vector2(1, 1);
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        frameBg.raycastTarget = false;

        // Initial scan
        Rescan();
        UpdateAllStyles();
        UpdateAllPositions();
    }

    void FindRefs()
    {
#if UNITY_2023_1_OR_NEWER
        courier = Object.FindFirstObjectByType<CourierController>();
        spawner = Object.FindFirstObjectByType<PackageSpawner>();
#else
        courier = Object.FindObjectOfType<CourierController>();
        spawner = Object.FindObjectOfType<PackageSpawner>();
#endif
    }

    void Update()
    {
        rescanTimer -= Time.unscaledDeltaTime;
        if (rescanTimer <= 0f)
        {
            Rescan();
            rescanTimer = Mathf.Max(0.05f, rescanInterval);
        }

        UpdateAllStyles();
        UpdateAllPositions();
    }

    void Rescan()
    {
        // Remove any destroyed/invalid
        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            if (tracked[i] == null || !tracked[i].IsValid)
            {
                if (tracked[i] != null && tracked[i].img)
                    Destroy(tracked[i].img.gameObject);
                tracked.RemoveAt(i);
            }
        }

        // Ensure we have exactly one courier tracked
        bool haveCourier = false;
        for (int i = 0; i < tracked.Count; i++) if (tracked[i] is TrackedCourier) { haveCourier = true; break; }
        if (!haveCourier && courier)
        {
            var t = new TrackedCourier(this) { tf = courier.transform };
            t.img = CreateIcon("Courier", container);
            tracked.Add(t);
        }

        // Planets via GravityField
        foreach (var p in GravityField.Planets)
        {
            if (!p) continue;
            bool exists = false;
            for (int i = 0; i < tracked.Count; i++)
                if (tracked[i] is TrackedPlanet tp && tp.tf == p.transform) { exists = true; break; }
            if (!exists)
            {
                var tp = new TrackedPlanet(this, p);
                tp.img = CreateIcon("Planet", container, true);
                tracked.Add(tp);
            }
        }

        // Packages (not carried)
#if UNITY_2023_1_OR_NEWER
        var pkgs = Object.FindObjectsByType<Package>(FindObjectsSortMode.None);
        var nodes = Object.FindObjectsByType<DeliveryNode>(FindObjectsSortMode.None);
#else
        var pkgs = Object.FindObjectsOfType<Package>();
        var nodes = Object.FindObjectsOfType<DeliveryNode>();
#endif
        for (int i = 0; i < pkgs.Length; i++)
        {
            var p = pkgs[i];
            if (!p || p.IsPickedUp) continue;
            bool exists = false;
            for (int j = 0; j < tracked.Count; j++)
                if (tracked[j] is TrackedPackage t && t.tf == p.transform) { exists = true; break; }
            if (!exists)
            {
                var tp = new TrackedPackage(this, p);
                tp.img = CreateIcon("Package", container);
                tracked.Add(tp);
            }
        }

        // Delivery nodes
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (!n) continue;
            bool exists = false;
            for (int j = 0; j < tracked.Count; j++)
                if (tracked[j] is TrackedNode t && t.tf == n.transform) { exists = true; break; }
            if (!exists)
            {
                var tn = new TrackedNode(this, n);
                tn.img = CreateIcon("Node", container);
                tracked.Add(tn);
            }
        }
    }

    void UpdateAllStyles()
    {
        for (int i = 0; i < tracked.Count; i++) tracked[i].UpdateStyle();
    }

    void UpdateAllPositions()
    {
        // Compute mapping bounds
        Vector2 center; Vector2 sizeWU;
        if (spawner)
        {
            center = spawner.areaCenter;
            sizeWU = spawner.areaSize;
        }
        else
        {
            // Fallback: derive bounds from tracked positions
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            bool init = false;
            for (int i = 0; i < tracked.Count; i++)
            {
                if (!tracked[i].IsValid) continue;
                var p = tracked[i].WorldPos;
                if (!init) { b = new Bounds(p, Vector3.zero); init = true; }
                else b.Encapsulate(p);
            }
            if (!init) { center = Vector2.zero; sizeWU = new Vector2(20f, 12f); }
            else
            {
                center = b.center;
                sizeWU = new Vector2(Mathf.Max(8f, b.size.x + 4f), Mathf.Max(6f, b.size.y + 3f)); // add padding
            }
        }

        // Avoid division by zero
        Vector2 half = Vector2.Max(sizeWU * 0.5f, new Vector2(1f, 1f));
        var rect = container.rect;
        Vector2 sz = rect.size - new Vector2(framePadding * 2f, framePadding * 2f);

        for (int i = 0; i < tracked.Count; i++)
        {
            var t = tracked[i];
            if (t == null || t.img == null || !t.IsValid) continue;

            Vector2 wp = t.WorldPos;
            // Normalize to [-1..+1] relative to center
            Vector2 n = new Vector2(
                Mathf.Clamp((wp.x - center.x) / half.x, -1f, 1f),
                Mathf.Clamp((wp.y - center.y) / half.y, -1f, 1f)
            );

            // Map to panel local (0..1) then to anchored pixels inside padding
            Vector2 uv = (n + Vector2.one) * 0.5f; // 0..1
            Vector2 local = new Vector2(framePadding + uv.x * sz.x, framePadding + uv.y * sz.y);

            var rt = t.img.rectTransform;
            rt.anchoredPosition = local;
        }
    }

    RectTransform CreatePanel()
    {
        var go = new GameObject("Minimap_Panel", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        panel = go.GetComponent<RectTransform>();

        switch (corner)
        {
            case Corner.TopLeft:
                panel.anchorMin = new Vector2(0f, 1f); panel.anchorMax = new Vector2(0f, 1f); panel.pivot = new Vector2(0f, 1f);
                panel.anchoredPosition = new Vector2(anchoredPos.x, -anchoredPos.y);
                break;
            case Corner.TopRight:
                panel.anchorMin = new Vector2(1f, 1f); panel.anchorMax = new Vector2(1f, 1f); panel.pivot = new Vector2(1f, 1f);
                panel.anchoredPosition = new Vector2(-anchoredPos.x, -anchoredPos.y);
                break;
            case Corner.BottomLeft:
                panel.anchorMin = new Vector2(0f, 0f); panel.anchorMax = new Vector2(0f, 0f); panel.pivot = new Vector2(0f, 0f);
                panel.anchoredPosition = new Vector2(anchoredPos.x, anchoredPos.y);
                break;
            case Corner.BottomRight:
                panel.anchorMin = new Vector2(1f, 0f); panel.anchorMax = new Vector2(1f, 0f); panel.pivot = new Vector2(1f, 0f);
                panel.anchoredPosition = new Vector2(-anchoredPos.x, anchoredPos.y);
                break;
        }
        panel.sizeDelta = size;
        return panel;
    }

    Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = s_WhiteSprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Image CreateIcon(string name, Transform parent, bool makeCircle = false)
    {
        var img = CreateImage(name, parent, Color.white);
        if (makeCircle)
        {
            // Use a simple mask by rounding via material if available; fallback keeps it square.
            // To keep this self-contained, we stay with square but size suggests circular planet.
        }
        // Positioning inside panel (top-left anchored)
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * 8f;
        return img;
    }

    static void EnsureWhiteSprite()
    {
        if (s_WhiteSprite != null) return;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        s_WhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}

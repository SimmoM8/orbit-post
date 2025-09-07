using System.Collections.Generic;
using UnityEngine;
using OrbitPost.Gameplay.Planets;

public interface IGravityBody
{
    Vector2 WorldPos { get; }
    float Mass { get; }
    float MinRadiusSqr { get; }
    bool enabled { get; }
}

public class GravityField : MonoBehaviour
{
    // New canonical container (duplicate-proof)
    public static readonly HashSet<IGravityBody> Bodies = new HashSet<IGravityBody>();

    // Backward-compat list (kept in sync for any legacy code still reading it)
    public static readonly List<Planet> Planets = new List<Planet>();

    [Range(0f, 20f)] public float G = 2.6f;      // tuned, not real units
    [Range(1f, 100f)] public float maxAccel = 25f;

    [Header("Tuning")]
    [Range(0f, 3f)] public float gravityScale = 1f; // global scalar on gravity strength

    [Header("Wrapping")]
    [Tooltip("When enabled, gravity uses the shortest wrapped vector between bodies across world boundaries (toroidal field).")]
    public bool wrapGravity = true;

    private const float FALLBACK_G = 2.6f;
    private const float FALLBACK_MAX_ACCEL = 25f;
    private const float FALLBACK_GRAVITY_SCALE = 1f;

    private static GravityField _instance;
    // Cached providers for world bounds
    private static WorldBuilder _wb;
    private static PackageSpawner _sp;
    void Awake()
    {
        _instance = this;
        // One-shot self-heal: register any already-enabled gravity bodies
        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            var mb = behaviours[i];
            if (mb is IGravityBody gb && mb.isActiveAndEnabled)
                Register(gb);
        }
    }

    static bool TryGetWorldBounds(out Vector2 center, out Vector2 half)
    {
#if UNITY_2023_1_OR_NEWER
        if (_wb == null) _wb = Object.FindFirstObjectByType<WorldBuilder>();
        if (_wb)
        {
            center = Vector2.zero;
            half = _wb.WorldHalfExtents;
            return true;
        }
        if (_sp == null) _sp = Object.FindFirstObjectByType<PackageSpawner>();
#else
        if (_wb == null) _wb = Object.FindObjectOfType<WorldBuilder>();
        if (_wb)
        {
            center = Vector2.zero;
            half = _wb.WorldHalfExtents;
            return true;
        }
        if (_sp == null) _sp = Object.FindObjectOfType<PackageSpawner>();
#endif
        if (_sp)
        {
            center = _sp.areaCenter;
            half = new Vector2(Mathf.Max(1f, _sp.areaSize.x * 0.5f), Mathf.Max(1f, _sp.areaSize.y * 0.5f));
            return true;
        }
        center = Vector2.zero;
        half = new Vector2(60f, 60f);
        return false;
    }

    public static void Register(IGravityBody body)
    {
        if (body == null) return;
        Bodies.Add(body);
        if (body is Planet p && !Planets.Contains(p))
            Planets.Add(p);
    }

    public static void Unregister(IGravityBody body)
    {
        if (body == null) return;
        Bodies.Remove(body);
        if (body is Planet p)
            Planets.Remove(p);
    }

    // Newtonian multi-body gravity in vacuum.
    // Double precision accumulation + softening by planet core radius for stability.
    // Uses instance-configured G and maxAccel, with safe fallbacks if the manager is not yet awake.
    public static Vector2 AccelAt(Vector2 p)
    {
        // Cache instance and define safe fallbacks so gravity never silently turns off
        var inst = _instance;
        float gVal = inst ? inst.G : FALLBACK_G;
        float maxA = inst ? inst.maxAccel : FALLBACK_MAX_ACCEL;
        float gScale = inst ? inst.gravityScale : FALLBACK_GRAVITY_SCALE;
        bool useWrap = inst ? inst.wrapGravity : true;
        Vector2 center = Vector2.zero, half = new Vector2(60f, 60f);
        float w = 0f, h = 0f;
        if (useWrap)
        {
            TryGetWorldBounds(out center, out half);
            w = Mathf.Max(1e-4f, half.x * 2f);
            h = Mathf.Max(1e-4f, half.y * 2f);
        }

        // Sum contributions from all registered planets using double precision
        double ax = 0d, ay = 0d;
        foreach (var body in Bodies)
        {
            // Skip disabled bodies defensively
            if (body == null || !body.enabled) continue;

            // Direction from p to body, optionally wrapped to nearest image
            Vector2 dir = body.WorldPos - p;
            if (useWrap)
            {
                // Work in local space relative to world center
                Vector2 bl = body.WorldPos - center;
                Vector2 pl = p - center;
                Vector2 d = bl - pl;
                if (half.x > 0f)
                {
                    if (d.x >  half.x) d.x -= w; else if (d.x < -half.x) d.x += w;
                }
                if (half.y > 0f)
                {
                    if (d.y >  half.y) d.y -= h; else if (d.y < -half.y) d.y += h;
                }
                dir = d;
            }
            double dx = dir.x, dy = dir.y;

            double r2 = dx * dx + dy * dy;
            double minR2 = body.MinRadiusSqr;
            if (r2 < minR2) r2 = minR2;
            r2 += 1e-6; // epsilon

            double invR = 1.0 / System.Math.Sqrt(r2);
            double invR2 = invR * invR;
            double scale = (gVal * gScale) * body.Mass * invR2;

            ax += scale * dx * invR;
            ay += scale * dy * invR;
        }

        // Cast back to float for Unity
        Vector2 a = new Vector2((float)ax, (float)ay);

        // Optional gameplay clamp so close passes remain controllable
        float mag = a.magnitude;
        if (mag > maxA)
            a *= maxA / mag;

        return a;
    }
}

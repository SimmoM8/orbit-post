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

    private const float FALLBACK_G = 2.6f;
    private const float FALLBACK_MAX_ACCEL = 25f;
    private const float FALLBACK_GRAVITY_SCALE = 1f;

    private static GravityField _instance;
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

        // Sum contributions from all registered planets using double precision
        double ax = 0d, ay = 0d;
        foreach (var body in Bodies)
        {
            // Skip disabled bodies defensively
            if (body == null || !body.enabled) continue;

            Vector2 dir = body.WorldPos - p;
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
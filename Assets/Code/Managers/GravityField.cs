using System.Collections.Generic;
using UnityEngine;

public class GravityField : MonoBehaviour
{
    // Drop this on an empty "Managers/GravityField" in the scene.
    public static readonly List<Planet> Planets = new List<Planet>();

    [Range(0f, 20f)] public float G = 2.6f;      // tuned, not real units
    [Range(1f, 100f)] public float maxAccel = 25f;

    [Header("Tuning")]
    [Range(0f, 3f)] public float gravityScale = 1f; // global scalar on gravity strength

    private const float FALLBACK_G = 2.6f;
    private const float FALLBACK_MAX_ACCEL = 25f;
    private const float FALLBACK_GRAVITY_SCALE = 1f;

    private static GravityField _instance;
    void Awake() { _instance = this; }

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
        for (int i = 0; i < Planets.Count; i++)
        {
            var pl = Planets[i];
            Vector2 dir = (Vector2)pl.WorldPos - p;
            double dx = dir.x, dy = dir.y;

            // Inverse-square with softening near the center to avoid singularities
            double r2 = dx * dx + dy * dy;
            double minR2 = pl.MinRadiusSqr;   // planet-provided safety radius^2
            if (r2 < minR2) r2 = minR2;
            r2 += 1e-6;                       // tiny epsilon for numerical stability

            double invR = 1.0 / System.Math.Sqrt(r2);
            double invR2 = invR * invR;
            double scale = (gVal * gScale) * pl.mass * invR2;   // a = (G * scale) * m / r^2

            ax += scale * dx * invR;  // multiply by unit direction
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
using System.Collections.Generic;
using UnityEngine;

public class GravityField : MonoBehaviour
{
    // Drop this on an empty "Managers/GravityField" in the scene.
    public static readonly List<Planet> Planets = new List<Planet>();

    [Range(0f, 20f)] public float G = 2.6f;      // tuned, not real units
    [Range(1f, 100f)] public float maxAccel = 25f;

    private static GravityField _instance;
    void Awake() { _instance = this; }

    public static Vector2 AccelAt(Vector2 p)
    {
        if (_instance == null) return Vector2.zero;

        Vector2 a = Vector2.zero;
        for (int i = 0; i < Planets.Count; i++)
        {
            var pl = Planets[i];
            Vector2 dir = (Vector2)pl.WorldPos - p;
            float r2 = Mathf.Max(dir.sqrMagnitude, pl.MinRadiusSqr);
            a += (_instance.G * pl.mass / r2) * dir.normalized;
        }
        float mag = a.magnitude;
        if (mag > _instance.maxAccel) a = a * (_instance.maxAccel / mag);
        return a;
    }
}
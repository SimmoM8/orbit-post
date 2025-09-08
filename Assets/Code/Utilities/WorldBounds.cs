using UnityEngine;

// Minimal, single-responsibility helper: provides world center and half-extents.
// Uses WorldBuilder (center at 0,0; WorldDefinition.halfExtents) as the single source of truth.
// Falls back to a sane default if WorldBuilder is not present. No singletons, no GameObjects.
public static class WorldBounds
{
    public static bool TryGet(out Vector2 center, out Vector2 half)
    {
#if UNITY_2023_1_OR_NEWER
        var wb = Object.FindFirstObjectByType<WorldBuilder>();
#else
        var wb = Object.FindObjectOfType<WorldBuilder>();
#endif
        if (wb)
        {
            center = Vector2.zero;
            half = wb.WorldHalfExtents;
            return true;
        }

        center = Vector2.zero;
        half = new Vector2(60f, 60f);
        return false;
    }

    public static Vector2 Center()
    {
        TryGet(out var c, out _); return c;
    }

    public static Vector2 HalfExtents()
    {
        TryGet(out _, out var h); return h;
    }
}

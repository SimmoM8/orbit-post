using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Orbit Post/Planet Prefab Library", fileName = "PlanetPrefabLibrary")]
public class PlanetPrefabLibrary : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public PlanetSize size;   // which SIZE this entry represents
        public GameObject prefab; // prefab that already has correct collider & TypeSpriteSet
    }

    public Entry[] entries;

    // Cache computed radius per size to avoid repeated component queries
    private readonly Dictionary<PlanetSize, float> _radiusCache = new Dictionary<PlanetSize, float>();

    public bool TryGet(PlanetSize size, out GameObject prefab)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].size == size && entries[i].prefab != null)
                {
                    prefab = entries[i].prefab;
                    return true;
                }
            }
        }
        prefab = null;
        return false;
    }

    public bool TryGetRadius(PlanetSize size, out float radius)
    {
        if (_radiusCache.TryGetValue(size, out radius))
            return true;

        if (TryGet(size, out var prefab) && prefab)
        {
            radius = ComputeRadius(prefab);
            _radiusCache[size] = radius;
            return true;
        }
        radius = 1f;
        return false;
    }

    static float ComputeRadius(GameObject prefab)
    {
        if (!prefab) return 1f;
        var col = prefab.GetComponent<CircleCollider2D>();
        if (col) return Mathf.Max(0.01f, col.radius);
        var sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr) return Mathf.Max(0.01f, Mathf.Max(sr.bounds.extents.x, sr.bounds.extents.y));
        return 1f;
    }

    void OnValidate()
    {
        _radiusCache.Clear();
    }
}

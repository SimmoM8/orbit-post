using UnityEngine;
using System.Collections.Generic;
using OrbitPost.Gameplay.Planets;

public class WorldBuilder : MonoBehaviour
{
    [SerializeField] private WorldDefinition definition;

    public IReadOnlyList<DeliveryNode> Posts => _posts;
    public IReadOnlyList<Planet> Planets => _planets;

    // Public exposure of world bounds so other systems (e.g., Minimap) can map the whole world.
    public Vector2 WorldHalfExtents => definition ? definition.halfExtents : new Vector2(60f, 60f);

    private readonly List<DeliveryNode> _posts = new();
    private readonly List<Planet> _planets = new();

    // Deterministic RNG for procedural generation (isolated from UnityEngine.Random)
    private System.Random _procRand;

    [Header("Debug Visualization")]
    [SerializeField] private bool _debugDrawGizmos = true;
    [SerializeField] private bool _debugDrawBounds = true;
    [SerializeField] private bool _debugDrawOnlyInProcedural = true;

    void Start()
    {
        if (!definition) { Debug.LogError("WorldDefinition missing"); return; }
        BuildWorld();
    }

    private void BuildWorld()
    {
        switch (definition.mode)
        {
            case WorldMode.Authored:
                SpawnAuthored();
                break;
            case WorldMode.Procedural:
                SpawnProcedural();
                break;
        }
        // TODO: after spawn, pass any world-level params to GameManager/RunManager if needed
    }

    private void SpawnAuthored()
    {
        if (definition.authoredPosts != null)
        {
            foreach (var ap in definition.authoredPosts)
                SpawnAuthoredPost(ap);
        }

        // Planets (with profiles and optional overrides)
        if (definition.authoredPlanets != null)
        {
            foreach (var ap in definition.authoredPlanets)
                SpawnAuthoredPlanet(ap);
        }
    }

    private void SpawnAuthoredPlanet(WorldDefinition.AuthoredPlanet ap)
    {
        if (!definition.planetPrefabs)
        {
            Debug.LogError("WorldDefinition: PlanetPrefabLibrary is missing.");
            return;
        }

        if (!definition.planetPrefabs.TryGet(ap.size, out var prefab) || !prefab)
        {
            Debug.LogError($"No planet prefab registered for size {ap.size} in PlanetPrefabLibrary.");
            return;
        }

        var go = Instantiate(prefab, ap.position, Quaternion.identity, transform);
        go.transform.localScale = Vector3.one; // ensure clean scale

        var planet = go.GetComponent<Planet>();
        if (!planet)
        {
            Debug.LogError("Spawned size prefab has no Planet component.");
            return;
        }

        // Centralized application of authored data (type/profile/mass + producer weights)
        WorldSpawnHelpers.ApplyAuthoredPlanet(planet, ap);

        _planets.Add(planet);
    }

    private void SpawnAuthoredPost(WorldDefinition.AuthoredPost ap)
    {
        if (!definition.postPrefab)
        {
            Debug.LogError("WorldDefinition: postPrefab is missing.");
            return;
        }

        var go = Instantiate(definition.postPrefab, ap.position, Quaternion.identity, transform);
        var node = go.GetComponent<DeliveryNode>();
        if (!node)
        {
            Debug.LogError("Spawned post prefab has no DeliveryNode component.");
            return;
        }

        // Centralized application of authored data (level/name/influence/request)
        WorldSpawnHelpers.ApplyAuthoredPost(node, ap);

        _posts.Add(node);
    }

    private void SpawnProcedural()
    {
        // Use System.Random for reproducible generation that doesn't affect Unity's global RNG
        _procRand = new System.Random(definition.seed);

        int postCount = _procRand.Next(definition.postCountRange.x, definition.postCountRange.y + 1);
        int planetCount = _procRand.Next(definition.planetCountRange.x, definition.planetCountRange.y + 1);

        // Place posts first with optional spacing constraint and edge padding
        var postPositions = new List<Vector2>(postCount);
        float minPostSpacing = Mathf.Max(0f, definition.minPostSpacing);
        float postEdgePadding = Mathf.Max(0f, definition.edgePaddingPosts);
        float postR = Mathf.Max(0f, GetPostRadius());
        float marginX = postR + postEdgePadding;
        float marginY = postR + postEdgePadding;
        if (minPostSpacing <= 0f)
        {
            for (int i = 0; i < postCount; i++)
            {
                var pos = RandomInBoundsWithMargin(marginX, marginY);
                postPositions.Add(pos);
                SpawnPost(pos);
            }
        }
        else
        {
            int placedPosts = 0;
            int maxPostAttempts = Mathf.Max(postCount * 50, 200);
            int attemptsPosts = 0;
            while (placedPosts < postCount && attemptsPosts < maxPostAttempts)
            {
                attemptsPosts++;
                var candidate = RandomInBoundsWithMargin(marginX, marginY);
                bool ok = true;
                for (int j = 0; j < postPositions.Count; j++)
                {
                    if (Vector2.SqrMagnitude(candidate - postPositions[j]) < (minPostSpacing * minPostSpacing))
                    { ok = false; break; }
                }
                if (!ok) continue;
                postPositions.Add(candidate);
                SpawnPost(candidate);
                placedPosts++;
            }
            if (placedPosts < postCount)
            {
                Debug.LogWarning($"[WorldBuilder] Procedural post placement placed {placedPosts}/{postCount} due to post spacing. Consider reducing constraints or expanding bounds.");
            }
        }

        // Place planets with constraints: min spacing between planets and min distance from posts
        var planetPositions = new List<Vector2>(planetCount);
        float minPlanetSpacing = Mathf.Max(0f, definition.minPlanetSpacing);
        float minFromPost = Mathf.Max(0f, definition.minDistanceFromPost);

        int placed = 0;
        int maxAttempts = Mathf.Max(planetCount * 50, 500); // cap attempts to avoid infinite loops
        int attempts = 0;
        while (placed < planetCount && attempts < maxAttempts)
        {
            attempts++;
            var candidate = RandomInBounds();

            bool ok = true;
            // Check against existing planets
            for (int pi = 0; pi < planetPositions.Count; pi++)
            {
                if (Vector2.SqrMagnitude(candidate - planetPositions[pi]) < (minPlanetSpacing * minPlanetSpacing))
                {
                    ok = false; break;
                }
            }
            if (!ok) continue;

            // Check against posts
            for (int si = 0; si < postPositions.Count; si++)
            {
                if (Vector2.SqrMagnitude(candidate - postPositions[si]) < (minFromPost * minFromPost))
                {
                    ok = false; break;
                }
            }
            if (!ok) continue;

            // Accept and spawn
            planetPositions.Add(candidate);
            SpawnPlanet(candidate);
            placed++;
        }

        if (placed < planetCount)
        {
            Debug.LogWarning($"[WorldBuilder] Procedural placement placed {placed}/{planetCount} planets due to spacing constraints. Consider reducing constraints or expanding bounds.");
        }

        // Clear after use so other systems fall back to Unity RNG as expected
        _procRand = null;
    }

    private Vector2 RandomInBounds()
    {
        if (_procRand != null)
        {
            float x = Mathf.Lerp(-definition.halfExtents.x, definition.halfExtents.x, (float)_procRand.NextDouble());
            float y = Mathf.Lerp(-definition.halfExtents.y, definition.halfExtents.y, (float)_procRand.NextDouble());
            return new Vector2(x, y);
        }
        else
        {
            return new Vector2(
                Random.Range(-definition.halfExtents.x, definition.halfExtents.x),
                Random.Range(-definition.halfExtents.y, definition.halfExtents.y)
            );
        }
    }

    // Random position within bounds with extra margins from edges
    private Vector2 RandomInBoundsWithMargin(float marginX, float marginY)
    {
        float hx = Mathf.Max(0f, definition.halfExtents.x);
        float hy = Mathf.Max(0f, definition.halfExtents.y);
        float minX = -hx + Mathf.Max(0f, marginX);
        float maxX =  hx - Mathf.Max(0f, marginX);
        float minY = -hy + Mathf.Max(0f, marginY);
        float maxY =  hy - Mathf.Max(0f, marginY);

        if (minX > maxX) { minX = maxX = 0f; }
        if (minY > maxY) { minY = maxY = 0f; }

        if (_procRand != null)
        {
            float x = Mathf.Lerp(minX, maxX, (float)_procRand.NextDouble());
            float y = Mathf.Lerp(minY, maxY, (float)_procRand.NextDouble());
            return new Vector2(x, y);
        }
        else
        {
            return new Vector2(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY)
            );
        }
    }

    private float GetPostRadius()
    {
        if (!definition || !definition.postPrefab) return 0f;
        var prefab = definition.postPrefab;
        var col = prefab.GetComponent<CircleCollider2D>();
        if (col) return Mathf.Max(0f, col.radius * Mathf.Max(prefab.transform.lossyScale.x, prefab.transform.lossyScale.y));
        var node = prefab.GetComponent<DeliveryNode>();
        if (node) return Mathf.Max(0f, node.radius * Mathf.Max(prefab.transform.lossyScale.x, prefab.transform.lossyScale.y));
        return 0f;
    }

    private void SpawnPost(Vector2 position)
    {
        if (!definition.postPrefab) { Debug.LogError("Post prefab missing"); return; }
        var go = Instantiate(definition.postPrefab, position, Quaternion.identity, transform);
        var node = go.GetComponent<DeliveryNode>();
        if (node) _posts.Add(node);
    }

    private void SpawnPlanet(Vector2 position)
    {
        if (!definition.planetPrefabs)
        {
            Debug.LogError("WorldDefinition: PlanetPrefabLibrary is missing.");
            return;
        }

        var randomSize = RandomEnumValue<PlanetSize>();
        var randomType = RandomEnumValue<PlanetType>();

        if (!definition.planetPrefabs.TryGet(randomSize, out var prefab) || !prefab)
        {
            Debug.LogError($"No planet prefab registered for size {randomSize} in PlanetPrefabLibrary.");
            return;
        }

        var go = Instantiate(prefab, position, Quaternion.identity, transform);
        var planet = go.GetComponent<Planet>();
        if (!planet)
        {
            Debug.LogError("Spawned prefab has no Planet component.");
            return;
        }

        planet.planetType = randomType;
        planet.ApplyTypeVisual();

        var producer = go.GetComponent<PlanetProducer>();
        if (producer) producer.RebuildTypeWeights();

        _planets.Add(planet);
    }

    private T RandomEnumValue<T>()
    {
        var values = System.Enum.GetValues(typeof(T));
        if (_procRand != null)
        {
            return (T)values.GetValue(_procRand.Next(0, values.Length));
        }
        else
        {
            return (T)values.GetValue(Random.Range(0, values.Length));
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!_debugDrawGizmos || !definition) return;
        if (_debugDrawOnlyInProcedural && definition.mode != WorldMode.Procedural) return;

        // Draw world bounds
        if (_debugDrawBounds)
        {
            var hx = definition.halfExtents.x;
            var hy = definition.halfExtents.y;
            var a = new Vector3(-hx, -hy, 0f);
            var b = new Vector3(-hx,  hy, 0f);
            var c = new Vector3( hx,  hy, 0f);
            var d = new Vector3( hx, -hy, 0f);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        // Posts: draw keep-out radius for planets and optional post spacing radius
        if (_posts != null)
        {
            float rKeepOut = Mathf.Max(0f, definition.minDistanceFromPost);
            float rPostSpacing = Mathf.Max(0f, definition.minPostSpacing);
            for (int i = 0; i < _posts.Count; i++)
            {
                var node = _posts[i];
                if (!node) continue;
                if (rKeepOut > 0f)
                {
                    Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.75f);
                    Gizmos.DrawWireSphere(node.transform.position, rKeepOut);
                }
                if (rPostSpacing > 0f)
                {
                    Gizmos.color = new Color(1f, 0.2f, 0.9f, 0.6f);
                    Gizmos.DrawWireSphere(node.transform.position, rPostSpacing);
                }
            }
        }

        // Planets: draw spacing radius for minimum planet spacing
        if (_planets != null)
        {
            float rPlanet = Mathf.Max(0f, definition.minPlanetSpacing);
            if (rPlanet > 0f)
            {
                Gizmos.color = new Color(0.2f, 1f, 1f, 0.6f);
                for (int i = 0; i < _planets.Count; i++)
                {
                    var p = _planets[i];
                    if (!p) continue;
                    Gizmos.DrawWireSphere(p.transform.position, rPlanet);
                }
            }
        }
    }
}

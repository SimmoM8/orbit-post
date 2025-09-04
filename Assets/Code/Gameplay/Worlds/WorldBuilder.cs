using UnityEngine;
using System.Collections.Generic;
using OrbitPost.Gameplay.Planets;

public class WorldBuilder : MonoBehaviour
{
    [SerializeField] private WorldDefinition definition;

    public IReadOnlyList<DeliveryNode> Posts => _posts;
    public IReadOnlyList<Planet> Planets => _planets;

    private readonly List<DeliveryNode> _posts = new();
    private readonly List<Planet> _planets = new();

    // Deterministic RNG for procedural generation (isolated from UnityEngine.Random)
    private System.Random _procRand;

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

        // Place posts first (no spacing constraint specified for posts)
        var postPositions = new List<Vector2>(postCount);
        for (int i = 0; i < postCount; i++)
        {
            var pos = RandomInBounds();
            postPositions.Add(pos);
            SpawnPost(pos);
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
}

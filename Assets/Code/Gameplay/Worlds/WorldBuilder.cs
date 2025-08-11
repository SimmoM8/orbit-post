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

        // Set the visual type (prefab's TypeSpriteSet will switch sprite)
        planet.planetType = ap.type;
        planet.ApplyTypeVisual();

        // Assign production profile (not visuals) and rebuild weights
        planet.profile = ap.profile;

        // Apply authored mass (prefab defaults may be 0 by design)
        planet.mass = ap.mass;
        if (planet.mass <= 0f)
        {
            Debug.LogWarning($"[WorldBuilder] Planet at {ap.position} has mass 0; it will not contribute to gravity until you set a positive mass in the WorldDefinition.");
        }

        var producer = go.GetComponent<PlanetProducer>();
        if (producer) producer.RebuildTypeWeights();

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

        // Apply starting level (clamped to at least 1)
        node.level = Mathf.Max(1, ap.startLevel);

        node.displayName = ap.displayName;
        node.influenceRadius = ap.influenceRadius;
        node.requestAmount = ap.initialRequestAmount; // syncs requestGoal internally
        node.SetRequest(ap.initialRequestMaterial);   // applies color/label/UI

        _posts.Add(node);
    }

    private void SpawnProcedural()
    {
        // Placeholder: next step we’ll implement seeded placement (Poisson/jittered grid).
        Random.InitState(definition.seed);

        int postCount = Random.Range(definition.postCountRange.x, definition.postCountRange.y + 1);
        int planetCount = Random.Range(definition.planetCountRange.x, definition.planetCountRange.y + 1);

        // For now, simple random inside bounds (we’ll upgrade immediately in Step 2)
        for (int i = 0; i < postCount; i++)
            SpawnPost(RandomInBounds());

        for (int i = 0; i < planetCount; i++)
            SpawnPlanet(RandomInBounds());
    }

    private Vector2 RandomInBounds()
    {
        return new Vector2(
            Random.Range(-definition.halfExtents.x, definition.halfExtents.x),
            Random.Range(-definition.halfExtents.y, definition.halfExtents.y)
        );
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
        return (T)values.GetValue(Random.Range(0, values.Length));
    }
}
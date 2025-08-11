using UnityEngine;

public enum WorldMode { Authored, Procedural }

[CreateAssetMenu(menuName = "Orbit Post/World Definition", fileName = "WorldDefinition")]
public class WorldDefinition : ScriptableObject
{
    [Header("Identity")]
    public string worldId = "world_1";
    public string displayName = "First Light";
    public WorldMode mode = WorldMode.Authored;

    [Header("Bounds (centered at 0,0)")]
    public Vector2 halfExtents = new Vector2(60f, 60f); // => 120x120 world
    [Header("Common Prefabs & Data")]

    public PlanetPrefabLibrary planetPrefabs; // NEW: size -> prefab
    public GameObject postPrefab;

    [System.Serializable]
    public struct AuthoredPlanet
    {
        public Vector2 position;
        public PlanetSize size;       // choose which prefab to spawn
        public PlanetType type;       // choose which sprite inside that prefab
        public PlanetProfile profile; // (optional) production bias, not visuals
        [Min(0f)] public float mass; // independent mass per planet (0 means no gravity contribution)
    }

    [System.Serializable]
    public struct AuthoredPost
    {
        public Vector2 position;
        [Min(1)] public int startLevel;
        public string displayName;
        public float influenceRadius;
        public PackageType initialRequestMaterial; // type of package requested
        public int initialRequestAmount; // how many packages to level up
    }

    [Header("Authored (used if mode == Authored)")]
    public AuthoredPost[] authoredPosts;
    public AuthoredPlanet[] authoredPlanets;   // position + profile + optional overrides

    [Header("Procedural (used if mode == Procedural)")]
    public int seed = 12345;
    public Vector2Int postCountRange = new Vector2Int(5, 8);
    public Vector2Int planetCountRange = new Vector2Int(15, 24);
    public float minPlanetSpacing = 7f;
    public float minDistanceFromPost = 5f;

    [Header("Gameplay Defaults")]
    public float basePostInfluenceRadius = 12f; // level 1
    public float influenceRadiusPerLevel = 3f;  // add per level
}
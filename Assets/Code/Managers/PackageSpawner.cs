using UnityEngine;
using System.Collections.Generic;

public class PackageSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public Package packagePrefab;
    public DeliveryNode deliveryPrefab;

    [Header("Approx Radii (for spawn clearance)")]
    [Tooltip("Approx visual/collider radius of a Package in world units (used only during spawning)")]
    public float packageApproxRadius = 0.6f;
    [Tooltip("Approx visual/collider radius of a DeliveryNode in world units (used only during spawning). If Delivery prefab has a radius field it will be used instead.")]
    public float nodeApproxRadius = 0.9f;

    [Header("Types")]
    public List<PackageType> packageTypes = new List<PackageType>();

    [Header("Counts")]
    public int concurrentPackages = 3;
    public int concurrentNodes = 2;
    public bool respawnOnPickup = false;
    public bool respawnOnDeliver = true;

    [Header("Spawn Area (world units)")]
    public Vector2 areaCenter = Vector2.zero;
    public Vector2 areaSize = new Vector2(18f, 12f);

    [Header("Constraints")]
    public float minDistFromPlanet = 1.8f;
    public float minDistFromCourier = 2.5f;
    public float minDistBetweenPkgs = 1.5f;
    public float minDistBetweenNodes = 1.5f;
    [Tooltip("Minimum distance between a spawned package and any delivery node (and vice versa)")]
    public float minDistBetweenTypes = 1.2f;

    [Tooltip("Keep spawns this far away from the spawn-area edges")]
    public float edgePadding = 0.5f;

    public int maxTries = 64;

    [Header("Randomness")]
    [Tooltip(">=0 for deterministic spawns")]
    public int randomSeed = -1;

    [Header("Bias")]
    [Tooltip("Higher values push spawns farther from the courier")] public float courierBiasWeight = 2f;

    [Header("Poisson Sampling")]
    [Tooltip("Use seeded Poisson-disk sampling for blue-noise spawn points")] public bool usePoisson = true;
    [Tooltip("k: number of candidates to try per active point (Bridson)")] public int poissonK = 20;
    [Tooltip("How many Poisson points to precompute inside the area")] public int poissonTarget = 128;

    // Internal Poisson state
    List<Vector2> _poissonPool = new List<Vector2>();
    int _poissonIndex = 0;

    readonly List<Package> activePackages = new List<Package>();
    readonly List<DeliveryNode> activeNodes = new List<DeliveryNode>();
    CourierController courier;

    void Start()
    {
#if UNITY_2023_1_OR_NEWER
        courier = Object.FindFirstObjectByType<CourierController>();
#else
        courier = Object.FindObjectOfType<CourierController>();
#endif

        if (randomSeed >= 0) Random.InitState(randomSeed);

        // Try to read node radius from prefab if available
        if (deliveryPrefab)
        {
            nodeApproxRadius = Mathf.Max(nodeApproxRadius, deliveryPrefab.radius);
        }
        // Try to read a circle collider on package prefab to get a better radius
        if (packagePrefab)
        {
            var cc = packagePrefab.GetComponent<CircleCollider2D>();
            if (cc) packageApproxRadius = Mathf.Max(packageApproxRadius, cc.radius);
        }

        // Ensure GameManager exists and register materials for demand
        if (GameManager.Instance == null)
        {
            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<GameManager>();
        }
        GameManager.Instance.RegisterMaterials(packageTypes);

        // Build Poisson pool if enabled
        if (usePoisson)
        {
            // Ensure Poisson minimum distance accounts for package diameter and desired extra spacing
            float r = (packageApproxRadius * 2f) + Mathf.Max(minDistBetweenPkgs, minDistBetweenTypes);
            BuildPoissonPool(r);
        }

        // Spawn node first so packages respect its spacing, then packages
        for (int i = 0; i < Mathf.Max(0, concurrentNodes); i++) SpawnNode();
        for (int i = 0; i < Mathf.Max(0, concurrentPackages); i++) SpawnPackage();
    }

    // ---------- Spawning ----------
    void SpawnPackage()
    {
        if (!packagePrefab) { Debug.LogError("[PackageSpawner] Missing Package prefab"); return; }

        Vector2 pos = usePoisson ? NextPoissonPosition(true, packageApproxRadius) : FindValidPosition(true, packageApproxRadius);
        var pkg = Instantiate(packagePrefab, pos, Quaternion.identity);

        // Apply a random type (if any)
        if (packageTypes != null && packageTypes.Count > 0)
        {
            var type = PickMaterialWeighted();
            pkg.ApplyType(type);
        }

        // Track & subscribe to pickup event
        activePackages.Add(pkg);
        pkg.OnPickedUp += HandlePickedUp;
        pkg.OnDelivered += HandlePackageDelivered;
    }

    void SpawnNode()
    {
        if (!deliveryPrefab) { Debug.LogError("[PackageSpawner] Missing DeliveryNode prefab"); return; }

        Vector2 pos = usePoisson ? NextPoissonPosition(false, nodeApproxRadius) : FindValidPosition(false, nodeApproxRadius);
        var node = Instantiate(deliveryPrefab, pos, Quaternion.identity);
        activeNodes.Add(node);
        node.availableMaterials = packageTypes != null ? packageTypes.ToArray() : new PackageType[0];
        // Initial request biased by demand
        if (packageTypes != null && packageTypes.Count > 0)
        {
            var req = PickMaterialWeighted();
            node.SetRequest(req);
        }
        // Do not subscribe to onDelivered; nodes are static and persist
    }

    // ---------- Callbacks ----------
    void HandlePickedUp(Package pkg)
    {
        // Remove from list when picked up; optionally spawn replacement
        int idx = activePackages.IndexOf(pkg);
        if (idx >= 0) activePackages.RemoveAt(idx);
        if (respawnOnPickup) SpawnPackage();
    }

    void HandlePackageDelivered(Package pkg, int score)
    {
        // Ensure it's not tracked anymore (it was usually removed on pickup)
        int idx = activePackages.IndexOf(pkg);
        if (idx >= 0) activePackages.RemoveAt(idx);

        // Only packages respawn on successful delivery
        SpawnPackage();
    }

    void HandleDelivered(DeliveryNode node)
    {
        // Nodes persist; this should not be called in current design.
    }

    // ---------- Positioning ----------
    Vector2 FindValidPosition(bool forPackage, float candRadius)
    {
        Vector2 half = areaSize * 0.5f;
        Vector2 best = areaCenter;
        float bestScore = -1f;

        for (int i = 0; i < maxTries; i++)
        {
            float x = Random.Range(areaCenter.x - half.x + edgePadding, areaCenter.x + half.x - edgePadding);
            float y = Random.Range(areaCenter.y - half.y + edgePadding, areaCenter.y + half.y - edgePadding);
            Vector2 c = new Vector2(x, y);

            if (IsValid(c, forPackage, candRadius))
                return c; // perfect candidate found

            // Keep the best-scoring candidate for fallback
            float score = EvaluateCandidate(c, forPackage, candRadius);
            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }
        return best; // fallback to the best available location
    }

    bool IsValid(Vector2 p, bool forPackage, float candRadius)
    {
        // Planets (distance from planet surface)
        for (int i = 0; i < GravityField.Planets.Count; i++)
        {
            var pl = GravityField.Planets[i];
            float surfDist = Vector2.Distance(p, (Vector2)pl.WorldPos) - (pl.radius + candRadius);
            if (surfDist < minDistFromPlanet) return false;
        }

        // Courier
        if (courier)
        {
            float dc = Vector2.Distance(p, (Vector2)courier.transform.position) - candRadius;
            if (dc < minDistFromCourier) return false;
        }

        // Same-type spacing
        if (forPackage)
        {
            for (int i = 0; i < activePackages.Count; i++)
            {
                var n = activePackages[i];
                if (!n) continue;
                float d = Vector2.Distance(p, (Vector2)n.transform.position) - (packageApproxRadius + candRadius);
                if (d < minDistBetweenPkgs) return false;
            }
            // Cross-type spacing (against nodes)
            for (int i = 0; i < activeNodes.Count; i++)
            {
                var n = activeNodes[i];
                if (!n) continue;
                float d = Vector2.Distance(p, (Vector2)n.transform.position) - (nodeApproxRadius + candRadius);
                if (d < minDistBetweenTypes) return false;
            }
        }
        else
        {
            for (int i = 0; i < activeNodes.Count; i++)
            {
                var n = activeNodes[i];
                if (!n) continue;
                float d = Vector2.Distance(p, (Vector2)n.transform.position) - (nodeApproxRadius + candRadius);
                if (d < minDistBetweenNodes) return false;
            }
            // Cross-type spacing (against packages)
            for (int i = 0; i < activePackages.Count; i++)
            {
                var n = activePackages[i];
                if (!n) continue;
                float d = Vector2.Distance(p, (Vector2)n.transform.position) - (packageApproxRadius + candRadius);
                if (d < minDistBetweenTypes) return false;
            }
        }

        // Inside area bounds with padding
        Vector2 half = areaSize * 0.5f;
        if (p.x < areaCenter.x - half.x + candRadius + edgePadding) return false;
        if (p.x > areaCenter.x + half.x - candRadius - edgePadding) return false;
        if (p.y < areaCenter.y - half.y + candRadius + edgePadding) return false;
        if (p.y > areaCenter.y + half.y - candRadius - edgePadding) return false;

        return true;
    }

    float EvaluateCandidate(Vector2 p, bool forPackage, float candRadius)
    {
        float score = 0f;

        // Prefer farther from planets (surface distance)
        for (int i = 0; i < GravityField.Planets.Count; i++)
        {
            var pl = GravityField.Planets[i];
            float surfDist = Mathf.Max(0f, Vector2.Distance(p, (Vector2)pl.WorldPos) - (pl.radius + candRadius));
            score += surfDist;
        }

        // Prefer some distance from courier
        if (courier)
        {
            float dc = Mathf.Max(0f, Vector2.Distance(p, (Vector2)courier.transform.position) - candRadius);
            score += courierBiasWeight * dc;
        }

        // Prefer spacing from existing items
        if (forPackage)
        {
            for (int i = 0; i < activePackages.Count; i++)
            {
                var n = activePackages[i];
                if (!n) continue;
                float d = Mathf.Max(0f, Vector2.Distance(p, (Vector2)n.transform.position) - (packageApproxRadius + candRadius));
                score += 0.75f * d;
            }
            for (int i = 0; i < activeNodes.Count; i++)
            {
                var n = activeNodes[i];
                if (!n) continue;
                float d = Mathf.Max(0f, Vector2.Distance(p, (Vector2)n.transform.position) - (nodeApproxRadius + candRadius));
                score += 0.5f * d;
            }
        }
        else
        {
            for (int i = 0; i < activeNodes.Count; i++)
            {
                var n = activeNodes[i];
                if (!n) continue;
                float d = Mathf.Max(0f, Vector2.Distance(p, (Vector2)n.transform.position) - (nodeApproxRadius + candRadius));
                score += 0.75f * d;
            }
            for (int i = 0; i < activePackages.Count; i++)
            {
                var n = activePackages[i];
                if (!n) continue;
                float d = Mathf.Max(0f, Vector2.Distance(p, (Vector2)n.transform.position) - (packageApproxRadius + candRadius));
                score += 0.5f * d;
            }
        }

        return score;
    }

    // ---------------- Poisson-disk sampling (Bridson) ----------------
    void BuildPoissonPool(float r)
    {
        _poissonPool.Clear();
        _poissonIndex = 0;

        // Seed already set in Start() if randomSeed >= 0
        var points = GeneratePoissonPoints(r, poissonK, poissonTarget);
        _poissonPool.AddRange(points);
    }

    Vector2 NextPoissonPosition(bool forPackage, float candRadius)
    {
        // Try precomputed pool first
        for (int guard = 0; guard < _poissonPool.Count; guard++)
        {
            if (_poissonIndex >= _poissonPool.Count) _poissonIndex = 0;
            var c = _poissonPool[_poissonIndex++];
            if (IsValid(c, forPackage, candRadius)) return c;
        }
        // Fallback to heuristic sampler
        return FindValidPosition(forPackage, candRadius);
    }

    List<Vector2> GeneratePoissonPoints(float r, int k, int target)
    {
        var results = new List<Vector2>();
        if (r <= 0f) return results;

        // Bounds with padding
        Vector2 half = areaSize * 0.5f;
        Rect rect = new Rect(areaCenter.x - half.x + edgePadding, areaCenter.y - half.y + edgePadding,
                             areaSize.x - 2f * edgePadding, areaSize.y - 2f * edgePadding);

        float cell = r / Mathf.Sqrt(2f);
        int gw = Mathf.Max(1, Mathf.CeilToInt(rect.width / cell));
        int gh = Mathf.Max(1, Mathf.CeilToInt(rect.height / cell));
        Vector2[,] grid = new Vector2[gw, gh];
        for (int x = 0; x < gw; x++) for (int y = 0; y < gh; y++) grid[x, y] = new Vector2(float.NaN, float.NaN);

        List<Vector2> active = new List<Vector2>();

        // Helper lambdas
        int GX(float x) => Mathf.Clamp((int)((x - rect.x) / cell), 0, gw - 1);
        int GY(float y) => Mathf.Clamp((int)((y - rect.y) / cell), 0, gh - 1);
        bool InRect(Vector2 p) => p.x >= rect.x && p.x <= rect.xMax && p.y >= rect.y && p.y <= rect.yMax;
        bool Far(Vector2 p)
        {
            int gx = GX(p.x), gy = GY(p.y);
            int i0 = Mathf.Max(0, gx - 2), i1 = Mathf.Min(gw - 1, gx + 2);
            int j0 = Mathf.Max(0, gy - 2), j1 = Mathf.Min(gh - 1, gy + 2);
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    var q = grid[i, j];
                    if (!float.IsNaN(q.x))
                    {
                        if ((p - q).sqrMagnitude < r * r) return false;
                    }
                }
            return true;
        }

        // Initial point biased away from courier (if present)
        Vector2 p0;
        for (int tries = 0; ; tries++)
        {
            p0 = new Vector2(Random.Range(rect.x, rect.xMax), Random.Range(rect.y, rect.yMax));
            if (courier)
            {
                float dc = Vector2.Distance(p0, (Vector2)courier.transform.position);
                float bias = Mathf.InverseLerp(0f, Mathf.Max(rect.width, rect.height), dc);
                if (bias < 0.5f && tries < 64) continue; // push start farther from courier
            }
            if (Far(p0)) break;
        }
        results.Add(p0);
        active.Add(p0);
        grid[GX(p0.x), GY(p0.y)] = p0;

        // Bridson loop
        while (active.Count > 0 && results.Count < target)
        {
            int idx = Random.Range(0, active.Count);
            Vector2 p = active[idx];
            bool found = false;

            for (int i = 0; i < k; i++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float rad = Random.Range(r, 2f * r);
                Vector2 q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                if (!InRect(q) || !Far(q)) continue;
                results.Add(q);
                active.Add(q);
                grid[GX(q.x), GY(q.y)] = q;
                found = true;
                if (results.Count >= target) break;
            }

            if (!found) active.RemoveAt(idx);
        }

        return results;
    }

    void OnDrawGizmosSelected()
    {
        // Outer area
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.15f);
        Vector3 c = new Vector3(areaCenter.x, areaCenter.y, 0f);
        Gizmos.DrawCube(c, new Vector3(areaSize.x, areaSize.y, 0.1f));

        // Inner (padded) area
        Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.2f);
        Vector3 inner = new Vector3(Mathf.Max(0f, areaSize.x - 2f * edgePadding), Mathf.Max(0f, areaSize.y - 2f * edgePadding), 0.1f);
        Gizmos.DrawCube(c, inner);
    }

    PackageType PickMaterialWeighted()
    {
        if (packageTypes == null || packageTypes.Count == 0) return null;
        float total = 0f;
        for (int i = 0; i < packageTypes.Count; i++)
        {
            total += GameManager.Instance ? GameManager.Instance.GetSpawnWeight(packageTypes[i]) : 1f;
        }
        if (total <= 0f) return packageTypes[Random.Range(0, packageTypes.Count)];
        float r = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < packageTypes.Count; i++)
        {
            acc += GameManager.Instance ? GameManager.Instance.GetSpawnWeight(packageTypes[i]) : 1f;
            if (r <= acc) return packageTypes[i];
        }
        return packageTypes[packageTypes.Count - 1];
    }
}
using UnityEngine;
using System.Collections.Generic;
using OrbitPost.Gameplay.Planets;

/// <summary>
/// Spawns packages in a shallow orbit around the planet.
/// Production per material = base per-minute × planet-type weight × demand × post-boost
/// Caps orbiters at maxOrbiters and spaces them evenly on spawn.
/// </summary>
[RequireComponent(typeof(Transform))]
public class PlanetProducer : MonoBehaviour
{
    [System.Serializable]
    public class MaterialRate
    {
        public PackageType material;
        [Tooltip("Packages per minute at base level (before boosts)")]
        public float perMinute = 6f;
    }

    [System.Serializable]
    public class MaterialWeight
    {
        public PackageType material;
        [Tooltip("Relative likelihood multiplier for this planet. 1 = neutral, 0.5 = half as likely, 2 = twice as likely.")]
        public float weight = 1f;
    }

    // --- Debug accessors (safe; read-only) ---
    public int DebugWeightCount => _typeWeightLookup?.Count ?? 0;
    public string DebugProfileName => (planet && planet.profile) ? planet.profile.displayName : "(none)";
    public bool DebugUsingProfileWeights => useProfileTypeWeights;

    [Header("Production")]
    public List<MaterialRate> baseRates = new List<MaterialRate>();

    [Tooltip("Optional planet-type weights to bias which materials this planet tends to produce.")]
    public MaterialWeight[] typeWeights;

    [Tooltip("How far posts can influence production boost.")]
    public float postSearchRadius = 12f;

    [Tooltip("Re-evaluate nearest posts this often (seconds).")]
    public float recalcPostsEvery = 2f;

    [Header("Orbit Emit")]
    public Package packagePrefab;
    public float spawnAltitude = 0.2f;          // offset from planet edge
    public float initialTangentialSpeed = 3.5f; // starting orbital-ish speed
    public float anglePhaseDeg = 0f;            // base phase for even spacing
    public int maxOrbiters = 3;
    public int randomSeed = -1;

    [Header("Planet")]
    [Tooltip("If you have a Planet script with a radius field, assign it; otherwise set fallbackRadius.")]
    public Planet planet; // optional
    public float fallbackRadius = 1f;

    [Header("Profile Settings")]
    public bool useProfileTypeWeights = true;

    // Runtime
    readonly Dictionary<PackageType, float> _accum = new Dictionary<PackageType, float>();
    readonly List<Package> _orbiters = new List<Package>(8);
    float _postTimer = 0f;
    System.Random _sysRand;

    DeliveryNode[] _nodeCache;
    float _cachedPostBoost = 1f;

    // Prebuilt lookup for type weights
    readonly Dictionary<PackageType, float> _typeWeightLookup = new Dictionary<PackageType, float>();

    void Awake()
    {
        if (randomSeed >= 0) Random.InitState(randomSeed);
        _sysRand = new System.Random(randomSeed >= 0 ? randomSeed : UnityEngine.Random.Range(int.MinValue, int.MaxValue));

        if (!planet) planet = GetComponent<Planet>();

        // accumulators for each material
        foreach (var r in baseRates)
        {
            if (r != null && r.material != null && !_accum.ContainsKey(r.material))
                _accum[r.material] = 0f;
        }

        RebuildTypeWeights();
    }

    public void RebuildTypeWeights()
    {
        _typeWeightLookup.Clear();
        if (useProfileTypeWeights && planet != null && planet.profile != null)
        {
            var profile = planet.profile;
            if (profile.typeWeights != null)
            {
                foreach (var tw in profile.typeWeights)
                {
                    if (tw != null && tw.material != null)
                    {
                        _typeWeightLookup[tw.material] = Mathf.Max(0f, tw.weight);
                    }
                }
            }
        }
        else if (typeWeights != null)
        {
            foreach (var tw in typeWeights)
            {
                if (tw != null && tw.material != null)
                {
                    _typeWeightLookup[tw.material] = Mathf.Max(0f, tw.weight);
                }
            }
        }
    }

    void Update()
    {
        _postTimer -= Time.deltaTime;
        if (_postTimer <= 0f)
        {
            // Refresh cache of posts and recompute boost less often
            _nodeCache = FindObjectsByType<DeliveryNode>(FindObjectsSortMode.None);
            _cachedPostBoost = ComputePostBoost(_nodeCache);
            _postTimer = Mathf.Max(0.1f, recalcPostsEvery);
        }

        float dt = Time.deltaTime;

        foreach (var r in baseRates)
        {
            if (r == null || r.material == null) continue;

            // Base per-second
            float perSec = Mathf.Max(0f, r.perMinute) / 60f;

            // Planet-type weight (bias which materials accumulate faster on this planet)
            float typeW = GetTypeWeight(r.material);

            // Demand
            float demand = (GameManager.Instance) ? GameManager.Instance.GetDemandMultiplier(r.material) : 1f;

            // Use cached post boost
            float postBoost = _cachedPostBoost;

            float rate = perSec * typeW * demand * postBoost;
            if (rate <= 0f) continue;

            _accum[r.material] += rate * dt;

            // Try to emit whole packages
            int emit = Mathf.FloorToInt(_accum[r.material]);
            if (emit <= 0) continue;

            for (int i = 0; i < emit; i++)
            {
                if (_orbiters.Count >= maxOrbiters) break; // cap orbiters
                EmitPackage(r.material);
            }

            _accum[r.material] -= emit;
            if (_accum[r.material] < 0f) _accum[r.material] = 0f;
        }
    }

    float GetTypeWeight(PackageType mat)
    {
        if (mat == null) return 1f;
        if (_typeWeightLookup.TryGetValue(mat, out var w)) return Mathf.Max(0f, w);
        return 1f;
    }

    float ComputePostBoost(DeliveryNode[] nodes)
    {
        if (nodes == null || nodes.Length == 0) return 1f;

        Vector2 p = transform.position;
        float R = Mathf.Max(0.01f, postSearchRadius);
        float boostSum = 0f;

        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (!n) continue;
            float d = Vector2.Distance(p, n.transform.position);
            if (d > R) continue;

            // Distance weight: smooth curve favoring closer posts
            float w = Mathf.Pow(1f - (d / R), 2f);

            // Each post contributes level * its productionBoostPerLevel * weight
            boostSum += n.level * n.productionBoostPerLevel * w;
        }

        return 1f + Mathf.Max(0f, boostSum);
    }

    void EmitPackage(PackageType mat)
    {
        if (!packagePrefab) return;

        float pr = planet ? planet.radius : fallbackRadius;
        pr = Mathf.Max(0.1f, pr);

        // Compute evenly spaced angles for N+1 orbiters; we reposition everything on spawn
        int newCount = Mathf.Min(_orbiters.Count + 1, maxOrbiters);
        float phase = anglePhaseDeg * Mathf.Deg2Rad;

        // Reposition existing orbiters to even angles
        for (int i = 0; i < _orbiters.Count; i++)
        {
            float ang = phase + (i * Mathf.PI * 2f / newCount);
            Vector2 normal = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector2 center = transform.position;
            Vector2 pos = center + normal * (pr + spawnAltitude);
            var t = _orbiters[i].transform;
            t.position = pos;
            Vector2 tangent = new Vector2(-normal.y, normal.x);
            // give them a tiny nudge along tangent so they keep orbiting smoothly
            var rbOld = _orbiters[i].GetComponent<Rigidbody2D>();
            if (rbOld && rbOld.simulated)
            {
#if UNITY_6000_0_OR_NEWER
                rbOld.linearVelocity = tangent * initialTangentialSpeed;
#else
                rbOld.velocity = tangent * initialTangentialSpeed;
#endif
            }
        }

        // Spawn the new orbiter at the next even slot (last index)
        {
            int i = newCount - 1;
            float ang = phase + (i * Mathf.PI * 2f / newCount);
            Vector2 normal = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector2 center = transform.position;
            Vector2 pos = center + normal * (pr + spawnAltitude);
            Vector2 tangent = new Vector2(-normal.y, normal.x);
            Vector2 v0 = tangent * initialTangentialSpeed;

            var pkg = Instantiate(packagePrefab, pos, Quaternion.identity);
            pkg.ApplyType(mat);

            var rb = pkg.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.simulated = true;
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = v0;
#else
                rb.velocity = v0;
#endif
            }

            // track & reclaim when gone
            _orbiters.Add(pkg);
            pkg.OnPickedUp += HandlePickedUp;
            var relay = pkg.gameObject.AddComponent<OrbitReclaim>();
            relay.Init(this, pkg);
        }
    }

    void HandlePickedUp(Package p)
    {
        ReleaseOrbiter(p);
    }

    void ReleaseOrbiter(Package p)
    {
        if (!p) return;
        p.OnPickedUp -= HandlePickedUp;
        _orbiters.Remove(p);
    }

    // Small helper that returns the orbiter slot when the package is destroyed (e.g., delivered or despawned)
    class OrbitReclaim : MonoBehaviour
    {
        PlanetProducer owner;
        Package pkg;
        public void Init(PlanetProducer o, Package p) { owner = o; pkg = p; }
        void OnDestroy()
        {
            if (owner) owner.ReleaseOrbiter(pkg);
        }
    }
}
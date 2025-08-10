using UnityEngine;
using System.Collections.Generic;

public class PlanetProducer : MonoBehaviour
{
    [System.Serializable]
    public class MaterialRate
    {
        public PackageType material;
        [Tooltip("Packages per minute at base level (before boosts)")]
        public float perMinute = 6f;
    }

    [Header("Production")]
    public List<MaterialRate> baseRates = new List<MaterialRate>();
    public float postSearchRadius = 12f;
    public float recalcNearestPostEvery = 2f;

    [Header("Orbit Emit")]
    public Package packagePrefab;
    public float spawnAltitude = 0.2f;    // offset from planet edge
    public float initialTangentialSpeed = 3.5f; // starting orbital-ish speed
    public float angleJitterDeg = 20f;
    public int randomSeed = -1;

    DeliveryNode nearestPost;
    float postTimer = 0f;
    System.Random sysRand;
    Dictionary<PackageType, float> accum = new Dictionary<PackageType, float>();

    Planet planet; // if you have a Planet script with radius, else remove and expose radius

    void Awake()
    {
        if (randomSeed >= 0) Random.InitState(randomSeed);
        sysRand = new System.Random(randomSeed >= 0 ? randomSeed : UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        planet = GetComponent<Planet>(); // optional

        // init accumulators
        foreach (var r in baseRates)
        {
            if (r != null && r.material != null && !accum.ContainsKey(r.material))
                accum[r.material] = 0f;
        }
    }

    void Update()
    {
        // find nearest post periodically
        postTimer -= Time.deltaTime;
        if (postTimer <= 0f)
        {
            nearestPost = FindNearestPost();
            postTimer = recalcNearestPostEvery;
        }

        // accumulate production using dt
        float dt = Time.deltaTime;
        foreach (var r in baseRates)
        {
            if (r == null || r.material == null) continue;

            float perMin = Mathf.Max(0f, r.perMinute);

            // boosts
            float postBoost = 1f + ((nearestPost != null) ? (nearestPost.level * nearestPost.productionBoostPerLevel) : 0f);
            float demandBoost = GameManager.Instance ? GameManager.Instance.GetDemandMultiplier(r.material) : 1f;

            float ratePerSec = (perMin / 60f) * postBoost * demandBoost;
            accum[r.material] += ratePerSec * dt;

            if (accum[r.material] >= 1f)
            {
                int toSpawn = Mathf.FloorToInt(accum[r.material]);
                accum[r.material] -= toSpawn;
                for (int i = 0; i < toSpawn; i++)
                    EmitPackage(r.material);
            }
        }
    }

    DeliveryNode FindNearestPost()
    {
        DeliveryNode best = null;
        float bestD2 = postSearchRadius * postSearchRadius;
        var all = FindObjectsOfType<DeliveryNode>();
        Vector2 p = transform.position;

        foreach (var node in all)
        {
            if (!node) continue;
            float d2 = ((Vector2)node.transform.position - p).sqrMagnitude;
            if (d2 <= bestD2)
            {
                best = node; bestD2 = d2;
            }
        }
        return best;
    }

    void EmitPackage(PackageType mat)
    {
        if (!packagePrefab) return;

        // planet radius: read from Planet script if you have one; otherwise assume 1
        float pr = 1f;
        if (planet) pr = planet.radius;

        // pick an angle, offset just above the surface
        float ang = UnityEngine.Random.value * Mathf.PI * 2f;
        if (angleJitterDeg > 0f)
        {
            float jitter = UnityEngine.Random.Range(-angleJitterDeg, angleJitterDeg) * Mathf.Deg2Rad;
            ang += jitter;
        }
        Vector2 center = transform.position;
        Vector2 normal = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        Vector2 pos = center + normal * (pr + spawnAltitude);

        // tangential direction (rotate normal by +90°)
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        Vector2 v0 = tangent * initialTangentialSpeed;

        // spawn
        var pkg = Instantiate(packagePrefab, pos, Quaternion.identity);
        pkg.ApplyType(mat);

        // ensure Rigidbody2D is simulated and set initial velocity
        var rb = pkg.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.simulated = true;
            rb.linearVelocity = v0; // GravityField will take over
        }
    }
}
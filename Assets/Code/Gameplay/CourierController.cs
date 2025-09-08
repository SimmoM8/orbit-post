using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(LineRenderer))]
public class CourierController : MonoBehaviour
{
    [Header("Launch & Aim")]
    public float launchImpulse = 5f;
    public float aimMaxLength = 3f;
    public float predictionDt = 0.033f;
    public int predictionSteps = 30;

    [Header("Fuel")]
    public float maxFuel = 100f;
    public float fuelCostPerImpulse = 20f;

    [Header("Physics Tune")]
    public int physicsSubsteps = 4;     // runtime gravity substeps
    public float speedScale = 0.7f;     // globally slows accel for control

    [Header("Facing")]
    [Tooltip("Degrees to rotate so sprite points along velocity; set to match your art (e.g., 90 if sprite points up)")]
    public float facingOffsetDegrees = 0f;

    Rigidbody2D rb;
    LineRenderer lr;
    Camera cam;
    TrailRenderer[] trailRenderers;
    float fuel;
    bool aiming;
    bool launched;

    // World bounds via WorldBuilder (preferred) or PackageSpawner fallback
    WorldBuilder worldBuilder;

    // Carrying
    [Header("Carry Package")]
    [SerializeField] Transform carriedPackage; // runtime
    public bool HasPackage => carriedPackage != null;

    // Fired when this courier wraps across the world boundary.
    // Delta is the translation applied to preserve continuity (newPos - oldPos).
    public event System.Action<Vector2> OnWrapped;

    public int Fuel => Mathf.CeilToInt(fuel);
    public float FuelPercent => maxFuel <= 0f ? 0f : Mathf.Clamp01(fuel / maxFuel);

    public void AddFuel(float amount)
    {
        fuel = Mathf.Clamp(fuel + amount, 0f, maxFuel);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        lr = GetComponent<LineRenderer>();
        cam = Camera.main;
        trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        // Find world bounds provider (WorldBuilder) if present
#if UNITY_2023_1_OR_NEWER
        worldBuilder = Object.FindFirstObjectByType<WorldBuilder>();
#else
        worldBuilder = Object.FindObjectOfType<WorldBuilder>();
#endif

        fuel = maxFuel;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true; // prevent endless spin; we'll rotate manually to face velocity
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (fuel > 0f)
        {
            if (Input.GetMouseButtonDown(0))
            {
                aiming = true;
                launched = false;            // we’re planning a shot; freeze motion (slow-mo)
                Time.timeScale = 0.05f;
            }

            if (Input.GetMouseButtonUp(0) && aiming)
            {
                Time.timeScale = 1f;
                Vector2 v = ComputeLaunchVelocity();

                float cost = v.magnitude * fuelCostPerImpulse;
                if (cost > fuel && fuel > 0f)
                {
                    // scale impulse to available fuel
                    float scale = fuel / Mathf.Max(cost, 1e-5f);
                    v *= scale;
                    cost = fuel;
                }

                if (fuel > 0f)
                {
                    rb.AddForce(v, ForceMode2D.Impulse);
                    fuel = Mathf.Max(0f, fuel - cost);
                    aiming = false;
                    launched = true;
                    lr.positionCount = 0;
                }
                else
                {
                    aiming = false;
                    launched = false;
                    lr.positionCount = 0;
                }
            }
        }

        if (aiming)
        {
            Vector2 startVel = rb.linearVelocity + ComputeLaunchVelocity();
            DrawPrediction(transform.position, startVel);
        }
    }

    Vector2 ComputeLaunchVelocity()
    {
        Vector3 wp = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = (Vector2)(transform.position - wp); // pull-back slingshot
        float mag = dir.magnitude;

        if (mag < 0.2f) return Vector2.zero; // dead zone

        mag = Mathf.Min(mag, aimMaxLength);
        float scaled = Mathf.Pow(mag / aimMaxLength, 0.85f); // easing curve
        return dir.normalized * (scaled * launchImpulse);
    }

    void FixedUpdate()
    {
        if (!launched) return;

        int n = Mathf.Max(1, physicsSubsteps);
        float dt = Time.fixedDeltaTime / n;

        for (int i = 0; i < n; i++)
        {
            Vector2 a = GravityField.AccelAt(rb.position);
            rb.linearVelocity += a * dt * speedScale; // semi-implicit Euler (matches preview)
        }

        // Align facing with velocity
        if (rb.linearVelocity.sqrMagnitude > 1e-4f)
        {
            float ang = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            rb.MoveRotation(ang + facingOffsetDegrees);
        }

        // Toroidal wrap across world bounds so travel continues seamlessly
        WrapIfOutOfBounds();
    }

    void WrapIfOutOfBounds()
    {
        Vector2 p = rb.position;
        WorldBounds.TryGet(out var center, out var half);
        float w = Mathf.Max(1e-3f, half.x * 2f);
        float h = Mathf.Max(1e-3f, half.y * 2f);

        bool wrapped = false;
        Vector2 local = p - center;
        if (local.x >  half.x) { local.x -= w; wrapped = true; }
        else if (local.x < -half.x) { local.x += w; wrapped = true; }
        if (local.y >  half.y) { local.y -= h; wrapped = true; }
        else if (local.y < -half.y) { local.y += h; wrapped = true; }
        p = local + center;

        if (wrapped)
        {
            Vector2 delta = p - rb.position;
            rb.position = p; // teleport; preserve velocity and rotation
            AdjustTrailsAfterWrap(delta);
            // Notify listeners (e.g., camera) so they can shift with us
            try { OnWrapped?.Invoke(delta); } catch { }
        }
    }

    void AdjustTrailsAfterWrap(Vector2 delta)
    {
        if (trailRenderers == null || trailRenderers.Length == 0) return;
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            var tr = trailRenderers[i];
            if (!tr) continue;

            // Try to shift existing trail points via reflection API (if available in this Unity version)
            try
            {
                var getM = typeof(TrailRenderer).GetMethod("GetPositions", new System.Type[] { typeof(Vector3[]) });
                var setM = typeof(TrailRenderer).GetMethod("SetPositions", new System.Type[] { typeof(Vector3[]) });
                if (getM != null && setM != null)
                {
                    int cap = 512; // reasonable upper bound to avoid allocations explosion
                    var tmp = new Vector3[cap];
                    object[] args = new object[] { tmp };
                    int count = (int)getM.Invoke(tr, args);
                    if (count > 0)
                    {
                        var arr = (Vector3[])args[0];
                        for (int j = 0; j < count && j < arr.Length; j++)
                        {
                            arr[j].x += delta.x;
                            arr[j].y += delta.y;
                        }
                        // Trim to 'count'
                        if (count != arr.Length)
                        {
                            var sliced = new Vector3[count];
                            System.Array.Copy(arr, sliced, count);
                            setM.Invoke(tr, new object[] { sliced });
                        }
                        else
                        {
                            setM.Invoke(tr, new object[] { arr });
                        }
                        continue;
                    }
                }
            }
            catch { /* fallback below */ }

            // Fallback: clear the trail to avoid long line across the wrap
            tr.Clear();
        }
    }

    // Removed legacy bounds helper; using WorldBounds.TryGet instead.

    void DrawPrediction(Vector2 startPos, Vector2 startVel)
    {
        lr.positionCount = predictionSteps;
        Vector2 pos = startPos;
        Vector2 vel = startVel;

        int n = 4; // prediction substeps
        float dt = predictionDt / n;

        for (int i = 0; i < predictionSteps; i++)
        {
            for (int s = 0; s < n; s++)
            {
                vel += GravityField.AccelAt(pos) * dt;
                pos += vel * dt;
            }
            lr.SetPosition(i, pos);
        }
    }

    // Carry API
    public void AttachPackage(Transform pkg)
    {
        if (!pkg || carriedPackage != null) return;

        var col = pkg.GetComponent<Collider2D>();
        if (col) col.enabled = false;
        var rb2 = pkg.GetComponent<Rigidbody2D>();
        if (rb2) rb2.simulated = false;

        pkg.SetParent(transform);
        pkg.localPosition = new Vector3(0f, 0.6f, 0f);
        carriedPackage = pkg;
    }

    public Transform GetCarriedPackage() => carriedPackage;

    public void ClearCarriedPackage()
    {
        if (!carriedPackage) return;
        carriedPackage.SetParent(null);
        Destroy(carriedPackage.gameObject);
        carriedPackage = null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // --- Pickup if not carrying ---
        if (carriedPackage == null)
        {
            var pkg = other.GetComponent<Package>();
            if (pkg)
            {
                pkg.OnPickup(transform); // handles disabling its own physics
                AttachPackage(pkg.transform);
                return;
            }
        }

        // --- If already carrying, approximate an impact for trigger contacts ---
        if (carriedPackage != null)
        {
            var carriedPkg = carriedPackage.GetComponent<Package>();
            if (carriedPkg && rb)
            {
                float approxImpact = rb.linearVelocity.magnitude * 0.5f; // mild value for triggers
                if (approxImpact > 0f) carriedPkg.RegisterImpact(approxImpact);
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (carriedPackage == null) return;
        var carriedPkg = carriedPackage.GetComponent<Package>();
        if (!carriedPkg) return;
        carriedPkg.RegisterImpact(collision.relativeVelocity.magnitude);
    }

    public void StopForNextShot()
    {
        launched = false;
        rb.linearVelocity = Vector2.zero;
    }
}

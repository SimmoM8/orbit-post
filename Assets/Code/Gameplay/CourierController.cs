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
    float fuel;
    bool aiming;
    bool launched;

    // Carrying
    [Header("Carry Package")]
    [SerializeField] Transform carriedPackage; // runtime
    public bool HasPackage => carriedPackage != null;

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
    }

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
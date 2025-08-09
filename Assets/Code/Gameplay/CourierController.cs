using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(LineRenderer))]
public class CourierController : MonoBehaviour
{
    public float launchImpulse = 5f; // reduced from 8 for slower, more controllable flight
    public int maxFuel = 500;               // launches per run
    public float aimMaxLength = 3f;     // clamp drag vector
    public float predictionDt = 0.033f;
    public int predictionSteps = 30;
    bool launched = false;

    Rigidbody2D rb;
    LineRenderer lr;
    Camera cam;
    int fuel;
    bool aiming;
    public int Fuel => fuel;
    public void AddFuel(int amount) { fuel = Mathf.Clamp(fuel + amount, 0, maxFuel); }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        lr = GetComponent<LineRenderer>();
        cam = Camera.main;
        fuel = maxFuel;

        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Time.timeScale = 1f;
    }

    void Update()
    {
        // Handle input (mouse or single finger)
        if (fuel > 0)
        {
            if (Input.GetMouseButtonDown(0))
            {
                aiming = true;
                launched = false;           // freeze motion while aiming
                Time.timeScale = 0.05f;
            }
            if (Input.GetMouseButtonUp(0) && aiming)
            {
                Time.timeScale = 1f;
                Vector2 v = ComputeLaunchVelocity();
                rb.AddForce(v, ForceMode2D.Impulse);
                fuel--;
                aiming = false;
                launched = true; // start moving
                lr.positionCount = 0;
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

        // Dead zone
        if (mag < 0.2f) return Vector2.zero;

        // Clamp and curve scaling
        mag = Mathf.Min(mag, aimMaxLength);
        float scaled = Mathf.Pow(mag / aimMaxLength, 0.85f); // easing curve
        dir = dir.normalized * (scaled * launchImpulse);

        return dir;
    }

    // FixedUpdate uses multiple substeps for more accurate gravity integration and momentum conservation.
    // Applies a speedScale factor to slow down natural acceleration for easier control.
    void FixedUpdate()
    {
        if (launched)
        {
            int substeps = 4;
            float dt = Time.fixedDeltaTime / substeps;
            float speedScale = 0.7f; // slow down overall gameplay speed
            for (int i = 0; i < substeps; i++)
            {
                Vector2 a = GravityField.AccelAt(rb.position);
                rb.linearVelocity += a * dt * speedScale;
            }
        }
    }

    // Preview uses same integrator and substeps as runtime (Unity semi-implicit via vel update + pos advance)
    // DrawPrediction uses the same multi-substep approach as FixedUpdate for preview accuracy.
    void DrawPrediction(Vector2 startPos, Vector2 startVel)
    {
        lr.positionCount = predictionSteps;
        Vector2 pos = startPos;
        Vector2 vel = startVel;

        for (int i = 0; i < predictionSteps; i++)
        {
            int substeps = 4;
            float dt = predictionDt / substeps;
            for (int s = 0; s < substeps; s++)
            {
                vel += GravityField.AccelAt(pos) * dt;
                pos += vel * dt;
            }
            lr.SetPosition(i, pos);
        }
    }

    public void StopForNextShot()
    {
        launched = false;
        rb.linearVelocity = Vector2.zero;
    }
}
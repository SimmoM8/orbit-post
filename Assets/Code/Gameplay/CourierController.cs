using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(LineRenderer))]
public class CourierController : MonoBehaviour
{
    public float launchImpulse = 9f;      // strength per launch
    public int maxFuel = 5;               // launches per run
    public float aimMaxLength = 3.5f;     // clamp drag vector
    public float predictionDt = 0.033f;
    public int predictionSteps = 30;

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
    }

    void Update()
    {
        // Handle input (mouse or single finger)
        if (fuel > 0)
        {
            if (Input.GetMouseButtonDown(0)) aiming = true;
            if (Input.GetMouseButtonUp(0) && aiming)
            {
                Vector2 v = ComputeLaunchVelocity();
                rb.AddForce(v, ForceMode2D.Impulse);
                fuel--;
                aiming = false;
                lr.positionCount = 0;
            }
        }

        if (aiming)
        {
            Vector2 startVel = rb.linearVelocity + ComputeLaunchVelocity(); // show post-launch path
            DrawPrediction(transform.position, startVel);
        }
    }

    Vector2 ComputeLaunchVelocity()
    {
        Vector3 wp = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = (Vector2)(transform.position - wp); // pull-back slingshot
        if (dir.magnitude > aimMaxLength) dir = dir.normalized * aimMaxLength;
        return dir * launchImpulse;
    }

    void FixedUpdate()
    {
        // Apply custom gravity each physics tick
        rb.linearVelocity += GravityField.AccelAt(transform.position) * Time.fixedDeltaTime;
    }

    void DrawPrediction(Vector2 startPos, Vector2 startVel)
    {
        lr.positionCount = predictionSteps;
        Vector2 pos = startPos;
        Vector2 vel = startVel;

        for (int i = 0; i < predictionSteps; i++)
        {
            vel += GravityField.AccelAt(pos) * predictionDt;
            pos += vel * predictionDt;
            lr.SetPosition(i, pos);
        }
    }
}
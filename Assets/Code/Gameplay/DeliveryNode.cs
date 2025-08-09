using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class DeliveryNode : MonoBehaviour
{
    public int basePoints = 100;
    public float radius = 0.9f;

    float cooldown = 0.35f; // prevent multi-triggers
    float lastTime = -999f;

    void OnValidate()
    {
        var col = GetComponent<CircleCollider2D>();
        if (col) { col.isTrigger = true; col.radius = radius; }
        transform.localScale = Vector3.one * (radius * 2f);
    }

    void OnTriggerEnter2D(Collider2D other) => TryDeliver(other);
    void OnTriggerStay2D(Collider2D other) => TryDeliver(other);  // catches very fast passes

    void TryDeliver(Collider2D other)
    {
        var courier = other.GetComponent<CourierController>();
        if (!courier) courier = other.GetComponentInParent<CourierController>();
        if (!courier) return;

        if (Time.time - lastTime < cooldown) return; // one score per pass
        lastTime = Time.time;

        var rb = courier.GetComponent<Rigidbody2D>();
        float speed = rb ? rb.linearVelocity.magnitude : 0f;
        float dist = Vector2.Distance(transform.position, courier.transform.position);
        float normalized = Mathf.Clamp01(dist / radius);

        Debug.Log($"Delivered! speed={speed:F1} distNorm={normalized:F2}");
        RunManager.Instance?.OnDeliver(speed, normalized, basePoints);
    }
}
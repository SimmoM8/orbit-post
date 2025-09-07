using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public Vector2 offset = new Vector2(0f, 1.2f);
    public float smooth = 6f;
    public float velocityLeadFactor = 0.5f;

    private Rigidbody2D targetRb;
    private CourierController courier;

    void Start()
    {
        if (target)
        {
            targetRb = target.GetComponent<Rigidbody2D>();
            courier = target.GetComponent<CourierController>();
            if (courier != null) courier.OnWrapped += HandleWrapped;
        }
    }

    void LateUpdate()
    {
        if (!target) return;
        // Include velocity lead and offset; no wrap search needed when we shift on wrap events.
        Vector3 baseDesired = new Vector3(target.position.x, target.position.y, transform.position.z);
        if (targetRb != null)
        {
            Vector2 lead = targetRb.linearVelocity * velocityLeadFactor;
            baseDesired += new Vector3(lead.x, lead.y, 0f);
        }
        baseDesired.x += offset.x;
        baseDesired.y += offset.y;

        transform.position = Vector3.Lerp(transform.position, baseDesired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }

    void OnDisable()
    {
        if (courier != null) courier.OnWrapped -= HandleWrapped;
    }

    void OnDestroy()
    {
        if (courier != null) courier.OnWrapped -= HandleWrapped;
    }

    // When the courier wraps, shift the camera by the same delta so the view remains continuous.
    void HandleWrapped(Vector2 delta)
    {
        transform.position += new Vector3(delta.x, delta.y, 0f);
    }
}

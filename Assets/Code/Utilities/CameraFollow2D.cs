using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public Vector2 offset = new Vector2(0f, 1.2f);
    public float smooth = 6f;
    public float velocityLeadFactor = 0.5f;

    private Rigidbody2D targetRb;

    void Start()
    {
        if (target)
        {
            targetRb = target.GetComponent<Rigidbody2D>();
        }
    }

    void LateUpdate()
    {
        if (!target) return;
        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        if (targetRb != null)
        {
            desired += (Vector3)(targetRb.linearVelocity * velocityLeadFactor);
        }
        desired.x += offset.x;
        desired.y += offset.y;
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }
}
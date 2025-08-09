using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class Planet : MonoBehaviour
{
    public float mass = 20f;
    public float radius = 1.2f;              // visual + collider
    public float minRadiusClamp = 0.6f;      // prevents singularity

    public Vector3 WorldPos => transform.position;
    public float MinRadiusSqr => minRadiusClamp * minRadiusClamp;

    void OnEnable() { GravityField.Planets.Add(this); }
    void OnDisable() { GravityField.Planets.Remove(this); }

    void OnValidate()
    {
        var col = GetComponent<CircleCollider2D>();
        if (col && col.isTrigger == false) { /* keep manual radius */ }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
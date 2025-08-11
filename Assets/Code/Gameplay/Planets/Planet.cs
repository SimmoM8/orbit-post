using UnityEngine;
using UnityEngine.Serialization;
using OrbitPost.Gameplay.Planets;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public class Planet : MonoBehaviour, IGravityBody
{
    public float mass = 20f;
    public float radius = 1.2f;              // visual + collider
    public float minRadiusClamp = 0.6f;      // prevents singularity

    [Tooltip("If true, the CircleCollider2D radius is driven by 'radius' to avoid scene overrides.")]
    public bool syncColliderToRadius = true;

    public PlanetProfile profile;
    private TypeSpriteSet _typeSprites;
    public bool overrideRadius = false;
    public bool overrideMass = false;

    [Header("Visual")]
    [Tooltip("If true, forces the root transform to uniform X/Y scale so the circle collider is a true circle in world space.")]
    public bool enforceUniformScale = true;
    [Tooltip("Optional explicit reference to the planet's SpriteRenderer (if left null, will auto-find in children). ")]
    public SpriteRenderer spriteRef;

    [Header("Sprite Visual (Runtime)")]

    [Tooltip("If true, apply a temporary debug color so planet type/profile is visible without final art.")]
    public bool useDebugColor = false;

    [Tooltip("If true and profile exists, use profile.tint for debug color; otherwise use the type colors below.")]
    public bool debugColorFromProfile = true;

    [Tooltip("Debug colors used if 'debugColorFromProfile' is false.")]
    public Color typeColorDefault = Color.white;
    public Color typeColorFire = new Color(1f, 0.45f, 0.15f);
    public Color typeColorWater = new Color(0.2f, 0.6f, 1f);
    public Color typeColorEarth = new Color(0.4f, 0.8f, 0.4f);

    public Vector2 WorldPos => (Vector2)transform.position;
    // Clamp to a small minimum so GravityField math stays stable even for tiny planets
    public float MinRadiusSqr => Mathf.Max(0.05f, minRadiusClamp) * Mathf.Max(0.05f, minRadiusClamp);

    public float Mass => mass;

    // Assuming a planet type enum and a method to get color by type, if not present keep white
    public global::PlanetType planetType = global::PlanetType.Default;

    // Assuming a dominant material string for demonstration
    public string dominantMaterial = "";

    public void ApplyProfileAndSyncCollider()
    {
        // Mass & radius are authored in prefabs; do not override here.
        // Optionally sync the collider to the authored radius if requested.
        var col = GetComponent<CircleCollider2D>();
        if (col && syncColliderToRadius)
        {
            col.isTrigger = false;
            col.radius = Mathf.Max(0.05f, radius);
        }

        // Apply visuals for current planet type (size-specific sprite lives in the prefab's TypeSpriteSet)
        ApplyTypeVisual();
        if (useDebugColor) ApplyDebugColorIfAny();
    }

    SpriteRenderer GetSprite()
    {
        if (spriteRef) return spriteRef;
        return GetComponentInChildren<SpriteRenderer>();
    }

    public void ApplyTypeVisual()
    {
        if (!_typeSprites) _typeSprites = GetComponent<TypeSpriteSet>();
        if (_typeSprites) _typeSprites.Apply(planetType);
    }

    void ApplyDebugColorIfAny()
    {
        if (!useDebugColor) return;
        var sr = GetSprite();
        if (!sr) return;

        Color colorToUse = typeColorDefault;

        switch (planetType)
        {
            case global::PlanetType.Fire: colorToUse = typeColorFire; break;
            case global::PlanetType.Water: colorToUse = typeColorWater; break;
            case global::PlanetType.Earth: colorToUse = typeColorEarth; break;
        }

        sr.color = colorToUse;
    }

    void LateUpdate()
    {
        if (useDebugColor) ApplyDebugColorIfAny();
    }

    void Awake()
    {
        _typeSprites = GetComponent<TypeSpriteSet>();
        // Physics/collider are authored in the prefab; only apply visuals here.
        ApplyTypeVisual();
        if (syncColliderToRadius)
        {
            var col = GetComponent<CircleCollider2D>();
            if (col)
            {
                col.isTrigger = false;
                col.radius = Mathf.Max(0.05f, radius);
            }
        }
    }

    void OnEnable()
    {
        GravityField.Register(this);
    }

    void OnDisable()
    {
        GravityField.Unregister(this);
    }

    void OnValidate()
    {
        // Keep visuals in sync in editor, and optionally keep collider matching authored radius.
        ApplyTypeVisual();
        if (syncColliderToRadius)
        {
            var col = GetComponent<CircleCollider2D>();
            if (col)
            {
                col.isTrigger = false;
                col.radius = Mathf.Max(0.05f, radius);
            }
        }
    }

    void Reset()
    {
        var col = GetComponent<CircleCollider2D>();
        if (col)
        {
            col.isTrigger = false;
            col.radius = Mathf.Max(0.05f, radius);
            radius = col.radius; // mirror authored collider into public radius for gameplay reads
        }
    }
    void OnDrawGizmosSelected()
    {
        // Visualize the intended world radius for quick verification
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.05f, radius));
    }
}
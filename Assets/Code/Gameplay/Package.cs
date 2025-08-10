using TMPro;
using UnityEngine;
using System;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class Package : MonoBehaviour
{
    public PackageType packageType;
    public int baseScore; // added field for scoring
    public Action<Package> OnPickedUp; // spawner subscribes
    public Action<Package, int> OnDelivered; // spawner subscribes

    [Header("Core Properties (always-on)")]
    [Tooltip(">=1 makes the package heavier to move/launch")] public float weight = 1f;
    [Tooltip("Seconds before value decays to 0")] public float perishTime = 20f;
    [Tooltip("Impact magnitude that counts as damage for fragile packages")] public float fragileImpactThreshold = 3f;
    [Tooltip("Score penalty per damaging impact")] public int penaltyPerHit = 5;

    // ---- UI Indicators (runtime-created) ----
    [Header("Perishable Ring")]
    [SerializeField] int ringSegments = 48;
    [SerializeField] float ringRadius = 0.6f;
    [SerializeField] float ringWidth = 0.03f;
    [SerializeField] Color ringFullColor = new Color(0.3f, 1f, 0.5f, 1f);
    [SerializeField] Color ringEmptyColor = new Color(1f, 0.25f, 0.25f, 1f);

    LineRenderer ringBG;
    LineRenderer ringFG;

    [Header("Damage Indicator")]
    [SerializeField] float damageShowDuration = 0.7f;
    [SerializeField] Vector3 damageOffset = new Vector3(0f, 0.9f, 0f);
    TextMeshPro dmgTMP;
    Coroutine dmgRoutine;

    [Header("Type Badge")]
    [SerializeField] Vector3 badgeOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] float badgeFontSize = 3f;
    TextMeshPro badgeTMP;

    // Runtime state
    bool pickedUp = false;
    float pickupTime = 0f;
    float maxImpact = 0f;

    int fragileHitCount = 0;

    public bool IsPickedUp => pickedUp;
    public float TimeSincePickup => pickedUp ? (Time.time - pickupTime) : 0f;
    public float MaxRecordedImpact => maxImpact;

    SpriteRenderer sr;
    Collider2D col;
    Rigidbody2D rb;

    // Reference to courier's movement component for speed adjustment
    ICourierMovement courierMovement;
    float originalCourierSpeed;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>(); // optional; may be null
    }

    void OnValidate()
    {
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true; // pickups should be triggers
    }

    public void ApplyType(PackageType type)
    {
        packageType = type;
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (type)
        {
            sr.color = type.materialColor;
            baseScore = Mathf.Max(baseScore, 0) + type.materialValue; // material adds value
        }
        else
        {
            sr.color = Color.white;
        }

        // Weight affects feel via courier while carried; local rigidbody mass is irrelevant when simulated=false
    }

    public void OnPickup(Transform courier)
    {
        if (col) col.enabled = false;
        if (rb) rb.simulated = false;

        transform.SetParent(courier);
        transform.localPosition = new Vector3(0f, 0.6f, 0f);

        pickedUp = true;
        pickupTime = Time.time;
        maxImpact = 0f;
        fragileHitCount = 0;

        if (perishTime > 0f)
        {
            CreatePerishRing();
            UpdatePerishRing(1f);
        }

        CreateTypeBadge();

        // Slow courier movement if heavy package
        if (weight > 1f)
        {
            courierMovement = courier.GetComponent<ICourierMovement>();
            if (courierMovement != null)
            {
                originalCourierSpeed = courierMovement.MovementSpeed;
                courierMovement.MovementSpeed /= Mathf.Max(1f, weight); // heavier => slower
            }
        }

        OnPickedUp?.Invoke(this);
    }

    public void RegisterImpact(float magnitude)
    {
        if (!pickedUp) return;

        if (magnitude > maxImpact) maxImpact = magnitude;

#if UNITY_EDITOR
        // Debug to verify impacts are registering while testing
        // Uncomment to see values in the Console:
        // Debug.Log($"[Package] Impact registered: {magnitude:F2}");
#endif

        // Increment fragile hit count if conditions met
        if (magnitude > fragileImpactThreshold)
        {
            fragileHitCount++;
        }

        // Always show a quick damage readout for debugging/feedback
        if (magnitude > 0.1f)
        {
            if (dmgRoutine != null) StopCoroutine(dmgRoutine);
            dmgRoutine = StartCoroutine(DamageFlash($"DMG {magnitude:F1}"));
        }
    }

    public void NotifyDelivered()
    {
        DestroyPerishRing();
        if (dmgTMP) Destroy(dmgTMP.gameObject);
        if (badgeTMP) Destroy(badgeTMP.gameObject);

        // Restore courier speed if it was slowed
        if (courierMovement != null)
        {
            courierMovement.MovementSpeed = originalCourierSpeed;
            courierMovement = null;
        }

        int score = CalculateScore();
        OnDelivered?.Invoke(this, score);
    }

    /// <summary>
    /// Calculates the final score for this package delivery based on type, perishability, fragility, and weight.
    /// </summary>
    public int CalculateScore()
    {
        int finalScore = baseScore;

        // Perishable: linear decay with remaining time fraction
        if (perishTime > 0f)
        {
            float tLeft = Mathf.Clamp(perishTime - TimeSincePickup, 0f, perishTime);
            float frac = Mathf.Clamp01(tLeft / perishTime);
            finalScore = Mathf.RoundToInt(finalScore * frac);
        }

        // Fragile penalties (per damaging hit)
        if (fragileHitCount > 0)
        {
            finalScore -= fragileHitCount * penaltyPerHit;
        }

        // Clamp
        return Mathf.Max(finalScore, 0);
    }

    void Update()
    {
        if (pickedUp && perishTime > 0f)
        {
            float tLeft = Mathf.Clamp(perishTime - TimeSincePickup, 0f, perishTime);
            float frac = (perishTime <= 0f) ? 0f : (tLeft / perishTime);
            UpdatePerishRing(frac);
        }
    }

    void CreatePerishRing()
    {
        // Background full circle (faint)
        ringBG = new GameObject("PerishRingBG").AddComponent<LineRenderer>();
        ringBG.transform.SetParent(transform, false);
        ConfigureRing(ringBG, ringEmptyColor * new Color(1f, 1f, 1f, 0.35f));
        SetRingCircle(ringBG, 1f);

        // Foreground fill circle (we'll trim segments to show remaining time)
        ringFG = new GameObject("PerishRingFG").AddComponent<LineRenderer>();
        ringFG.transform.SetParent(transform, false);
        ConfigureRing(ringFG, ringFullColor);
        SetRingCircle(ringFG, 1f);
    }

    void ConfigureRing(LineRenderer lrn, Color col)
    {
        lrn.useWorldSpace = false;
        lrn.loop = false;
        lrn.positionCount = ringSegments + 1; // closed circle polyline
        lrn.startWidth = ringWidth;
        lrn.endWidth = ringWidth;
        lrn.material = new Material(Shader.Find("Sprites/Default"));
        lrn.startColor = col;
        lrn.endColor = col;
        lrn.sortingOrder = 100; // above sprite
    }

    void SetRingCircle(LineRenderer lrn, float alpha)
    {
        // Build a full circle polyline; we'll later shorten FG to show fraction
        for (int i = 0; i <= ringSegments; i++)
        {
            float t = (float)i / ringSegments * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f) * ringRadius;
            lrn.SetPosition(i, p);
        }
        var c = lrn.startColor; c.a = alpha * c.a; lrn.startColor = c; lrn.endColor = c;
    }

    void UpdatePerishRing(float frac)
    {
        if (!ringFG) return;
        frac = Mathf.Clamp01(frac);

        // Flash ring when fraction below 0.25
        if (frac < 0.25f)
        {
            float flash = Mathf.PingPong(Time.time * 6f, 1f); // fast blink
            Color blinkColor = Color.Lerp(ringEmptyColor, Color.white, flash);
            ringFG.startColor = blinkColor;
            ringFG.endColor = blinkColor;
        }
        else
        {
            // Update color blend (green->red as time runs out)
            Color col = Color.Lerp(ringEmptyColor, ringFullColor, frac);
            ringFG.startColor = col; ringFG.endColor = col;
        }

        // Show only a fraction of the circle by adjusting positionCount
        int count = Mathf.Max(2, Mathf.RoundToInt(frac * ringSegments) + 1);
        ringFG.positionCount = count;
        // Ensure the first N points match the BG circle
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / ringSegments * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f) * ringRadius;
            ringFG.SetPosition(i, p);
        }
    }

    void DestroyPerishRing()
    {
        if (ringBG) Destroy(ringBG.gameObject);
        if (ringFG) Destroy(ringFG.gameObject);
        ringBG = null; ringFG = null;
    }

    System.Collections.IEnumerator DamageFlash(string text)
    {
        if (!dmgTMP)
        {
            var go = new GameObject("DamageTMP");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = damageOffset;
            dmgTMP = go.AddComponent<TextMeshPro>();
            dmgTMP.fontSize = 4.5f;
            dmgTMP.alignment = TextAlignmentOptions.Center;
            dmgTMP.color = new Color(1f, 0.3f, 0.3f, 1f);
            dmgTMP.sortingOrder = 110;
        }

        dmgTMP.text = text;
        Color c0 = dmgTMP.color;
        Vector3 start = damageOffset;
        Vector3 end = damageOffset + new Vector3(0f, 0.3f, 0f);
        float t = 0f;
        while (t < damageShowDuration)
        {
            float u = t / damageShowDuration;
            dmgTMP.transform.localPosition = Vector3.Lerp(start, end, u);
            dmgTMP.color = new Color(c0.r, c0.g, c0.b, 1f - u);
            t += Time.deltaTime;
            yield return null;
        }
        dmgTMP.color = new Color(c0.r, c0.g, c0.b, 0f);
        dmgTMP.transform.localPosition = damageOffset;
    }

    void CreateTypeBadge()
    {
        string label = "?";
        Color col = new Color(0.9f, 0.9f, 0.9f, 1f);
        if (packageType)
        {
            label = string.IsNullOrEmpty(packageType.materialName) ? "?" : packageType.materialName.Substring(0, 1).ToUpper();
            col = packageType.materialColor;
        }

        var go = new GameObject("MaterialBadgeTMP");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = badgeOffset;
        badgeTMP = go.AddComponent<TextMeshPro>();
        badgeTMP.text = label;
        badgeTMP.fontSize = badgeFontSize;
        badgeTMP.alignment = TextAlignmentOptions.Center;
        badgeTMP.color = col;
        badgeTMP.sortingOrder = 105;
    }
}

public interface ICourierMovement
{
    float MovementSpeed { get; set; }
}
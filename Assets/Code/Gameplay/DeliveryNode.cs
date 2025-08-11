using UnityEngine;
using System;
using TMPro;

[RequireComponent(typeof(CircleCollider2D))]
public class DeliveryNode : MonoBehaviour
{
    public int basePoints = 100;
    public float radius = 0.9f;

    [Header("Popup UI")] public float popupDuration = 0.9f; public float popupRise = 0.8f; public Color popupColor = new Color(1f, 0.95f, 0.5f, 1f);

    [Header("Scoring Weights")] public float perishMaxBonusFrac = 0.5f; // up to +50% of type.baseScore if instant
    public float fragilePenaltyFrac = 0.5f; // halve if exceeded threshold
    public float heavyBonusFracPerExtra = 0.5f; // +50% per extra weight unit over 1 (e.g., 2.0 -> +50%)

    [Header("Delivery Effects")] public float refuelFraction = 0.25f; // 25% tank on delivery

    [Header("Post Progression")]
    public int level = 1;
    public int progress = 0;
    public int requestGoal = 3;                  // deliveries needed to level up
    // Backing field for authored request amount
    private int _requestAmount = 3;
    public int requestAmount
    {
        get => _requestAmount;
        set
        {
            _requestAmount = Mathf.Max(1, value);
            requestGoal = _requestAmount;
        }
    }
    public float influenceRadius = 7f;           // planets within this get a boost
    public float productionBoostPerLevel = 0.25f; // +25% per level to nearby planets
    public float nodeDemandBiasPerLevel = 0.10f;  // +10% per level when choosing next request

    [Header("Post UI")]
    public Vector3 levelLabelOffset = new Vector3(0f, 1.55f, 0f);
    TextMeshPro levelTMP;
    public Vector3 nameLabelOffset = new Vector3(0f, 2.0f, 0f);
    TextMeshPro nameTMP;

    [Header("Material Requests")]
    public PackageType requestedMaterial;
    public PackageType[] availableMaterials; // assign in Inspector

    [Header("Request UI")]
    public Vector3 requestLabelOffset = new Vector3(0f, 1.1f, 0f);
    TextMeshPro requestTMP;

    public string displayName;

    public Action<DeliveryNode> onDelivered; // spawner subscribes

    void Awake()
    {
        if (!requestTMP)
        {
            var go = new GameObject("RequestTMP");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = requestLabelOffset;
            requestTMP = go.AddComponent<TextMeshPro>();
            requestTMP.fontSize = 3.2f;
            requestTMP.alignment = TextAlignmentOptions.Center;
            requestTMP.sortingOrder = 900;
        }
        if (requestedMaterial)
            SetRequest(requestedMaterial);

        if (!levelTMP)
        {
            var goL = new GameObject("LevelTMP");
            goL.transform.SetParent(transform, false);
            goL.transform.localPosition = levelLabelOffset;
            levelTMP = goL.AddComponent<TextMeshPro>();
            levelTMP.fontSize = 2.8f;
            levelTMP.alignment = TextAlignmentOptions.Center;
            levelTMP.sortingOrder = 901;
        }
        if (!nameTMP)
        {
            var goN = new GameObject("NameTMP");
            goN.transform.SetParent(transform, false);
            goN.transform.localPosition = nameLabelOffset;
            nameTMP = goN.AddComponent<TextMeshPro>();
            nameTMP.fontSize = 2.5f;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.sortingOrder = 902;
            nameTMP.text = displayName;
        }
        RefreshLevelUI();
        RefreshNameUI();
    }

    void OnValidate()
    {
        var col = GetComponent<CircleCollider2D>();
        if (col) { col.isTrigger = true; col.radius = radius; }
        transform.localScale = Vector3.one * (radius * 2f);
        if (_requestAmount != requestGoal)
            _requestAmount = requestGoal;
        RefreshNameUI();
    }

    public void SetRequest(PackageType mat)
    {
        requestedMaterial = mat;
        // Tint node sprite if present
        var sr = GetComponent<SpriteRenderer>();
        if (sr && mat) sr.color = mat.materialColor;
        if (requestTMP)
        {
            string label = mat ? (string.IsNullOrEmpty(mat.materialName) ? "?" : mat.materialName.ToUpper()) : "?";
            requestTMP.text = label;
            if (mat) requestTMP.color = mat.materialColor;
        }
    }

    void RefreshLevelUI()
    {
        if (levelTMP) levelTMP.text = $"L{level}";
    }

    void RefreshNameUI()
    {
        if (nameTMP) nameTMP.text = displayName;
    }

    void OnTriggerEnter2D(Collider2D other) => TryDeliver(other);
    void OnTriggerStay2D(Collider2D other) => TryDeliver(other);

    void TryDeliver(Collider2D other)
    {
        var courier = other.GetComponent<CourierController>();
        if (!courier) courier = other.GetComponentInParent<CourierController>();
        if (!courier || !courier.HasPackage) return;

        // Pull the carried package and its material
        var pkgTf = courier.GetCarriedPackage();
        var pkg = pkgTf ? pkgTf.GetComponent<Package>() : null;
        var mat = pkg ? pkg.packageType : null;

        // Block wrong material
        if (requestedMaterial && mat && mat != requestedMaterial)
        {
            StartCoroutine(ScorePopup("WRONG MATERIAL", (Vector3)transform.position + Vector3.up * 0.2f));
            return;
        }

        int points = basePoints + (pkg ? pkg.CalculateScore() : 0);
        if (points < 0) points = 0;

        // Ensure GameManager exists so score/demand can update
        if (GameManager.Instance == null)
        {
            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<GameManager>();
        }

        // Apply DEMAND multiplier before chain multiplier
        float demandMult = GameManager.Instance.GetDemandMultiplier(requestedMaterial);
        int demandApplied = Mathf.RoundToInt(points * Mathf.Max(0f, demandMult));

        // Apply chain multiplier and award
        float chainMultShown = GameManager.Instance.GetCurrentMultiplier();
        int totalAwarded = GameManager.Instance.RegisterDelivery(demandApplied);
        if (courier) courier.AddFuel(courier.maxFuel * refuelFraction);

        // Update demand (supply satisfied)
        GameManager.Instance.OnMaterialDelivered(requestedMaterial);

        // Build breakdown string
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"+{totalAwarded} (x{demandMult:0.00} × x{chainMultShown:0.00})");
        var parts = new System.Collections.Generic.List<string>();
        parts.Add($"{basePoints} base");
        if (pkg) parts.Add($"{pkg.baseScore} pkg");
        if (mat) parts.Add($"{mat.materialValue} {mat.materialName}");

        if (parts.Count > 0)
        {
            sb.Append(" [");
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(parts[i]);
            }
            sb.Append("]");
        }

        StartCoroutine(ScorePopup(sb.ToString(), (Vector3)transform.position + Vector3.up * 0.2f));

        // Post progression: advance progress and level up if goal reached
        progress++;
        if (progress >= requestGoal)
        {
            progress = 0;
            level++;
            requestGoal = Mathf.CeilToInt(requestGoal * 1.5f); // ramp the requirement
            RefreshLevelUI();
            StartCoroutine(ScorePopup($"POST LEVEL UP! L{level}", (Vector3)transform.position + Vector3.up * 0.6f));
        }

        // Rotate the node's request to a new demand-weighted material
        if (availableMaterials != null && availableMaterials.Length > 0)
        {
            var next = PickMaterialWeighted();
            SetRequest(next);
        }

        // Notify package delivered before clearing
        if (pkg) pkg.NotifyDelivered();
        // Remove & destroy the carried package only
        courier.ClearCarriedPackage();
        // Node persists (static); no respawn
    }

    System.Collections.IEnumerator ScorePopup(string text, Vector3 worldPos)
    {
        // Create a simple world-space TMP label
        var go = new GameObject("ScorePopup");
        go.transform.position = worldPos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = popupColor;
        tmp.sortingOrder = 1000;

        float t = 0f;
        Vector3 start = worldPos;
        Vector3 end = worldPos + Vector3.up * popupRise;
        Color startCol = tmp.color;
        Color endCol = new Color(popupColor.r, popupColor.g, popupColor.b, 0f);

        while (t < popupDuration)
        {
            float u = t / popupDuration;
            go.transform.position = Vector3.Lerp(start, end, u);
            tmp.color = Color.Lerp(startCol, endCol, u);
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(go);
    }
    // Picks a material weighted by demand × material demandWeight.
    private PackageType PickMaterialWeighted()
    {
        if (availableMaterials == null || availableMaterials.Length == 0) return null;
        if (GameManager.Instance == null) return availableMaterials[UnityEngine.Random.Range(0, availableMaterials.Length)];

        float total = 0f;
        for (int i = 0; i < availableMaterials.Length; i++)
        {
            var m = availableMaterials[i];
            if (!m) continue;
            total += GameManager.Instance.GetSpawnWeight(m) * (1f + level * nodeDemandBiasPerLevel);
        }
        if (total <= 0f) return availableMaterials[UnityEngine.Random.Range(0, availableMaterials.Length)];

        float r = UnityEngine.Random.value * total;
        float acc = 0f;
        for (int i = 0; i < availableMaterials.Length; i++)
        {
            var m = availableMaterials[i];
            if (!m) continue;
            acc += GameManager.Instance.GetSpawnWeight(m) * (1f + level * nodeDemandBiasPerLevel);
            if (r <= acc) return m;
        }
        return availableMaterials[availableMaterials.Length - 1];
    }
}
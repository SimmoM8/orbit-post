using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Score { get; private set; }
    public int Chain { get; private set; }

    [Header("Chain Settings")]
    public float chainWindow = 12f;           // seconds to deliver next package
    public float chainStepMultiplier = 0.15f; // +15% per chain step
    public float maxMultiplier = 3f;          // cap at x3.00
    public float ChainTimeLeft { get; private set; }

    // Optional: last awarded amount for telemetry/debug
    public int LastAwarded { get; private set; }

    [Header("Demand / Supply")]
    [Range(0.25f, 3f)] public float demandMin = 0.6f;
    [Range(0.25f, 3f)] public float demandMax = 2.0f;
    [Tooltip("Per-second drift of demand multipliers back toward 1.0")] public float demandRecoverRate = 0.1f;
    [Tooltip("Hit applied to demand multiplier when a material is delivered (reduces demand)")] public float deliverDemandHit = 0.2f;

    [System.Serializable]
    public class DemandEntry
    {
        public PackageType material;
        public float multiplier = 1f;
    }

    public List<DemandEntry> demandTable = new List<DemandEntry>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // Count down chain timer; reset when it expires
        if (Chain > 0 && ChainTimeLeft > 0f)
        {
            ChainTimeLeft -= Time.deltaTime;
            if (ChainTimeLeft <= 0f)
            {
                Chain = 0;
                ChainTimeLeft = 0f;
            }
        }

        // Demand drift toward 1.0
        if (demandTable != null)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < demandTable.Count; i++)
            {
                var e = demandTable[i];
                if (e == null) continue;
                float m = e.multiplier;
                if (Mathf.Approximately(m, 1f)) continue;
                float dir = (m < 1f) ? 1f : -1f; // move toward 1
                m += dir * demandRecoverRate * dt;
                if (m < demandMin) m = demandMin;
                if (m > demandMax) m = demandMax;
                // clamp around 1 if crossed
                if ((dir > 0f && m > 1f) || (dir < 0f && m < 1f)) m = 1f;
                e.multiplier = m;
            }
        }
    }

    // Simple add (used by non-delivery events if any)
    public void AddScore(int points)
    {
        if (points < 0) points = 0;
        Score += points;
    }

    // Delivery-aware add: applies current multiplier, then advances chain and resets timer
    public int RegisterDelivery(int basePoints)
    {
        if (basePoints < 0) basePoints = 0;
        float mult = GetCurrentMultiplier();
        int awarded = Mathf.RoundToInt(basePoints * mult);

        Score += awarded;
        LastAwarded = awarded;

        // advance chain and reset the window
        Chain++;
        ChainTimeLeft = chainWindow;

        return awarded;
    }

    public float GetCurrentMultiplier()
    {
        float mult = 1f + (Chain * chainStepMultiplier);
        if (mult > maxMultiplier) mult = maxMultiplier;
        return mult;
    }

    // -------- Demand API --------
    public void RegisterMaterials(IList<PackageType> mats)
    {
        if (mats == null) return;
        for (int i = 0; i < mats.Count; i++)
        {
            var mat = mats[i];
            if (!mat) continue;
            if (!HasMaterial(mat))
            {
                demandTable.Add(new DemandEntry { material = mat, multiplier = 1f });
            }
        }
    }

    bool HasMaterial(PackageType mat)
    {
        for (int i = 0; i < demandTable.Count; i++)
        {
            var e = demandTable[i];
            if (e != null && e.material == mat) return true;
        }
        return false;
    }

    public float GetDemandMultiplier(PackageType mat)
    {
        if (!mat) return 1f;
        for (int i = 0; i < demandTable.Count; i++)
        {
            var e = demandTable[i];
            if (e != null && e.material == mat) return Mathf.Clamp(e.multiplier, demandMin, demandMax);
        }
        return 1f;
    }

    public void OnMaterialDelivered(PackageType mat)
    {
        if (!mat) return;
        for (int i = 0; i < demandTable.Count; i++)
        {
            var e = demandTable[i];
            if (e != null && e.material == mat)
            {
                float m = e.multiplier;
                m -= deliverDemandHit;
                if (m < demandMin) m = demandMin;
                e.multiplier = m;
                return;
            }
        }
        // If we reach here, the material was not registered; add it.
        demandTable.Add(new DemandEntry { material = mat, multiplier = Mathf.Max(1f - deliverDemandHit, demandMin) });
    }

    public float GetSpawnWeight(PackageType mat)
    {
        // Weight spawns by current demand and material's own weight
        if (!mat) return 1f;
        float d = GetDemandMultiplier(mat);
        float w = mat.demandWeight <= 0f ? 1f : mat.demandWeight;
        return Mathf.Max(0.0001f, d * w);
    }

    public void ResetScore()
    {
        Score = 0;
    }

    public void ResetChain()
    {
        Chain = 0;
        ChainTimeLeft = 0f;
    }
}
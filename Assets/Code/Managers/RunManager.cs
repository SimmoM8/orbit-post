using UnityEngine;
using UnityEngine.SceneManagement;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("Refs")]
    public CourierController courier;

    [Header("Runtime")]
    public int score = 0;
    public int chain = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void OnDeliver(float speed, float normalizedDistance, int basePoints)
    {
        // Points scale with chain; close passes get bonus
        float closeBonus = Mathf.Lerp(0f, 0.5f, Mathf.Clamp01(1f - normalizedDistance)); // up to +50%
        int gained = Mathf.RoundToInt(basePoints * (1f + chain * 0.25f) * (1f + closeBonus));
        score += gained;
        chain++;

        // "Perfect" delivery rule → refuel +1 (tweak as you like)
        bool speedOk = speed >= 6f && speed <= 12f;
        bool closeOk = normalizedDistance <= 0.35f;
        if (speedOk && closeOk && courier != null) courier.AddFuel(1);

        courier?.AddFuel(courier.maxFuel); // full refuel after each delivery
        chain = 0; // reset chain after scoring (or you can keep chain if you prefer)
    }

    public void ResetChain() => chain = 0;

    public void ResetRun()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
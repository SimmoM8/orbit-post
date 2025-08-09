using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    public CourierController courier;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI chainText;
    public TextMeshProUGUI fuelText;
    public Button resetButton;

    void Start()
    {
        if (resetButton) resetButton.onClick.AddListener(() => RunManager.Instance.ResetRun());
    }

    void Update()
    {
        if (!courier || RunManager.Instance == null) return;

        fuelText.text = $"FUEL {courier.Fuel}";
        chainText.text = $"CHAIN x{RunManager.Instance.chain}";
        scoreText.text = $"{RunManager.Instance.score:n0}";
    }
}
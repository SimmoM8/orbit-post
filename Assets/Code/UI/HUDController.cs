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

    [Header("Fuel Bar (optional)")]
    public Image fuelBarFill;                 // assign the FUEL BAR FILL image (Image Type: Simple or Filled)
    public Color fuelFullColor = new Color(0.3f, 1f, 0.5f, 1f);
    public Color fuelEmptyColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("Fuel Bar Sizing")]
    public float fuelBarMaxWidth = 320f; // full width in pixels when 100%
    public bool useInitialWidth = true;  // if true, capture current width on Start

    void Start()
    {
        if (resetButton) resetButton.onClick.AddListener(() => RunManager.Instance.ResetRun());

        if (useInitialWidth && fuelBarFill)
        {
            // Capture the current width as the max reference
            var rt = fuelBarFill.rectTransform;
            fuelBarMaxWidth = rt.sizeDelta.x;
        }
    }

    void Update()
    {
        if (!courier || RunManager.Instance == null) return;

        // Score & chain always update
        if (chainText) chainText.text = $"CHAIN x{RunManager.Instance.chain}";
        if (scoreText) scoreText.text = $"{RunManager.Instance.score:n0}";

        // Fuel as number (optional)
        if (fuelText) fuelText.text = $"FUEL {courier.Fuel}";

        // Fuel bar (preferred)
        if (fuelBarFill)
        {
            float t = courier.FuelPercent;

            // Resize width: ensure the bar shrinks from the left (set anchors/pivot in the editor)
            var rt = fuelBarFill.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(0f, fuelBarMaxWidth * t), rt.sizeDelta.y);

            // Optional: set color gradient
            fuelBarFill.color = Color.Lerp(fuelEmptyColor, fuelFullColor, t);
        }
    }
}
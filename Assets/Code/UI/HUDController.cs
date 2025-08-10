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

    [Header("Chain Timer Bar")]
    public Image chainTimerBar;

    [Header("Fuel Bar (optional)")]
    public Image fuelBarFill;                 // assign the FUEL BAR FILL image (Image Type: Simple or Filled)
    public Color fuelFullColor = new Color(0.3f, 1f, 0.5f, 1f);
    public Color fuelEmptyColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("Fuel Bar Sizing")]
    public float fuelBarMaxWidth = 320f; // full width in pixels when 100%
    public bool useInitialWidth = true;  // if true, capture current width on Start

    [Header("Fuel Bar Mode")]
    public bool useFillAmount = true; // when true, use Image.fillAmount; when false, resize width

    void Start()
    {
        if (resetButton) resetButton.onClick.AddListener(() => { GameManager.Instance?.ResetScore(); GameManager.Instance?.ResetChain(); RunManager.Instance.ResetRun(); });

        if (!useFillAmount && useInitialWidth && fuelBarFill)
        {
            // Capture the current width as the max reference
            var rt = fuelBarFill.rectTransform;
            fuelBarMaxWidth = rt.sizeDelta.x;
        }

        // Auto-create chain timer bar if not assigned
        if (!chainTimerBar)
        {
            chainTimerBar = CreateChainTimerBar();
        }
    }

    void Update()
    {
        if (!courier || GameManager.Instance == null) return;

        // Score & chain always update
        if (chainText)
        {
            float mult = GameManager.Instance.GetCurrentMultiplier();
            float t = GameManager.Instance.ChainTimeLeft;
            int ch = GameManager.Instance.Chain;
            chainText.text = (ch > 0 && t > 0f)
                ? $"x{mult:0.00} · {t:0}s"
                : "x1.00";
        }

        // Chain timer bar update
        if (chainTimerBar && GameManager.Instance != null)
        {
            float tLeft = GameManager.Instance.ChainTimeLeft;
            float window = GameManager.Instance.chainWindow;
            float frac = (GameManager.Instance.Chain > 0 && window > 0f) ? Mathf.Clamp01(tLeft / window) : 0f;

            // Toggle visibility
            if (chainTimerBar.gameObject.activeSelf != (frac > 0f))
                chainTimerBar.gameObject.SetActive(frac > 0f);

            chainTimerBar.fillAmount = frac;
            // Base color LERP: red -> green as fraction increases
            Color baseCol = Color.Lerp(new Color(1f, 0.2f, 0.2f, 1f), new Color(0.2f, 1f, 0.4f, 1f), frac);

            // Subtle pulse in last 1.5s
            if (tLeft > 0f && tLeft < 2.5f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 10f));
                baseCol = Color.Lerp(baseCol, Color.white, 0.25f * pulse);
            }
            chainTimerBar.color = baseCol;
        }

        if (scoreText) scoreText.text = $"{GameManager.Instance.Score:n0}";

        // Fuel as number (optional)
        if (fuelText) fuelText.text = $"FUEL {courier.Fuel}";

        // Fuel bar (preferred)
        if (fuelBarFill)
        {
            float t = courier.FuelPercent;

            if (useFillAmount)
            {
                // Use Image.FillAmount (set Image Type = Filled, Horizontal, Origin Left in Inspector)
                fuelBarFill.fillAmount = t;
            }
            else
            {
                // Resize width: ensure the bar shrinks from the left (set anchors/pivot in the editor)
                var rt = fuelBarFill.rectTransform;
                rt.sizeDelta = new Vector2(Mathf.Max(0f, fuelBarMaxWidth * t), rt.sizeDelta.y);
            }

            // Optional: set color gradient
            fuelBarFill.color = Color.Lerp(fuelEmptyColor, fuelFullColor, t);
        }
    }
    Image CreateChainTimerBar()
    {
        var go = new GameObject("ChainTimerBar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        // Top stretch
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 8f);       // 8 px tall
        rt.anchoredPosition = new Vector2(0f, -8f); // 8 px below top edge

        var img = go.GetComponent<Image>();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 0f;
        img.color = new Color(0.2f, 1f, 0.4f, 1f); // starting color (will be lerped in Update)
        go.SetActive(false);
        return img;
    }
}
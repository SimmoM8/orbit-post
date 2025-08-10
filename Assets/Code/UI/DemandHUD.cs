using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
public class DemandHUD : MonoBehaviour
{
    [Header("Build & Layout")]
    [Tooltip("If no container assigned, a panel will be created here under this object.")]
    public RectTransform container;
    public Vector2 anchoredPos = new Vector2(20f, -80f);
    public Vector2 size = new Vector2(240f, 120f);
    public float rowHeight = 22f;
    public float rowGap = 4f;
    public bool buildOnStart = true;

    [Header("Style")]
    public Sprite barSprite;
    public Color barBg = new Color(1f, 1f, 1f, 0.12f);
    public Color textColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);
    public Color frameColor = new Color(0f, 0f, 0f, 0.35f);
    public float framePadding = 8f;

    class Row
    {
        public PackageType mat;
        public Image colorSwatch;
        public TMP_Text label;
        public Image barBg;
        public Image barFill;
        public TMP_Text multText;
    }

    readonly List<Row> rows = new List<Row>();
    RectTransform panel;

    void Start()
    {
        if (!container)
            container = CreatePanel();

        if (buildOnStart)
            Rebuild();
    }

    void Update()
    {
        if (!GameManager.Instance) return;
        EnsureRowsMatchTable();
        float min = GameManager.Instance.demandMin;
        float max = GameManager.Instance.demandMax;
        float range = Mathf.Max(0.0001f, max - min);

        foreach (var r in rows)
        {
            if (!r.mat) continue;
            float mult = GameManager.Instance.GetDemandMultiplier(r.mat);
            float frac = Mathf.Clamp01((mult - min) / range);

            if (r.colorSwatch) r.colorSwatch.color = r.mat.materialColor;
            if (r.label) r.label.text = r.mat.materialName;
            if (r.barFill) r.barFill.fillAmount = frac;
            if (r.multText) r.multText.text = $"x{mult:0.00}";
        }
    }

    public void Rebuild()
    {
        // Clear old
        foreach (Transform child in container) Destroy(child.gameObject);
        rows.Clear();

        if (!GameManager.Instance || GameManager.Instance.demandTable == null) return;

        var list = GameManager.Instance.demandTable;
        float y = -framePadding;

        // Optional frame bg
        var frame = CreateImage("Frame", container, frameColor);
        var frt = frame.rectTransform;
        frt.anchorMin = new Vector2(0, 1);
        frt.anchorMax = new Vector2(1, 1);
        frt.pivot = new Vector2(0.5f, 1);
        float totalHeight = framePadding + list.Count * (rowHeight + rowGap) - rowGap + framePadding;
        frt.anchoredPosition = Vector2.zero;
        frt.sizeDelta = new Vector2(0, totalHeight);
        frame.raycastTarget = false;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || e.material == null) continue;
            var mat = e.material;

            // Row root
            var rowRt = new GameObject($"Row_{i}", typeof(RectTransform)).GetComponent<RectTransform>();
            rowRt.SetParent(container, false);
            rowRt.anchorMin = new Vector2(0, 1);
            rowRt.anchorMax = new Vector2(1, 1);
            rowRt.pivot = new Vector2(0, 1);
            rowRt.anchoredPosition = new Vector2(framePadding, y);
            rowRt.sizeDelta = new Vector2(-framePadding * 2f, rowHeight);
            y -= (rowHeight + rowGap);

            // Color swatch
            var swImg = CreateImage("Swatch", rowRt, mat.materialColor, preserveAspect: true);
            var swRt = swImg.rectTransform;
            swRt.anchorMin = new Vector2(0, 0.5f);
            swRt.anchorMax = new Vector2(0, 0.5f);
            swRt.pivot = new Vector2(0, 0.5f);
            swRt.anchoredPosition = Vector2.zero;
            swRt.sizeDelta = new Vector2(rowHeight, rowHeight);

            // Label
            var nameTxt = CreateTMP("Label", rowRt, textColor, mat.materialName);
            var ntRt = nameTxt.rectTransform;
            ntRt.anchorMin = new Vector2(0, 0.5f);
            ntRt.anchorMax = new Vector2(0, 0.5f);
            ntRt.pivot = new Vector2(0, 0.5f);
            ntRt.anchoredPosition = new Vector2(rowHeight + 6f, 0);
            nameTxt.fontSize = 14;

            // Bar BG
            var barBgImg = CreateImage("BarBG", rowRt, barBg);
            var bbRt = barBgImg.rectTransform;
            bbRt.anchorMin = new Vector2(0, 0.5f);
            bbRt.anchorMax = new Vector2(1, 0.5f);
            bbRt.pivot = new Vector2(0, 0.5f);
            bbRt.anchoredPosition = new Vector2(rowHeight + 6f + 70f, 0);
            bbRt.sizeDelta = new Vector2(-(rowHeight + 6f + 70f) - 48f, 10f);
            barBgImg.sprite = barSprite;
            barBgImg.type = barSprite ? Image.Type.Sliced : Image.Type.Simple;

            // Bar Fill
            var barFillImg = CreateImage("BarFill", barBgImg.rectTransform, new Color(0.3f, 0.9f, 0.5f, 1f));
            var bfRt = barFillImg.rectTransform;
            bfRt.anchorMin = new Vector2(0, 0);
            bfRt.anchorMax = new Vector2(1, 1);
            bfRt.pivot = new Vector2(0, 0.5f);
            bfRt.offsetMin = Vector2.zero;
            bfRt.offsetMax = Vector2.zero;
            barFillImg.type = Image.Type.Filled;
            barFillImg.fillMethod = Image.FillMethod.Horizontal;
            barFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFillImg.fillAmount = 0f;
            barFillImg.sprite = barSprite;
            barFillImg.type = barSprite ? Image.Type.Filled : Image.Type.Filled;

            // Mult text
            var multTxt = CreateTMP("Mult", rowRt, textColor, "x1.00");
            var mtRt = multTxt.rectTransform;
            mtRt.anchorMin = new Vector2(1, 0.5f);
            mtRt.anchorMax = new Vector2(1, 0.5f);
            mtRt.pivot = new Vector2(1, 0.5f);
            mtRt.anchoredPosition = new Vector2(0, 0);
            multTxt.fontSize = 14;
            multTxt.alignment = TextAlignmentOptions.MidlineRight;

            rows.Add(new Row
            {
                mat = mat,
                colorSwatch = swImg,
                label = nameTxt,
                barBg = barBgImg,
                barFill = barFillImg,
                multText = multTxt
            });
        }
    }

    RectTransform CreatePanel()
    {
        var go = new GameObject("DemandHUD_Panel", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
    }

    Image CreateImage(string name, Transform parent, Color color, bool preserveAspect = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.preserveAspect = preserveAspect;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text CreateTMP(string name, Transform parent, Color color, string text)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    void EnsureRowsMatchTable()
    {
        if (!GameManager.Instance || GameManager.Instance.demandTable == null) return;

        // If the table size changed (materials added at runtime), rebuild
        int nonNull = 0;
        foreach (var e in GameManager.Instance.demandTable)
            if (e != null && e.material != null) nonNull++;

        if (nonNull != rows.Count)
            Rebuild();
    }
}
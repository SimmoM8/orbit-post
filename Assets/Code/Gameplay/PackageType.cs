using UnityEngine;

// NOTE: This now represents a MATERIAL, not gameplay toggles. All packages are fragile/perishable/weighted by default.
[CreateAssetMenu(fileName = "NewMaterial", menuName = "OrbitPost/Material")]
public class PackageType : ScriptableObject
{
    [Header("Material Identity")]
    public string materialName = "Ore";
    public Color materialColor = Color.white;

    [Header("Economy")]
    [Tooltip("Base value contributed by this material on delivery")] public int materialValue = 100;
    [Tooltip("Relative demand weight for spawn/scoring adjustments")] public float demandWeight = 1f;
}
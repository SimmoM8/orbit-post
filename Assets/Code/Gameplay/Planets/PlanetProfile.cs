using UnityEngine;

[CreateAssetMenu(menuName = "Orbit Post/Planet Profile", fileName = "PlanetProfile")]
public class PlanetProfile : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Fire";

    [Header("Defaults")]
    [Min(0.01f)] public float defaultRadius = 1.2f;
    [Min(0.01f)] public float defaultMass = 20f;

    [System.Serializable]
    public class MaterialWeight
    {
        public PackageType material;
        [Tooltip("Relative likelihood multiplier for this profile. 1 = neutral, 0.5 = half as likely, 2 = twice as likely.")]
        public float weight = 1f;
    }

    [Header("Production Weights")]
    public MaterialWeight[] typeWeights;
}
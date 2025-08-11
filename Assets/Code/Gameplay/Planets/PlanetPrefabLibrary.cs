using UnityEngine;

[CreateAssetMenu(menuName = "Orbit Post/Planet Prefab Library", fileName = "PlanetPrefabLibrary")]
public class PlanetPrefabLibrary : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public PlanetSize size;   // which SIZE this entry represents
        public GameObject prefab; // prefab that already has correct collider & TypeSpriteSet
    }

    public Entry[] entries;

    public bool TryGet(PlanetSize size, out GameObject prefab)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].size == size && entries[i].prefab != null)
                {
                    prefab = entries[i].prefab;
                    return true;
                }
            }
        }
        prefab = null;
        return false;
    }
}
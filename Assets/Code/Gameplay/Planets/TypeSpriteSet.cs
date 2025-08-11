using UnityEngine;

namespace OrbitPost.Gameplay.Planets
{
    [System.Serializable]
    public struct TypeSprite
    {
        public global::PlanetType type;
        public Sprite sprite;
    }

    [DisallowMultipleComponent]
    public class TypeSpriteSet : MonoBehaviour
    {
        [Tooltip("SpriteRenderer to show the sprite for this size. Leave empty to auto-find in children.")]
        public SpriteRenderer targetRenderer;

        [Tooltip("One sprite per planet type for THIS SIZE.")]
        public TypeSprite[] spritesByType;

        public bool TryGet(global::PlanetType t, out Sprite sprite)
        {
            if (spritesByType != null)
            {
                for (int i = 0; i < spritesByType.Length; i++)
                {
                    if (spritesByType[i].type == t && spritesByType[i].sprite != null)
                    {
                        sprite = spritesByType[i].sprite;
                        return true;
                    }
                }
            }
            sprite = null;
            return false;
        }

        public void Apply(global::PlanetType t)
        {
            if (!targetRenderer) targetRenderer = GetComponentInChildren<SpriteRenderer>();
            if (targetRenderer && TryGet(t, out var s))
                targetRenderer.sprite = s;
        }
    }
}
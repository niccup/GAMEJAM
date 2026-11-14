using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Data for the arena: size, look and spawn layout. Swap the background sprite or
    /// tweak the numbers here - no code or scene changes needed.
    /// </summary>
    [CreateAssetMenu(menuName = "GameJam/Map Config", fileName = "MapConfig")]
    public class MapConfig : ScriptableObject
    {
        [Header("Layout")]
        [Tooltip("Map size in world units (width, height).")]
        public Vector2 size = new Vector2(16f, 9f);

        [Tooltip("Radius of the rounded map corners. Keep it equal to the player radius (collider radius x scale): " +
                 "players spawn at the center of the corner arc, so they fit it exactly.")]
        public float cornerRadius = 0.25f;

        [Tooltip("Thickness of the invisible wall that keeps players inside the map.")]
        public float wallThickness = 1f;

        [Header("Look")]
        [Tooltip("Background sprite. It is stretched to cover the whole map.")]
        public Sprite background;

        public Color backgroundTint = Color.white;

        public int backgroundSortingOrder = -100;
    }
}

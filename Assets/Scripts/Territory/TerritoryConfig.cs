using UnityEngine;

namespace GameJam
{
    /// <summary>Tuning for trails and territory capture. Look (opacity, layering) and rules (grid resolution, loop length) live here.</summary>
    [CreateAssetMenu(menuName = "GameJam/Territory Config", fileName = "TerritoryConfig")]
    public class TerritoryConfig : ScriptableObject
    {
        [Header("Rules")]
        [Tooltip("Size of one grid cell in world units. Smaller = finer trails and borders, but a larger grid.")]
        [Min(0.02f)] public float cellSize = 0.1f;

        [Tooltip("Color a character laid less than this much path (world units) ago doesn't count as 'own color' for closing a loop. " +
                 "Keeps the trail from 'closing' on the bit directly behind the character.")]
        [Min(0.1f)] public float minLoopLength = 1f;

        [Header("Look")]
        [Tooltip("Opacity of trails and territory (they always share the player's color).")]
        [Range(0f, 1f)] public float alpha = 1f;

        [Header("Layering")]
        [Tooltip("Above the map background (-100), below the players (10).")]
        public int sortingOrder = -50;
    }
}

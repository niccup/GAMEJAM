using UnityEngine;

namespace GameJam
{
    /// <summary>Raises the character's movement speed by a fixed share (30% by default).</summary>
    [CreateAssetMenu(menuName = "GameJam/Modifiers/Speed Boost", fileName = "SpeedBoost")]
    public class SpeedBoostModifier : PlayerModifier
    {
        [Tooltip("Extra movement speed as a share of the normal speed: 0.3 = +30%.")]
        [SerializeField, Range(0f, 2f)] float speedBonus = 0.3f;

        public override void Apply(ref PlayerStats stats) => stats.SpeedMultiplier *= 1f + speedBonus;
    }
}

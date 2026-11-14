using System.Collections.Generic;

namespace GameJam
{
    /// <summary>
    /// The numbers modifiers can change. A fresh set starts at <see cref="Default"/>; every <see cref="PlayerModifier"/>
    /// then adjusts it in <see cref="PlayerModifier.Apply"/>. To let a future modifier change something new, add a field
    /// here (with a neutral default) and read it where it matters.
    /// </summary>
    public struct PlayerStats
    {
        /// <summary>Multiplier on the player's base movement speed (1 = unchanged).</summary>
        public float SpeedMultiplier;

        public static PlayerStats Default => new PlayerStats { SpeedMultiplier = 1f };

        /// <summary>The stats that result from applying all <paramref name="modifiers"/> to the defaults.</summary>
        public static PlayerStats From(IEnumerable<PlayerModifier> modifiers)
        {
            PlayerStats stats = Default;
            if (modifiers != null)
                foreach (PlayerModifier modifier in modifiers)
                    if (modifier != null) modifier.Apply(ref stats);
            return stats;
        }
    }
}

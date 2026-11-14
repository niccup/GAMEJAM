using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// A power-up that is a plain buff on the chooser's character in the coming round only; the recording of that round
    /// keeps it, so that round's ghost replays with it in every later round. Implement <see cref="Apply"/> to change the
    /// <see cref="PlayerStats"/>; everything else (picking, assigning, recording) is handled for you.
    /// </summary>
    public abstract class PlayerModifier : PowerUp
    {
        [Tooltip("If ticked, winning this modifier again adds another copy (the effects multiply). Otherwise a second win changes nothing.")]
        [SerializeField] bool stackable;

        public bool Stackable => stackable;

        /// <summary>Adjusts <paramref name="stats"/> for a character carrying this modifier.</summary>
        public abstract void Apply(ref PlayerStats stats);

        /// <summary>Gives the modifier to the chooser's next-round character. No further steps are needed.</summary>
        public override SelectionStep Use(PowerUpContext context)
        {
            context.Chooser.AddModifier(this);
            Debug.Log($"Player {context.Chooser.Index + 1} took {DisplayName}.", this);
            return null;
        }
    }
}

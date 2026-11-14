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

        /// <summary>
        /// Called each time the character carrying this modifier physically touches another player's current character. This
        /// is the hook for collision-based abilities (see <see cref="BouncePowerUp"/>): override it and use
        /// <see cref="PlayerContact"/> to see who was hit and how, and to act on the other character. Never called for
        /// ghosts - they have no collider, so they neither trigger nor receive contact abilities. Does nothing by default.
        /// </summary>
        public virtual void OnContact(PlayerContact contact) { }

        /// <summary>Gives the modifier to the chooser's next-round character. No further steps are needed.</summary>
        public override SelectionStep Use(PowerUpContext context)
        {
            context.Chooser.AddModifier(this);
            Debug.Log($"Player {context.Chooser.Index + 1} took {DisplayName}.", this);
            return null;
        }
    }
}

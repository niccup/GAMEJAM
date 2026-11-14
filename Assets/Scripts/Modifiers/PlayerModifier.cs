using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// A power-up a player can win between rounds. It applies to their character in the coming round only; the recording
    /// of that round keeps it, so that round's ghost replays with it in every later round. Each kind is a
    /// ScriptableObject asset, so adding a new modifier means: subclass this, override <see cref="Apply"/>, create an
    /// asset (Create > GameJam > Modifiers) and drop it into the pool of the <see cref="PowerUpSelection"/>. Nothing
    /// else needs to change - players, ghosts and the selection screen only deal with this base class.
    /// </summary>
    public abstract class PlayerModifier : ScriptableObject
    {
        [SerializeField] string displayName;

        [SerializeField, TextArea(1, 3)] string description;

        [Tooltip("Shown on the power-up selection screen.")]
        [SerializeField] Sprite icon;

        [Tooltip("If ticked, winning this modifier again adds another copy (the effects multiply). Otherwise a second win changes nothing.")]
        [SerializeField] bool stackable;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public bool Stackable => stackable;

        /// <summary>Adjusts <paramref name="stats"/> for a character carrying this modifier.</summary>
        public abstract void Apply(ref PlayerStats stats);
    }
}

using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Something a player can pick between rounds. Each kind is a ScriptableObject asset in the pool of the
    /// <see cref="PowerUpSelection"/>. To add a new power-up: subclass this (or <see cref="PlayerModifier"/> for a plain
    /// buff on the next round's character), implement <see cref="Use"/>, create an asset (Create > GameJam) and add it
    /// to the pool - the selection screen, its input and its step flow do not need to change.
    /// </summary>
    public abstract class PowerUp : ScriptableObject
    {
        [SerializeField] string displayName;

        [SerializeField, TextArea(1, 3)] string description;

        [Header("Artwork")]
        [Tooltip("The picture on this power-up's card. Drag any sprite here to replace it - no code or scene change needed. " +
                 "Left empty, the selection screen shows its placeholder picture instead. Any aspect ratio works: the picture is fitted into the card without stretching.")]
        [SerializeField] Sprite icon;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;

        /// <summary>
        /// True for a power-up that can be taken only once during the whole game, by whichever player: as soon as it has
        /// been taken it is gone from every later draft, even though the pool otherwise starts full again each round.
        /// </summary>
        public virtual bool OncePerGame => false;

        /// <summary>False if the power-up cannot be used right now (it is then shown greyed out and cannot be picked).</summary>
        public virtual bool IsAvailable(PowerUpContext context) => true;

        /// <summary>
        /// Called when the chooser picks this power-up. Do the work right away and return null, or return the first
        /// <see cref="SelectionStep"/> of a longer selection (the flow then runs until a step calls
        /// <see cref="PowerUpContext.Complete"/>, which is what lets the next round start).
        /// </summary>
        public abstract SelectionStep Use(PowerUpContext context);
    }
}

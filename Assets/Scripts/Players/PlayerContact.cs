using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// A physical touch between two <i>current</i> characters, as seen by one of them (<see cref="Self"/>) - the one whose
    /// abilities are asked to react (<see cref="PlayerModifier.OnContact"/>). Ghosts never take part: they have no collider
    /// and no <see cref="Player"/>, so no contact is ever created for them.
    /// </summary>
    public readonly struct PlayerContact
    {
        /// <summary>The character that owns the ability being run.</summary>
        public Player Self { get; }

        /// <summary>The other player's current character.</summary>
        public Player Other { get; }

        /// <summary>Unit vector along the line between the two characters' centres, pointing from <see cref="Self"/> to <see cref="Other"/>.</summary>
        public Vector2 Normal { get; }

        /// <summary>Where the two touch.</summary>
        public Vector2 Point { get; }

        /// <summary>Velocity of <see cref="Self"/> just before the collision (what its own movement set in the last physics step).</summary>
        public Vector2 SelfVelocity { get; }

        /// <summary>Velocity of <see cref="Other"/> just before the collision.</summary>
        public Vector2 OtherVelocity { get; }

        public PlayerContact(Player self, Player other, Vector2 normal, Vector2 point, Vector2 selfVelocity, Vector2 otherVelocity)
        {
            Self = self;
            Other = other;
            Normal = normal;
            Point = point;
            SelfVelocity = selfVelocity;
            OtherVelocity = otherVelocity;
        }

        /// <summary>
        /// For abilities with a cooldown: true if <paramref name="source"/> may act on <see cref="Other"/> now (and starts the
        /// cooldown), false if it last did so less than <paramref name="seconds"/> ago. 0 seconds = no cooldown.
        /// </summary>
        public bool TryStartCooldown(PlayerModifier source, float seconds) => Self.TryStartContactCooldown(source, Other, seconds);
    }
}

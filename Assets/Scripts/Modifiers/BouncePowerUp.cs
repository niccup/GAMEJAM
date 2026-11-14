using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Bounce: for the coming round the chooser's character knocks every other player's <i>current</i> character away when
    /// they touch. The shove is applied to the other character's velocity (see <see cref="Knockback"/>), so it is ordinary
    /// movement from then on: it is part of what is recorded for that player and their ghost replays it in later rounds
    /// without needing Bounce itself. Ghosts never take part - they have no collider, so they neither bounce anyone nor are bounced.
    /// <para>
    /// This is a <see cref="PlayerModifier"/> that uses the <see cref="PlayerModifier.OnContact"/> hook. Another collision-based
    /// ability only has to override the same hook; nothing else needs to change.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "GameJam/Modifiers/Bounce", fileName = "Bounce")]
    public class BouncePowerUp : PlayerModifier
    {
        [Header("Knockback")]
        [Tooltip("How hard the hit character is thrown: the extra speed, in world units per second, it gets ON TOP of its own top " +
                 "speed. Because it is relative to the character's top speed, a bounce stays strong when you retune Max Speed. " +
                 "Higher = further and faster.")]
        [SerializeField, Range(1f, 40f)] float bounceStrength = 12f;

        [Tooltip("Seconds the knockback lasts: it dominates the hit character's movement at first and fades out smoothly " +
                 "into normal control. A character that is not steering is carried roughly (Max Speed + Strength) x Duration / 3 units.")]
        [SerializeField, Range(0.1f, 2f)] float bounceDuration = 0.7f;

        [Tooltip("Seconds before the same character can be bounced by this ability again. 0 = every contact bounces. " +
                 "Raise it if repeated touches launch a character too often.")]
        [SerializeField, Min(0f)] float bounceCooldown;

        [Tooltip("Seconds the shove takes to build up (0 = an instant jolt). A few hundredths of a second keeps the hit smooth.")]
        [SerializeField, Range(0f, 0.2f)] float impactRampIn = 0.04f;

        [Header("Direction")]
        [Tooltip("How much the owner's own movement bends the knockback. 0 = straight away from the owner along the line between " +
                 "the two characters; 1 = in the direction the owner was moving. Ignored when the owner moves away from the hit character.")]
        [SerializeField, Range(0f, 1f)] float ownerMomentumInfluence = 0.3f;

        [Tooltip("Degrees the knockback direction is turned afterwards (positive = counter-clockwise). 0 = no deflection.")]
        [SerializeField, Range(-90f, 90f)] float angleOffset;

        public float BounceStrength => bounceStrength;
        public float BounceDuration => bounceDuration;
        public float BounceCooldown => bounceCooldown;

        // Bounce changes no stats: everything it does happens on contact.
        public override void Apply(ref PlayerStats stats) { }

        public override void OnContact(PlayerContact contact)
        {
            if (!contact.TryStartCooldown(this, bounceCooldown)) return;

            PlayerMovement victim = contact.Other.Movement;
            victim.ApplyKnockback(KnockbackDirection(contact) * (victim.MaxSpeed + bounceStrength), bounceDuration, impactRampIn);

            // The owner is not pushed by their own ability: take back the recoil the collision gave them.
            PlayerMovement owner = contact.Self.Movement;
            if (!owner.IsKnockedBack) owner.RestoreVelocity();
        }

        /// <summary>The unit direction the hit character is thrown in: away from the owner, bent by the owner's movement and the angle offset.</summary>
        public Vector2 KnockbackDirection(PlayerContact contact)
        {
            Vector2 away = contact.Normal;
            Vector2 direction = away;

            Vector2 ownerVelocity = contact.SelfVelocity;
            if (ownerVelocity.sqrMagnitude > 1e-4f)
            {
                Vector2 ownerDirection = ownerVelocity.normalized;
                if (Vector2.Dot(ownerDirection, away) > 0f) // only when the owner really drives into the other one
                    direction = Vector2.Lerp(away, ownerDirection, ownerMomentumInfluence).normalized;
            }

            if (angleOffset != 0f) direction = Quaternion.Euler(0f, 0f, angleOffset) * direction;
            return direction;
        }
    }
}

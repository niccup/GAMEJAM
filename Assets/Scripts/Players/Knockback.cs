using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// A knockback in progress on one character: a strong shove that builds up over a moment, then wears off smoothly
    /// into normal movement. Plain data plus one function, <see cref="Step"/>, like <see cref="MovementPhysics"/> - no
    /// Unity components, one instance per character (kept by its <see cref="PlayerMovement"/>).
    /// <para>
    /// It is done in two phases:
    /// <list type="bullet">
    /// <item><b>Build-up</b> (<c>rampIn</c>, a few hundredths of a second): the velocity is pulled to the knockback velocity
    /// at a steady rate, so the shove is a hard but not instantaneous jolt.</item>
    /// <item><b>Wear-off</b> (the rest of <c>duration</c>): only the speed <i>above what the character's own controls ask
    /// for</i> is the knockback, and it fades out along a smooth curve. The rest of the velocity is the character's own,
    /// which the normal <see cref="MovementPhysics"/> keeps steering, so control comes back gradually by itself - the
    /// stronger the shove, the longer it dominates. A character that is not steering simply comes to rest as the knockback
    /// ends; one that is holding a direction is moving normally by then, with no jump.</item>
    /// </list>
    /// Every step starts from the velocity physics actually left (walls and other bodies have already taken their share),
    /// so a character knocked into a wall slides along it or stops instead of storing the shove up.
    /// </para>
    /// The result is only ever a change of velocity: the positions the character then travels through are what gets
    /// recorded for its ghost, so a knockback becomes part of the recorded movement without the ghost knowing about it.
    /// </summary>
    public struct Knockback
    {
        Vector2 target;
        float duration;
        float rampIn;
        float rampRate;
        float elapsed;
        bool reached;

        /// <summary>True from <see cref="Begin"/> until the knockback has worn off (or <see cref="Cancel"/>).</summary>
        public bool Active { get; private set; }

        /// <summary>
        /// Starts (or restarts) a knockback towards <paramref name="targetVelocity"/>. <paramref name="currentVelocity"/> is
        /// what the character moved with just before; <paramref name="rampIn"/> is how long the build-up takes
        /// (0 = at once) and is limited to half of <paramref name="duration"/>.
        /// </summary>
        public void Begin(Vector2 targetVelocity, Vector2 currentVelocity, float duration, float rampIn)
        {
            target = targetVelocity;
            this.duration = Mathf.Max(0.05f, duration);
            this.rampIn = Mathf.Clamp(rampIn, 0f, this.duration * 0.5f);
            rampRate = this.rampIn > 0f ? (targetVelocity - currentVelocity).magnitude / this.rampIn : 0f;
            elapsed = 0f;
            reached = false;
            Active = true;
        }

        /// <summary>Forgets the knockback (round over, teleport).</summary>
        public void Cancel() => this = default;

        /// <summary>
        /// The velocity one physics step later. <paramref name="velocity"/> is what physics left over; <paramref name="input"/>
        /// and <paramref name="speedMultiplier"/> are the character's normal controls, which keep working all the time.
        /// </summary>
        public Vector2 Step(MovementPhysics physics, ref MovementPhysics.State motion, Vector2 velocity, Vector2 input,
            float deltaTime, float speedMultiplier)
        {
            if (!Active) return physics.Step(ref motion, velocity, input, deltaTime, speedMultiplier);

            float previous = elapsed;
            elapsed += deltaTime;
            if (elapsed >= duration) Active = false;

            // Build-up: a steady pull towards the knockback velocity.
            if (previous < rampIn)
            {
                if (elapsed >= rampIn) reached = true;
                return Vector2.MoveTowards(velocity, target, rampRate * deltaTime);
            }

            if (!reached) // no build-up configured: the jolt is applied right now
            {
                velocity = target;
                reached = true;
            }

            // Wear-off: only the part above what the character's own controls ask for is knockback, and it fades out as
            // (1 - progress)^2. With no input that is everything, so the character comes to rest as the knockback ends;
            // while a direction is held it is the speed that input wants, so control takes over exactly as the shove fades.
            float window = Mathf.Max(duration - rampIn, 1e-4f);
            float progressBefore = Mathf.Clamp01((previous - rampIn) / window);
            float progressNow = Mathf.Clamp01((elapsed - rampIn) / window);
            float fadeBefore = (1f - progressBefore) * (1f - progressBefore);
            float fadeNow = (1f - progressNow) * (1f - progressNow);
            float fade = fadeBefore > 1e-4f ? fadeNow / fadeBefore : 0f;

            float topSpeed = physics.maxSpeed * speedMultiplier;
            float speed = velocity.magnitude;
            Vector2 direction = speed > 1e-4f ? velocity / speed : Vector2.zero;
            float ownSpeed = Mathf.Min(speed, Mathf.Clamp01(input.magnitude) * topSpeed);
            float excess = (speed - ownSpeed) * fade;

            // The character's own part keeps moving under the normal rules (and keeps its heading up to date).
            Vector2 own = physics.Step(ref motion, direction * ownSpeed, input, deltaTime, speedMultiplier);
            return own + direction * excess;
        }
    }
}

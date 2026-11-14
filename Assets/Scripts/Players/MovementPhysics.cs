using System;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// The "feel" of player movement: a slightly icy, spaceship-like model with momentum. Plain tuning data plus one pure
    /// function, <see cref="Step"/>, that turns the current velocity and the player's input into the next velocity. It
    /// knows nothing about Unity components, so it is shared by every player (it lives in the <see cref="PlayersConfig"/>)
    /// and every value can be changed in the Inspector - also while the game runs.
    /// <para>
    /// How a step works. The velocity chases a target velocity (the pressed direction x the wanted speed). How fast it may
    /// change depends on the direction of the change:
    /// <list type="bullet">
    /// <item><b>Along the pressed direction</b>: speeding up is limited by <see cref="acceleration"/>; while the character
    /// moves against the input it is additionally slowed by up to <see cref="brakingStrength"/>.</item>
    /// <item><b>Sideways</b> (momentum that does not point where the player wants to go): limited by
    /// <see cref="turningResponsiveness"/> x (1 - <see cref="inertia"/>). This is what turns a direction change into a short arc.</item>
    /// </list>
    /// Both limits apply together, so the velocity heads straight for the target (which is also why a character pressed
    /// diagonally into a wall slides along it at the correct speed). With no input the character coasts to a stop at
    /// <see cref="deceleration"/>.
    /// </para>
    /// Ghosts do not use this: they replay recorded positions, which already contain every acceleration, slide and turn.
    /// </summary>
    [Serializable]
    public class MovementPhysics
    {
        // Below this the stick / keys count as released. (Gamepad dead zones are already applied by the Input System.)
        const float InputThreshold = 0.01f;

        // Keeps the maths finite if a rate is set to 0 (the character then simply does not change that way).
        const float MinStep = 1e-6f;

        [Header("Speed")]
        [Tooltip("Top speed in world units per second (before power-ups; a Speed Boost multiplies it).")]
        [Range(0.5f, 10f)] public float maxSpeed = 2f;

        [Tooltip("Units per second squared. How fast the character speeds up towards its target speed while a direction is held. " +
                 "Time to top speed = Max Speed / Acceleration (default: 0.2 s). Higher = snappier.")]
        [Range(1f, 60f)] public float acceleration = 10f;

        [Tooltip("Units per second squared. How fast the character slows down after the input is released (the slide). " +
                 "Slide distance = Max Speed^2 / (2 x Deceleration). Higher = shorter slide; low values feel like ice.")]
        [Range(0.5f, 60f)] public float deceleration = 7f;

        [Header("Turning")]
        [Tooltip("Units per second squared. How fast the sideways part of the velocity is turned towards the pressed direction. " +
                 "Higher = tighter turns; lower = wider, slidier arcs.")]
        [Range(1f, 60f)] public float turningResponsiveness = 18f;

        [Tooltip("How much of its old velocity the character keeps when you change direction. 0 = none (turns are limited only by " +
                 "Turning Responsiveness), higher = it plows on in its old direction for longer. " +
                 "Sideways correction = Turning Responsiveness x (1 - Inertia).")]
        [Range(0f, 0.9f)] public float inertia = 0.4f;

        [Tooltip("Units per second squared, added to Acceleration while the character moves AGAINST the pressed direction " +
                 "(an active brake / quick reversal). 0 = reversing is only as fast as accelerating. Higher = stops on a dime when you push back.")]
        [Range(0f, 80f)] public float brakingStrength = 14f;

        [Header("Stopping")]
        [Tooltip("When no input is held, a character slower than this stops dead instead of crawling the last few millimetres. " +
                 "It never blocks starting to move. 0 = off.")]
        [Range(0f, 1f)] public float minimumMovementSpeed = 0.05f;

        [Header("Power-ups")]
        [Tooltip("On: a Speed Boost also scales Acceleration, Deceleration, Braking and Turning, so a boosted character handles " +
                 "exactly like a normal one, just faster (same time to top speed). Off: those stay fixed, so a boosted character " +
                 "takes longer to reach its higher top speed and slides further.")]
        public bool scaleWithSpeedBoost = true;

        /// <summary>
        /// The velocity one physics step later. <paramref name="input"/> is the raw movement input (length up to 1; a smaller
        /// length asks for a proportionally lower speed), <paramref name="speedMultiplier"/> comes from the character's
        /// power-ups (1 = none). Pure: nothing is stored, so the caller can feed back whatever velocity physics left over
        /// (e.g. after a wall took the speed into it away).
        /// </summary>
        public Vector2 Step(Vector2 velocity, Vector2 input, float deltaTime, float speedMultiplier = 1f)
        {
            float topSpeed = maxSpeed * speedMultiplier;
            float handling = scaleWithSpeedBoost ? speedMultiplier : 1f;
            float accelerate = Mathf.Max(0f, acceleration) * handling;
            float decelerate = Mathf.Max(0f, deceleration) * handling;
            float brake = Mathf.Max(0f, brakingStrength) * handling;
            float turn = Mathf.Max(0f, turningResponsiveness) * Mathf.Clamp01(1f - inertia) * handling;

            input = Vector2.ClampMagnitude(input, 1f);
            float inputStrength = input.magnitude;

            // No input: coast to a stop.
            if (inputStrength < InputThreshold)
            {
                float speed = velocity.magnitude;
                float slowed = speed - decelerate * deltaTime;
                if (slowed <= minimumMovementSpeed) return Vector2.zero;
                return velocity * (slowed / speed); // slowed > 0, so speed > 0
            }

            Vector2 direction = input / inputStrength;
            Vector2 target = direction * (inputStrength * topSpeed);
            Vector2 delta = target - velocity;
            float deltaAlong = Vector2.Dot(delta, direction); // > 0: has to speed up, < 0: is faster than asked for
            float deltaSideways = (delta - direction * deltaAlong).magnitude;

            float alongRate;
            if (deltaAlong >= 0f)
            {
                alongRate = accelerate;

                // Moving against the input: the more directly against it, the harder the brake bites.
                float speed = velocity.magnitude;
                if (speed > 0f) alongRate += brake * Mathf.Clamp01(-Vector2.Dot(velocity, direction) / speed);
            }
            else
            {
                alongRate = decelerate; // e.g. the stick was eased off: lose the surplus like coasting
            }

            // This step may change the velocity by alongRate*dt along the input and turn*dt sideways. Both limits apply at
            // once (an ellipse), so the change keeps pointing straight at the target. `steps` = how many such steps away it is.
            float alongSteps = deltaAlong / Mathf.Max(alongRate * deltaTime, MinStep);
            float sidewaysSteps = deltaSideways / Mathf.Max(turn * deltaTime, MinStep);
            float steps = Mathf.Sqrt(alongSteps * alongSteps + sidewaysSteps * sidewaysSteps);

            Vector2 result = steps <= 1f ? target : velocity + delta / steps;
            return Vector2.ClampMagnitude(result, topSpeed);
        }
    }
}

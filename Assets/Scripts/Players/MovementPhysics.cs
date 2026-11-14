using System;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// The "feel" of player movement: a racing-game drift on ice. Plain tuning data plus one function, <see cref="Step"/>,
    /// that turns the current velocity and the player's input into the next velocity. It knows nothing about Unity
    /// components, so it is shared by every player (it lives in the <see cref="PlayersConfig"/>) and every value can be
    /// changed in the Inspector - also while the game runs.
    /// <para>
    /// The core idea: <b>turning rotates the momentum, it never spends it.</b> Two things are kept apart:
    /// <list type="bullet">
    /// <item><b>Heading</b> - where the character "points" and thrusts. It chases the pressed direction quickly
    /// (<see cref="steeringResponsiveness"/>, capped by <see cref="turningSpeed"/>, boosted for sharp changes by
    /// <see cref="directionChangeResponsiveness"/>). This is what makes steering feel immediate.</item>
    /// <item><b>Velocity direction</b> - where the character actually travels. It slides round to the heading instead of
    /// snapping, so after a sharp turn the character keeps carrying its old direction for a moment and then smoothly
    /// aligns. How long it slides is set by <see cref="lateralMomentumRetention"/>, and <see cref="driftStrength"/> makes
    /// sharp changes of direction slide more than gentle ones.</item>
    /// </list>
    /// Speed is handled separately from direction: the slide only rotates the velocity, so it adds sideways motion
    /// without braking (<see cref="speedRetentionDuringTurns"/> sets the small cost, 1 = none) and the thrust along the heading
    /// keeps the character at speed. Only pressing against the motion applies the brake (<see cref="brakingStrength"/>),
    /// and with no input the character coasts to a stop at <see cref="deceleration"/> (the ice).
    /// </para>
    /// Ghosts do not use this: they replay recorded positions, which already contain every slide, drift and turn.
    /// </summary>
    [Serializable]
    public class MovementPhysics
    {
        /// <summary>
        /// What the movement needs to remember between steps (the heading). One per character; keep it next to the
        /// character, pass it to every <see cref="Step"/> and call <see cref="Reset"/> after a teleport. Opaque to everyone else.
        /// </summary>
        public struct State
        {
            public float Heading;      // radians
            public float TurnSide;     // +1 / -1: which way the last turn went (settles exact 180-degree reversals)
            public bool HasHeading;

            public void Reset() => this = default;
        }

        // Below this the stick / keys count as released. (Gamepad dead zones are already applied by the Input System.)
        const float InputThreshold = 0.01f;

        // Below this share of top speed the character is (nearly) standing: the heading simply snaps to the input, so
        // starting from rest has no turning delay.
        const float HeadingSnapSpeed = 0.1f;

        // Within this angle of an exact reversal the side to turn to is taken from the last turn instead of the (noisy) sign.
        const float ReversalAngle = Mathf.PI - 0.05f;

        // Base rate (1/s) at which the velocity direction catches up with the heading when nothing is retained.
        const float LateralGripRate = 30f;

        // How strongly the sharpness of a direction change (0 = none, 1 = reversal) weakens the grip at Drift Strength 1.
        const float DriftSharpnessGain = 6f;

        [Header("Speed")]
        [Tooltip("Top speed in world units per second (before power-ups; a Speed Boost multiplies it).")]
        [Range(0.5f, 10f)] public float maxSpeed = 2f;

        [Tooltip("Units per second squared. How fast the character speeds up towards its target speed along its heading. " +
                 "Time to top speed = Max Speed / Acceleration (default: 0.17 s). Higher = snappier.")]
        [Range(1f, 60f)] public float acceleration = 12f;

        [Tooltip("Units per second squared. How fast the character slows down once the input is released (the ice slide). " +
                 "Slide distance = Max Speed^2 / (2 x Deceleration). It does NOT apply while turning - turning never slows the character.")]
        [Range(0.5f, 60f)] public float deceleration = 7f;

        [Tooltip("Units per second squared. Slows the character only while the input points AGAINST its motion (the strongest at an " +
                 "exact reversal, none at 90 degrees or less). 0 = a reversal is a pure U-turn that keeps all its speed; higher = " +
                 "a push-back shortens the slide and stops sooner.")]
        [Range(0f, 40f)] public float brakingStrength = 4f;

        [Header("Steering (the heading)")]
        [Tooltip("1/s. How quickly the heading swings towards the pressed direction (angular speed = angle left x this). " +
                 "Higher = the character reacts at once; lower = lazier steering. The default reaches a new direction in about 0.1 s.")]
        [Range(1f, 60f)] public float steeringResponsiveness = 14f;

        [Tooltip("Degrees per second. The fastest the heading may swing. Only limits very sharp changes - lower it for wider, " +
                 "more car-like U-turns.")]
        [Range(90f, 2160f)] public float turningSpeed = 900f;

        [Tooltip("Extra steering boost for sharp direction changes: the larger the angle to the new direction, the more " +
                 "Steering Responsiveness is multiplied (up to 1 + this value at a full reversal). 0 = every angle is treated the same.")]
        [Range(0f, 3f)] public float directionChangeResponsiveness = 0.5f;

        [Header("Drift (the velocity)")]
        [Tooltip("How much sharp direction changes make the character slide. The sharper the change, the less the velocity " +
                 "follows the heading, so the character keeps travelling in its old direction for longer; small corrections " +
                 "stay grippy. 0 = every turn grips the same, 1 = sharp turns slide a lot. The slide only rotates the velocity - it never slows the character.")]
        [Range(0f, 1f)] public float driftStrength = 0.5f;

        [Tooltip("How much sideways momentum is kept while the velocity catches up with the heading. 0 = grips at once (no slide at all), " +
                 "higher = the old momentum carries on longer. The catch-up rate is 30 x (1 - this) per second, " +
                 "before Drift Strength weakens it for sharp turns.")]
        [Range(0f, 0.95f)] public float lateralMomentumRetention = 0.3f;

        [Tooltip("Share of its speed the character keeps through a 180-degree turn of its movement direction. 1 = turning costs no speed at all. " +
                 "The thrust along the heading brings back what little is lost.")]
        [Range(0.5f, 1f)] public float speedRetentionDuringTurns = 0.95f;

        [Header("Stopping")]
        [Tooltip("When no input is held, a character slower than this stops dead instead of crawling the last few millimetres. " +
                 "It never blocks starting to move. 0 = off.")]
        [Range(0f, 1f)] public float minimumMovementSpeed = 0.05f;

        [Header("Power-ups")]
        [Tooltip("On: a Speed Boost also scales Acceleration, Deceleration and Braking, so a boosted character reaches its higher top speed " +
                 "in the same time and its turns keep the same shape, just larger. Off: those stay fixed, so a boosted character " +
                 "takes longer to reach top speed and slides further.")]
        public bool scaleWithSpeedBoost = true;

        /// <summary>
        /// The velocity one physics step later. <paramref name="input"/> is the raw movement input (length up to 1; a smaller
        /// length asks for a proportionally lower speed), <paramref name="speedMultiplier"/> comes from the character's
        /// power-ups (1 = none), <paramref name="state"/> is the character's own <see cref="State"/>. Nothing else is stored,
        /// so the caller can feed back whatever velocity physics left over (e.g. after a wall took the speed into it away).
        /// </summary>
        public Vector2 Step(ref State state, Vector2 velocity, Vector2 input, float deltaTime, float speedMultiplier = 1f)
        {
            float topSpeed = maxSpeed * speedMultiplier;
            float handling = scaleWithSpeedBoost ? speedMultiplier : 1f;
            float accelerate = Mathf.Max(0f, acceleration) * handling;
            float decelerate = Mathf.Max(0f, deceleration) * handling;
            float brake = Mathf.Max(0f, brakingStrength) * handling;

            input = Vector2.ClampMagnitude(input, 1f);
            float inputStrength = input.magnitude;
            float speed = velocity.magnitude;

            // No input: coast to a stop in a straight line.
            if (inputStrength < InputThreshold)
            {
                float slowed = speed - decelerate * deltaTime;
                if (slowed <= minimumMovementSpeed) return Vector2.zero;
                return velocity * (slowed / speed); // slowed > 0, so speed > 0
            }

            float inputAngle = Mathf.Atan2(input.y, input.x);
            float targetSpeed = inputStrength * topSpeed;
            float velocityAngle = speed > 1e-4f ? Mathf.Atan2(velocity.y, velocity.x) : inputAngle;

            // 1. Heading: snap to the input when (nearly) standing, otherwise swing towards it.
            if (!state.HasHeading || speed < topSpeed * HeadingSnapSpeed)
            {
                state.Heading = inputAngle;
                state.HasHeading = true;
            }
            else
            {
                float error = Wrap(inputAngle - state.Heading);
                float size = Mathf.Abs(error);
                float side = SideOf(error, size, ref state);

                float gain = steeringResponsiveness * (1f + directionChangeResponsiveness * size / Mathf.PI);
                float rate = Mathf.Min(size * gain, turningSpeed * Mathf.Deg2Rad);
                state.Heading = Wrap(state.Heading + side * Mathf.Min(rate * deltaTime, size));
            }

            // 2. Velocity direction: slide round to the heading (this is the drift). Pure rotation - the speed is untouched here.
            float slip = Wrap(state.Heading - velocityAngle);
            float slipSize = Mathf.Abs(slip);
            float slipSide = slipSize > ReversalAngle ? (state.TurnSide == 0f ? 1f : state.TurnSide) : Mathf.Sign(slip);

            float sharpness = Mathf.Abs(Wrap(inputAngle - velocityAngle)) / Mathf.PI; // 0 = going where asked, 1 = the opposite way
            float catchUp = LateralGripRate * (1f - Mathf.Clamp(lateralMomentumRetention, 0f, 0.95f))
                            / (1f + driftStrength * DriftSharpnessGain * sharpness);
            float rotation = slipSide * slipSize * (1f - Mathf.Exp(-catchUp * deltaTime));
            velocityAngle += rotation;

            // 3. Speed: thrust along the heading brings the character to the wanted speed; turning costs almost nothing.
            float newSpeed = speed;
            float forward = Mathf.Cos(Wrap(state.Heading - velocityAngle)); // how much of the thrust points along the motion
            if (newSpeed < targetSpeed && forward > 0f) newSpeed = Mathf.Min(targetSpeed, newSpeed + accelerate * forward * deltaTime);
            else if (newSpeed > targetSpeed) newSpeed = Mathf.Max(targetSpeed, newSpeed - decelerate * deltaTime); // e.g. the stick was eased off

            float opposed = Mathf.Max(0f, -Mathf.Cos(Wrap(inputAngle - velocityAngle))); // only pressing AGAINST the motion brakes
            newSpeed = Mathf.Max(0f, newSpeed - brake * opposed * deltaTime);

            newSpeed *= Mathf.Pow(Mathf.Clamp(speedRetentionDuringTurns, 0.01f, 1f), Mathf.Abs(rotation) / Mathf.PI);
            newSpeed = Mathf.Min(newSpeed, topSpeed);

            return new Vector2(Mathf.Cos(velocityAngle), Mathf.Sin(velocityAngle)) * newSpeed;
        }

        // Which way to turn for a signed angle. Right at a reversal the sign is noise, so the last turn's side is used.
        static float SideOf(float signedAngle, float size, ref State state)
        {
            if (size > ReversalAngle) return state.TurnSide == 0f ? 1f : state.TurnSide;

            float side = signedAngle >= 0f ? 1f : -1f;
            if (size > 0.01f) state.TurnSide = side;
            return side;
        }

        static float Wrap(float angle) => Mathf.Repeat(angle + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
    }
}

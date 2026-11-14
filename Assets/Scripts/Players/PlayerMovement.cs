using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam
{
    /// <summary>
    /// Top-down movement shared by all players. Reads the "Move" action of the player's PlayerInput and feeds it to the
    /// shared <see cref="MovementPhysics"/> (acceleration, drift steering, braking) - see the Players Config asset to tune it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerInput))]
    public class PlayerMovement : MonoBehaviour
    {
        const string MoveActionName = "Move";

        // Replaced by the shared settings in Configure; the defaults only cover a movement that was never configured.
        MovementPhysics physics = new MovementPhysics();

        // This character's own steering state (its heading); the settings above are shared, this is not.
        MovementPhysics.State motion;

        /// <summary>Multiplier on the top speed from the character's power-ups (1 = none, 1.3 = Speed Boost).</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Current top speed: the configured Max Speed with power-ups applied.</summary>
        public float MaxSpeed => physics.maxSpeed * SpeedMultiplier;

        /// <summary>While false the player ignores input and stands still (e.g. between rounds): it stops at once, it does not slide.</summary>
        public bool MovementEnabled { get; set; } = true;

        Rigidbody2D body;
        PlayerInput playerInput;
        InputAction move;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            playerInput = GetComponent<PlayerInput>();

            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // No friction, so players slide along walls instead of sticking to them.
            body.sharedMaterial = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
        }

        /// <summary>Uses the given movement settings. Every player gets the same instance, so editing it retunes all of them live.</summary>
        public void Configure(MovementPhysics settings)
        {
            if (settings != null) physics = settings;
        }

        /// <summary>Teleports the player and stops it.</summary>
        public void TeleportTo(Vector3 position)
        {
            transform.SetPositionAndRotation(position, Quaternion.identity);
            body.position = position;
            body.linearVelocity = Vector2.zero;
            motion.Reset();
            Physics2D.SyncTransforms();
        }

        void FixedUpdate()
        {
            if (!MovementEnabled)
            {
                body.linearVelocity = Vector2.zero;
                motion.Reset();
                return;
            }

            // PlayerInput clones its actions on enable, so look the action up lazily.
            move ??= playerInput.actions?.FindAction(MoveActionName);
            Vector2 input = move != null ? move.ReadValue<Vector2>() : Vector2.zero;

            // Start from the velocity physics actually left us with: a wall has already removed the speed into it, so
            // the character slides along walls and never "stores up" momentum against them.
            body.linearVelocity = physics.Step(ref motion, body.linearVelocity, input, Time.fixedDeltaTime, SpeedMultiplier);
        }
    }
}

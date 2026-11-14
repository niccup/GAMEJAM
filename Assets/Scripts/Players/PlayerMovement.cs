using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam
{
    /// <summary>Top-down movement shared by all players. Reads the "Move" action of the player's PlayerInput.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerInput))]
    public class PlayerMovement : MonoBehaviour
    {
        const string MoveActionName = "Move";

        public float Speed { get; set; } = 5f;

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

        void FixedUpdate()
        {
            // PlayerInput clones its actions on enable, so look the action up lazily.
            move ??= playerInput.actions?.FindAction(MoveActionName);
            Vector2 direction = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
            body.linearVelocity = Vector2.ClampMagnitude(direction, 1f) * Speed;
        }
    }
}

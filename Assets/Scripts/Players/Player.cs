using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam
{
    /// <summary>Root component of the player prefab: identity, look and wiring of its parts.</summary>
    [RequireComponent(typeof(PlayerInput), typeof(PlayerMovement))]
    public class Player : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer;

        TrailEmitter trailEmitter;

        public int Index { get; private set; }

        /// <summary>The renderer of the character's sprite (used e.g. to build matching ghosts).</summary>
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public PlayerInput PlayerInput { get; private set; }

        /// <summary>The player's main color (territory color; the trail uses a shade of it).</summary>
        public Color Color { get; private set; }

        /// <summary>The circle that draws this player's trail. A separate child object, independent of the sprite.</summary>
        public TrailEmitter TrailEmitter => trailEmitter != null ? trailEmitter : (trailEmitter = GetComponentInChildren<TrailEmitter>());

        /// <summary>Puts the player back at a position and stops it.</summary>
        public void ResetTo(Vector3 position) => GetComponent<PlayerMovement>().TeleportTo(position);

        public bool MovementEnabled
        {
            get => GetComponent<PlayerMovement>().MovementEnabled;
            set => GetComponent<PlayerMovement>().MovementEnabled = value;
        }

        public void Initialize(int index, PlayersConfig config)
        {
            Index = index;
            name = $"Player {index + 1}";
            PlayerInput = GetComponent<PlayerInput>();

            PlayerSlot slot = config.slots[index];
            Color = slot.color;

            if (spriteRenderer != null)
            {
                if (slot.spriteOverride != null) spriteRenderer.sprite = slot.spriteOverride;
                spriteRenderer.color = slot.color;
            }

            GetComponent<PlayerMovement>().Speed = config.moveSpeed;
        }
    }
}

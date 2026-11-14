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
        public PlayerInput PlayerInput { get; private set; }

        /// <summary>The player's main color (territory color; the trail uses a shade of it).</summary>
        public Color Color { get; private set; }

        /// <summary>The circle that draws this player's trail. A separate child object, independent of the sprite.</summary>
        public TrailEmitter TrailEmitter => trailEmitter != null ? trailEmitter : (trailEmitter = GetComponentInChildren<TrailEmitter>());

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

using System.Collections.Generic;
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

        PlayerMovement movement;
        Rigidbody2D body;

        public PlayerMovement Movement => movement != null ? movement : (movement = GetComponent<PlayerMovement>());

        /// <summary>Puts the player back at a position and stops it.</summary>
        public void ResetTo(Vector3 position)
        {
            contactCooldowns.Clear();
            Movement.TeleportTo(position);
        }

        public bool MovementEnabled
        {
            get => GetComponent<PlayerMovement>().MovementEnabled;
            set => GetComponent<PlayerMovement>().MovementEnabled = value;
        }

        readonly List<PlayerModifier> modifiers = new List<PlayerModifier>();

        /// <summary>Power-ups of the player's current character (won for this round only; ghosts keep their own copies).</summary>
        public IReadOnlyList<PlayerModifier> Modifiers => modifiers;

        /// <summary>Current stats with all modifiers applied.</summary>
        public PlayerStats Stats { get; private set; } = PlayerStats.Default;

        public bool HasModifier(PlayerModifier modifier) => modifiers.Contains(modifier);

        /// <summary>
        /// Gives the player a modifier and applies its effect right away. Returns false if nothing changed
        /// (null, or an already owned modifier that is not stackable).
        /// </summary>
        public bool AddModifier(PlayerModifier modifier)
        {
            if (modifier == null || (!modifier.Stackable && modifiers.Contains(modifier))) return false;
            modifiers.Add(modifier);
            RefreshStats();
            return true;
        }

        /// <summary>Removes all modifiers and restores the normal speed (between rounds).</summary>
        public void ClearModifiers()
        {
            modifiers.Clear();
            contactCooldowns.Clear();
            RefreshStats();
        }

        // ---- Contact abilities (see PlayerModifier.OnContact) ----

        // When each (ability, other player) pair last acted, for abilities with a cooldown.
        readonly Dictionary<(PlayerModifier, int), float> contactCooldowns = new Dictionary<(PlayerModifier, int), float>();

        /// <summary>
        /// True if <paramref name="source"/> may act on <paramref name="other"/> now - and starts its cooldown - or false if it
        /// did so less than <paramref name="seconds"/> ago. 0 seconds = always.
        /// </summary>
        public bool TryStartContactCooldown(PlayerModifier source, Player other, float seconds)
        {
            var key = (source, other.Index);
            float now = Time.time;
            if (seconds > 0f && contactCooldowns.TryGetValue(key, out float last) && now - last < seconds) return false;

            contactCooldowns[key] = now;
            return true;
        }

        // Physics tells the character when it touches something. Only a touch with another player's CURRENT character
        // counts: ghosts have no collider and no Player component, so they can neither cause nor receive a contact.
        void OnCollisionEnter2D(Collision2D collision)
        {
            if (modifiers.Count == 0) return;

            Player other = collision.collider.GetComponentInParent<Player>();
            if (other == null || other == this) return;
            if (!Movement.MovementEnabled || !other.Movement.MovementEnabled) return; // between rounds nothing interacts

            if (body == null) body = GetComponent<Rigidbody2D>();
            Vector2 selfPosition = body.position;
            Vector2 offset = other.GetComponent<Rigidbody2D>().position - selfPosition;
            Vector2 normal = offset.sqrMagnitude > 1e-8f ? offset.normalized : Vector2.right;
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : selfPosition + normal * 0.5f * offset.magnitude;

            var contact = new PlayerContact(this, other, normal, point, Movement.LastVelocity, other.Movement.LastVelocity);
            for (int i = 0; i < modifiers.Count; i++) modifiers[i].OnContact(contact);
        }

        void RefreshStats()
        {
            Stats = PlayerStats.From(modifiers);
            GetComponent<PlayerMovement>().SpeedMultiplier = Stats.SpeedMultiplier;
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

            GetComponent<PlayerMovement>().Configure(config.movement);
            RefreshStats();
        }
    }
}

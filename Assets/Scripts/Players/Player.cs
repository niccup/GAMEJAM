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

        /// <summary>Puts the player back at a position and stops it.</summary>
        public void ResetTo(Vector3 position) => GetComponent<PlayerMovement>().TeleportTo(position);

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
            RefreshStats();
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

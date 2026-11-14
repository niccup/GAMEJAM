using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace GameJam
{
    /// <summary>
    /// The power-up offer between rounds. <see cref="Begin"/> picks one modifier from the pool and shows it on the
    /// <see cref="PowerUpOverlay"/> for ONE player - the chooser (the loser of the round that just ended; the
    /// <see cref="RoundManager"/> determines who that is). Only the chooser's "Action" button (the action of that name in
    /// their own PlayerInput, so the bindings are whatever you set in the Input Actions asset) is listened to, so nobody
    /// else can claim it. When the chooser presses it they get the modifier for the coming round only (the RoundManager
    /// clears the previous round's power-ups first), the overlay closes and the callback passed to Begin runs - that is
    /// where the next round starts, so the power-up is always assigned before the round begins.
    /// Add more <see cref="PlayerModifier"/> assets to the pool to offer other power-ups; one is picked at random each time.
    /// To offer several choices later, extend the offer to a list and let the overlay/input pick one entry of it.
    /// </summary>
    public class PowerUpSelection : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerSpawner spawner;
        [SerializeField] PowerUpOverlay overlay;

        [Header("Offer")]
        [Tooltip("Modifiers that can be offered. One is picked at random for every selection.")]
        [SerializeField] PlayerModifier[] pool;

        [Header("Rules")]
        [Tooltip("Name of the button action in the players' Input Actions that claims the power-up.")]
        [SerializeField] string actionName = "Action";

        [Tooltip("Presses in the first moments are ignored, so the press that clicked 'Next Round' cannot also claim the power-up.")]
        [SerializeField, Min(0f)] float inputDelay = 0.15f;

        PlayerModifier offered;
        Player chooser;
        InputAction action;
        Action onFinished;
        float armedAt;
        bool claimed;

        public bool IsActive => offered != null;

        /// <summary>The modifier currently on offer (null when no selection is running).</summary>
        public PlayerModifier Offered => offered;

        /// <summary>The only player who can claim the current offer (null when no selection is running).</summary>
        public Player Chooser => chooser;

        /// <summary>Raised when the chooser has claimed the power-up, just before the selection closes.</summary>
        public event Action<Player, PlayerModifier> Awarded;

        /// <summary>
        /// Offers a power-up to <paramref name="player"/> alone. <paramref name="finished"/> is called once they have
        /// claimed it (or at once if there is nothing to offer or the player cannot press the action).
        /// </summary>
        public void Begin(Player player, Action finished)
        {
            if (IsActive) return;

            PlayerModifier pick = PickOffer();
            InputAction claimAction = player != null && player.PlayerInput != null ? player.PlayerInput.actions?.FindAction(actionName) : null;
            if (pick == null || claimAction == null)
            {
                if (pick != null) Debug.LogWarning($"Player {(player != null ? player.Index + 1 : 0)} has no '{actionName}' action - skipping the power-up selection.", this);
                finished?.Invoke();
                return;
            }

            offered = pick;
            chooser = player;
            action = claimAction;
            onFinished = finished;
            claimed = false;
            armedAt = Time.realtimeSinceStartup + inputDelay;

            action.performed += OnPerformed;
            if (overlay != null) overlay.Show(pick, player);
        }

        void Update()
        {
            if (IsActive && claimed) Finish();
        }

        void OnDisable() => Unsubscribe();

        void OnPerformed(InputAction.CallbackContext context)
        {
            if (IsActive && Time.realtimeSinceStartup >= armedAt) claimed = true;
        }

        void Unsubscribe()
        {
            if (action != null) action.performed -= OnPerformed;
            action = null;
        }

        void Finish()
        {
            PlayerModifier modifier = offered;
            Player winner = chooser;
            Action callback = onFinished;

            Unsubscribe();
            offered = null;
            chooser = null;
            onFinished = null;
            claimed = false;
            if (overlay != null) overlay.Hide();

            winner.AddModifier(modifier);
            Debug.Log($"Player {winner.Index + 1} took {modifier.DisplayName}.", this);
            Awarded?.Invoke(winner, modifier);

            callback?.Invoke();
        }

        PlayerModifier PickOffer()
        {
            var candidates = new List<PlayerModifier>();
            if (pool != null)
                foreach (PlayerModifier modifier in pool)
                    if (modifier != null) candidates.Add(modifier);

            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }
    }
}

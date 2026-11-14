using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// The power-up selection between rounds. <see cref="Begin"/> takes the players who may choose (today: the loser of
    /// the round that just ended) and gives each of them a turn, one after another. A turn starts with the
    /// <see cref="OfferStep"/> listing the pool; a power-up either applies at once or opens further
    /// <see cref="SelectionStep"/>s (Mirror: player, ghost, confirm). Only the current chooser's own input is read
    /// ("Action" confirms, "Move" left / right cycles, "Move" down goes back - the bindings are whatever you set in the
    /// Input Actions asset), so nobody else can take part. When the last turn is complete the callback passed to Begin
    /// runs - that is where the next round starts, so every power-up is in place before the round begins.
    /// <para>
    /// Add a <see cref="PowerUp"/> asset to the pool to offer something new; add a <see cref="SelectionStep"/> subclass
    /// for any extra screen it needs. The previews and markers it uses are separate objects that never touch the game.
    /// </para>
    /// </summary>
    public class PowerUpSelection : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerSpawner spawner;
        [SerializeField] MapView map;
        [Tooltip("Needed for power-ups that work on ghosts (Mirror).")]
        [SerializeField] ReplayManager replay;
        [Tooltip("Gives the ghost preview the game's own trail settings (cell size, loop rules, opacity).")]
        [SerializeField] TerritorySystem territory;
        [SerializeField] PowerUpOverlay overlay;

        [Header("Offer")]
        [Tooltip("The power-ups offered to the chooser, in the order they are shown.")]
        [SerializeField] PowerUp[] pool;

        [Header("Input")]
        [Tooltip("Name of the button action in the players' Input Actions that confirms.")]
        [SerializeField] string actionName = "Action";

        [Tooltip("Name of the Vector2 action that cycles (left / right) and goes back (down).")]
        [SerializeField] string moveActionName = "Move";

        [Tooltip("Input is ignored for this long when a turn starts, so the press that clicked 'Next Round' cannot also take a power-up.")]
        [SerializeField, Min(0f)] float inputDelay = 0.15f;

        [Header("Ghost preview (speed and the post-Mirror preview are set on the Mirror asset)")]
        [Tooltip("Pause on the finished trail before the preview plays again.")]
        [SerializeField, Min(0f)] float previewHoldSeconds = 0.6f;

        [Tooltip("Longest time the preview may spend on its trail per frame. At very high speeds it slows down rather than skip any of the path.")]
        [SerializeField, Min(1f)] float previewFrameBudgetMilliseconds = 6f;

        readonly Queue<Player> waiting = new Queue<Player>();
        readonly List<SelectionStep> stack = new List<SelectionStep>();

        PowerUpContext context;
        SelectionInput input;
        Action onFinished;
        SelectionMarkers markers;
        GhostPreview preview;

        /// <summary>True from <see cref="Begin"/> until the last turn is complete.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The player whose turn it is (null when no selection is running).</summary>
        public Player Chooser => context?.Chooser;

        void Awake()
        {
            markers = CreateChild<SelectionMarkers>("SelectionMarkers");
            markers.Initialize(map, spawner != null ? spawner.Config : null);
            preview = CreateChild<GhostPreview>("GhostPreview");
            if (territory == null) territory = FindFirstObjectByType<TerritorySystem>();
            preview.Configure(map, territory != null ? territory.Config : null, previewHoldSeconds, previewFrameBudgetMilliseconds);
        }

        T CreateChild<T>(string childName) where T : Component
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var component = go.AddComponent<T>();
            go.SetActive(false);
            return component;
        }

        /// <summary>
        /// Gives every player in <paramref name="choosers"/> a turn, in order. <paramref name="finished"/> is called once
        /// the last turn is complete (at once if nobody has anything to take).
        /// </summary>
        public void Begin(IReadOnlyList<Player> choosers, Action finished)
        {
            if (IsActive) return;

            waiting.Clear();
            foreach (Player player in choosers)
                if (player != null) waiting.Enqueue(player);

            onFinished = finished;
            IsActive = true;
            if (!StartNextTurn()) Finish();
        }

        void Update()
        {
            if (!IsActive || input == null || stack.Count == 0) return;

            NavCommand command = input.Poll();
            if (command != NavCommand.None) stack[stack.Count - 1].Handle(command);
        }

        void OnDisable() => EndTurn();

        // Starts the turn of the next waiting player that has something to take. False if there is none.
        bool StartNextTurn()
        {
            while (waiting.Count > 0)
            {
                Player chooser = waiting.Dequeue();
                SelectionInput chooserInput = SelectionInput.Create(chooser, moveActionName, actionName, inputDelay);
                if (chooserInput == null)
                {
                    Debug.LogWarning($"Player {chooser.Index + 1} has no '{actionName}' / '{moveActionName}' action - skipping their power-up selection.", this);
                    continue;
                }

                var turn = new PowerUpContext(chooser, spawner, map, replay, overlay, markers, preview, Push, Back, Complete);
                var offer = new OfferStep(turn, pool);
                if (!offer.HasOptions) continue;

                context = turn;
                input = chooserInput;
                if (overlay != null) overlay.Open();
                Push(offer);
                return true;
            }
            return false;
        }

        void Push(SelectionStep step)
        {
            if (stack.Count > 0) stack[stack.Count - 1].Exit();
            stack.Add(step);
            step.Enter();
        }

        void Back()
        {
            if (stack.Count <= 1) return;

            stack[stack.Count - 1].Exit();
            stack.RemoveAt(stack.Count - 1);
            stack[stack.Count - 1].Enter();
        }

        void Complete()
        {
            EndTurn();
            if (!StartNextTurn()) Finish();
        }

        // Removes everything the turn showed.
        void EndTurn()
        {
            if (stack.Count > 0) stack[stack.Count - 1].Exit();
            stack.Clear();
            context = null;
            input = null;

            if (markers != null) markers.Hide();
            if (preview != null) preview.Hide();
            if (overlay != null) overlay.Close();
        }

        void Finish()
        {
            Action callback = onFinished;
            onFinished = null;
            IsActive = false;
            callback?.Invoke();
        }
    }
}

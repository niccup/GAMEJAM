using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// The power-up draft between rounds. <see cref="Begin"/> takes the players in pick order (the caller decides it once:
    /// today the smallest share of the finished map first) and gives each of them exactly one turn, one after another. A
    /// draft starts with the whole pool; whatever a player takes is removed for everyone after them, and the next draft
    /// starts from the whole pool again. A turn starts with the <see cref="OfferStep"/> listing what is left; a power-up
    /// either applies at once or opens further
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
        [Tooltip("Needed for power-ups that work on ghosts (Mirror, Swap, Reset).")]
        [SerializeField] ReplayManager replay;
        [Tooltip("Needed by Reset, which adds a round to the game. Found in the scene if left empty.")]
        [SerializeField] RoundManager round;
        [Tooltip("Needed by Reset, which takes the removed round out of the scores. Found in the scene if left empty.")]
        [SerializeField] ScoreManager scores;
        [Tooltip("Gives the ghost preview the game's own trail settings (cell size, loop rules, opacity).")]
        [SerializeField] TerritorySystem territory;
        [SerializeField] PowerUpOverlay overlay;

        [Header("Offer")]
        [Tooltip("Every power-up a draft starts with, in the order they are shown. Add an asset here to offer something new.")]
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
        readonly List<PowerUp> remaining = new List<PowerUp>();
        readonly HashSet<PowerUp> takenForGame = new HashSet<PowerUp>(); // once-per-game power-ups that have been taken
        readonly List<SelectionStep> stack = new List<SelectionStep>();

        PowerUpContext context;
        int draftSize;
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
            if (round == null) round = FindFirstObjectByType<RoundManager>();
            if (scores == null) scores = FindFirstObjectByType<ScoreManager>();
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
        /// Starts a new draft: every player in <paramref name="choosers"/> gets exactly one turn, in the order given (the
        /// order is not recomputed). The pool starts full - nothing carries over from an earlier draft.
        /// <paramref name="finished"/> is called once the last turn is complete (at once if nobody can take anything).
        /// </summary>
        public void Begin(IReadOnlyList<Player> choosers, Action finished)
        {
            if (IsActive) return;

            waiting.Clear();
            foreach (Player player in choosers)
                if (player != null && !waiting.Contains(player)) waiting.Enqueue(player);

            // The pool starts full - except for the once-per-game power-ups that have already been taken.
            remaining.Clear();
            foreach (PowerUp powerUp in pool)
                if (powerUp != null && !takenForGame.Contains(powerUp)) remaining.Add(powerUp);

            draftSize = waiting.Count;

            onFinished = finished;
            IsActive = true;
            if (!StartNextTurn()) Finish();
        }

        /// <summary>Makes the once-per-game power-ups (Reset) available again. Call it when a brand new game starts.</summary>
        public void ResetGame() => takenForGame.Clear();

        void Update()
        {
            if (!IsActive || input == null || stack.Count == 0) return;

            NavCommand command = input.Poll();
            if (command != NavCommand.None) stack[stack.Count - 1].Handle(command);
        }

        void OnDisable() => EndTurn();

        // Starts the turn of the next waiting player. A player who cannot be asked (no input actions) or when no power-up
        // is left at all is passed over. False if no turn is left.
        bool StartNextTurn()
        {
            while (waiting.Count > 0)
            {
                if (remaining.Count == 0)
                {
                    waiting.Clear(); // the pool is used up: the rest of the draft has nothing to pick from
                    break;
                }

                Player chooser = waiting.Dequeue();
                SelectionInput chooserInput = SelectionInput.Create(chooser, moveActionName, actionName, inputDelay);
                if (chooserInput == null)
                {
                    Debug.LogWarning($"Player {chooser.Index + 1} has no '{actionName}' / '{moveActionName}' action - skipping their power-up selection.", this);
                    continue;
                }

                var turn = new PowerUpContext(chooser, spawner, map, round, scores, replay, overlay, markers, preview, Push, Back, Complete);
                turn.DraftPosition = draftSize - waiting.Count;
                turn.DraftSize = draftSize;
                var offer = new OfferStep(turn, remaining);
                if (!offer.HasAnyPowerUp) continue;

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
            // A taken power-up is gone for every later player of this draft (a skipped turn took nothing).
            if (context != null && context.Picked != null)
            {
                remaining.Remove(context.Picked);
                if (context.Picked.OncePerGame) takenForGame.Add(context.Picked); // gone for the rest of the game
            }
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

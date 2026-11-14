using System;

namespace GameJam
{
    /// <summary>
    /// What a <see cref="PowerUp"/> and its <see cref="SelectionStep"/>s can see and do during one chooser's turn: who is
    /// choosing, the game objects they may read or change (only when a power-up is finally applied) and the screen
    /// pieces they draw with. The flow methods are provided by the <see cref="PowerUpSelection"/>.
    /// </summary>
    public sealed class PowerUpContext
    {
        readonly Action<SelectionStep> push;
        readonly Action back;
        readonly Action complete;

        /// <summary>The player whose turn it is. Only this player's input is read.</summary>
        public Player Chooser { get; }

        public PlayerSpawner Spawner { get; }
        public MapView Map { get; }

        /// <summary>The saved movement of previous rounds. Null if the scene has no ReplayManager wired.</summary>
        public ReplayManager Replay { get; }

        public PowerUpOverlay Overlay { get; }

        /// <summary>Spawn point rings and the selection arrow drawn on the map.</summary>
        public SelectionMarkers Markers { get; }

        /// <summary>Fast replay of one movement on the map, completely separate from the game.</summary>
        public GhostPreview Preview { get; }

        public PowerUpContext(Player chooser, PlayerSpawner spawner, MapView map, ReplayManager replay, PowerUpOverlay overlay,
            SelectionMarkers markers, GhostPreview preview, Action<SelectionStep> push, Action back, Action complete)
        {
            Chooser = chooser;
            Spawner = spawner;
            Map = map;
            Replay = replay;
            Overlay = overlay;
            Markers = markers;
            Preview = preview;
            this.push = push;
            this.back = back;
            this.complete = complete;
        }

        /// <summary>This chooser's place in the draft, starting at 1 (1 = picks first).</summary>
        public int DraftPosition { get; set; } = 1;

        /// <summary>How many players pick in this draft.</summary>
        public int DraftSize { get; set; } = 1;

        /// <summary>
        /// The power-up the chooser is currently going for. Set when they confirm it in the offer; it only counts once the
        /// turn is completed (backing out of a multi-step power-up and taking another replaces it). The draft removes it
        /// from the pool for everybody after them.
        /// </summary>
        public PowerUp Picked { get; private set; }

        public void SetPicked(PowerUp powerUp) => Picked = powerUp;

        /// <summary>Opens <paramref name="step"/> on top of the current one.</summary>
        public void Push(SelectionStep step) => push(step);

        /// <summary>Returns to the previous step, discarding the current one. Nothing is applied.</summary>
        public void Back() => back();

        /// <summary>Ends the chooser's turn: the next waiting chooser gets theirs, or the next round starts.</summary>
        public void Complete() => complete();
    }
}

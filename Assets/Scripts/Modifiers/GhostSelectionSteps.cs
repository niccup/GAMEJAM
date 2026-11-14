using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GameJam
{
    /// <summary>Small helpers shared by the ghost-related steps.</summary>
    public static class GhostLabels
    {
        /// <summary>The spawned player with this index (null if that slot has no player, e.g. in keyboard test mode).</summary>
        public static Player PlayerFor(PowerUpContext context, int index)
        {
            foreach (Player player in context.Spawner.Players)
                if (player.Index == index) return player;
            return null;
        }

        /// <summary>What a ghost carries, for its chip: the power-ups it was recorded with and what has been done to its movement ("Speed Boost, mirrored").</summary>
        public static string Describe(MovementRecording recording)
        {
            var text = new StringBuilder();
            if (recording.OriginRound > 0) Append(text, $"round {recording.OriginRound}");
            foreach (PlayerModifier modifier in recording.Modifiers) Append(text, modifier.DisplayName);
            if (recording.IsMirrored) Append(text, "mirrored");
            if (recording.IsSwapped) Append(text, "swapped");
            return text.ToString();
        }

        static void Append(StringBuilder text, string part)
        {
            if (text.Length > 0) text.Append(", ");
            text.Append(part);
        }
    }

    /// <summary>
    /// A "choose a player" step for any power-up: the four spawn points are drawn on the map with an arrow over the
    /// selected player, left / right cycles through the players that are allowed, Action confirms (handing the player to
    /// the callback) and Back returns to the previous step. Players that are not allowed are dimmed and skipped.
    /// </summary>
    public sealed class SelectPlayerStep : SelectionStep
    {
        readonly Func<int, string> title;
        readonly string hint;
        readonly Action<Player> chosen;
        readonly bool[] available = new bool[PlayersConfig.MaxPlayers];
        int selected;

        /// <param name="title">Heading for the currently selected player index.</param>
        /// <param name="isSelectable">Which players can be picked.</param>
        /// <param name="chosen">Called when the chooser confirms a player. Usually pushes the next step.</param>
        public SelectPlayerStep(PowerUpContext context, Func<int, string> title, string hint, Func<Player, bool> isSelectable, Action<Player> chosen)
            : base(context)
        {
            this.title = title;
            this.hint = hint;
            this.chosen = chosen;

            foreach (Player player in context.Spawner.Players)
                if (isSelectable(player)) available[player.Index] = true;

            selected = Mathf.Max(0, NextAllowed(available.Length, -1, 1, i => available[i]));
        }

        public override void Enter()
        {
            Context.Overlay.SetDimmed(false);
            Context.Overlay.HideOptions();
            Context.Preview.Hide();
            Refresh();
        }

        public override void Exit() => Context.Markers.Hide();

        public override void Handle(NavCommand command)
        {
            switch (command)
            {
                case NavCommand.Previous:
                case NavCommand.Next:
                    selected = NextAllowed(available.Length, selected, command == NavCommand.Next ? 1 : -1, i => available[i]);
                    Refresh();
                    break;
                case NavCommand.Confirm:
                    Player player = GhostLabels.PlayerFor(Context, selected);
                    if (available[selected] && player != null) chosen(player);
                    break;
                case NavCommand.Back:
                    Context.Back();
                    break;
            }
        }

        void Refresh()
        {
            Context.Overlay.SetHeader(title(selected), hint, Context.Chooser.Color);
            Context.Markers.Show(available, selected);
        }
    }

    /// <summary>
    /// A "choose a ghost" step for any power-up: the chosen player's ghosts G1, G2 ... appear as chips (the ones that
    /// cannot be picked are greyed out and say why), left / right cycles through the selectable ones and the selected ghost
    /// is previewed at high speed on an empty map. Action confirms (handing the ghost number to the callback), Back returns
    /// to the previous step. Nothing is changed by the step itself.
    /// </summary>
    public sealed class SelectGhostStep : SelectionStep
    {
        readonly int player;
        readonly Func<int, string> title;
        readonly string hint;
        readonly Func<int, bool> isSelectable;
        readonly string notSelectableLabel;
        readonly float previewSpeed;
        readonly Action<int> chosen;
        readonly int count;
        int selected;

        /// <param name="player">Whose ghosts are shown.</param>
        /// <param name="title">Heading for the currently selected ghost index (0-based).</param>
        /// <param name="isSelectable">Which ghosts can be picked.</param>
        /// <param name="notSelectableLabel">Chip text of a ghost that cannot be picked, e.g. "already swapped".</param>
        /// <param name="previewSpeed">Recorded seconds per real second of the preview.</param>
        /// <param name="chosen">Called when the chooser confirms a ghost (0-based index).</param>
        public SelectGhostStep(PowerUpContext context, int player, Func<int, string> title, string hint, Func<int, bool> isSelectable,
            string notSelectableLabel, float previewSpeed, Action<int> chosen) : base(context)
        {
            this.player = player;
            this.title = title;
            this.hint = hint;
            this.isSelectable = isSelectable;
            this.notSelectableLabel = notSelectableLabel;
            this.previewSpeed = previewSpeed;
            this.chosen = chosen;

            count = context.Replay.GetSavedRoundCount(player);
            selected = Mathf.Max(0, NextAllowed(count, -1, 1, isSelectable));
        }

        public override void Enter()
        {
            Context.Overlay.SetDimmed(false);
            Context.Markers.Hide(); // the preview shows the ghost alone on an empty map

            var chips = new List<OptionData>(count);
            for (int ghost = 0; ghost < count; ghost++)
            {
                bool allowed = isSelectable(ghost);
                chips.Add(new OptionData
                {
                    Title = $"G{ghost + 1}",
                    Detail = allowed ? GhostLabels.Describe(Context.Replay.GetRecording(player, ghost)) : notSelectableLabel,
                    Enabled = allowed,
                });
            }
            Context.Overlay.ShowOptions(chips, selected, OptionsLayout.Chips);
            Refresh();
        }

        public override void Exit()
        {
            Context.Preview.Hide();
            Context.Overlay.HideOptions();
            Context.Markers.Hide();
        }

        public override void Handle(NavCommand command)
        {
            switch (command)
            {
                case NavCommand.Previous:
                case NavCommand.Next:
                    selected = NextAllowed(count, selected, command == NavCommand.Next ? 1 : -1, isSelectable);
                    Context.Overlay.SetSelected(selected);
                    Refresh();
                    break;
                case NavCommand.Confirm:
                    if (count > 0 && isSelectable(selected)) chosen(selected);
                    break;
                case NavCommand.Back:
                    Context.Back();
                    break;
            }
        }

        void Refresh()
        {
            if (count == 0) return;

            Context.Overlay.SetHeader(title(selected), hint, Context.Chooser.Color);
            Context.Preview.Show(Context.Replay.GetRecording(player, selected), GhostLabels.PlayerFor(Context, player), previewSpeed);
        }
    }
}

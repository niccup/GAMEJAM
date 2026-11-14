using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Reset step 1: the finished rounds as chips (left / right browses them). The selected round is previewed with all of
    /// its ghosts at once, on an empty map, so the chooser can see exactly what they are about to remove. Action selects the
    /// round (and opens the confirmation), Back returns to the offer. Nothing is changed by the step itself.
    /// </summary>
    public sealed class SelectRoundStep : SelectionStep
    {
        const string Hint = "LEFT / RIGHT: browse rounds     ACTION: select     DOWN: back";

        readonly ResetPowerUp reset;
        readonly List<int> rounds;
        int selected;

        public SelectRoundStep(PowerUpContext context, ResetPowerUp reset) : base(context)
        {
            this.reset = reset;
            rounds = context.Replay.GetSavedRounds();
        }

        public override void Enter()
        {
            Context.Overlay.SetDimmed(false);
            Context.Markers.Hide(); // the preview shows the round's ghosts alone on an empty map

            var chips = new List<OptionData>(rounds.Count);
            foreach (int round in rounds)
            {
                int ghosts = Context.Replay.GetRecordingsOfRound(round).Count;
                chips.Add(new OptionData { Title = $"R{round}", Detail = ghosts == 1 ? "1 ghost" : $"{ghosts} ghosts", Enabled = true });
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
                    selected = NextAllowed(rounds.Count, selected, command == NavCommand.Next ? 1 : -1, _ => true);
                    Context.Overlay.SetSelected(selected);
                    Refresh();
                    break;
                case NavCommand.Confirm:
                    if (rounds.Count > 0) Context.Push(new ResetConfirmStep(Context, reset, rounds[selected]));
                    break;
                case NavCommand.Back:
                    Context.Back();
                    break;
            }
        }

        void Refresh()
        {
            if (rounds.Count == 0) return;

            int round = rounds[selected];
            Context.Overlay.SetHeader($"Reset - browse rounds: round {round} ({selected + 1} of {rounds.Count})", Hint, Context.Chooser.Color);
            Context.Preview.Show(ResetPowerUp.CreateTracks(Context, round), reset.PreviewSpeed);
        }
    }

    /// <summary>
    /// Reset step 2: the chosen round is shown once more and has to be confirmed. Confirming removes the round for good
    /// (its ghosts never appear again, the game gets an extra round) and ends the chooser's turn. Back leaves everything as it was.
    /// </summary>
    public sealed class ResetConfirmStep : SelectionStep
    {
        const string Hint = "ACTION: remove this round for good     DOWN: back";

        readonly ResetPowerUp reset;
        readonly int round;

        public ResetConfirmStep(PowerUpContext context, ResetPowerUp reset, int round) : base(context)
        {
            this.reset = reset;
            this.round = round;
        }

        public override void Enter()
        {
            Context.Overlay.SetDimmed(false);
            Context.Overlay.HideOptions();
            Context.Overlay.SetHeader($"Reset - remove round {round}? Its ghosts will never return, and an extra round is played instead", Hint, Context.Chooser.Color);
            Context.Markers.Hide();
            Context.Preview.Show(ResetPowerUp.CreateTracks(Context, round), reset.PreviewSpeed);
        }

        public override void Exit()
        {
            Context.Preview.Hide();
            Context.Markers.Hide();
        }

        public override void Handle(NavCommand command)
        {
            if (command == NavCommand.Back)
            {
                Context.Back();
            }
            else if (command == NavCommand.Confirm)
            {
                if (reset.Apply(Context, round)) Context.Complete();
                else Context.Back(); // the round is no longer there: back to browsing, nothing was changed
            }
        }
    }
}

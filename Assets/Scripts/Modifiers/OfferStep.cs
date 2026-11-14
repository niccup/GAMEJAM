using System.Collections.Generic;

namespace GameJam
{
    /// <summary>
    /// The first step of every turn: the power-ups still left in the draft as cards, the chooser cycles with left / right
    /// and takes one with Action. Power-ups that this chooser cannot use right now are greyed out and skipped. A power-up
    /// that needs no further steps is applied at once and ends the turn; otherwise its first step is opened.
    /// <para>
    /// If nothing can be taken (every remaining power-up is unusable for this chooser) the step shows no cards and Action
    /// skips the turn, so a draft with fewer power-ups than players never gets stuck.
    /// </para>
    /// </summary>
    public sealed class OfferStep : SelectionStep
    {
        const string Hint = "LEFT / RIGHT: choose     ACTION: take it";
        const string SkipHint = "Nothing you can take is left     ACTION: skip";

        readonly List<PowerUp> offer = new List<PowerUp>();
        readonly List<bool> available = new List<bool>();
        int selected;

        public OfferStep(PowerUpContext context, IEnumerable<PowerUp> remaining) : base(context)
        {
            foreach (PowerUp powerUp in remaining)
            {
                if (powerUp == null) continue;
                offer.Add(powerUp);
                available.Add(powerUp.IsAvailable(context));
            }

            selected = System.Math.Max(0, NextAllowed(offer.Count, -1, 1, i => available[i]));
        }

        /// <summary>False if there is nothing the chooser could take (their turn is then a skip).</summary>
        public bool HasOptions => available.Contains(true);

        /// <summary>False if no power-up is left in the draft at all (nothing to show, not even a skip screen).</summary>
        public bool HasAnyPowerUp => offer.Count > 0;

        public override void Enter()
        {
            // Back from a longer power-up lands here again: whatever was picked before is no longer picked.
            Context.SetPicked(null);

            string title = $"Player {Context.Chooser.Index + 1}: choose a power-up   (pick {Context.DraftPosition} of {Context.DraftSize})";
            Context.Overlay.SetDimmed(true);

            if (!HasOptions)
            {
                Context.Overlay.SetHeader($"Player {Context.Chooser.Index + 1}: no power-up for you   (pick {Context.DraftPosition} of {Context.DraftSize})", SkipHint, Context.Chooser.Color);
                Context.Overlay.HideOptions();
                return;
            }

            Context.Overlay.SetHeader(title, Hint, Context.Chooser.Color);

            var cards = new List<OptionData>(offer.Count);
            for (int i = 0; i < offer.Count; i++)
                cards.Add(new OptionData { Icon = offer[i].Icon != null ? offer[i].Icon : Context.Overlay.PlaceholderIcon, Title = offer[i].DisplayName, Detail = offer[i].Description, Enabled = available[i] });
            Context.Overlay.ShowOptions(cards, selected, OptionsLayout.Cards);
        }

        public override void Exit() => Context.Overlay.HideOptions();

        public override void Handle(NavCommand command)
        {
            if (!HasOptions)
            {
                if (command == NavCommand.Confirm) Context.Complete();
                return;
            }

            switch (command)
            {
                case NavCommand.Previous:
                case NavCommand.Next:
                    selected = NextAllowed(offer.Count, selected, command == NavCommand.Next ? 1 : -1, i => available[i]);
                    Context.Overlay.SetSelected(selected);
                    break;
                case NavCommand.Confirm:
                    if (!available[selected]) break;
                    Context.SetPicked(offer[selected]);
                    SelectionStep next = offer[selected].Use(Context);
                    if (next == null) Context.Complete();
                    else Context.Push(next);
                    break;
            }
        }
    }
}

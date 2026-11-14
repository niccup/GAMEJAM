using System.Collections.Generic;

namespace GameJam
{
    /// <summary>
    /// The first step of every turn: the pool's power-ups as cards, the chooser cycles with left / right and takes one
    /// with Action. Power-ups that are not available right now are greyed out and skipped. A power-up that needs no
    /// further steps is applied at once and ends the turn; otherwise its first step is opened.
    /// </summary>
    public sealed class OfferStep : SelectionStep
    {
        const string Hint = "LEFT / RIGHT: choose     ACTION: take it";

        readonly List<PowerUp> offer = new List<PowerUp>();
        readonly List<bool> available = new List<bool>();
        int selected;

        public OfferStep(PowerUpContext context, IEnumerable<PowerUp> pool) : base(context)
        {
            foreach (PowerUp powerUp in pool)
            {
                if (powerUp == null) continue;
                offer.Add(powerUp);
                available.Add(powerUp.IsAvailable(context));
            }

            selected = System.Math.Max(0, NextAllowed(offer.Count, -1, 1, i => available[i]));
        }

        /// <summary>False if there is nothing the chooser could take (then their turn is skipped).</summary>
        public bool HasOptions => available.Contains(true);

        public override void Enter()
        {
            Context.Overlay.SetDimmed(true);
            Context.Overlay.SetHeader($"Player {Context.Chooser.Index + 1}: choose a power-up", Hint, Context.Chooser.Color);

            var cards = new List<OptionData>(offer.Count);
            for (int i = 0; i < offer.Count; i++)
                cards.Add(new OptionData { Icon = offer[i].Icon, Title = offer[i].DisplayName, Detail = offer[i].Description, Enabled = available[i] });
            Context.Overlay.ShowOptions(cards, selected, OptionsLayout.Cards);
        }

        public override void Exit() => Context.Overlay.HideOptions();

        public override void Handle(NavCommand command)
        {
            switch (command)
            {
                case NavCommand.Previous:
                case NavCommand.Next:
                    selected = NextAllowed(offer.Count, selected, command == NavCommand.Next ? 1 : -1, i => available[i]);
                    Context.Overlay.SetSelected(selected);
                    break;
                case NavCommand.Confirm:
                    if (!available[selected]) break;
                    SelectionStep next = offer[selected].Use(Context);
                    if (next == null) Context.Complete();
                    else Context.Push(next);
                    break;
            }
        }
    }
}

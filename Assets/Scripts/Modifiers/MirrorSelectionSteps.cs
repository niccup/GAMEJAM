using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GameJam
{
    /// <summary>Mirror step 1: choose whose ghost to mirror. The four spawn points are shown with an arrow over the selected player.</summary>
    public sealed class MirrorPlayerStep : SelectionStep
    {
        const string Hint = "LEFT / RIGHT: choose player     ACTION: confirm     DOWN: back";

        readonly MirrorPowerUp mirror;
        readonly bool[] available = new bool[PlayersConfig.MaxPlayers];
        int selected;

        public MirrorPlayerStep(PowerUpContext context, MirrorPowerUp mirror) : base(context)
        {
            this.mirror = mirror;
            foreach (Player player in context.Spawner.Players)
                if (MirrorPowerUp.IsValidTarget(context, player)) available[player.Index] = true;

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
                    if (available[selected]) Context.Push(new MirrorGhostStep(Context, mirror, selected));
                    break;
                case NavCommand.Back:
                    Context.Back();
                    break;
            }
        }

        void Refresh()
        {
            Context.Overlay.SetHeader($"Mirror - select a player: Player {selected + 1}", Hint, Context.Chooser.Color);
            Context.Markers.Show(available, selected);
        }
    }

    /// <summary>Mirror step 2: cycle through the chosen player's ghosts. The selected one is previewed on the map at high speed.</summary>
    public sealed class MirrorGhostStep : SelectionStep
    {
        readonly MirrorPowerUp mirror;
        readonly int target;
        readonly int count;
        int selected;

        public MirrorGhostStep(PowerUpContext context, MirrorPowerUp mirror, int target) : base(context)
        {
            this.mirror = mirror;
            this.target = target;
            count = context.Replay.GetSavedRoundCount(target);
            selected = Mathf.Max(0, NextAllowed(count, -1, 1, IsAllowed));
        }

        public override void Enter()
        {
            Context.Overlay.SetDimmed(false);
            Context.Markers.Hide(); // the preview shows the ghost alone on an empty map

            var chips = new List<OptionData>(count);
            for (int ghost = 0; ghost < count; ghost++)
            {
                MovementRecording recording = Context.Replay.GetRecording(target, ghost);
                chips.Add(new OptionData
                {
                    Title = $"G{ghost + 1}",
                    Detail = recording.IsMirrored ? "already mirrored" : Describe(recording),
                    Enabled = IsAllowed(ghost),
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
                    selected = NextAllowed(count, selected, command == NavCommand.Next ? 1 : -1, IsAllowed);
                    Context.Overlay.SetSelected(selected);
                    Refresh();
                    break;
                case NavCommand.Confirm:
                    if (!IsAllowed(selected)) break;
                    if (mirror.PostMirrorPreview)
                    {
                        Context.Push(new MirrorConfirmStep(Context, mirror, target, selected));
                    }
                    else
                    {
                        // No second preview: choosing the ghost is the confirmation.
                        mirror.Apply(Context, target, selected, MirrorPowerUp.CreateMirrored(Context, target, selected));
                        Context.Complete();
                    }
                    break;
                case NavCommand.Back:
                    Context.Back();
                    break;
            }
        }

        bool IsAllowed(int ghost) => MirrorPowerUp.IsMirrorable(Context, target, ghost);

        void Refresh()
        {
            string hint = mirror.PostMirrorPreview
                ? "LEFT / RIGHT: choose ghost     ACTION: confirm     DOWN: back to players"
                : "LEFT / RIGHT: choose ghost     ACTION: mirror this ghost for good     DOWN: back to players";
            Context.Overlay.SetHeader($"Mirror - Player {target + 1}: select a ghost (G{selected + 1})", hint, Context.Chooser.Color);
            Context.Preview.Show(Context.Replay.GetRecording(target, selected), PlayerFor(Context, target), mirror.PreviewSpeed);
        }

        // The power-ups the ghost carries, e.g. "Speed Boost".
        static string Describe(MovementRecording recording)
        {
            if (recording.Modifiers.Count == 0) return "";

            var text = new StringBuilder();
            foreach (PlayerModifier modifier in recording.Modifiers)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(modifier.DisplayName);
            }
            return text.ToString();
        }

        internal static Player PlayerFor(PowerUpContext context, int index)
        {
            foreach (Player player in context.Spawner.Players)
                if (player.Index == index) return player;
            return null;
        }
    }

    /// <summary>
    /// Mirror step 3: the mirrored movement of the chosen ghost is previewed; confirming replaces the ghost's saved
    /// movement with it for good and ends the chooser's turn. Back leaves everything as it was.
    /// </summary>
    public sealed class MirrorConfirmStep : SelectionStep
    {
        const string Hint = "ACTION: mirror this ghost for good     DOWN: back";

        readonly MirrorPowerUp mirror;
        readonly int target;
        readonly int ghost;
        readonly MovementRecording mirrored;

        public MirrorConfirmStep(PowerUpContext context, MirrorPowerUp mirror, int target, int ghost) : base(context)
        {
            this.mirror = mirror;
            this.target = target;
            this.ghost = ghost;
            mirrored = MirrorPowerUp.CreateMirrored(context, target, ghost); // computed here, applied only on confirmation
        }

        public override void Enter()
        {
            Context.Overlay.SetDimmed(false);
            Context.Overlay.HideOptions();
            Context.Overlay.SetHeader($"Mirror - Player {target + 1}, G{ghost + 1}: apply this movement?", Hint, Context.Chooser.Color);
            Context.Markers.Hide(); // the preview shows the ghost alone on an empty map
            Context.Preview.Show(mirrored, MirrorGhostStep.PlayerFor(Context, target), mirror.PreviewSpeed);
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
                mirror.Apply(Context, target, ghost, mirrored);
                Context.Complete();
            }
        }
    }
}

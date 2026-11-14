using UnityEngine;

namespace GameJam
{
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
            Context.Preview.Show(mirrored, GhostLabels.PlayerFor(Context, target), mirror.PreviewSpeed);
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

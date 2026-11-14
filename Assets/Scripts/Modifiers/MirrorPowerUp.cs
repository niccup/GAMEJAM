using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Mirror: lets the chooser pick one ghost of ANY player (their own included) and permanently mirror its movement (see
    /// <see cref="MovementRecording.CreateMirrored"/>). Picking runs in up to three steps - player, ghost, confirm (the shared <see cref="SelectPlayerStep"/> and <see cref="SelectGhostStep"/> plus <see cref="MirrorConfirmStep"/>) - and nothing
    /// changes until the last one is confirmed; then the ghost's saved recording is replaced by the mirrored one, which
    /// every later round replays. Only that one ghost is affected, and a ghost can be mirrored only once.
    /// </summary>
    [CreateAssetMenu(menuName = "GameJam/Power-Ups/Mirror", fileName = "Mirror")]
    public class MirrorPowerUp : PowerUp
    {
        [Header("Preview")]
        [Tooltip("After a ghost has been chosen, show its MIRRORED movement once more and ask for a final confirmation. " +
                 "Off: choosing the ghost applies the Mirror at once. The preview used to choose the ghost is always on.")]
        [SerializeField] bool postMirrorPreview = true;

        [Tooltip("How fast the ghost previews play, in recorded seconds per real second (30 = a two-minute round in four seconds). " +
                 "Only the previews use it; the real ghost replays and the game are not affected.")]
        [SerializeField, Min(0.1f)] float previewSpeed = 30f;

        public bool PostMirrorPreview => postMirrorPreview;
        public float PreviewSpeed => previewSpeed;

        /// <summary>Available while at least one player has a ghost that is not mirrored yet.</summary>
        public override bool IsAvailable(PowerUpContext context)
        {
            if (context.Replay == null || context.Map == null) return false;

            foreach (Player player in context.Spawner.Players)
                if (IsValidTarget(context, player)) return true;
            return false;
        }

        // Steps 1 and 2 are the shared player and ghost steps; step 3 (the mirrored preview) is MirrorConfirmStep.
        public override SelectionStep Use(PowerUpContext context) => new SelectPlayerStep(context,
            player => $"Mirror - select a player: Player {player + 1}",
            "LEFT / RIGHT: choose player     ACTION: confirm     DOWN: back",
            player => IsValidTarget(context, player),
            target => context.Push(new SelectGhostStep(context, target.Index,
                ghost => $"Mirror - Player {target.Index + 1}: select a ghost (G{ghost + 1})",
                postMirrorPreview
                    ? "LEFT / RIGHT: choose ghost     ACTION: confirm     DOWN: back to players"
                    : "LEFT / RIGHT: choose ghost     ACTION: mirror this ghost for good     DOWN: back to players",
                ghost => IsMirrorable(context, target.Index, ghost),
                "already mirrored",
                previewSpeed,
                ghost => OnGhostChosen(context, target.Index, ghost))));

        void OnGhostChosen(PowerUpContext context, int target, int ghost)
        {
            if (postMirrorPreview)
            {
                context.Push(new MirrorConfirmStep(context, this, target, ghost));
            }
            else
            {
                // No second preview: choosing the ghost is the confirmation.
                Apply(context, target, ghost, CreateMirrored(context, target, ghost));
                context.Complete();
            }
        }

        /// <summary>A player (the chooser included) with at least one ghost that can still be mirrored.</summary>
        public static bool IsValidTarget(PowerUpContext context, Player target) =>
            HasMirrorableGhost(context, target.Index);

        public static bool HasMirrorableGhost(PowerUpContext context, int player)
        {
            int count = context.Replay.GetSavedRoundCount(player);
            for (int ghost = 0; ghost < count; ghost++)
                if (IsMirrorable(context, player, ghost)) return true;
            return false;
        }

        /// <summary>A saved ghost that has not been mirrored yet (mirroring is applied once, to the stored movement).</summary>
        public static bool IsMirrorable(PowerUpContext context, int player, int ghost) =>
            !context.Replay.GetRecording(player, ghost).IsMirrored;

        /// <summary>Makes the mirrored recording the ghost's saved movement for good. Every later round replays it.</summary>
        public void Apply(PowerUpContext context, int player, int ghost, MovementRecording mirrored)
        {
            context.Replay.ReplaceRecording(player, ghost, mirrored);
            Debug.Log($"Player {context.Chooser.Index + 1} mirrored Player {player + 1}'s ghost G{ghost + 1}.", this);
        }

        /// <summary>The mirrored version of a saved ghost, for the preview and for the final apply. Does not change anything.</summary>
        public static MovementRecording CreateMirrored(PowerUpContext context, int player, int ghost)
        {
            // The characters' centres can reach the spawn corners, i.e. half the map minus the corner radius.
            Vector2 limits = context.Map.Size * 0.5f - Vector2.one * context.Map.CornerRadius;
            return context.Replay.GetRecording(player, ghost).CreateMirrored(context.Map.Center, limits);
        }
    }
}

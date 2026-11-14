using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// A previous-round character: replays one <see cref="MovementRecording"/> on the round clock. No input, no physics,
    /// nothing to collide with - it only follows the recorded path and carries a <see cref="TrailEmitter"/> that is
    /// registered with the <see cref="TerritorySystem"/> for the owning player, so its trail and captures are the
    /// player's own. Built from the owner's look (sprite, scale, trail radius) by <see cref="Create"/>.
    /// </summary>
    public class PlayerGhost : MonoBehaviour
    {
        MovementRecording recording;
        SpriteRenderer spriteRenderer;
        bool hideWhenFinished;

        public int PlayerIndex { get; private set; }

        /// <summary>The power-ups the character had when this path was recorded.</summary>
        public System.Collections.Generic.IReadOnlyList<PlayerModifier> Modifiers => recording.Modifiers;

        /// <summary>The stats those power-ups give (e.g. a Speed Boost shows up as a higher speed multiplier).</summary>
        public PlayerStats Stats => PlayerStats.From(recording.Modifiers);
        public TrailEmitter TrailEmitter { get; private set; }

        /// <summary>True once the round clock has passed the end of the recorded path.</summary>
        public bool Finished { get; private set; }

        /// <summary>
        /// True once an opponent's loop has caught this ghost in the current round (see <see cref="GhostElimination"/>). It is
        /// state of this ghost object only: it hides the ghost and stops its trail, but the <see cref="MovementRecording"/> is
        /// not touched, so the next round builds a fresh ghost from the unchanged history.
        /// </summary>
        public bool IsEliminated { get; private set; }

        /// <summary>True while the ghost is still replaying and shown in the arena - the only time a loop can catch it.</summary>
        public bool IsOnArena => !IsEliminated && !(Finished && hideWhenFinished);

        /// <summary>Takes the ghost out of the current round: it disappears and stops drawing. Nothing recorded changes.</summary>
        public void Eliminate()
        {
            if (IsEliminated) return;
            IsEliminated = true;
            TrailEmitter.Emitting = false;
            spriteRenderer.enabled = false;
        }

        /// <summary>Builds a ghost that looks like <paramref name="owner"/> but in <paramref name="color"/> and replays <paramref name="recording"/>.</summary>
        public static PlayerGhost Create(Player owner, MovementRecording recording, Color color, int sortingOrderOffset,
            bool hideWhenFinished, string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = owner.transform.localScale;

            var ghost = go.AddComponent<PlayerGhost>();
            ghost.PlayerIndex = owner.Index;
            ghost.recording = recording;
            ghost.hideWhenFinished = hideWhenFinished;

            SpriteRenderer source = owner.SpriteRenderer;
            ghost.spriteRenderer = go.AddComponent<SpriteRenderer>();
            if (source != null)
            {
                ghost.spriteRenderer.sprite = source.sprite;
                ghost.spriteRenderer.sharedMaterial = source.sharedMaterial;
                ghost.spriteRenderer.sortingLayerID = source.sortingLayerID;
                ghost.spriteRenderer.sortingOrder = source.sortingOrder + sortingOrderOffset;
            }
            ghost.spriteRenderer.color = color;

            // Same emitter as the owner's: same offset and thickness, so the ghost's trail looks exactly like the player's.
            TrailEmitter ownerEmitter = owner.TrailEmitter;
            var emitterGo = new GameObject("TrailEmitter");
            emitterGo.transform.SetParent(go.transform, false);
            if (ownerEmitter != null) emitterGo.transform.localPosition = ownerEmitter.transform.localPosition;
            ghost.TrailEmitter = emitterGo.AddComponent<TrailEmitter>();
            if (ownerEmitter != null) ghost.TrailEmitter.Radius = ownerEmitter.Radius;

            ghost.Apply(0f);
            return ghost;
        }

        /// <summary>Puts the ghost where the recorded character was at <paramref name="roundTime"/> seconds into the round.</summary>
        public void Apply(float roundTime)
        {
            if (!recording.Evaluate(roundTime, out Vector2 position, out bool emitting)) return;

            Finished = roundTime > recording.Duration;
            transform.position = new Vector3(position.x, position.y, transform.position.z);

            // After its path ends the ghost stops drawing (it would otherwise keep re-claiming the cells it stands on).
            // An eliminated ghost keeps following its recording (so it stays in sync) but neither shows nor draws.
            TrailEmitter.Emitting = emitting && !Finished && !IsEliminated;
            spriteRenderer.enabled = !(Finished && hideWhenFinished) && !IsEliminated;
        }
    }
}

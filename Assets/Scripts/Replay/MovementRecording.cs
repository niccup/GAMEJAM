using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// The movement of one character during one round: position samples against round time (seconds since the round
    /// started). <see cref="Evaluate"/> returns the position at any time by linear interpolation, so playback is exact
    /// at the sampled moments and follows the same straight segments in between, whatever the playback frame rate is.
    /// Plain data, no Unity objects.
    /// </summary>
    public sealed class MovementRecording
    {
        struct Sample
        {
            public float Time;
            public Vector2 Position;
            public bool Emitting;
        }

        readonly List<Sample> samples = new List<Sample>();

        /// <summary>
        /// The modifiers the character carried while this was recorded (a snapshot: modifiers won in later rounds are
        /// not added). The samples are stored against real round time, so a character that moved faster because of a
        /// Speed Boost is replayed at exactly that pace - no speed has to be re-applied on playback.
        /// </summary>
        public IReadOnlyList<PlayerModifier> Modifiers { get; }

        public MovementRecording(IEnumerable<PlayerModifier> modifiers = null)
        {
            Modifiers = modifiers == null ? System.Array.Empty<PlayerModifier>() : new List<PlayerModifier>(modifiers).AsReadOnly();
        }

        /// <summary>True for a recording produced by <see cref="CreateMirrored"/>. Its samples are the stored, final movement.</summary>
        public bool IsMirrored { get; private set; }

        /// <summary>
        /// True once this movement has taken part in a Swap. It belongs to the movement, so it stays with it through any
        /// later change (a Mirror keeps it) and a swapped ghost can never be swapped again.
        /// </summary>
        public bool IsSwapped { get; private set; }

        public int SampleCount => samples.Count;

        /// <summary>A copy of this movement (samples, timing, carried modifiers and flags) that is marked as swapped. The source is not changed.</summary>
        public MovementRecording MarkedAsSwapped()
        {
            var copy = new MovementRecording(Modifiers) { IsMirrored = IsMirrored, IsSwapped = true };
            copy.samples.AddRange(samples);
            return copy;
        }

        /// <summary>Reads one stored sample (0-based). Returns false if the index is out of range.</summary>
        public bool TryGetSample(int index, out float time, out Vector2 position, out bool emitting)
        {
            time = 0f;
            position = default;
            emitting = false;
            if (index < 0 || index >= samples.Count) return false;

            Sample sample = samples[index];
            time = sample.Time;
            position = sample.Position;
            emitting = sample.Emitting;
            return true;
        }

        /// <summary>
        /// A new recording with the movement directions mirrored by the corner the path starts in (the first sample), as
        /// seen from <paramref name="mapCenter"/>:
        /// top-left and bottom-right start - right &lt;-&gt; down and left &lt;-&gt; up;
        /// top-right and bottom-left start - right &lt;-&gt; up and left &lt;-&gt; down.
        /// Only the directions change: every sample keeps its time, so the timing, the sequence and the speed (also a
        /// Speed Boost) stay as recorded, and the carried <see cref="Modifiers"/> are kept. Each recorded step is turned
        /// and then walked from the start point, and like a character the path slides along the arena edge instead of
        /// leaving it: <paramref name="limits"/> are the half extents around <paramref name="mapCenter"/> the centre may reach.
        /// The result is stored as it is and is the baseline from then on - do not mirror it again. The source is not changed.
        /// </summary>
        public MovementRecording CreateMirrored(Vector2 mapCenter, Vector2 limits)
        {
            var result = new MovementRecording(Modifiers) { IsMirrored = true, IsSwapped = IsSwapped };
            if (samples.Count == 0) return result;

            Vector2 start = samples[0].Position;
            Vector2 fromCenter = start - mapCenter;
            bool swapAxes = fromCenter.x * fromCenter.y > 0f; // top-right or bottom-left corner

            result.samples.Add(samples[0]);
            Vector2 position = start;
            for (int i = 1; i < samples.Count; i++)
            {
                Vector2 step = samples[i].Position - samples[i - 1].Position;
                position += swapAxes ? new Vector2(step.y, step.x) : new Vector2(-step.y, -step.x);
                position.x = Mathf.Clamp(position.x, mapCenter.x - limits.x, mapCenter.x + limits.x);
                position.y = Mathf.Clamp(position.y, mapCenter.y - limits.y, mapCenter.y + limits.y);
                result.samples.Add(new Sample { Time = samples[i].Time, Position = position, Emitting = samples[i].Emitting });
            }
            return result;
        }

        /// <summary>Time of the last sample = how long the recording lasts.</summary>
        public float Duration => samples.Count == 0 ? 0f : samples[samples.Count - 1].Time;

        /// <summary>Appends a sample. Samples must come in increasing time order; older or equal times are ignored.</summary>
        public bool Add(float time, Vector2 position, bool emitting)
        {
            if (samples.Count > 0 && time <= samples[samples.Count - 1].Time) return false;
            samples.Add(new Sample { Time = time, Position = position, Emitting = emitting });
            return true;
        }

        /// <summary>
        /// Position at <paramref name="time"/>, clamped to the first/last sample outside the recorded range.
        /// <paramref name="emitting"/> is the trail state of the sample the time falls after. Returns false if empty.
        /// </summary>
        public bool Evaluate(float time, out Vector2 position, out bool emitting)
        {
            position = default;
            emitting = false;
            int count = samples.Count;
            if (count == 0) return false;

            if (time <= samples[0].Time)
            {
                position = samples[0].Position;
                emitting = samples[0].Emitting;
                return true;
            }

            if (time >= samples[count - 1].Time)
            {
                position = samples[count - 1].Position;
                emitting = samples[count - 1].Emitting;
                return true;
            }

            // Binary search for the last sample at or before `time`.
            int low = 0, high = count - 1;
            while (high - low > 1)
            {
                int mid = (low + high) >> 1;
                if (samples[mid].Time <= time) low = mid;
                else high = mid;
            }

            Sample a = samples[low], b = samples[high];
            position = Vector2.Lerp(a.Position, b.Position, Mathf.InverseLerp(a.Time, b.Time, time));
            emitting = a.Emitting;
            return true;
        }
    }
}

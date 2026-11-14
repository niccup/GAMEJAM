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

        public int SampleCount => samples.Count;

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

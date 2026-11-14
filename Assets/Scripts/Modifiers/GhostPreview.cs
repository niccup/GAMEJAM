using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameJam
{
    /// <summary>One ghost of a preview: its movement and the player whose colour and look it is drawn with.</summary>
    public readonly struct PreviewTrack
    {
        public readonly MovementRecording Recording;
        public readonly Player Owner;

        public PreviewTrack(MovementRecording recording, Player owner)
        {
            Recording = recording;
            Owner = owner;
        }
    }

    /// <summary>
    /// A fast, looping replay of one <see cref="MovementRecording"/> (a ghost) or of several at once (all ghosts of one
    /// round, see <see cref="PreviewTrack"/>) drawn on the map while the chooser looks at them.
    /// <para>
    /// The trail is made by the real thing: a <see cref="TerritoryEngine"/> with the game's own <see cref="TerritoryConfig"/>
    /// (cell size, loop rules, opacity) lays it onto a <see cref="TerritoryGrid"/> and a <see cref="TerritoryTexture"/> draws
    /// it, exactly like the live map, including captured loops. The only difference is that this grid belongs to the
    /// preview alone: it is not the game's grid, nothing is registered with the TerritorySystem, the ReplayManager or the
    /// players, so the preview cannot change any territory, trail, saved movement or other game state.
    /// </para>
    /// <para>
    /// While it plays, the map looks empty: an opaque cover that copies the map background (same sprite, tint, size and
    /// rounded corners) is drawn above everything on the map - the finished round's territory and trails, the players and
    /// their ghosts - and only the selected ghost and its trail are drawn above the cover. Nothing is hidden, disabled or
    /// moved, so there is nothing to restore: <see cref="Hide"/> removes the cover and the map is exactly as it was.
    /// </para>
    /// <para>
    /// With several ghosts the samples of all of them are fed in the order of their recorded time, so the trails overwrite
    /// each other the way they did in the round.
    /// </para>
    /// <para>
    /// The engine is fed every recorded sample in order (it connects them with gap-free lines, as it does in a round), so
    /// the drawn geometry is the recorded path whatever the playback speed is. The speed only decides how many samples are
    /// walked per second. If one frame would take longer than the frame budget the preview plays a little slower for that
    /// moment instead of skipping part of the path.
    /// </para>
    /// </summary>
    public class GhostPreview : MonoBehaviour
    {
        // One ghost being replayed. Holds a copy of the recording, so nothing here can reach the saved history.
        sealed class Track
        {
            public Vector2[] Positions;
            public float[] Times;
            public bool[] Emitting;
            public int Actor;
            public float Radius;
            public Vector2 EmitterOffset; // where the real ghost's trail emitter sits relative to the character
            public SpriteRenderer Marker;
            public int Segment;           // the path up to sample `Segment` has been laid
            public bool Connected;        // the engine's trail currently ends at the end of the processed path
            public Vector2 Head;

            public bool Done => Segment + 1 >= Times.Length;
        }

        // The preview is layered above the highest thing the map draws: the owner's sprite order + this, at least MinCoverOrder.
        const int MinCoverOrder = 30;
        const int OrderAbovePlayers = 10;

        float holdSeconds = 0.6f;
        float frameBudgetMilliseconds = 6f;

        MapView map;
        TerritoryConfig config;

        TerritoryGrid grid;
        TerritoryEngine engine;
        TerritoryPalette palette;
        TerritoryTexture view;
        SpriteRenderer cover;

        readonly List<Track> tracks = new List<Track>();
        readonly List<SpriteRenderer> markers = new List<SpriteRenderer>(); // one per track, reused between shows
        float duration;     // recorded length of the longest track
        float speed;
        float playTime;     // recorded seconds shown so far
        float hold;

        public void Configure(MapView map, TerritoryConfig config, float holdSeconds, float frameBudgetMilliseconds)
        {
            this.map = map;
            this.config = config;
            this.holdSeconds = Mathf.Max(0f, holdSeconds);
            this.frameBudgetMilliseconds = Mathf.Max(1f, frameBudgetMilliseconds);
        }

        /// <summary>
        /// Starts (or restarts) the preview of <paramref name="source"/> in the color and look of <paramref name="owner"/>.
        /// <paramref name="playbackSpeed"/> = recorded seconds per real second (30 = a two-minute round in four seconds).
        /// </summary>
        public void Show(MovementRecording source, Player owner, float playbackSpeed) =>
            Show(new[] { new PreviewTrack(source, owner) }, playbackSpeed);

        /// <summary>
        /// Starts (or restarts) the preview of several ghosts at once, each in the color and look of its owner. They share
        /// one map and one clock, so what is shown is what those ghosts did together.
        /// </summary>
        public void Show(IReadOnlyList<PreviewTrack> source, float playbackSpeed)
        {
            if (map == null || config == null)
            {
                Debug.LogWarning("The ghost preview needs the map and the territory config (assign the TerritorySystem on the PowerUpSelection).", this);
                return;
            }

            Build();

            // Forget the previous show: its actors leave the engine and its markers are hidden until reused.
            foreach (Track old in tracks) engine.RemoveActor(old.Actor);
            tracks.Clear();
            foreach (SpriteRenderer marker in markers) marker.gameObject.SetActive(false);

            speed = Mathf.Max(0.1f, playbackSpeed);
            duration = 0f;
            int layer = 0;
            int order = MinCoverOrder;

            foreach (PreviewTrack entry in source)
            {
                if (entry.Recording == null || entry.Owner == null || entry.Recording.SampleCount == 0) continue;

                Player owner = entry.Owner;
                int count = entry.Recording.SampleCount;
                var track = new Track { Positions = new Vector2[count], Times = new float[count], Emitting = new bool[count] };
                for (int i = 0; i < count; i++) entry.Recording.TryGetSample(i, out track.Times[i], out track.Positions[i], out track.Emitting[i]);
                duration = Mathf.Max(duration, track.Times[count - 1]);

                // Same color and trail thickness as the real ghost.
                palette.SetPlayerColor(owner.Index, owner.Color);
                track.Actor = engine.AddActor(owner.Index);
                TrailEmitter emitter = owner.TrailEmitter;
                track.Radius = emitter != null ? emitter.Radius : 0.1f;
                track.EmitterOffset = emitter != null ? (Vector2)(emitter.transform.position - owner.transform.position) : Vector2.zero;

                SpriteRenderer look = owner.SpriteRenderer;
                track.Marker = MarkerFor(tracks.Count);
                track.Marker.gameObject.SetActive(true);
                track.Marker.sprite = look != null ? look.sprite : null;
                track.Marker.transform.localScale = owner.transform.localScale;
                track.Marker.color = owner.Color;

                // Same sorting layer as the players, and an order above everything the map draws.
                if (tracks.Count == 0) layer = look != null ? look.sortingLayerID : 0;
                order = Mathf.Max(order, (look != null ? look.sortingOrder : 0) + OrderAbovePlayers);
                tracks.Add(track);
            }

            if (tracks.Count == 0)
            {
                Hide();
                return;
            }

            Layer(cover, layer, order);
            Layer(view.Renderer, layer, order + 1);
            for (int i = 0; i < tracks.Count; i++) Layer(tracks[i].Marker, layer, order + 2);
            CopyBackground();

            gameObject.SetActive(true);
            Restart();
        }

        public void Hide() => gameObject.SetActive(false);

        void Build()
        {
            if (engine != null) return;

            // The same construction as the TerritorySystem's, on a grid of our own.
            Vector2 size = map.Size;
            grid = new TerritoryGrid(map.Center - size * 0.5f, size, config.cellSize, PlayersConfig.MaxPlayers);
            engine = new TerritoryEngine(grid, config.minLoopLength);
            palette = new TerritoryPalette(config, PlayersConfig.MaxPlayers);

            var coverGo = new GameObject("Cover");
            coverGo.transform.SetParent(transform, false);
            cover = coverGo.AddComponent<SpriteRenderer>();
            cover.maskInteraction = SpriteMaskInteraction.VisibleInsideMask; // the map's rounded corners, like the background

            view = new TerritoryTexture(grid, palette, transform, MinCoverOrder + 1);
        }

        SpriteRenderer MarkerFor(int index)
        {
            while (markers.Count <= index)
            {
                var markerGo = new GameObject($"Marker {markers.Count + 1}");
                markerGo.transform.SetParent(transform, false);
                markers.Add(markerGo.AddComponent<SpriteRenderer>());
            }
            return markers[index];
        }

        static void Layer(SpriteRenderer renderer, int sortingLayerId, int sortingOrder)
        {
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
        }

        // The cover is a copy of the map background, so the empty map looks like the map itself: same art, tint and shape.
        void CopyBackground()
        {
            SpriteRenderer background = map.Background;
            if (background != null && background.sprite != null)
            {
                cover.sprite = background.sprite;
                cover.color = new Color(background.color.r, background.color.g, background.color.b, 1f);
                cover.transform.position = background.transform.position;
                cover.transform.localScale = background.transform.lossyScale;
            }
            else
            {
                cover.sprite = GeneratedSprites.Square;
                cover.color = new Color(0.16f, 0.2f, 0.26f, 1f);
                cover.transform.position = new Vector3(map.Center.x, map.Center.y, 0f);
                cover.transform.localScale = new Vector3(map.Size.x, map.Size.y, 1f);
            }
        }

        void Restart()
        {
            engine.Reset();
            foreach (Track track in tracks)
            {
                track.Segment = 0;
                track.Connected = false;
            }
            playTime = 0f;
            hold = 0f;
            AdvanceTo(0f);
            Present();
        }

        void Update()
        {
            if (tracks.Count == 0) return;

            float dt = Time.unscaledDeltaTime;
            if (hold > 0f)
            {
                hold -= dt;
                if (hold <= 0f) Restart();
                return;
            }

            playTime = Mathf.Min(playTime + dt * speed, duration);
            AdvanceTo(playTime);
            Present();

            // Every path fully laid: stay on the finished trails for a moment, then play again.
            if (AllDone()) hold = Mathf.Max(holdSeconds, 0.001f);
        }

        bool AllDone()
        {
            foreach (Track track in tracks)
                if (!track.Done) return false;
            return true;
        }

        void Present()
        {
            foreach (Track track in tracks) track.Marker.transform.position = new Vector3(track.Head.x, track.Head.y, 0f);
            view.Refresh();
        }

        // Lays the paths up to recorded time `target`: every whole segment of every track, oldest sample first across all
        // tracks, then the part of each track's current segment.
        void AdvanceTo(float target)
        {
            long deadline = Stopwatch.GetTimestamp() + (long)(frameBudgetMilliseconds * 0.001 * Stopwatch.Frequency);
            int worked = 0;

            while (true)
            {
                Track next = null;
                float nextTime = float.MaxValue;
                foreach (Track track in tracks)
                {
                    if (track.Done) continue;
                    float time = track.Times[track.Segment + 1];
                    if (time <= target && time < nextTime)
                    {
                        next = track;
                        nextTime = time;
                    }
                }
                if (next == null) break;

                LayTo(next, next.Segment, next.Positions[next.Segment + 1]);
                next.Segment++;

                // Out of time for this frame: slow down instead of skipping samples, which would cut corners.
                if ((++worked & 15) == 0 && Stopwatch.GetTimestamp() > deadline)
                {
                    playTime = nextTime;
                    target = nextTime;
                    break;
                }
            }

            foreach (Track track in tracks)
            {
                int count = track.Times.Length;
                if (!track.Done)
                {
                    float u = Mathf.InverseLerp(track.Times[track.Segment], track.Times[track.Segment + 1], target);
                    track.Head = Vector2.Lerp(track.Positions[track.Segment], track.Positions[track.Segment + 1], u);
                    LayTo(track, track.Segment, track.Head);
                }
                else
                {
                    track.Head = track.Positions[track.Segment];
                    if (count == 1) LayTo(track, 0, track.Head);
                }
            }
        }

        // Moves a track's trail along segment `from` -> `to`. The segment draws only if the sample it starts at was emitting,
        // as in a round; otherwise the trail is broken so no line is drawn across the gap.
        void LayTo(Track track, int from, Vector2 to)
        {
            if (!track.Emitting[from])
            {
                engine.BreakPath(track.Actor);
                track.Connected = false;
                return;
            }

            // The real ghost draws at its trail emitter, which sits at a fixed offset from the character.
            if (!track.Connected) engine.Step(track.Actor, track.Positions[from] + track.EmitterOffset, track.Radius);
            engine.Step(track.Actor, to + track.EmitterOffset, track.Radius);
            track.Connected = true;
        }

        void OnDestroy() => view?.Dispose();
    }
}

using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameJam
{
    /// <summary>
    /// A fast, looping replay of one <see cref="MovementRecording"/> drawn on the map while the chooser looks at a ghost.
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
    /// The engine is fed every recorded sample in order (it connects them with gap-free lines, as it does in a round), so
    /// the drawn geometry is the recorded path whatever the playback speed is. The speed only decides how many samples are
    /// walked per second. If one frame would take longer than the frame budget the preview plays a little slower for that
    /// moment instead of skipping part of the path.
    /// </para>
    /// </summary>
    public class GhostPreview : MonoBehaviour
    {
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
        SpriteRenderer marker;
        SpriteRenderer cover;

        // A copy of the recording, so nothing here can reach the saved history.
        Vector2[] positions;
        float[] times;
        bool[] emitting;

        int actor = -1;
        float radius;
        Vector2 emitterOffset; // where the real ghost's trail emitter sits relative to the character
        float speed;
        int segment;        // the path up to sample `segment` has been laid
        bool connected;     // the engine's trail currently ends at the end of the processed path
        float playTime;     // recorded seconds shown so far
        float hold;
        Vector2 head;

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
        public void Show(MovementRecording source, Player owner, float playbackSpeed)
        {
            if (map == null || config == null)
            {
                Debug.LogWarning("The ghost preview needs the map and the territory config (assign the TerritorySystem on the PowerUpSelection).", this);
                return;
            }

            Build();

            int count = source.SampleCount;
            positions = new Vector2[count];
            times = new float[count];
            emitting = new bool[count];
            for (int i = 0; i < count; i++) source.TryGetSample(i, out times[i], out positions[i], out emitting[i]);
            speed = Mathf.Max(0.1f, playbackSpeed);

            // Same color and trail thickness as the real ghost.
            palette.SetPlayerColor(owner.Index, owner.Color);
            if (actor >= 0) engine.RemoveActor(actor);
            actor = engine.AddActor(owner.Index);
            TrailEmitter emitter = owner.TrailEmitter;
            radius = emitter != null ? emitter.Radius : 0.1f;
            emitterOffset = emitter != null ? (Vector2)(emitter.transform.position - owner.transform.position) : Vector2.zero;

            SpriteRenderer look = owner.SpriteRenderer;
            marker.sprite = look != null ? look.sprite : null;
            marker.transform.localScale = owner.transform.localScale;
            marker.color = owner.Color;

            // Same sorting layer as the players, and an order above everything the map draws.
            int layer = look != null ? look.sortingLayerID : 0;
            int order = Mathf.Max(MinCoverOrder, (look != null ? look.sortingOrder : 0) + OrderAbovePlayers);
            Layer(cover, layer, order);
            Layer(view.Renderer, layer, order + 1);
            Layer(marker, layer, order + 2);
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

            var markerGo = new GameObject("Marker");
            markerGo.transform.SetParent(transform, false);
            marker = markerGo.AddComponent<SpriteRenderer>();
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
            segment = 0;
            connected = false;
            playTime = 0f;
            hold = 0f;
            AdvanceTo(0f);
            Present();
        }

        void Update()
        {
            if (times == null) return;

            float dt = Time.unscaledDeltaTime;
            if (hold > 0f)
            {
                hold -= dt;
                if (hold <= 0f) Restart();
                return;
            }

            playTime = Mathf.Min(playTime + dt * speed, times[times.Length - 1]);
            AdvanceTo(playTime);
            Present();

            // Path fully laid: stay on the finished trail for a moment, then play again.
            if (segment + 1 >= times.Length) hold = Mathf.Max(holdSeconds, 0.001f);
        }

        void Present()
        {
            marker.transform.position = new Vector3(head.x, head.y, 0f);
            view.Refresh();
        }

        // Lays the path up to recorded time `target`: every whole segment in order, then the part of the current one.
        void AdvanceTo(float target)
        {
            int count = times.Length;
            long deadline = Stopwatch.GetTimestamp() + (long)(frameBudgetMilliseconds * 0.001 * Stopwatch.Frequency);
            int worked = 0;

            while (segment + 1 < count && times[segment + 1] <= target)
            {
                LayTo(segment, positions[segment + 1]);
                segment++;

                // Out of time for this frame: slow down instead of skipping samples, which would cut corners.
                if ((++worked & 15) == 0 && Stopwatch.GetTimestamp() > deadline)
                {
                    playTime = times[segment];
                    head = positions[segment];
                    return;
                }
            }

            if (segment + 1 < count)
            {
                float u = Mathf.InverseLerp(times[segment], times[segment + 1], target);
                head = Vector2.Lerp(positions[segment], positions[segment + 1], u);
                LayTo(segment, head);
            }
            else
            {
                head = positions[segment];
                if (count == 1) LayTo(0, head);
            }
        }

        // Moves the trail along segment `from` -> `to`. The segment draws only if the sample it starts at was emitting,
        // as in a round; otherwise the trail is broken so no line is drawn across the gap.
        void LayTo(int from, Vector2 to)
        {
            if (!emitting[from])
            {
                engine.BreakPath(actor);
                connected = false;
                return;
            }

            // The real ghost draws at its trail emitter, which sits at a fixed offset from the character.
            if (!connected) engine.Step(actor, positions[from] + emitterOffset, radius);
            engine.Step(actor, to + emitterOffset, radius);
            connected = true;
        }

        void OnDestroy() => view?.Dispose();
    }
}

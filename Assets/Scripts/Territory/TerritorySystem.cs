using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Scene glue for the coloring mechanic (trails and captured areas are the same colored pixels). Creates the grid over the map, registers players as the spawner
    /// creates them, and every frame feeds each registered <see cref="TrailEmitter"/> position to the
    /// <see cref="TerritoryEngine"/>. Besides players' own emitters, other emitters (e.g. replayed ghosts) can be added
    /// with <see cref="AddEmitter"/>: they draw for a given player exactly like that player's character does.
    /// Contains no capture logic and no drawing - see <see cref="TerritoryRenderer"/> for the visuals.
    /// Subscribe to <see cref="LoopClosed"/> for scoring, sound, effects.
    /// </summary>
    public class TerritorySystem : MonoBehaviour
    {
        sealed class Source
        {
            public TrailEmitter Emitter;
            public int Actor;
        }

        [SerializeField] MapView map;
        [SerializeField] PlayerSpawner spawner;
        [SerializeField] TerritoryConfig config;

        readonly List<Source> sources = new List<Source>();
        readonly HashSet<TrailEmitter> registered = new HashSet<TrailEmitter>();

        /// <summary>The tuning (cell size, loop length, opacity ...) the territory runs with.</summary>
        public TerritoryConfig Config => config;

        public TerritoryEngine Engine { get; private set; }
        public TerritoryGrid Grid { get; private set; }
        public TerritoryPalette Palette { get; private set; }

        /// <summary>While false the map is frozen: nothing is colored and no loops can close (e.g. between rounds).</summary>
        public bool Running { get; set; } = true;

        /// <summary>Raised when a player closes a loop: (player index, cells captured).</summary>
        public event Action<int, int> LoopClosed;

        void Awake()
        {
            Vector2 size = map.Size;
            Grid = new TerritoryGrid(map.Center - size * 0.5f, size, config.cellSize, PlayersConfig.MaxPlayers);
            Engine = new TerritoryEngine(Grid, config.minLoopLength);
            Palette = new TerritoryPalette(config, PlayersConfig.MaxPlayers);
            Engine.LoopClosed += (player, cells) => LoopClosed?.Invoke(player, cells);
        }

        void OnEnable()
        {
            spawner.PlayerSpawned += Register;
            foreach (Player player in spawner.Players) Register(player);
        }

        void OnDisable() => spawner.PlayerSpawned -= Register;

        /// <summary>Wipes all colored cells (new round). Registered emitters stay registered.</summary>
        public void ResetTerritory() => Engine.Reset();

        /// <summary>
        /// Makes an emitter draw trail and capture territory for a player, in that player's color.
        /// Use it for additional characters of a player (e.g. ghosts). Does nothing if the emitter is already registered.
        /// </summary>
        public void AddEmitter(TrailEmitter emitter, int player)
        {
            if (emitter == null || !registered.Add(emitter)) return;
            sources.Add(new Source { Emitter = emitter, Actor = Engine.AddActor(player) });
        }

        /// <summary>Stops drawing for an emitter added with <see cref="AddEmitter"/>. Call it when the map is reset or before destroying the emitter.</summary>
        public void RemoveEmitter(TrailEmitter emitter)
        {
            if (emitter == null || !registered.Remove(emitter)) return;
            int index = sources.FindIndex(s => s.Emitter == emitter);
            Engine.RemoveActor(sources[index].Actor);
            sources.RemoveAt(index);
        }

        void Register(Player player)
        {
            if (registered.Contains(player.TrailEmitter)) return;
            if (player.TrailEmitter == null)
            {
                Debug.LogWarning($"{player.name} has no TrailEmitter child, so it leaves no trail. Run Tools > GameJam > Setup Scene.", player);
                return;
            }

            Palette.SetPlayerColor(player.Index, player.Color);
            AddEmitter(player.TrailEmitter, player.Index);
        }

        // LateUpdate: players move in physics and are interpolated, so their final position for the frame is known here.
        void LateUpdate()
        {
            if (!Running) return;

            foreach (Source source in sources)
            {
                TrailEmitter emitter = source.Emitter;
                if (emitter.Emitting) Engine.Step(source.Actor, emitter.Position, emitter.Radius);
                else Engine.BreakPath(source.Actor);
            }
        }
    }
}

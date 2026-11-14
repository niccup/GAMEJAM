using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Scene glue for the territory mechanic. Creates the grid over the map, registers players as the spawner
    /// creates them, and every frame feeds each player's <see cref="TrailEmitter"/> position to the
    /// <see cref="TerritoryEngine"/>. Contains no capture logic and no drawing - see <see cref="TerritoryRenderer"/>
    /// for the visuals. Subscribe to <see cref="LoopClosed"/> for scoring, sound, effects.
    /// </summary>
    public class TerritorySystem : MonoBehaviour
    {
        [SerializeField] MapView map;
        [SerializeField] PlayerSpawner spawner;
        [SerializeField] TerritoryConfig config;

        readonly List<Player> players = new List<Player>();

        public TerritoryEngine Engine { get; private set; }
        public TerritoryGrid Grid { get; private set; }
        public TerritoryPalette Palette { get; private set; }

        /// <summary>While false the territory is frozen: trails are not laid and no loops can close (e.g. between rounds).</summary>
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

        /// <summary>Wipes all territory and trails (new round).</summary>
        public void ResetTerritory() => Engine.Reset();

        void Register(Player player)
        {
            if (players.Contains(player)) return;
            if (player.TrailEmitter == null)
            {
                Debug.LogWarning($"{player.name} has no TrailEmitter child, so it leaves no trail. Run Tools > GameJam > Setup Scene.", player);
                return;
            }

            Palette.SetPlayerColor(player.Index, player.Color);
            players.Add(player);
        }

        // LateUpdate: players move in physics and are interpolated, so their final position for the frame is known here.
        void LateUpdate()
        {
            if (!Running) return;

            foreach (Player player in players)
            {
                TrailEmitter emitter = player.TrailEmitter;
                if (emitter.Emitting) Engine.Step(player.Index, emitter.Position, emitter.Radius);
                else Engine.BreakPath(player.Index);
            }
        }
    }
}

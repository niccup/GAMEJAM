using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace GameJam
{
    /// <summary>
    /// Spawns one player per slot of the <see cref="PlayersConfig"/> at the map's spawn points.
    /// Each player is created through <see cref="PlayerInput.Instantiate"/> with its slot's control scheme
    /// and paired with a free device that scheme needs (one keyboard, or the next unused gamepad).
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] MapView map;
        [SerializeField] PlayersConfig config;

        readonly List<Player> players = new List<Player>();
        readonly HashSet<InputDevice> claimedDevices = new HashSet<InputDevice>();

        public IReadOnlyList<Player> Players => players;

        /// <summary>The players configuration (slot colors etc.).</summary>
        public PlayersConfig Config => config;

        /// <summary>Raised once a new player is fully initialized (color, index, input).</summary>
        public event System.Action<Player> PlayerSpawned;

        void Start()
        {
            if (config.keyboardTestMode)
                Debug.Log($"Keyboard test mode: spawning {config.ActivePlayerCount} players on the keyboard, the rest stay inactive.", this);

            for (int i = 0; i < config.ActivePlayerCount; i++) Spawn(i);
        }

        public Player Spawn(int index)
        {
            Transform point = map.GetSpawnPoint(index);
            if (point == null)
            {
                Debug.LogError($"No spawn point for player {index + 1}.", this);
                return null;
            }

            GameObject prefab = config.playerPrefab.gameObject;
            string scheme = config.GetControlScheme(index);
            InputDevice[] devices = ClaimDevices(prefab.GetComponent<PlayerInput>().actions, scheme, index);

            WarnIfRadiusMismatch(index);

            PlayerInput input = PlayerInput.Instantiate(prefab, index, scheme, -1, devices);
            // Test mode: several players share the keyboard through different schemes, so never let one hop to another's scheme.
            if (config.keyboardTestMode) input.neverAutoSwitchControlSchemes = true;
            input.transform.SetPositionAndRotation(point.position, Quaternion.identity);
            input.transform.SetParent(transform, true);
            Physics2D.SyncTransforms(); // PlayerInput.Instantiate has no position argument; apply the teleport to physics now

            var player = input.GetComponent<Player>();
            player.Initialize(index, config);
            players.Add(player);
            PlayerSpawned?.Invoke(player);
            return player;
        }

        /// <summary>Moves every player back to its spawn point and stops it. Colors, input and settings stay as they are.</summary>
        public void ResetPlayers()
        {
            foreach (Player player in players)
            {
                Transform point = map.GetSpawnPoint(player.Index);
                if (point != null) player.ResetTo(point.position);
            }
        }

        /// <summary>Lets all players move (true) or freezes them in place (false).</summary>
        public void SetPlayersMovable(bool movable)
        {
            foreach (Player player in players) player.MovementEnabled = movable;
        }

        void WarnIfRadiusMismatch(int index)
        {
            if (index != 0) return;
            var circle = config.playerPrefab.GetComponent<CircleCollider2D>();
            if (circle == null) return;

            float radius = circle.radius * Mathf.Max(config.playerPrefab.transform.localScale.x, config.playerPrefab.transform.localScale.y);
            if (!Mathf.Approximately(radius, map.CornerRadius))
                Debug.LogWarning($"Player radius ({radius}) differs from map corner radius ({map.CornerRadius}); players won't fit the corners exactly.", this);
        }

        InputDevice[] ClaimDevices(InputActionAsset actions, string schemeName, int playerIndex)
        {
            var result = new List<InputDevice>();
            InputControlScheme? scheme = actions.FindControlScheme(schemeName);
            if (scheme == null)
            {
                Debug.LogWarning($"Control scheme '{schemeName}' not found for player {playerIndex + 1}.", this);
                return result.ToArray();
            }

            foreach (var requirement in scheme.Value.deviceRequirements)
            {
                InputDevice match = null;
                foreach (InputDevice device in InputSystem.devices)
                {
                    // Test mode shares the keyboard between players (each with its own control scheme).
                    if (!config.keyboardTestMode && claimedDevices.Contains(device)) continue;
                    if (!InputControlPath.Matches(requirement.controlPath, device)) continue;
                    match = device;
                    break;
                }

                if (match != null)
                {
                    claimedDevices.Add(match);
                    result.Add(match);
                }
                else if (!requirement.isOptional)
                {
                    Debug.LogWarning($"Player {playerIndex + 1}: no free device for '{requirement.controlPath}' ({schemeName}).", this);
                }
            }

            return result.ToArray();
        }
    }
}

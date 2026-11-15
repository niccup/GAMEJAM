using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace GameJam
{
    /// <summary>
    /// Works out which input devices a player gets for a control scheme (the keyboard, or the next gamepad nobody uses yet).
    /// The character selection and the <see cref="PlayerSpawner"/> use the same rule, so a player ends up on the same device
    /// in both. Nothing here is specific to one scheme: every player may use another device, or another scheme.
    /// </summary>
    public static class PlayerDevices
    {
        /// <summary>
        /// Finds a device for every requirement of the control scheme and adds it to <paramref name="claimed"/>. A device that is
        /// already in the set is skipped unless <paramref name="shareDevices"/> is true (several players on one keyboard).
        /// <paramref name="missingRequirement"/> is the control path (e.g. "&lt;Gamepad&gt;") of the first required device that
        /// could not be found, or null if the scheme is complete. The devices that were found are returned either way.
        /// </summary>
        public static InputDevice[] Claim(InputControlScheme scheme, ISet<InputDevice> claimed, bool shareDevices, out string missingRequirement)
        {
            missingRequirement = null;
            var result = new List<InputDevice>();

            foreach (var requirement in scheme.deviceRequirements)
            {
                InputDevice match = null;
                foreach (InputDevice device in InputSystem.devices)
                {
                    if (!shareDevices && claimed.Contains(device)) continue;
                    if (!InputControlPath.Matches(requirement.controlPath, device)) continue;
                    match = device;
                    break;
                }

                if (match != null)
                {
                    claimed.Add(match);
                    result.Add(match);
                }
                else if (!requirement.isOptional && missingRequirement == null)
                {
                    missingRequirement = requirement.controlPath;
                }
            }

            return result.ToArray();
        }
    }
}

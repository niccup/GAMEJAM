using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>Tools > GameJam > Keyboard Test Mode: one-click toggle for <see cref="PlayersConfig.keyboardTestMode"/>.</summary>
    public static class KeyboardTestModeMenu
    {
        const string MenuPath = "Tools/GameJam/Keyboard Test Mode";

        [MenuItem(MenuPath, priority = 101)]
        static void Toggle()
        {
            PlayersConfig config = FindConfig();
            if (config == null) return;

            Undo.RecordObject(config, "Toggle Keyboard Test Mode");
            config.keyboardTestMode = !config.keyboardTestMode;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log(config.keyboardTestMode
                ? "Keyboard test mode ON: Player 1 = WASD, Player 2 = arrow keys, players 3-4 inactive."
                : "Keyboard test mode OFF: normal player setup.");
        }

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            PlayersConfig config = FindConfig();
            Menu.SetChecked(MenuPath, config != null && config.keyboardTestMode);
            return config != null;
        }

        static PlayersConfig FindConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:PlayersConfig");
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<PlayersConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}

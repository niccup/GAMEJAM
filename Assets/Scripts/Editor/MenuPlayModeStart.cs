using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// Makes the Play button always start the game from the Main Menu, whichever scene is open, exactly like a build does
    /// (the menu is scene 0 of the Build Settings). Switch it off with Tools > GameJam > Always Play From Main Menu to test
    /// the game scene directly - the game then uses the colors of the Players Config, as it did before the menu existed.
    /// </summary>
    [InitializeOnLoad]
    public static class MenuPlayModeStart
    {
        const string PrefKey = "GameJam.AlwaysPlayFromMainMenu";
        const string MenuPath = "Tools/GameJam/Always Play From Main Menu";

        static MenuPlayModeStart() => EditorApplication.delayCall += Apply;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set
            {
                EditorPrefs.SetBool(PrefKey, value);
                Apply();
            }
        }

        /// <summary>Sets (or clears) the scene Play mode starts from, according to the setting.</summary>
        public static void Apply()
        {
            var menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuSceneSetup.MenuScenePath);
            if (Enabled)
            {
                if (menuScene != null && EditorSceneManager.playModeStartScene != menuScene) EditorSceneManager.playModeStartScene = menuScene;
            }
            else if (menuScene != null && EditorSceneManager.playModeStartScene == menuScene)
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }

        [MenuItem(MenuPath, priority = 102)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Debug.Log(Enabled
                ? "Play always starts from the Main Menu."
                : "Play starts from the open scene. Open the game scene and press Play to test it directly (player colors come from the Players Config).");
        }

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameJam.Editor
{
    /// <summary>
    /// One-click setup of the main menu: Tools > GameJam > Setup Main Menu. Creates the placeholder art and sounds, the
    /// Character Roster and Menu Audio Set assets, the prefabs and the MainMenu scene, and puts the menu first in the build.
    /// Safe to run repeatedly: anything that already exists - above all everything the artists have replaced - is left
    /// untouched. To rebuild the scene from scratch, delete Assets/Scenes/MainMenu.unity and run it again.
    /// </summary>
    public static class MenuSceneSetup
    {
        public const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        public const string GameScenePath = "Assets/Main.unity";

        const string MenuArtDir = "Assets/Art/Menu";
        const string SelectArtDir = "Assets/Art/CharacterSelect";
        const string AudioDir = "Assets/Audio/Menu";
        const string ConfigDir = "Assets/Config/Menu";
        const string PrefabDir = "Assets/Prefabs/Menu";
        const string InputPath = "Assets/Input/PlayerControls.inputactions";
        const string PlayersConfigPath = "Assets/Config/PlayersConfig.asset";

        static readonly Color Cream = new Color(0.97f, 0.91f, 0.75f);
        static readonly Color Ink = new Color(0.10f, 0.11f, 0.16f);

        // The Back buttons sit in the bottom-left corner, small enough not to cover the hint line of the character selection.
        static readonly Vector2 BackButtonPosition = new Vector2(56f, 44f);
        static readonly Vector2 BackButtonSize = new Vector2(290f, 86f);

        // Everything the menu is made of that is not code.
        sealed class MenuAssets
        {
            public Sprite mainBackground, authorsBackground, selectBackground;
            public Sprite buttonNormal, buttonHover, buttonPressed, buttonDisabled;
            public readonly Sprite[] portraits = new Sprite[CharacterRoster.CharacterCount];
            public readonly Sprite[] indicators = new Sprite[PlayersConfig.MaxPlayers];
            public Sprite arrow, slotFrame, slotHover, slotSelected, ownerBadge, colorChip;
            public Material greyMaterial;
            public CharacterRoster roster;
            public MenuAudioSet audioSet;
            public InputActionAsset actions;
            public PlayersConfig playersConfig;
            public GameObject buttonPrefab, slotPrefab, cursorPrefab;
            public PlayerInput inputPrefab;
        }

        [MenuItem("Tools/GameJam/Setup Main Menu")]
        public static void SetupMainMenu()
        {
            string report = Run();
            Debug.Log(report);
        }

        [MenuItem("Tools/GameJam/Open Artist Guide (Menu and Character Select)")]
        static void OpenArtistGuide()
        {
            const string guide = "Docs/ArtistGuide_MainMenu_and_CharacterSelect.md";
            if (File.Exists(guide)) EditorUtility.OpenWithDefaultApp(guide);
            else Debug.LogWarning("The artist guide was not found at " + guide + ".");
        }

        [MenuItem("Tools/GameJam/Rebuild Main Menu Scene (discards changes to it)")]
        static void RebuildMainMenuScene()
        {
            if (!EditorUtility.DisplayDialog("Rebuild the Main Menu scene?",
                    "This replaces Assets/Scenes/MainMenu.unity with a fresh one. Changes you made to that scene are lost; " +
                    "the prefabs, sprites and other assets are kept.", "Rebuild", "Cancel")) return;

            Debug.Log(Run(rebuildScene: true));
        }

        /// <summary>
        /// Creates whatever is missing and returns a short report of what was done. An existing MainMenu scene is only replaced
        /// when <paramref name="rebuildScene"/> is true.
        /// </summary>
        public static string Run(bool rebuildScene = false)
        {
            if (TMP_Settings.instance == null)
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh();
                if (TMP_Settings.instance == null) return "TextMesh Pro essentials are missing. Import them (Window > TextMeshPro > Import TMP Essential Resources) and run the setup again.";
            }

            foreach (string dir in new[] { MenuArtDir, SelectArtDir, AudioDir, ConfigDir, PrefabDir, "Assets/Scenes" }) EnsureFolder(dir);

            var log = new List<string>();
            Scene previousActive = SceneManager.GetActiveScene();
            Scene work = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(work);

            try
            {
                MenuAssets assets = CreateAssets(log);

                bool exists = File.Exists(MenuScenePath);
                if (exists && EditorSceneManager.GetSceneByPath(MenuScenePath).isLoaded)
                {
                    log.Add("Scene " + MenuScenePath + " is open in the editor - close it, then run the setup again.");
                }
                else if (exists && !rebuildScene)
                {
                    log.Add("Scene " + MenuScenePath + " already exists - left as it is.");
                }
                else
                {
                    BuildScene(assets);
                    EditorSceneManager.SaveScene(work, MenuScenePath);
                    log.Add((exists ? "Rebuilt scene " : "Created scene ") + MenuScenePath + ".");
                }
            }
            finally
            {
                SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(work, true);
            }

            EnsureBuildScenes(log);
            MenuPlayModeStart.Apply();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Main menu setup complete.\n - " + string.Join("\n - ", log);
        }

        // ---- Assets -------------------------------------------------------------------------------------------

        static MenuAssets CreateAssets(List<string> log)
        {
            var a = new MenuAssets();

            // Menu backgrounds: soft gradients, to be replaced by the artists' paintings.
            a.mainBackground = MenuPlaceholderArt.EnsureSprite(MenuArtDir + "/MainMenuBackground.png",
                () => MenuPlaceholderArt.Gradient(480, 270, new Color(0.20f, 0.27f, 0.50f), new Color(0.05f, 0.07f, 0.17f)));
            a.authorsBackground = MenuPlaceholderArt.EnsureSprite(MenuArtDir + "/AuthorsBackground.png",
                () => MenuPlaceholderArt.Gradient(480, 270, new Color(0.34f, 0.20f, 0.46f), new Color(0.09f, 0.05f, 0.17f)));
            a.selectBackground = MenuPlaceholderArt.EnsureSprite(SelectArtDir + "/CharacterSelectBackground.png",
                () => MenuPlaceholderArt.Gradient(480, 270, new Color(0.14f, 0.36f, 0.40f), new Color(0.04f, 0.10f, 0.15f)));

            // Button states. The three painted ones are already in the project; only the disabled state is missing.
            a.buttonNormal = MenuPlaceholderArt.EnsureSprite(MenuArtDir + "/button_normal.png",
                () => MenuPlaceholderArt.RoundedRect(584, 172, 40f, 10f, new Color(0.45f, 0.32f, 0.18f), new Color(0.12f, 0.09f, 0.05f)));
            a.buttonHover = MenuPlaceholderArt.EnsureSprite(MenuArtDir + "/button_hover.png",
                () => MenuPlaceholderArt.RoundedRect(584, 172, 40f, 10f, new Color(0.58f, 0.42f, 0.24f), new Color(0.12f, 0.09f, 0.05f)));
            a.buttonPressed = MenuPlaceholderArt.EnsureSprite(MenuArtDir + "/button_pressed.png",
                () => MenuPlaceholderArt.RoundedRect(584, 172, 40f, 10f, new Color(0.25f, 0.18f, 0.10f), new Color(0.12f, 0.09f, 0.05f)));
            a.buttonDisabled = MenuPlaceholderArt.EnsureSprite(MenuArtDir + "/button_disabled.png", () =>
            {
                // A grey, darker copy of the normal button, cut to the same rectangle.
                Rect r = a.buttonNormal.rect;
                Texture2D grey = MenuPlaceholderArt.GreyCopy(MenuArtDir + "/button_normal.png",
                    new RectInt((int)r.x, (int)r.y, (int)r.width, (int)r.height), 0.55f);
                return grey != null ? grey : MenuPlaceholderArt.RoundedRect(584, 172, 40f, 10f, new Color(0.3f, 0.3f, 0.3f), new Color(0.1f, 0.1f, 0.1f));
            });

            // Four placeholder characters with different silhouettes (so they stay apart even when greyed out).
            Color[] fills = { new Color(0.09f, 0.09f, 0.10f), new Color(0.90f, 0.16f, 0.16f), new Color(0.97f, 0.97f, 0.97f), new Color(1.00f, 0.84f, 0.12f) };
            for (int i = 0; i < a.portraits.Length; i++)
            {
                int shape = i;
                bool dark = i == 0;
                a.portraits[i] = MenuPlaceholderArt.EnsureSprite($"{SelectArtDir}/Character_{i + 1}.png",
                    () => MenuPlaceholderArt.Portrait(shape, fills[shape], dark ? new Color(0.78f, 0.78f, 0.82f) : new Color(0.10f, 0.10f, 0.14f),
                        dark ? new Color(0.9f, 0.9f, 0.92f) : new Color(0.08f, 0.08f, 0.1f)));
            }

            for (int i = 0; i < a.indicators.Length; i++)
                a.indicators[i] = MenuPlaceholderArt.EnsureSprite($"{SelectArtDir}/Indicator_P{i + 1}.png",
                    () => MenuPlaceholderArt.RoundedRect(160, 120, 34f, 9f, new Color(0.96f, 0.96f, 0.98f), Ink));

            a.arrow = MenuPlaceholderArt.EnsureSprite(SelectArtDir + "/NavigationArrow.png",
                () => MenuPlaceholderArt.ArrowDown(96, Color.white, Ink));
            a.slotFrame = MenuPlaceholderArt.EnsureSprite(SelectArtDir + "/SlotFrame.png",
                () => MenuPlaceholderArt.RoundedRect(128, 128, 26f, 4f, new Color(0.05f, 0.07f, 0.12f, 0.78f), new Color(0.78f, 0.82f, 0.92f, 0.9f)),
                new Vector4(34, 34, 34, 34));
            a.slotHover = MenuPlaceholderArt.EnsureSprite(SelectArtDir + "/SlotHighlight.png",
                () => MenuPlaceholderArt.RoundedRect(128, 128, 28f, 8f, new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.95f)),
                new Vector4(36, 36, 36, 36));
            a.slotSelected = MenuPlaceholderArt.EnsureSprite(SelectArtDir + "/SlotSelected.png",
                () => MenuPlaceholderArt.RoundedRect(128, 128, 28f, 12f, new Color(1f, 0.82f, 0.2f, 0f), new Color(1f, 0.82f, 0.2f, 1f)),
                new Vector4(40, 40, 40, 40));
            a.ownerBadge = MenuPlaceholderArt.EnsureSprite(SelectArtDir + "/OwnerBadge.png",
                () => MenuPlaceholderArt.RoundedRect(128, 72, 36f, 6f, new Color(0.08f, 0.09f, 0.14f, 0.96f), new Color(1f, 0.82f, 0.2f, 1f)),
                new Vector4(36, 36, 36, 36));
            a.colorChip = MenuPlaceholderArt.EnsureSprite(SelectArtDir + "/ColorChip.png",
                () => MenuPlaceholderArt.Circle(64, Color.white, Color.white, 0f));

            a.greyMaterial = EnsureGreyMaterial(SelectArtDir + "/UIGreyscale.mat", log);
            a.audioSet = EnsureAudio();
            a.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            a.playersConfig = AssetDatabase.LoadAssetAtPath<PlayersConfig>(PlayersConfigPath);
            a.roster = EnsureRoster(a);

            if (a.actions == null || a.actions.FindActionMap("Menu") == null)
                log.Add("WARNING: " + InputPath + " has no 'Menu' action map (Navigate / Confirm / Cancel). The character selection needs it.");
            if (a.playersConfig == null)
                log.Add("WARNING: " + PlayersConfigPath + " was not found. Run Tools > GameJam > Setup Scene first.");

            a.buttonPrefab = EnsureButtonPrefab(a);
            a.slotPrefab = EnsureSlotPrefab(a);
            a.cursorPrefab = EnsureCursorPrefab(a);
            a.inputPrefab = EnsureInputPrefab(a);
            log.Add("Assets, prefabs and placeholder art are in place.");
            return a;
        }

        static Material EnsureGreyMaterial(string path, List<string> log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("GameJam/UI/Desaturate");
            if (shader == null)
            {
                log.Add("WARNING: shader 'GameJam/UI/Desaturate' was not found, so taken characters are only tinted. Re-run the setup once Unity has imported Assets/Shaders/UIDesaturate.shader.");
                return null;
            }

            var material = new Material(shader) { name = "UIGreyscale" };
            material.SetFloat("_GreyAmount", 1f);
            material.SetFloat("_Darken", 0.3f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static MenuAudioSet EnsureAudio()
        {
            const float v = 0.5f;
            var set = LoadOrCreate<MenuAudioSet>(ConfigDir + "/MenuAudioSet.asset", out bool created);
            if (!created) return set;

            set.buttonHover.clip = MenuPlaceholderArt.EnsureSound(AudioDir + "/ButtonHover.wav", () => MenuPlaceholderArt.Note(920f, 0.07f, v * 0.7f));
            set.buttonClick.clip = MenuPlaceholderArt.EnsureSound(AudioDir + "/ButtonClick.wav", () => MenuPlaceholderArt.Join(
                MenuPlaceholderArt.Note(600f, 0.05f, v), MenuPlaceholderArt.Note(900f, 0.11f, v)));
            set.back.clip = MenuPlaceholderArt.EnsureSound(AudioDir + "/Back.wav", () => MenuPlaceholderArt.Join(
                MenuPlaceholderArt.Note(700f, 0.05f, v), MenuPlaceholderArt.Note(470f, 0.11f, v)));
            set.screenTransition.clip = MenuPlaceholderArt.EnsureSound(AudioDir + "/ScreenTransition.wav", () => MenuPlaceholderArt.Sweep(260f, 820f, 0.28f, v * 0.55f));
            set.characterMove.clip = MenuPlaceholderArt.EnsureSound(AudioDir + "/CharacterMove.wav", () => MenuPlaceholderArt.Note(540f, 0.06f, v * 0.8f));
            set.characterConfirm.clip = MenuPlaceholderArt.EnsureSound(AudioDir + "/CharacterConfirm.wav", () => MenuPlaceholderArt.Join(
                MenuPlaceholderArt.Note(523f, 0.07f, v), MenuPlaceholderArt.Note(659f, 0.07f, v), MenuPlaceholderArt.Note(784f, 0.2f, v)));
            set.characterCancel.clip = MenuPlaceholderArt.EnsureSound(AudioDir + "/CharacterCancel.wav", () => MenuPlaceholderArt.Join(
                MenuPlaceholderArt.Note(784f, 0.06f, v * 0.8f), MenuPlaceholderArt.Note(587f, 0.12f, v * 0.8f)));
            set.characterUnavailable.clip = MenuPlaceholderArt.EnsureSound(AudioDir + "/CharacterUnavailable.wav", () => MenuPlaceholderArt.Join(
                MenuPlaceholderArt.Buzz(150f, 0.11f, v * 0.9f), MenuPlaceholderArt.Silence(0.04f), MenuPlaceholderArt.Buzz(130f, 0.14f, v * 0.9f)));
            set.allPlayersReady.clip = MenuPlaceholderArt.EnsureSound(AudioDir + "/AllPlayersReady.wav", () => MenuPlaceholderArt.Join(
                MenuPlaceholderArt.Note(523f, 0.09f, v), MenuPlaceholderArt.Note(659f, 0.09f, v), MenuPlaceholderArt.Note(784f, 0.09f, v), MenuPlaceholderArt.Note(1047f, 0.35f, v)));
            set.gameStart.clip = MenuPlaceholderArt.EnsureSound(AudioDir + "/GameStart.wav", () => MenuPlaceholderArt.Sweep(220f, 990f, 0.4f, v * 0.7f));
            // No music yet: the slot is there, empty. Drop a clip into "Menu Music" to hear it.
            EditorUtility.SetDirty(set);
            return set;
        }

        static CharacterRoster EnsureRoster(MenuAssets a)
        {
            var roster = LoadOrCreate<CharacterRoster>(ConfigDir + "/CharacterRoster.asset", out bool created);
            if (!created) return roster;

            // Left to right: Black, Red, White, Yellow - the four player colors. Change a character's color with its dropdown.
            PlayerColorOption[] colors = { PlayerColorOption.Black, PlayerColorOption.Red, PlayerColorOption.White, PlayerColorOption.Yellow };
            roster.characters = new CharacterDefinition[CharacterRoster.CharacterCount];
            for (int i = 0; i < roster.characters.Length; i++)
                roster.characters[i] = new CharacterDefinition { color = colors[i], portrait = a.portraits[i] };
            EditorUtility.SetDirty(roster);
            return roster;
        }

        // ---- Prefabs ------------------------------------------------------------------------------------------

        static GameObject EnsureButtonPrefab(MenuAssets a)
        {
            string path = PrefabDir + "/MenuButton.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("MenuButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonFeedback));
            ((RectTransform)root.transform).sizeDelta = new Vector2(584f, 172f); // the size of the painted banner

            var image = root.GetComponent<Image>();
            image.sprite = a.buttonNormal;
            image.raycastTarget = true;

            var button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = a.buttonHover,
                pressedSprite = a.buttonPressed,
                selectedSprite = a.buttonHover,
                disabledSprite = a.buttonDisabled,
            };

            TextMeshProUGUI label = NewText("Label", root.transform, "Button", 56f, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(label.rectTransform);

            return SavePrefab(root, path);
        }

        static GameObject EnsureSlotPrefab(MenuAssets a)
        {
            string path = PrefabDir + "/CharacterSlot.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("CharacterSlot", typeof(RectTransform), typeof(CharacterSlotView));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(340f, 460f);

            Image frame = NewImage("Frame", rect, a.slotFrame, Color.white, Image.Type.Sliced);
            Stretch(frame.rectTransform);

            Image portrait = NewImage("Portrait", rect, null, Color.white);
            portrait.preserveAspect = true;
            Place(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(284f, 284f));

            Image swatch = NewImage("ColorSwatch", rect, a.colorChip, Color.white);
            Place(swatch.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 102f), new Vector2(44f, 44f));
            var rim = swatch.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(1f, 1f, 1f, 0.95f);
            rim.effectDistance = new Vector2(3f, -3f);

            TextMeshProUGUI nameLabel = NewText("NameLabel", rect, "NAME", 44f, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(nameLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(316f, 72f));

            Image hover = NewImage("Highlight", rect, a.slotHover, Color.white, Image.Type.Sliced);
            Stretch(hover.rectTransform);
            hover.rectTransform.offsetMin = new Vector2(-8f, -8f);
            hover.rectTransform.offsetMax = new Vector2(8f, 8f);
            hover.gameObject.SetActive(false);

            Image selected = NewImage("SelectedFrame", rect, a.slotSelected, Color.white, Image.Type.Sliced);
            Stretch(selected.rectTransform);
            selected.rectTransform.offsetMin = new Vector2(-8f, -8f);
            selected.rectTransform.offsetMax = new Vector2(8f, 8f);
            selected.gameObject.SetActive(false);

            Image badge = NewImage("OwnerBadge", rect, a.ownerBadge, Color.white, Image.Type.Sliced);
            Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(112f, 60f));
            TextMeshProUGUI ownerLabel = NewText("Label", badge.transform, "P1", 38f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(ownerLabel.rectTransform);
            badge.gameObject.SetActive(false);

            var anchor = new GameObject("CursorAnchor", typeof(RectTransform)).GetComponent<RectTransform>();
            anchor.SetParent(rect, false);
            Place(anchor, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(10f, 10f));

            var so = new SerializedObject(root.GetComponent<CharacterSlotView>());
            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.FindProperty("colorSwatch").objectReferenceValue = swatch;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("hoverIndicator").objectReferenceValue = hover.gameObject;
            so.FindProperty("selectedIndicator").objectReferenceValue = selected.gameObject;
            so.FindProperty("ownerBadge").objectReferenceValue = badge.gameObject;
            so.FindProperty("ownerLabel").objectReferenceValue = ownerLabel;
            so.FindProperty("cursorAnchor").objectReferenceValue = anchor;
            so.FindProperty("unavailableMaterial").objectReferenceValue = a.greyMaterial;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, path);
        }

        static GameObject EnsureCursorPrefab(MenuAssets a)
        {
            string path = PrefabDir + "/PlayerCursor.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("PlayerCursor", typeof(RectTransform), typeof(PlayerCursorView));
            var rect = (RectTransform)root.transform;
            rect.pivot = new Vector2(0.5f, 0f); // the arrow tip
            rect.sizeDelta = new Vector2(100f, 190f);

            Image arrow = NewImage("Arrow", rect, a.arrow, Color.white);
            Place(arrow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(56f, 56f));

            Image badge = NewImage("Badge", rect, a.indicators[0], Color.white);
            Place(badge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(80f, 60f));
            TextMeshProUGUI label = NewText("Label", badge.transform, "P1", 36f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(label.rectTransform);

            Image chip = NewImage("ColorChip", rect, a.colorChip, Color.white);
            Place(chip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(40f, 118f), new Vector2(34f, 34f));
            var rim = chip.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(1f, 1f, 1f, 0.95f);
            rim.effectDistance = new Vector2(2.5f, -2.5f);
            chip.gameObject.SetActive(false);

            var ready = new GameObject("ReadyMarker", typeof(RectTransform)).GetComponent<RectTransform>();
            ready.SetParent(rect, false);
            Place(ready, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 144f), new Vector2(150f, 40f));
            TextMeshProUGUI readyLabel = NewText("Label", ready, "READY", 30f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(readyLabel.rectTransform);
            ready.gameObject.SetActive(false);

            var so = new SerializedObject(root.GetComponent<PlayerCursorView>());
            so.FindProperty("badge").objectReferenceValue = badge;
            so.FindProperty("playerLabel").objectReferenceValue = label;
            so.FindProperty("arrow").objectReferenceValue = arrow;
            so.FindProperty("colorChip").objectReferenceValue = chip;
            so.FindProperty("readyMarker").objectReferenceValue = ready.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, path);
        }

        static PlayerInput EnsureInputPrefab(MenuAssets a)
        {
            string path = PrefabDir + "/MenuPlayerInput.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<PlayerInput>();

            var root = new GameObject("MenuPlayerInput", typeof(PlayerInput));
            var so = new SerializedObject(root.GetComponent<PlayerInput>());
            so.FindProperty("m_Actions").objectReferenceValue = a.actions;
            so.FindProperty("m_DefaultActionMap").stringValue = "Menu";
            so.FindProperty("m_NotificationBehavior").enumValueIndex = (int)PlayerNotifications.InvokeCSharpEvents;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, path).GetComponent<PlayerInput>();
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ---- The scene ----------------------------------------------------------------------------------------

        static void BuildScene(MenuAssets a)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
            camera.orthographic = true;
            camera.cullingMask = 0; // the menu is all UI; the camera only clears the screen and hears the sounds

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var uiModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();
            uiModule.deselectOnBackgroundClick = false; // clicking empty space must not drop the keyboard / controller focus

            var canvasObject = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRect = (RectTransform)canvasObject.transform;

            MenuScreen mainScreen = BuildMainMenuScreen(canvasRect, a, out Button start, out Button options, out Button authors);
            MenuScreen authorsScreen = BuildAuthorsScreen(canvasRect, a, out Button authorsBack);
            MenuScreen selectScreen = BuildCharacterSelectScreen(canvasRect, a, out Button selectBack, out CharacterSelectScreen select);
            CanvasGroup fader = BuildFader(canvasRect);

            // Audio: one object with the player and its two audio sources.
            var audioObject = new GameObject("MenuAudio", typeof(MenuAudioPlayer));
            var effects = audioObject.AddComponent<AudioSource>();
            var music = audioObject.AddComponent<AudioSource>();
            var audioSO = new SerializedObject(audioObject.GetComponent<MenuAudioPlayer>());
            audioSO.FindProperty("audioSet").objectReferenceValue = a.audioSet;
            audioSO.FindProperty("effectsSource").objectReferenceValue = effects;
            audioSO.FindProperty("musicSource").objectReferenceValue = music;
            audioSO.ApplyModifiedPropertiesWithoutUndo();

            // The flow of the whole menu.
            var flowObject = new GameObject("MenuFlow", typeof(MenuFlow));
            var flowSO = new SerializedObject(flowObject.GetComponent<MenuFlow>());
            flowSO.FindProperty("mainMenuScreen").objectReferenceValue = mainScreen;
            flowSO.FindProperty("authorsScreen").objectReferenceValue = authorsScreen;
            flowSO.FindProperty("characterSelectScreen").objectReferenceValue = selectScreen;
            flowSO.FindProperty("characterSelect").objectReferenceValue = select;
            flowSO.FindProperty("startGameButton").objectReferenceValue = start;
            flowSO.FindProperty("optionsButton").objectReferenceValue = options;
            flowSO.FindProperty("authorsButton").objectReferenceValue = authors;
            flowSO.FindProperty("authorsBackButton").objectReferenceValue = authorsBack;
            flowSO.FindProperty("characterSelectBackButton").objectReferenceValue = selectBack;
            flowSO.FindProperty("fader").objectReferenceValue = fader;
            flowSO.ApplyModifiedPropertiesWithoutUndo();

            // The Main Menu is what the scene shows; the other screens are switched on by the menu when needed.
            authorsScreen.gameObject.SetActive(false);
            selectScreen.gameObject.SetActive(false);
            fader.alpha = 0f; // visible in the editor; the menu fades in from black when it starts
            fader.blocksRaycasts = false;
        }

        static MenuScreen BuildMainMenuScreen(RectTransform canvas, MenuAssets a, out Button start, out Button options, out Button authors)
        {
            RectTransform screen = NewScreen("MainMenuScreen", canvas);
            NewBackground(screen, a.mainBackground);
            NewDecorations(screen);

            Image logo = NewImage("Logo", screen, null, Color.white);
            logo.preserveAspect = true;
            Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(900f, 300f));
            logo.gameObject.SetActive(false); // switch it on and drop your logo into Source Image

            TextMeshProUGUI title = NewText("Title", screen, "GAME TITLE", 150f, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1400f, 200f));

            start = NewButton(a, "StartGameButton", screen, "Start Game", new Vector2(0f, 40f), new Vector2(584f, 172f));
            options = NewButton(a, "OptionsButton", screen, "Options", new Vector2(0f, -160f), new Vector2(584f, 172f));
            authors = NewButton(a, "AuthorsButton", screen, "Authors", new Vector2(0f, -360f), new Vector2(584f, 172f));

            var menuScreen = screen.gameObject.AddComponent<MenuScreen>();
            var so = new SerializedObject(menuScreen);
            so.FindProperty("firstSelected").objectReferenceValue = start;
            so.ApplyModifiedPropertiesWithoutUndo();
            return menuScreen;
        }

        static MenuScreen BuildAuthorsScreen(RectTransform canvas, MenuAssets a, out Button back)
        {
            RectTransform screen = NewScreen("AuthorsScreen", canvas);
            NewBackground(screen, a.authorsBackground);
            NewDecorations(screen);

            TextMeshProUGUI title = NewText("Title", screen, "Authors", 120f, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 380f), new Vector2(1200f, 160f));

            for (int i = 0; i < 5; i++)
            {
                TextMeshProUGUI name = NewText($"Author_{i + 1}", screen, "Name Surname", 70f, Color.white, TextAlignmentOptions.Center);
                Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f - i * 105f), new Vector2(1200f, 100f));
            }

            back = NewButton(a, "BackButton", screen, "Back", BackButtonPosition, BackButtonSize, corner: true, backSound: true);

            var menuScreen = screen.gameObject.AddComponent<MenuScreen>();
            var so = new SerializedObject(menuScreen);
            so.FindProperty("firstSelected").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();
            return menuScreen;
        }

        static MenuScreen BuildCharacterSelectScreen(RectTransform canvas, MenuAssets a, out Button back, out CharacterSelectScreen select)
        {
            RectTransform screen = NewScreen("CharacterSelectScreen", canvas);
            NewBackground(screen, a.selectBackground);
            NewDecorations(screen);

            TextMeshProUGUI title = NewText("Title", screen, "Choose Your Character", 100f, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 455f), new Vector2(1600f, 140f));

            RectTransform slotsRoot = NewRect("Slots", screen);
            Stretch(slotsRoot);
            var slots = new CharacterSlotView[CharacterRoster.CharacterCount];
            var slotPositions = new Vector2[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(a.slotPrefab, slotsRoot);
                instance.name = $"CharacterSlot_{i + 1}";
                slotPositions[i] = new Vector2(-600f + 400f * i, -90f);
                var rect = (RectTransform)instance.transform;
                Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), slotPositions[i], rect.sizeDelta);
                slots[i] = instance.GetComponent<CharacterSlotView>();
                slots[i].Bind(a.roster.Get(i), a.roster.GetColor(i), a.roster.GetDisplayName(i)); // what the roster says, so the editor shows it
                RecordOverrides(instance);
            }

            // The indicators are drawn above the slots, each one at the sub-position of its player above its starting character.
            RectTransform cursorsRoot = NewRect("PlayerIndicators", screen);
            Stretch(cursorsRoot);
            var cursors = new PlayerCursorView[PlayersConfig.MaxPlayers];
            for (int p = 0; p < cursors.Length; p++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(a.cursorPrefab, cursorsRoot);
                instance.name = $"PlayerIndicator_P{p + 1}";
                var rect = (RectTransform)instance.transform;
                float offset = (p - (PlayersConfig.MaxPlayers - 1) * 0.5f) * 86f;
                Vector2 tip = slotPositions[p % slotPositions.Length] + new Vector2(offset, 244f);
                Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), tip, rect.sizeDelta);

                instance.transform.Find("Badge").GetComponent<Image>().sprite = a.indicators[p];
                instance.transform.Find("Badge/Label").GetComponent<TMP_Text>().text = $"P{p + 1}";
                cursors[p] = instance.GetComponent<PlayerCursorView>();
                RecordOverrides(instance);
            }

            TextMeshProUGUI status = NewText("StatusLabel", screen, "Players ready: 0 / 4", 54f, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -400f), new Vector2(1300f, 80f));
            TextMeshProUGUI hint = NewText("ControlsHint", screen, "Move: stick / A D / arrows      Confirm: A / Space / Enter      Undo: B / Shift      Back: Esc", 30f,
                new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -470f), new Vector2(1700f, 50f));

            back = NewButton(a, "BackButton", screen, "Back", BackButtonPosition, BackButtonSize, corner: true, backSound: true);

            var menuScreen = screen.gameObject.AddComponent<MenuScreen>();
            select = screen.gameObject.AddComponent<CharacterSelectScreen>();
            var so = new SerializedObject(select);
            so.FindProperty("roster").objectReferenceValue = a.roster;
            so.FindProperty("playersConfig").objectReferenceValue = a.playersConfig;
            so.FindProperty("menuInputPrefab").objectReferenceValue = a.inputPrefab;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            SerializedProperty slotList = so.FindProperty("slots");
            slotList.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) slotList.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            SerializedProperty cursorList = so.FindProperty("cursors");
            cursorList.arraySize = cursors.Length;
            for (int i = 0; i < cursors.Length; i++) cursorList.GetArrayElementAtIndex(i).objectReferenceValue = cursors[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return menuScreen;
        }

        static CanvasGroup BuildFader(RectTransform canvas)
        {
            Image black = NewImage("ScreenFader", canvas, null, Color.black, raycast: true);
            Stretch(black.rectTransform);
            return black.gameObject.AddComponent<CanvasGroup>();
        }

        // ---- UI building blocks -------------------------------------------------------------------------------

        static RectTransform NewScreen(string name, RectTransform parent)
        {
            RectTransform screen = NewRect(name, parent);
            Stretch(screen);
            return screen;
        }

        static void NewBackground(RectTransform screen, Sprite sprite)
        {
            Image background = NewImage("Background", screen, sprite, Color.white);
            Stretch(background.rectTransform);
            // Covers the whole screen without distorting the picture, whatever the aspect ratio of the window.
            var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
        }

        // An empty object for extra pictures: drag sprites in here and they sit on top of the background.
        static void NewDecorations(RectTransform screen) => Stretch(NewRect("Decorations", screen));

        static Button NewButton(MenuAssets a, string name, RectTransform parent, string text, Vector2 position, Vector2 size, bool corner = false, bool backSound = false)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(a.buttonPrefab, parent);
            instance.name = name;
            var rect = (RectTransform)instance.transform;
            if (corner) Place(rect, Vector2.zero, Vector2.zero, position, size);   // pinned to the bottom-left corner of the screen
            else Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);

            instance.GetComponentInChildren<TMP_Text>().text = text;
            if (corner) instance.GetComponentInChildren<TMP_Text>().fontSize = 34f;
            if (backSound)
            {
                var so = new SerializedObject(instance.GetComponent<MenuButtonFeedback>());
                so.FindProperty("clickSound").enumValueIndex = (int)MenuSound.Back;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            RecordOverrides(instance);
            return instance.GetComponent<Button>();
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type = Image.Type.Simple, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = type;
            image.raycastTarget = raycast;
            return image;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Changes made by script to an instance of a prefab are only kept as overrides when they are recorded.
        static void RecordOverrides(GameObject instance)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            foreach (Component component in instance.GetComponentsInChildren<Component>(true))
                if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        // ---- Project plumbing ---------------------------------------------------------------------------------

        static void EnsureBuildScenes(List<string> log)
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuScenePath, true), // the game always opens on the menu
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
                if (existing.path != MenuScenePath && existing.path != GameScenePath) scenes.Add(new EditorBuildSettingsScene(existing.path, false));

            EditorBuildSettings.scenes = scenes.ToArray();
            log.Add("Build Settings: 0 = MainMenu, 1 = Main (the game). Other scenes stay in the list but are switched off.");
        }

        static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (!created) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

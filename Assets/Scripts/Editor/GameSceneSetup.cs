using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameJam.Editor
{
    /// <summary>
    /// One-click setup: Tools > GameJam > Setup Scene. Creates missing config assets, placeholder art,
    /// the player prefab and the scene objects. Safe to run repeatedly - existing assets are never overwritten.
    /// </summary>
    public static class GameSceneSetup
    {
        const string ConfigDir = "Assets/Config";
        const string PrefabDir = "Assets/Prefabs";
        const string ArtDir = "Assets/Art";
        const string InputPath = "Assets/Input/PlayerControls.inputactions";

        [MenuItem("Tools/GameJam/Setup Scene")]
        public static void SetupScene()
        {
            EnsureFolder(ConfigDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(ArtDir + "/Map");
            EnsureFolder(ArtDir + "/Players");

            Sprite mapSprite = EnsureSprite(ArtDir + "/Map/Background.png", 64, circle: false);
            Sprite playerSprite = EnsureSprite(ArtDir + "/Players/Player.png", 64, circle: true);

            var mapConfig = EnsureAsset<MapConfig>(ConfigDir + "/MapConfig.asset", c =>
            {
                c.background = mapSprite;
                c.backgroundTint = new Color(0.16f, 0.2f, 0.26f);
            });

            Player prefab = EnsurePlayerPrefab(playerSprite);
            EnsureTrailEmitter(prefab);

            var territoryConfig = EnsureAsset<TerritoryConfig>(ConfigDir + "/TerritoryConfig.asset", _ => { });

            var playersConfig = EnsureAsset<PlayersConfig>(ConfigDir + "/PlayersConfig.asset", c =>
            {
                c.playerPrefab = prefab;
                c.slots = new[]
                {
                    new PlayerSlot { controlScheme = "Keyboard", color = new Color(0.93f, 0.3f, 0.3f) },
                    new PlayerSlot { controlScheme = "Gamepad", color = new Color(0.3f, 0.55f, 0.95f) },
                    new PlayerSlot { controlScheme = "Gamepad", color = new Color(0.35f, 0.8f, 0.4f) },
                    new PlayerSlot { controlScheme = "Gamepad", color = new Color(0.95f, 0.8f, 0.3f) },
                };
            });

            SetupSceneObjects(mapConfig, playersConfig, territoryConfig);
            AssetDatabase.SaveAssets();
            Debug.Log("GameJam scene setup complete.");
        }

        static void SetupSceneObjects(MapConfig mapConfig, PlayersConfig playersConfig, TerritoryConfig territoryConfig)
        {
            var map = Object.FindFirstObjectByType<MapView>();
            if (map == null)
            {
                var go = new GameObject("Map");
                Undo.RegisterCreatedObjectUndo(go, "Create Map");
                map = go.AddComponent<MapView>();
            }
            var mapSO = new SerializedObject(map);
            mapSO.FindProperty("config").objectReferenceValue = mapConfig;
            mapSO.ApplyModifiedPropertiesWithoutUndo();
            map.Rebuild(); // creates background, walls and spawn points

            var spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            if (spawner == null)
            {
                var go = new GameObject("Players");
                Undo.RegisterCreatedObjectUndo(go, "Create Players");
                spawner = go.AddComponent<PlayerSpawner>();
            }
            var spawnerSO = new SerializedObject(spawner);
            spawnerSO.FindProperty("map").objectReferenceValue = map;
            spawnerSO.FindProperty("config").objectReferenceValue = playersConfig;
            spawnerSO.ApplyModifiedPropertiesWithoutUndo();

            TerritorySystem territory = SetupTerritory(map, spawner, territoryConfig);
            SetupRound(territory, spawner);

            Camera cam = Camera.main;
            if (cam != null)
            {
                var fit = cam.GetComponent<MapCameraFit>();
                if (fit == null) fit = Undo.AddComponent<MapCameraFit>(cam.gameObject);
                var fitSO = new SerializedObject(fit);
                fitSO.FindProperty("map").objectReferenceValue = map;
                fitSO.ApplyModifiedPropertiesWithoutUndo();
                fit.Fit();
            }
            else
            {
                Debug.LogWarning("No camera tagged MainCamera found - add MapCameraFit to your camera manually.");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static TerritorySystem SetupTerritory(MapView map, PlayerSpawner spawner, TerritoryConfig territoryConfig)
        {
            var system = Object.FindFirstObjectByType<TerritorySystem>();
            if (system == null)
            {
                var go = new GameObject("Territory");
                Undo.RegisterCreatedObjectUndo(go, "Create Territory");
                system = go.AddComponent<TerritorySystem>();
            }
            var systemSO = new SerializedObject(system);
            systemSO.FindProperty("map").objectReferenceValue = map;
            systemSO.FindProperty("spawner").objectReferenceValue = spawner;
            systemSO.FindProperty("config").objectReferenceValue = territoryConfig;
            systemSO.ApplyModifiedPropertiesWithoutUndo();

            var view = system.GetComponent<TerritoryRenderer>();
            if (view == null) view = Undo.AddComponent<TerritoryRenderer>(system.gameObject);
            var viewSO = new SerializedObject(view);
            viewSO.FindProperty("system").objectReferenceValue = system;
            viewSO.FindProperty("config").objectReferenceValue = territoryConfig;
            viewSO.ApplyModifiedPropertiesWithoutUndo();
            return system;
        }

        // ---- round timer + end-of-round overlay ---------------------------------------------------------------

        static void SetupRound(TerritorySystem territory, PlayerSpawner spawner)
        {
            EnsureTmpEssentials();
            EnsureEventSystem();

            // Canvas with the overlay (end-of-round screen) and the timer HUD.
            var overlay = Object.FindFirstObjectByType<RoundEndOverlay>();
            if (overlay == null)
            {
                var canvasGo = new GameObject("RoundCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Round UI");
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                overlay = canvasGo.AddComponent<RoundEndOverlay>();
                BuildOverlay(overlay);
            }

            var round = Object.FindFirstObjectByType<RoundManager>();
            if (round == null)
            {
                var go = new GameObject("Round");
                Undo.RegisterCreatedObjectUndo(go, "Create Round");
                round = go.AddComponent<RoundManager>();
            }
            var roundSO = new SerializedObject(round);
            roundSO.FindProperty("territory").objectReferenceValue = territory;
            roundSO.FindProperty("spawner").objectReferenceValue = spawner;
            roundSO.FindProperty("overlay").objectReferenceValue = overlay;
            roundSO.ApplyModifiedPropertiesWithoutUndo();

            EnsureOverlayTitle(overlay);
            EnsureReplay(round, territory, spawner);
            ScoreManager scores = EnsureScores(round, spawner, overlay);
            EnsurePowerUps(round, spawner, overlay);

            var timer = Object.FindFirstObjectByType<RoundTimerDisplay>();
            if (timer == null)
            {
                TMP_Text label = CreateText("TimerLabel", overlay.transform, "2:00", 64, TextAlignmentOptions.Center);
                var rt = label.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -10f);
                rt.sizeDelta = new Vector2(300f, 80f);
                label.raycastTarget = false;
                timer = label.gameObject.AddComponent<RoundTimerDisplay>();
                var timerSO = new SerializedObject(timer);
                timerSO.FindProperty("round").objectReferenceValue = round;
                timerSO.FindProperty("label").objectReferenceValue = label;
                timerSO.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Ghost replays of previous rounds: records every player and replays the saved paths in later rounds.
        static void EnsureReplay(RoundManager round, TerritorySystem territory, PlayerSpawner spawner)
        {
            var replay = Object.FindFirstObjectByType<ReplayManager>();
            if (replay == null)
            {
                var go = new GameObject("Replay");
                Undo.RegisterCreatedObjectUndo(go, "Create Replay");
                replay = go.AddComponent<ReplayManager>();
            }
            var so = new SerializedObject(replay);
            so.FindProperty("round").objectReferenceValue = round;
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.FindProperty("territory").objectReferenceValue = territory;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Cumulative + per-map scores: the ScoreManager keeps them (also during play) and only the between-round screens display them.
        static ScoreManager EnsureScores(RoundManager round, PlayerSpawner spawner, RoundEndOverlay overlay)
        {
            var scores = Object.FindFirstObjectByType<ScoreManager>();
            if (scores == null)
            {
                var go = new GameObject("Score");
                Undo.RegisterCreatedObjectUndo(go, "Create Score");
                scores = go.AddComponent<ScoreManager>();
            }
            var so = new SerializedObject(scores);
            so.FindProperty("round").objectReferenceValue = round;
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.ApplyModifiedPropertiesWithoutUndo();

            var roundSO = new SerializedObject(round);
            roundSO.FindProperty("scores").objectReferenceValue = scores;
            roundSO.ApplyModifiedPropertiesWithoutUndo();

            // "Winner: Player 2" / "Tie: ..." line on the final screen, in the spot of the hidden Next Round button.
            var overlaySO = new SerializedObject(overlay);
            if (overlaySO.FindProperty("winnerLabel").objectReferenceValue == null)
            {
                var panelObj = overlaySO.FindProperty("panel").objectReferenceValue as GameObject;
                TMP_Text label = CreateText("WinnerLabel", panelObj != null ? panelObj.transform : overlay.transform,
                    "Winner: Player 1", 72, TextAlignmentOptions.Center);
                label.raycastTarget = false;
                RectTransform rt = label.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(1200f, 120f);
                label.gameObject.SetActive(false);
                overlaySO.FindProperty("winnerLabel").objectReferenceValue = label;
                overlaySO.ApplyModifiedPropertiesWithoutUndo();
            }
            return scores;
        }

        // Power-up contest between rounds: the Speed Boost asset, its selection screen and the logic that runs after "Next Round".
        static void EnsurePowerUps(RoundManager round, PlayerSpawner spawner, RoundEndOverlay roundOverlay)
        {
            EnsureFolder(ConfigDir + "/Modifiers");
            EnsureFolder(ArtDir + "/Modifiers");

            Sprite boltIcon = EnsureBoltSprite(ArtDir + "/Modifiers/SpeedBoost.png", 128);
            var speedBoost = EnsureAsset<SpeedBoostModifier>(ConfigDir + "/Modifiers/SpeedBoost.asset", m =>
            {
                var so = new SerializedObject(m);
                so.FindProperty("displayName").stringValue = "Speed Boost";
                so.FindProperty("description").stringValue = "+30% movement speed for the next round";
                so.FindProperty("icon").objectReferenceValue = boltIcon;
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            var view = Object.FindFirstObjectByType<PowerUpOverlay>();
            if (view == null)
            {
                view = Undo.AddComponent<PowerUpOverlay>(roundOverlay.gameObject);
                BuildPowerUpPanel(view, boltIcon);
            }

            var selection = Object.FindFirstObjectByType<PowerUpSelection>();
            if (selection == null)
            {
                var go = new GameObject("PowerUps");
                Undo.RegisterCreatedObjectUndo(go, "Create PowerUps");
                selection = go.AddComponent<PowerUpSelection>();
            }
            var selectionSO = new SerializedObject(selection);
            selectionSO.FindProperty("spawner").objectReferenceValue = spawner;
            selectionSO.FindProperty("overlay").objectReferenceValue = view;
            SerializedProperty pool = selectionSO.FindProperty("pool");
            if (pool.arraySize == 0)
            {
                pool.arraySize = 1;
                pool.GetArrayElementAtIndex(0).objectReferenceValue = speedBoost;
            }
            selectionSO.ApplyModifiedPropertiesWithoutUndo();

            var roundSO = new SerializedObject(round);
            roundSO.FindProperty("powerUps").objectReferenceValue = selection;
            roundSO.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildPowerUpPanel(PowerUpOverlay view, Sprite icon)
        {
            var panel = new GameObject("PowerUpPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(view.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            iconGo.transform.SetParent(panel.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0f, 90f);
            iconRt.sizeDelta = new Vector2(280f, 280f);
            var iconImage = iconGo.GetComponent<UnityEngine.UI.Image>();
            iconImage.sprite = icon;
            iconImage.raycastTarget = false;

            TMP_Text name = CenteredLabel("NameLabel", panel.transform, "Speed Boost", 80, new Vector2(0f, -110f), new Vector2(1200f, 110f));
            TMP_Text description = CenteredLabel("DescriptionLabel", panel.transform, "", 40, new Vector2(0f, -200f), new Vector2(1400f, 70f));
            TMP_Text prompt = CenteredLabel("PromptLabel", panel.transform, "", 52, new Vector2(0f, -300f), new Vector2(1400f, 90f));
            prompt.color = new Color(1f, 0.9f, 0.3f);

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("descriptionLabel").objectReferenceValue = description;
            so.FindProperty("promptLabel").objectReferenceValue = prompt;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
        }

        static TMP_Text CenteredLabel(string name, Transform parent, string text, float size, Vector2 position, Vector2 sizeDelta)
        {
            TMP_Text label = CreateText(name, parent, text, size, TextAlignmentOptions.Center);
            label.raycastTarget = false;
            RectTransform rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = sizeDelta;
            return label;
        }

        // Placeholder power-up icon: a yellow lightning bolt on a dark disc. Replace the PNG with real art any time.
        static Sprite EnsureBoltSprite(string path, int size)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Vector2[] bolt =
            {
                new Vector2(0.60f, 0.92f), new Vector2(0.24f, 0.48f), new Vector2(0.46f, 0.48f),
                new Vector2(0.38f, 0.08f), new Vector2(0.76f, 0.56f), new Vector2(0.53f, 0.56f),
            };

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                Color c = Color.clear;
                if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) <= r) c = new Color(0.12f, 0.14f, 0.2f);
                if (InsidePolygon(p, bolt)) c = new Color(1f, 0.85f, 0.2f);
                tex.SetPixel(x, y, c);
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static bool InsidePolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        // "Round 2 / 4" / "Final Results" heading above the Next Round button.
        static void EnsureOverlayTitle(RoundEndOverlay overlay)
        {
            var so = new SerializedObject(overlay);
            SerializedProperty title = so.FindProperty("titleLabel");
            if (title.objectReferenceValue != null) return;

            Transform panel = so.FindProperty("panel").objectReferenceValue != null
                ? ((GameObject)so.FindProperty("panel").objectReferenceValue).transform
                : overlay.transform;
            TMP_Text label = CreateText("TitleLabel", panel, "Round 1 / 4", 64, TextAlignmentOptions.Center);
            label.raycastTarget = false;
            RectTransform rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 140f);
            rt.sizeDelta = new Vector2(800f, 90f);

            title.objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildOverlay(RoundEndOverlay overlay)
        {
            // Full-screen dimmed panel on top of the map.
            var panel = new GameObject("RoundEndPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(overlay.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // Top: four percentages in a row.
            var row = new GameObject("Percentages", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(panel.transform, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -80f);
            rowRt.sizeDelta = new Vector2(-200f, 260f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            layout.spacing = 40f;

            var labels = new TMP_Text[PlayersConfig.MaxPlayers];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = CreateText($"Percent_P{i + 1}", row.transform, $"P{i + 1}\n0.0%", 56, TextAlignmentOptions.Center);
                labels[i].raycastTarget = false;
            }

            // Center: Next Round button.
            var buttonGo = new GameObject("NextRoundButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
            buttonGo.transform.SetParent(panel.transform, false);
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.anchorMin = buttonRt.anchorMax = buttonRt.pivot = new Vector2(0.5f, 0.5f);
            buttonRt.anchoredPosition = Vector2.zero;
            buttonRt.sizeDelta = new Vector2(520f, 140f);
            buttonGo.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.6f, 0.35f);
            var button = buttonGo.GetComponent<Button>();
            TMP_Text buttonLabel = CreateText("Label", buttonGo.transform, "Next Round", 56, TextAlignmentOptions.Center);
            buttonLabel.raycastTarget = false;
            Stretch(buttonLabel.rectTransform);

            var so = new SerializedObject(overlay);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("nextRoundButton").objectReferenceValue = button;
            SerializedProperty list = so.FindProperty("percentageLabels");
            list.arraySize = labels.Length;
            for (int i = 0; i < labels.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
        }

        static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            return tmp;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                Undo.RegisterCreatedObjectUndo(es.gameObject, "Create EventSystem");
            }
            // The project uses the new Input System only, so the legacy StandaloneInputModule would not work.
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Object.DestroyImmediate(legacy);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        static void EnsureTmpEssentials()
        {
            if (TMP_Settings.instance != null) return;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
            if (TMP_Settings.instance == null)
                Debug.LogWarning("TextMesh Pro essentials were just imported - run Tools > GameJam > Setup Scene once more so the new text objects pick up the default font.");
        }

        // The trail is drawn by a small circle on its own child object, independent of the player's sprite.
        static void EnsureTrailEmitter(Player prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponentInChildren<TrailEmitter>(true) != null) return;
                var child = new GameObject("TrailEmitter");
                child.transform.SetParent(root.transform, false);
                child.AddComponent<TrailEmitter>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Player EnsurePlayerPrefab(Sprite sprite)
        {
            string path = PrefabDir + "/Player.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Player>(path);
            if (existing != null) return existing;

            var go = new GameObject("Player");
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1f); // radius 0.5 * 0.5 = 0.25 = MapConfig.cornerRadius
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 10;
            go.AddComponent<Rigidbody2D>();
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            var player = go.AddComponent<Player>(); // pulls in PlayerInput + PlayerMovement
            var so = new SerializedObject(player);
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();

            var input = go.GetComponent<PlayerInput>();
            var inputSO = new SerializedObject(input);
            inputSO.FindProperty("m_Actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            inputSO.FindProperty("m_DefaultActionMap").stringValue = "Player";
            inputSO.FindProperty("m_NotificationBehavior").enumValueIndex = (int)PlayerNotifications.InvokeCSharpEvents;
            inputSO.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved.GetComponent<Player>();
        }

        static T EnsureAsset<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // White placeholder sprite, 1 world unit in size. Replace the PNG (or the sprite reference in the config) with real art.
        static Sprite EnsureSprite(string path, int size, bool circle)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool inside = !circle || Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) <= r;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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

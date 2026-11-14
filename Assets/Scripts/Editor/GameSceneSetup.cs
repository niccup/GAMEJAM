using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

            SetupTerritory(map, spawner, territoryConfig);

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

        static void SetupTerritory(MapView map, PlayerSpawner spawner, TerritoryConfig territoryConfig)
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

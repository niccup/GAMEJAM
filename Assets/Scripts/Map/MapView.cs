using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Builds the arena from a <see cref="MapConfig"/>: rounded background, boundary wall and
    /// one spawn point per player (one per corner). Place it at the center of the map.
    /// Use the component's context menu "Rebuild" to refresh after editing the config in Edit mode.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        // Corner of each player's spawn point as (x, y) signs: P1 top-left, P2 top-right, P3 bottom-left, P4 bottom-right.
        static readonly Vector2[] CornerSigns =
        {
            new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f),
        };

        [SerializeField] MapConfig config;
        [SerializeField] SpriteRenderer background;
        [SerializeField] SpawnPoint[] spawnPoints = new SpawnPoint[PlayersConfig.MaxPlayers];

        [Tooltip("Off (default): spawn points snap to the corner arcs on every rebuild. On: they keep the positions you give them.")]
        [SerializeField] bool placeSpawnPointsManually;

        public Vector2 Size => config != null ? config.size : Vector2.one;
        public float CornerRadius => config != null ? config.cornerRadius : 0f;
        public Vector2 Center => transform.position;
        /// <summary>The renderer of the map background (null before the map is built).</summary>
        public SpriteRenderer Background => background;

        public Bounds WorldBounds => new Bounds(transform.position, new Vector3(Size.x, Size.y, 0f));

        void Awake() => Rebuild();

        public Transform GetSpawnPoint(int playerIndex) =>
            playerIndex >= 0 && playerIndex < spawnPoints.Length && spawnPoints[playerIndex] != null
                ? spawnPoints[playerIndex].transform
                : null;

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (config == null)
            {
                Debug.LogWarning("MapView has no MapConfig assigned.", this);
                return;
            }

            BuildBackground();
            BuildMask();
            BuildWalls();
            BuildSpawnPoints();
        }

        void BuildBackground()
        {
            if (background == null)
            {
                var go = new GameObject("Background");
                go.transform.SetParent(transform, false);
                background = go.AddComponent<SpriteRenderer>();
            }

            background.sprite = config.background;
            background.color = config.backgroundTint;
            background.sortingOrder = config.backgroundSortingOrder;
            background.transform.localPosition = Vector3.zero;

            if (config.background != null)
            {
                Vector2 spriteSize = config.background.bounds.size;
                background.transform.localScale = new Vector3(config.size.x / spriteSize.x, config.size.y / spriteSize.y, 1f);
            }
        }

        void BuildWalls()
        {
            Transform wallsRoot = transform.Find("Walls");
            if (wallsRoot == null)
            {
                wallsRoot = new GameObject("Walls").transform;
                wallsRoot.SetParent(transform, false);
            }

            // Older versions used box colliders; the rounded wall is a single edge loop.
            foreach (var box in wallsRoot.GetComponents<BoxCollider2D>()) DestroyObject(box);

            var edge = wallsRoot.GetComponent<EdgeCollider2D>();
            if (edge == null) edge = wallsRoot.gameObject.AddComponent<EdgeCollider2D>();

            // The edge is thickened by edgeRadius on both sides, so trace the loop outward by that amount:
            // the inner face of the wall then follows the exact rounded rectangle.
            float thickness = config.wallThickness;
            edge.edgeRadius = thickness;
            edge.points = RoundedRectLoop(config.size * 0.5f + Vector2.one * thickness, config.cornerRadius + thickness, 16);
        }

        // Closed counter-clockwise loop around a rounded rectangle with the given half extents and corner radius.
        static Vector2[] RoundedRectLoop(Vector2 half, float radius, int segmentsPerCorner)
        {
            Vector2 center = new Vector2(half.x - radius, half.y - radius);
            var points = new List<Vector2>();
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 sign = new Vector2(corner == 0 || corner == 3 ? 1f : -1f, corner < 2 ? 1f : -1f);
                for (int i = 0; i <= segmentsPerCorner; i++)
                {
                    float angle = (corner * 90f + 90f * i / segmentsPerCorner) * Mathf.Deg2Rad;
                    points.Add(new Vector2(sign.x * center.x + radius * Mathf.Cos(angle), sign.y * center.y + radius * Mathf.Sin(angle)));
                }
            }
            points.Add(points[0]);
            return points.ToArray();
        }

        void BuildMask()
        {
            Transform maskTransform = transform.Find("Mask");
            if (maskTransform == null)
            {
                maskTransform = new GameObject("Mask").transform;
                maskTransform.SetParent(transform, false);
            }

            var mask = maskTransform.GetComponent<SpriteMask>();
            if (mask == null) mask = maskTransform.gameObject.AddComponent<SpriteMask>();

            mask.sprite = CreateRoundedRectSprite(config.size, config.cornerRadius);
            mask.alphaCutoff = 0.5f;
            background.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        // Procedural alpha-only sprite shaped like the map, so any background art gets rounded corners.
        static Sprite CreateRoundedRectSprite(Vector2 size, float radius)
        {
            const float pixelsPerUnit = 96f;
            int w = Mathf.Max(1, Mathf.RoundToInt(size.x * pixelsPerUnit));
            int h = Mathf.Max(1, Mathf.RoundToInt(size.y * pixelsPerUnit));
            var pixels = new byte[w * h];
            Vector2 half = size * 0.5f;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2((x + 0.5f) / pixelsPerUnit - half.x, (y + 0.5f) / pixelsPerUnit - half.y);
                var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (half - Vector2.one * radius);
                float d = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                pixels[y * w + x] = (byte)(Mathf.Clamp01(0.5f - d * pixelsPerUnit) * 255f);
            }

            var texture = new Texture2D(w, h, TextureFormat.Alpha8, false) { filterMode = FilterMode.Bilinear, name = "MapMask" };
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        static void DestroyObject(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        void BuildSpawnPoints()
        {
            if (spawnPoints == null || spawnPoints.Length != PlayersConfig.MaxPlayers)
                System.Array.Resize(ref spawnPoints, PlayersConfig.MaxPlayers);

            // Spawn at the center of each corner arc: a player whose radius equals the corner radius fits the corner exactly.
            Vector2 half = config.size * 0.5f - Vector2.one * config.cornerRadius;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null)
                {
                    var go = new GameObject($"SpawnPoint_P{i + 1}");
                    go.transform.SetParent(transform, false);
                    spawnPoints[i] = go.AddComponent<SpawnPoint>();
                    spawnPoints[i].PlayerIndex = i;
                    go.transform.localPosition = LocalCornerPosition(i, half);
                }
                else if (!placeSpawnPointsManually)
                {
                    spawnPoints[i].transform.localPosition = LocalCornerPosition(i, half);
                }
            }
        }

        static Vector3 LocalCornerPosition(int index, Vector2 half) =>
            new Vector3(CornerSigns[index].x * half.x, CornerSigns[index].y * half.y, 0f);
    }
}

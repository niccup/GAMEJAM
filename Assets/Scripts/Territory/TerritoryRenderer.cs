using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Draws the <see cref="TerritoryGrid"/>: one texture pixel per cell in the color of whoever
    /// holds it (trail on top of territory; both share the player's color). Purely a view - replace or extend it (shaders, outlines,
    /// animations) without touching the mechanics. Runs after <see cref="TerritorySystem"/> in the frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class TerritoryRenderer : MonoBehaviour
    {
        [SerializeField] TerritorySystem system;
        [SerializeField] TerritoryConfig config;

        TerritoryGrid grid;
        Texture2D texture;
        Color32[] pixels;
        int drawnVersion = -1;

        void Start()
        {
            grid = system.Grid;

            texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                name = "TerritoryTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[grid.CellCount];

            // One pixel per cell: pixelsPerUnit = 1 / cellSize makes the sprite span the grid exactly.
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, grid.Width, grid.Height), Vector2.zero, 1f / grid.CellSize);
            sprite.name = "TerritorySprite";

            var view = new GameObject("TerritoryView");
            view.transform.SetParent(transform, false);
            view.transform.position = grid.Origin;

            var spriteRenderer = view.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = config.sortingOrder;
            spriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask; // keep the map's rounded corners
        }

        void LateUpdate()
        {
            if (grid == null || grid.Version == drawnVersion) return;
            drawnVersion = grid.Version;

            TerritoryPalette palette = system.Palette;
            for (int i = 0; i < pixels.Length; i++)
            {
                int colorOwner = grid.GetColorOwner(i);
                pixels[i] = colorOwner != TerritoryGrid.None ? palette.GetColor(colorOwner) : default;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }
    }
}

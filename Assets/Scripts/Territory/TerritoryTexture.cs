using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Draws a <see cref="TerritoryGrid"/> as one texture pixel per cell in the color the cell shows. This is the drawing
    /// code of the real map (<see cref="TerritoryRenderer"/>), kept in one place so anything that shows a grid - the live
    /// map or a preview on a grid of its own - looks exactly the same. Call <see cref="Refresh"/> every frame; it only
    /// repaints when the grid has changed.
    /// </summary>
    public sealed class TerritoryTexture
    {
        readonly TerritoryGrid grid;
        readonly TerritoryPalette palette;
        readonly Texture2D texture;
        readonly Sprite sprite;
        readonly Color32[] pixels;
        int drawnVersion = -1;

        /// <summary>The object carrying the sprite. Child of the parent given to the constructor.</summary>
        public GameObject View { get; }

        /// <summary>The sprite renderer drawing the grid, e.g. to change its sorting layer.</summary>
        public SpriteRenderer Renderer { get; }

        public TerritoryTexture(TerritoryGrid grid, TerritoryPalette palette, Transform parent, int sortingOrder)
        {
            this.grid = grid;
            this.palette = palette;

            texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                name = "TerritoryTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[grid.CellCount];

            // One pixel per cell: pixelsPerUnit = 1 / cellSize makes the sprite span the grid exactly.
            sprite = Sprite.Create(texture, new Rect(0, 0, grid.Width, grid.Height), Vector2.zero, 1f / grid.CellSize);
            sprite.name = "TerritorySprite";

            View = new GameObject("TerritoryView");
            View.transform.SetParent(parent, false);
            View.transform.position = grid.Origin;

            Renderer = View.AddComponent<SpriteRenderer>();
            Renderer.sprite = sprite;
            Renderer.sortingOrder = sortingOrder;
            Renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask; // keep the map's rounded corners
        }

        public void Refresh()
        {
            if (grid.Version == drawnVersion) return;
            drawnVersion = grid.Version;

            for (int i = 0; i < pixels.Length; i++)
            {
                int colorOwner = grid.GetColorOwner(i);
                pixels[i] = colorOwner != TerritoryGrid.None ? palette.GetColor(colorOwner) : default;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        public void Dispose()
        {
            if (sprite != null) Object.Destroy(sprite);
            if (texture != null) Object.Destroy(texture);
        }
    }
}

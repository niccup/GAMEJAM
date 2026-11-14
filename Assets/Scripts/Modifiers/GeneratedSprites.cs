using UnityEngine;

namespace GameJam
{
    /// <summary>Tiny white sprites drawn in code for the selection screens (spawn rings, the arrow), so they need no assets. Tint them with the sprite color.</summary>
    public static class GeneratedSprites
    {
        static Sprite ring;
        static Sprite arrow;

        static Sprite square;

        /// <summary>A plain white square exactly 1 world unit wide, centered. Scale and tint it.</summary>
        public static Sprite Square
        {
            get
            {
                if (square != null) return square;
                var pixels = new Color32[4];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                return square = Create(pixels, 2, new Vector2(0.5f, 0.5f), "SelectionSquare");
            }
        }

        /// <summary>A ring exactly 1 world unit wide.</summary>
        public static Sprite Ring => ring != null ? ring : ring = CreateRing(128, 0.12f);

        /// <summary>A downward arrow 1 unit wide and tall whose pivot is the tip.</summary>
        public static Sprite Arrow => arrow != null ? arrow : arrow = CreateArrow(64);

        static Sprite CreateRing(int size, float thickness)
        {
            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            float inner = radius * (1f - thickness * 2f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                float alpha = Mathf.Clamp01(radius - d) * Mathf.Clamp01(d - inner); // soft outer and inner edge, one pixel wide
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }

            return Create(pixels, size, new Vector2(0.5f, 0.5f), "SelectionRing");
        }

        static Sprite CreateArrow(int size)
        {
            Vector2[] triangle = { new Vector2(0.08f, 0.95f), new Vector2(0.92f, 0.95f), new Vector2(0.5f, 0.05f) };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool inside = Inside(new Vector2((x + 0.5f) / size, (y + 0.5f) / size), triangle);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
            }

            return Create(pixels, size, new Vector2(0.5f, 0.05f), "SelectionArrow");
        }

        static Sprite Create(Color32[] pixels, int size, Vector2 pivot, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), pivot, size);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static bool Inside(Vector2 point, Vector2[] polygon)
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
    }
}

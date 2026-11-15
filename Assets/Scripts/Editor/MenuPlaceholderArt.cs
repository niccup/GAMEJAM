using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameJam.Editor
{
    /// <summary>
    /// Procedural placeholder art and sounds for the main menu and the character selection, created by
    /// Tools > GameJam > Setup Main Menu when they are missing. Nothing here is ever overwritten: artists replace the files
    /// (same name, or a new file dropped into the Inspector field) and the setup leaves them alone.
    /// </summary>
    static class MenuPlaceholderArt
    {
        // ---- Sprites ------------------------------------------------------------------------------------------

        /// <summary>Returns the sprite at <paramref name="path"/>; creates the PNG with <paramref name="create"/> first if the file does not exist.</summary>
        public static Sprite EnsureSprite(string path, Func<Texture2D> create, Vector4 border = default)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Texture2D texture = create();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = border;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // UI sprites are drawn as plain rectangles
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Texture2D Gradient(int width, int height, Color top, Color bottom)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float t = y / (float)(height - 1);
                Color color = Color.Lerp(bottom, top, t);

                // A soft vignette so the edges of the screen fall off a little.
                float dx = x / (float)(width - 1) * 2f - 1f;
                float dy = t * 2f - 1f;
                float vignette = 1f - 0.32f * (dx * dx * 0.6f + dy * dy * 0.6f);
                color.r *= vignette;
                color.g *= vignette;
                color.b *= vignette;
                color.a = 1f;
                pixels[y * width + x] = color;
            }
            texture.SetPixels(pixels);
            return texture;
        }

        /// <summary>A rounded rectangle with a border. A transparent fill makes it a frame.</summary>
        public static Texture2D RoundedRect(int width, int height, float radius, float borderWidth, Color fill, Color border)
        {
            Vector2 center = new Vector2(width, height) * 0.5f;
            Vector2 half = center;
            return Draw(width, height, p => Shape(RoundRectDistance(p, center, half, radius), borderWidth, fill, border));
        }

        public static Texture2D Circle(int size, Color fill, Color border, float borderWidth)
        {
            Vector2 center = new Vector2(size, size) * 0.5f;
            return Draw(size, size, p => Shape((p - center).magnitude - size * 0.5f + 0.5f, borderWidth, fill, border));
        }

        /// <summary>A rounded triangle that points down, with its tip at the bottom centre.</summary>
        public static Texture2D ArrowDown(int size, Color fill, Color border)
        {
            float s = size / 96f;
            Vector2[] triangle = { new Vector2(48f, 17f) * s, new Vector2(15f, 66f) * s, new Vector2(81f, 66f) * s };
            return Draw(size, size, p => Shape(PolygonDistance(p, triangle) - 8f * s, 5f * s, fill, border));
        }

        /// <summary>
        /// A placeholder character: one of four silhouettes (circle, rounded square, triangle, diamond) with a face. The different
        /// shapes keep the characters distinguishable even when they are greyed out.
        /// </summary>
        public static Texture2D Portrait(int shape, Color fill, Color outline, Color faceInk)
        {
            const int size = 256;
            Vector2[] triangle = { new Vector2(128f, 206f), new Vector2(40f, 52f), new Vector2(216f, 52f) };
            Vector2[] diamond = { new Vector2(128f, 200f), new Vector2(48f, 122f), new Vector2(128f, 44f), new Vector2(208f, 122f) };
            Vector2 center = new Vector2(128f, 122f);

            // Eyes sit lower in the triangle, where it is wide enough.
            float eyeY = shape == 2 ? 98f : shape == 3 ? 128f : 136f;
            float eyeX = shape == 2 ? 26f : 32f;
            float eyeRadius = shape == 2 ? 14f : 17f;

            return Draw(size, size, p =>
            {
                float body;
                switch (shape)
                {
                    case 0: body = (p - center).magnitude - 100f; break;
                    case 1: body = RoundRectDistance(p, center, new Vector2(88f, 88f), 36f); break;
                    case 2: body = PolygonDistance(p, triangle) - 18f; break;
                    default: body = PolygonDistance(p, diamond) - 22f; break;
                }

                Color color = Shape(body, 9f, fill, outline);

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 eye = new Vector2(128f + side * eyeX, eyeY);
                    color = Over(color, Shape((p - eye).magnitude - eyeRadius, 3f, Color.white, faceInk));
                    color = Over(color, Shape((p - (eye + new Vector2(0f, -2f))).magnitude - eyeRadius * 0.48f, 0f, faceInk, faceInk));
                }

                // A smile: the lower half of a thin ring.
                Vector2 mouthCenter = new Vector2(128f, eyeY - 14f);
                float ring = Mathf.Abs((p - mouthCenter).magnitude - 26f) - 3.5f;
                if (p.y > mouthCenter.y - 2f) ring = 100f;
                color = Over(color, Shape(ring, 0f, faceInk, faceInk));
                return color;
            });
        }

        /// <summary>A grey, darker copy of a rectangle of an existing PNG (used for the disabled state of the button). Null if the source is missing.</summary>
        public static Texture2D GreyCopy(string sourcePath, RectInt area, float brightness)
        {
            if (!File.Exists(sourcePath)) return null;

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(sourcePath)))
            {
                Object.DestroyImmediate(source);
                return null;
            }

            RectInt clamped = new RectInt(
                Mathf.Clamp(area.x, 0, source.width - 1), Mathf.Clamp(area.y, 0, source.height - 1), 1, 1);
            clamped.width = Mathf.Clamp(area.width, 1, source.width - clamped.x);
            clamped.height = Mathf.Clamp(area.height, 1, source.height - clamped.y);

            Color[] pixels = source.GetPixels(clamped.x, clamped.y, clamped.width, clamped.height);
            for (int i = 0; i < pixels.Length; i++)
            {
                float grey = pixels[i].r * 0.299f + pixels[i].g * 0.587f + pixels[i].b * 0.114f;
                grey *= brightness;
                pixels[i] = new Color(grey, grey, grey, pixels[i].a);
            }

            var copy = new Texture2D(clamped.width, clamped.height, TextureFormat.RGBA32, false);
            copy.SetPixels(pixels);
            Object.DestroyImmediate(source);
            return copy;
        }

        // ---- Drawing helpers ----------------------------------------------------------------------------------

        static Texture2D Draw(int width, int height, Func<Vector2, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel(new Vector2(x + 0.5f, y + 0.5f));
            texture.SetPixels(pixels);
            return texture;
        }

        // Colors a point by its signed distance to a shape's edge (negative inside), with an anti-aliased border band inside the edge.
        static Color Shape(float distance, float borderWidth, Color fill, Color border)
        {
            float outer = Mathf.Clamp01(0.5f - distance);
            if (borderWidth <= 0f) return new Color(fill.r, fill.g, fill.b, fill.a * outer);

            float inner = Mathf.Clamp01(0.5f - (distance + borderWidth));
            Color color = Color.Lerp(border, fill, inner);
            color.a = outer * Mathf.Lerp(border.a, fill.a, inner);
            return color;
        }

        // Source-over blend of two non-premultiplied colors.
        static Color Over(Color below, Color above)
        {
            float alpha = above.a + below.a * (1f - above.a);
            if (alpha <= 1e-5f) return new Color(above.r, above.g, above.b, 0f);

            float keep = below.a * (1f - above.a);
            return new Color(
                (above.r * above.a + below.r * keep) / alpha,
                (above.g * above.a + below.g * keep) / alpha,
                (above.b * above.a + below.b * keep) / alpha,
                alpha);
        }

        static float RoundRectDistance(Vector2 p, Vector2 center, Vector2 half, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - (half - new Vector2(radius, radius));
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return (p - (a + ab * t)).magnitude;
        }

        static float PolygonDistance(Vector2 p, Vector2[] polygon)
        {
            float distance = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                distance = Mathf.Min(distance, SegmentDistance(p, polygon[i], polygon[j]));
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                    p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            }
            return inside ? -distance : distance;
        }

        // ---- Sounds -------------------------------------------------------------------------------------------

        const int SampleRate = 44100;

        /// <summary>Creates a short synthesized placeholder WAV (if the file is missing) and imports it as a tiny uncompressed UI sound.</summary>
        public static AudioClip EnsureSound(string path, Func<float[]> synthesize)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null) return existing;

            WriteWav(path, synthesize());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            importer.loadInBackground = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; // tiny UI sounds: no decoding delay
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>A sine note with a quick attack and an exponential fade. <paramref name="endFrequency"/> &gt; 0 glides to that pitch.</summary>
        public static float[] Note(float frequency, float seconds, float volume, float endFrequency = 0f)
        {
            int count = Mathf.Max(1, (int)(seconds * SampleRate));
            var samples = new float[count];
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float pitch = endFrequency > 0f ? Mathf.Lerp(frequency, endFrequency, t) : frequency;
                phase += 2.0 * Math.PI * pitch / SampleRate;
                float attack = Mathf.Clamp01(i / (SampleRate * 0.004f));
                float fade = Mathf.Exp(-4.5f * t);
                float wave = (float)Math.Sin(phase) + 0.25f * (float)Math.Sin(phase * 2.0); // a little overtone so it is not a pure beep
                samples[i] = wave * 0.8f * attack * fade * volume;
            }
            return samples;
        }

        /// <summary>A softened buzz (for "not available").</summary>
        public static float[] Buzz(float frequency, float seconds, float volume)
        {
            int count = Mathf.Max(1, (int)(seconds * SampleRate));
            var samples = new float[count];
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                phase += 2.0 * Math.PI * frequency / SampleRate;
                float triangle = (float)(2.0 / Math.PI * Math.Asin(Math.Sin(phase)));
                float envelope = Mathf.Clamp01(i / (SampleRate * 0.006f)) * (1f - t);
                samples[i] = triangle * envelope * volume;
            }
            return samples;
        }

        /// <summary>A sine sweep that fades in and out (screen change, game start).</summary>
        public static float[] Sweep(float from, float to, float seconds, float volume)
        {
            int count = Mathf.Max(1, (int)(seconds * SampleRate));
            var samples = new float[count];
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                phase += 2.0 * Math.PI * Mathf.Lerp(from, to, t * t) / SampleRate;
                samples[i] = (float)Math.Sin(phase) * Mathf.Sin(t * Mathf.PI) * volume;
            }
            return samples;
        }

        public static float[] Join(params float[][] parts)
        {
            int total = 0;
            foreach (float[] part in parts) total += part.Length;

            var joined = new float[total];
            int offset = 0;
            foreach (float[] part in parts)
            {
                Array.Copy(part, 0, joined, offset, part.Length);
                offset += part.Length;
            }
            return joined;
        }

        public static float[] Silence(float seconds) => new float[Mathf.Max(0, (int)(seconds * SampleRate))];

        static void WriteWav(string path, float[] samples)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);          // PCM
                writer.Write((short)1);          // mono
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);    // byte rate
                writer.Write((short)2);          // block align
                writer.Write((short)16);         // bits per sample
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                foreach (float sample in samples)
                    writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }
    }
}

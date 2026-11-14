using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// Creates the placeholder pictures of the power-up selection screen: Assets/Art/Modifiers/Reset.png (the Reset power-up's
    /// own slot) and Assets/Art/Modifiers/Placeholder.png (shown for any power-up whose Icon is empty), and assigns them.
    /// Existing files are never overwritten, so once you drop your own art over them nothing here touches it again.
    /// To replace a picture: drag a sprite into the Icon field of the power-up asset (Assets/Config/Modifiers), or overwrite the PNG.
    /// </summary>
    public static class PowerUpPlaceholderIcons
    {
        const string ArtPath = "Assets/Art/Modifiers";
        const int Size = 128;

        [MenuItem("Tools/GameJam/Create Power-Up Placeholder Icons")]
        public static void Create()
        {
            Sprite reset = EnsureSprite(ArtPath + "/Reset.png", DrawReset);
            Sprite placeholder = EnsureSprite(ArtPath + "/Placeholder.png", DrawPlaceholder);

            var resetAsset = AssetDatabase.LoadAssetAtPath<ResetPowerUp>("Assets/Config/Modifiers/Reset.asset");
            if (resetAsset != null)
            {
                var so = new SerializedObject(resetAsset);
                if (so.FindProperty("icon").objectReferenceValue == null) so.FindProperty("icon").objectReferenceValue = reset;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(resetAsset);
            }

            var overlay = Object.FindFirstObjectByType<PowerUpOverlay>(FindObjectsInactive.Include);
            if (overlay != null)
            {
                var so = new SerializedObject(overlay);
                if (so.FindProperty("placeholderIcon").objectReferenceValue == null) so.FindProperty("placeholderIcon").objectReferenceValue = placeholder;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(overlay);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(overlay.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }

        static Sprite EnsureSprite(string path, System.Func<Vector2, Color> shade)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                tex.SetPixel(x, y, shade(new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size)));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = Size;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static readonly Color Disc = new Color(0.12f, 0.14f, 0.2f);

        // Reset: a counter-clockwise circular arrow (red-orange) on the dark disc, like the other power-up icons.
        static Color DrawReset(Vector2 p)
        {
            Color c = Vector2.Distance(p, new Vector2(0.5f, 0.5f)) <= 0.5f ? Disc : Color.clear;
            Vector2 d = p - new Vector2(0.5f, 0.5f);
            float radius = d.magnitude;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; // -180..180, counter-clockwise from +x
            Color arrow = new Color(1f, 0.45f, 0.35f);

            // The ring covers everything except a gap between 20 and 80 degrees; the arrowhead sits at the 20 degree end.
            if (radius > 0.25f && radius < 0.34f && (angle < 20f || angle > 80f)) c = arrow;

            Vector2 center = new Vector2(0.5f, 0.5f) + new Vector2(Mathf.Cos(20f * Mathf.Deg2Rad), Mathf.Sin(20f * Mathf.Deg2Rad)) * 0.295f;
            Vector2 tangent = new Vector2(-Mathf.Sin(20f * Mathf.Deg2Rad), Mathf.Cos(20f * Mathf.Deg2Rad));
            Vector2 radial = new Vector2(Mathf.Cos(20f * Mathf.Deg2Rad), Mathf.Sin(20f * Mathf.Deg2Rad));
            if (InTriangle(p, center + tangent * 0.16f, center - tangent * 0.02f + radial * 0.13f, center - tangent * 0.02f - radial * 0.13f)) c = arrow;
            return c;
        }

        // Placeholder: a dashed grey ring around a question mark, so it clearly reads as "artwork goes here".
        static Color DrawPlaceholder(Vector2 p)
        {
            Color c = Vector2.Distance(p, new Vector2(0.5f, 0.5f)) <= 0.5f ? Disc : Color.clear;
            Vector2 d = p - new Vector2(0.5f, 0.5f);
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 180f;
            Color grey = new Color(0.75f, 0.78f, 0.85f);

            if (Mathf.Abs(d.magnitude - 0.41f) < 0.025f && Mathf.FloorToInt(angle / 15f) % 2 == 0) c = grey; // dashes

            // Question mark: a hook on top (upper half and right side of a ring), a stem and a dot.
            Vector2 hook = p - new Vector2(0.5f, 0.6f);
            float hookAngle = Mathf.Atan2(hook.y, hook.x) * Mathf.Rad2Deg;
            if (Mathf.Abs(hook.magnitude - 0.12f) < 0.03f && hookAngle > -45f) c = grey;
            if (DistanceToSegment(p, new Vector2(0.5f + 0.12f * Mathf.Cos(-45f * Mathf.Deg2Rad), 0.6f + 0.12f * Mathf.Sin(-45f * Mathf.Deg2Rad)), new Vector2(0.5f, 0.42f)) < 0.03f) c = grey;
            if (Vector2.Distance(p, new Vector2(0.5f, 0.3f)) < 0.04f) c = grey;
            return c;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }
}

using TMPro;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Colors a text label with a player's color. The Black player's color would vanish on the dark panels of the game's UI,
    /// so very dark colors get a thin light outline; every other color is applied exactly as it is.
    /// </summary>
    public static class PlayerColorText
    {
        const float DarkLuminance = 0.22f;
        const float OutlineThickness = 0.22f;

        public static void Apply(TMP_Text label, Color color)
        {
            if (label == null) return;
            label.color = color;

            if (Luminance(color) < DarkLuminance)
            {
                label.outlineColor = new Color32(235, 235, 235, 255);
                label.outlineWidth = OutlineThickness;
                // TextMesh Pro only draws an outline when the keyword is on in the text's own material (the line above made one).
                label.fontMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            }
            else if (label.outlineWidth > 0.001f)
            {
                label.outlineWidth = 0f;
            }
        }

        static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }
}

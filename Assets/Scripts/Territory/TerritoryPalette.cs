using UnityEngine;

namespace GameJam
{
    /// <summary>Per-player color. Trails and territory use exactly the same color.</summary>
    public sealed class TerritoryPalette
    {
        readonly TerritoryConfig config;
        readonly Color32[] colors;

        public TerritoryPalette(TerritoryConfig config, int playerCount)
        {
            this.config = config;
            colors = new Color32[playerCount];
        }

        public void SetPlayerColor(int player, Color main)
        {
            main.a = config.alpha;
            colors[player] = main;
        }

        public Color32 GetColor(int player) => colors[player];
    }
}

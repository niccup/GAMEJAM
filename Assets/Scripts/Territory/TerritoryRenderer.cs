using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Draws the <see cref="TerritoryGrid"/>: one texture pixel per cell in the color the cell
    /// shows (trail and captured area are the same colored cells). Purely a view - replace or extend it (shaders, outlines,
    /// animations) without touching the mechanics. Runs after <see cref="TerritorySystem"/> in the frame.
    /// The actual drawing is done by <see cref="TerritoryTexture"/>, which the Mirror ghost preview uses too.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class TerritoryRenderer : MonoBehaviour
    {
        [SerializeField] TerritorySystem system;
        [SerializeField] TerritoryConfig config;

        TerritoryTexture view;

        void Start() => view = new TerritoryTexture(system.Grid, system.Palette, transform, config.sortingOrder);

        void LateUpdate() => view?.Refresh();

        void OnDestroy() => view?.Dispose();
    }
}

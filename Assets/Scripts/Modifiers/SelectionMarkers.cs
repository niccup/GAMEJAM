using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Draws the four spawn points on the map during a selection: a ring in each player's color (dimmed if that player
    /// cannot be chosen) and a bobbing arrow above the selected one. Only sprites of its own - it reads positions from
    /// the map and never changes game objects.
    /// </summary>
    public class SelectionMarkers : MonoBehaviour
    {
        const int SortingOrder = 40;

        [SerializeField, Min(0.1f)] float ringSize = 0.8f;
        [SerializeField, Min(0.1f)] float arrowSize = 0.4f;

        MapView map;
        PlayersConfig config;
        SpriteRenderer[] rings;
        SpriteRenderer arrow;
        int selected = -1;

        public void Initialize(MapView map, PlayersConfig config)
        {
            this.map = map;
            this.config = config;
        }

        /// <summary>Shows the rings. <paramref name="available"/>[i] says whether player i can be chosen; the arrow goes above <paramref name="selected"/> (-1 = no arrow).</summary>
        public void Show(IReadOnlyList<bool> available, int selected)
        {
            if (map == null || config == null) return;
            Build();

            gameObject.SetActive(true);
            this.selected = selected;

            for (int i = 0; i < rings.Length; i++)
            {
                Transform spawn = map.GetSpawnPoint(i);
                rings[i].gameObject.SetActive(spawn != null);
                if (spawn == null) continue;

                bool canChoose = i < available.Count && available[i];
                Color color = config.GetPlayerColor(i);
                color.a = canChoose ? 1f : 0.3f;
                rings[i].color = color;
                rings[i].transform.position = spawn.position;
                rings[i].transform.localScale = Vector3.one * (i == selected ? ringSize * 1.2f : ringSize);
            }

            arrow.gameObject.SetActive(selected >= 0 && map.GetSpawnPoint(selected) != null);
            LateUpdate();
        }

        public void Hide() => gameObject.SetActive(false);

        void Build()
        {
            if (rings != null) return;

            rings = new SpriteRenderer[PlayersConfig.MaxPlayers];
            for (int i = 0; i < rings.Length; i++) rings[i] = CreateSprite($"Ring P{i + 1}", GeneratedSprites.Ring);

            arrow = CreateSprite("Arrow", GeneratedSprites.Arrow);
            arrow.transform.localScale = Vector3.one * arrowSize;
            arrow.sortingOrder = SortingOrder + 1;
        }

        SpriteRenderer CreateSprite(string objectName, Sprite sprite)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = SortingOrder;
            return renderer;
        }

        void LateUpdate()
        {
            if (arrow == null || !arrow.gameObject.activeSelf) return;

            Transform spawn = map.GetSpawnPoint(selected);
            if (spawn == null) return;

            float tipY = spawn.position.y + ringSize * 0.6f + Mathf.Sin(Time.unscaledTime * 6f) * 0.06f;

            // Spawn points sit near the screen edge: keep the whole arrow inside the camera view.
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float top = cam.transform.position.y + cam.orthographicSize;
                tipY = Mathf.Min(tipY, top - arrowSize - 0.05f);
            }

            arrow.transform.position = new Vector3(spawn.position.x, tipY, 0f);
            arrow.color = config.GetPlayerColor(selected);
        }
    }
}

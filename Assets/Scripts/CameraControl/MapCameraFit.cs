using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Static orthographic camera that frames the whole map so it fills <see cref="screenFill"/> of the screen.
    /// Untick <see cref="autoFit"/> to take over zoom and position by hand.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class MapCameraFit : MonoBehaviour
    {
        [SerializeField] MapView map;

        [Tooltip("Keep the camera fitted to the map (also when the window size changes). Turn off to set zoom/position yourself.")]
        [SerializeField] bool autoFit = true;

        [Tooltip("Fraction of the screen the map occupies along its limiting axis.")]
        [SerializeField, Range(0.1f, 1f)] float screenFill = 0.9f;

        Camera cam;
        Vector2Int lastScreenSize;
        Vector2 lastMapSize;

        void OnEnable() => Fit();

        void LateUpdate()
        {
            if (!autoFit || map == null) return;

            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen != lastScreenSize || map.Size != lastMapSize) Fit();
        }

#if UNITY_EDITOR
        void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) Fit(); };
#endif

        [ContextMenu("Fit Now")]
        public void Fit()
        {
            if (!autoFit || map == null) return;
            if (cam == null) cam = GetComponent<Camera>();

            Vector2 size = map.Size;
            float aspect = cam.aspect;

            float halfHeightNeeded = size.y * 0.5f / screenFill;
            float halfWidthNeeded = size.x * 0.5f / (screenFill * aspect);

            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(halfHeightNeeded, halfWidthNeeded);

            Vector2 center = map.Center;
            transform.position = new Vector3(center.x, center.y, transform.position.z);

            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastMapSize = size;
        }
    }
}

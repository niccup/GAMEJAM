using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// The point that draws a player's trail: a small circle that follows its GameObject. Put it on a child of the
    /// player (the prefab has one called "TrailEmitter") and keep it separate from the player's sprite - the sprite can
    /// be swapped, scaled or animated freely, the trail stays the same. Move the child to shift the trail origin
    /// (e.g. under the character's feet) or change <see cref="Radius"/> for a thicker/thinner line.
    /// </summary>
    public class TrailEmitter : MonoBehaviour
    {
        [Tooltip("Trail thickness: radius of the drawing circle in world units (not affected by the parent's scale).")]
        [SerializeField, Min(0.01f)] float radius = 0.1f;

        [Tooltip("Untick to stop leaving a trail (e.g. while dead or safe).")]
        [SerializeField] bool emitting = true;

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0.01f, value);
        }

        public bool Emitting
        {
            get => emitting;
            set => emitting = value;
        }

        public Vector2 Position => transform.position;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

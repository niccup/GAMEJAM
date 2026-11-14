using UnityEngine;

namespace GameJam
{
    /// <summary>Marks where a given player starts. Move it freely if you want custom positions.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] int playerIndex;

        public int PlayerIndex
        {
            get => playerIndex;
            set => playerIndex = value;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}

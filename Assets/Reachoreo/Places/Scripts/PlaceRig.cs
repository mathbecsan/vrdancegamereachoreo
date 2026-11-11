using UnityEngine;

namespace Reachoreo
{
    /// <summary>
    /// The shared layout every place uses: where you dance, where the dance coach stands, where the
    /// mirror is, and where the two Reachy Minis stand. Put one PlaceRig in each place scene and move
    /// its children to fit the real spot. The rig's own position is the dance mark; you face its +Z
    /// (toward the coach and the mirror).
    /// </summary>
    public class PlaceRig : MonoBehaviour
    {
        [Tooltip("Where the player stands. Usually this object itself.")]
        public Transform danceMark;

        [Tooltip("Where the dance coach stands, in front of the player.")]
        public Transform coach;

        [Tooltip("Center of the mirror surface, in front of the player. Its +Z points away from the player.")]
        public Transform mirror;

        [Tooltip("Reachy A: on the player's left.")]
        public Transform reachyA;

        [Tooltip("Reachy B: on the player's right.")]
        public Transform reachyB;

        [Tooltip("The accurately built area around the dance mark, in meters. Beyond it is the 360° photo.")]
        public Vector2 playableArea = new Vector2(4f, 4f);

        void OnDrawGizmos()
        {
            var mark = danceMark != null ? danceMark : transform;
            Gizmos.matrix = Matrix4x4.TRS(mark.position, Quaternion.Euler(0f, mark.eulerAngles.y, 0f), Vector3.one);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(playableArea.x, 0.01f, playableArea.y));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.5f);
            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            if (reachyA != null) Gizmos.DrawWireCube(reachyA.position + Vector3.up * 0.14f, new Vector3(0.16f, 0.28f, 0.16f));
            if (reachyB != null) Gizmos.DrawWireCube(reachyB.position + Vector3.up * 0.14f, new Vector3(0.16f, 0.28f, 0.16f));

            Gizmos.color = new Color(0.95f, 0.45f, 0.35f, 0.9f);
            if (coach != null) Gizmos.DrawWireCube(coach.position + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.3f));
        }
    }
}

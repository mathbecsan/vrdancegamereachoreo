using UnityEngine;

namespace Reachoreo
{
    /// <summary>
    /// Tags a greybox piece whose size or position is a guess, not a measurement of the real
    /// Robotics Center kitchen. When you measure the real thing, fix the piece, write the real
    /// value in References/&lt;place&gt;/measurements.md, and tick "measured".
    /// Find every guess left: type "t:EstimatedDimension" in the Hierarchy search box.
    /// </summary>
    public class EstimatedDimension : MonoBehaviour
    {
        [TextArea(2, 4)]
        public string estimate;

        public bool measured;

        void OnDrawGizmosSelected()
        {
            if (measured) return;
            var r = GetComponentInChildren<Renderer>();
            if (r == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f);
            Gizmos.DrawWireCube(r.bounds.center, r.bounds.size);
        }
    }
}

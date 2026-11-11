using System.Collections.Generic;
using UnityEngine;

namespace Reachoreo
{
    /// <summary>
    /// A real-looking mirror for VR: behind the (see-through) mirror surface we build a reflected
    /// copy of the room and keep reflected copies of the moving things (you, the coach, the Reachys)
    /// in sync every frame. Each eye then sees a correct reflection with depth, and it costs the
    /// Quest far less than rendering the scene again through a mirror camera.
    ///
    /// The copies are visuals only (meshes and materials); scripts and colliders are not copied.
    /// Anything behind the mirror plane is left out of the copy, so the wall the mirror hangs on
    /// does not block the view.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class MirrorWorld : MonoBehaviour
    {
        [Tooltip("The mirror surface. Its +Z points away from the player, into the mirror.")]
        public Transform mirror;

        [Tooltip("Copied once: the room, its props, the barre.")]
        public List<Transform> staticSources = new List<Transform>();

        [Tooltip("Copied and kept in sync every frame: you, the coach, the Reachys.")]
        public List<Transform> dynamicSources = new List<Transform>();

        [Tooltip("Point and spot lights copied into the reflected room so it is lit like the real one.")]
        public List<Light> lights = new List<Light>();

        [Tooltip("Also reflect every SelfAvatar (your tracked body) in the scene.")]
        public bool includeSelfAvatars = true;

        Transform world;   // its world matrix is the reflection across the mirror plane
        readonly List<Transform> src = new List<Transform>();
        readonly List<Transform> dst = new List<Transform>();
        readonly List<int> roots = new List<int>();

        void Start()
        {
            if (mirror == null) { enabled = false; return; }
            world = BuildReflection();
            if (includeSelfAvatars)
                foreach (var a in FindObjectsByType<SelfAvatar>(FindObjectsSortMode.None))
                    if (!dynamicSources.Contains(a.visuals)) dynamicSources.Add(a.visuals);
            foreach (var s in staticSources) if (s != null) Copy(s, world, false);
            foreach (var s in dynamicSources) if (s != null) Copy(s, world, true);
            foreach (var l in lights)
            {
                if (l == null || l.type == LightType.Directional) continue;
                var g = new GameObject("Mirror_" + l.name);
                g.transform.SetParent(world, false);
                g.transform.SetPositionAndRotation(l.transform.position, l.transform.rotation);
                // Local = source's world pose: placed under 'world' it lands at the reflected spot.
                g.transform.localPosition = l.transform.position; g.transform.localRotation = l.transform.rotation;
                var c = g.AddComponent<Light>();
                c.type = l.type; c.color = l.color; c.intensity = l.intensity; c.range = l.range; c.spotAngle = l.spotAngle;
                c.shadows = LightShadows.None; c.renderMode = l.renderMode; c.cullingMask = l.cullingMask;
            }
        }

        /// <summary>Three nested transforms whose product is the reflection across the mirror plane:
        /// T(p) * R(m) * S(1,1,-1) * R(m)^-1 * T(-p).</summary>
        Transform BuildReflection()
        {
            var a = new GameObject("MirrorWorld").transform;
            a.SetPositionAndRotation(mirror.position, mirror.rotation);
            var b = new GameObject("Flip").transform; b.SetParent(a, false); b.localScale = new Vector3(1, 1, -1);
            var c = new GameObject("Reflected").transform; c.SetParent(b, false);
            Quaternion inv = Quaternion.Inverse(mirror.rotation);
            c.localRotation = inv;
            c.localPosition = inv * -mirror.position;
            return c;
        }

        float SignedDistance(Vector3 p) => Vector3.Dot(mirror.forward, p - mirror.position);

        void Copy(Transform source, Transform parent, bool dynamic)
        {
            var root = CopyNode(source, parent, dynamic, true);
            if (root == null) return;
            // The copy's root sits at the source's world pose inside the reflected space.
            root.localPosition = source.position; root.localRotation = source.rotation; root.localScale = source.lossyScale;
            if (dynamic) { roots.Add(src.IndexOf(source)); }
        }

        Transform CopyNode(Transform s, Transform parent, bool dynamic, bool isRoot)
        {
            if (s.GetComponent<Canvas>() != null) return null;   // UI panels are not reflected
            var r = s.GetComponent<MeshRenderer>();
            var f = s.GetComponent<MeshFilter>();
            if (!dynamic && r != null && SignedDistance(r.bounds.center) > 0f) return null;   // behind the mirror
            if (r != null && r.isPartOfStaticBatch)
            {
                // Static batching merges meshes at load, so the original mesh is gone: turn off "Batching Static" on it.
                Debug.LogWarning($"MirrorWorld: '{s.name}' is static-batched and can't be reflected. Untick Static > Batching Static on it.", s);
                return null;
            }

            var g = new GameObject(s.name);
            var t = g.transform;
            t.SetParent(parent, false);
            t.localPosition = s.localPosition; t.localRotation = s.localRotation; t.localScale = s.localScale;
            g.SetActive(s.gameObject.activeSelf);
            if (r != null && f != null)
            {
                g.AddComponent<MeshFilter>().sharedMesh = f.sharedMesh;
                var cr = g.AddComponent<MeshRenderer>();
                cr.sharedMaterials = r.sharedMaterials;
                cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cr.enabled = true;   // a self-avatar hidden in first person still shows in the mirror
            }
            if (dynamic) { src.Add(s); dst.Add(t); }
            foreach (Transform child in s) CopyNode(child, t, dynamic, false);
            return t;
        }

        void LateUpdate()
        {
            if (world == null) return;
            for (int i = 0; i < src.Count; i++)
            {
                var s = src[i]; var d = dst[i];
                if (s == null || d == null) continue;
                if (d.gameObject.activeSelf != s.gameObject.activeSelf) d.gameObject.SetActive(s.gameObject.activeSelf);
                if (roots.Contains(i)) { d.localPosition = s.position; d.localRotation = s.rotation; d.localScale = s.lossyScale; }
                else { d.localPosition = s.localPosition; d.localRotation = s.localRotation; d.localScale = s.localScale; }
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Reachoreo
{
    /// <summary>
    /// Your body in the game: a simple mannequin (the coach's style, in a different colour) whose
    /// pieces follow the joints from Quest body tracking (Meta Movement SDK, full-body joint set:
    /// tracked upper body, generated legs).
    ///
    /// Look down and you see your body (head and neck are hidden so they don't block your eyes); the
    /// mirror (MirrorWorld) shows all of it.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public class SelfAvatar : MonoBehaviour
    {
        public OVRSkeleton skeleton;
        public Material bodyMaterial;
        public Material jointMaterial;
        public bool showInFirstPerson = true;

        [Tooltip("Shown while body tracking has data. Visuals are built under it.")]
        public Transform visuals;

        struct Segment { public OVRSkeleton.BoneId a, b; public float thickness; public Transform t; }
        struct Joint { public OVRSkeleton.BoneId at; public float size; public Vector3 offsetUp; public Transform t; }

        readonly List<Segment> segments = new List<Segment>();
        readonly List<Joint> balls = new List<Joint>();
        readonly Dictionary<OVRSkeleton.BoneId, Transform> bones = new Dictionary<OVRSkeleton.BoneId, Transform>();
        int bonesVersion = -1;

        void Awake()
        {
            if (visuals == null) { visuals = new GameObject("Visuals").transform; visuals.SetParent(transform, false); }
            // Torso and head
            Seg(OVRSkeleton.BoneId.FullBody_Hips, OVRSkeleton.BoneId.FullBody_Chest, 0.26f);
            Seg(OVRSkeleton.BoneId.FullBody_Chest, OVRSkeleton.BoneId.FullBody_Neck, 0.24f);
            Seg(OVRSkeleton.BoneId.FullBody_Neck, OVRSkeleton.BoneId.FullBody_Head, 0.08f);
            Ball(OVRSkeleton.BoneId.FullBody_Head, 0.21f, new Vector3(0, 0.08f, 0), bodyMaterial);
            // Arms
            foreach (var side in new[] { "Left", "Right" })
            {
                Seg(B("FullBody_" + side + "Shoulder"), B("FullBody_" + side + "ArmUpper"), 0.09f);
                Seg(B("FullBody_" + side + "ArmUpper"), B("FullBody_" + side + "ArmLower"), 0.085f);
                Seg(B("FullBody_" + side + "ArmLower"), B("FullBody_" + side + "HandWrist"), 0.075f);
                Ball(B("FullBody_" + side + "ArmLower"), 0.085f, Vector3.zero, jointMaterial);
                Ball(B("FullBody_" + side + "HandPalm"), 0.09f, Vector3.zero, jointMaterial);
                // Legs
                Seg(OVRSkeleton.BoneId.FullBody_Hips, B("FullBody_" + side + "UpperLeg"), 0.12f);
                Seg(B("FullBody_" + side + "UpperLeg"), B("FullBody_" + side + "LowerLeg"), 0.12f);
                Seg(B("FullBody_" + side + "LowerLeg"), B("FullBody_" + side + "FootAnkle"), 0.10f);
                Seg(B("FullBody_" + side + "FootAnkle"), B("FullBody_" + side + "FootBall"), 0.08f);
                Ball(B("FullBody_" + side + "LowerLeg"), 0.11f, Vector3.zero, jointMaterial);
            }
            visuals.gameObject.SetActive(false);
        }

        static OVRSkeleton.BoneId B(string name) => (OVRSkeleton.BoneId)System.Enum.Parse(typeof(OVRSkeleton.BoneId), name);

        void Seg(OVRSkeleton.BoneId a, OVRSkeleton.BoneId b, float thickness)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            g.name = a.ToString().Replace("FullBody_", "") + "-" + b.ToString().Replace("FullBody_", "");
            Prepare(g, bodyMaterial);
            segments.Add(new Segment { a = a, b = b, thickness = thickness, t = g.transform });
        }

        void Ball(OVRSkeleton.BoneId at, float size, Vector3 offsetUp, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = at.ToString().Replace("FullBody_", "");
            Prepare(g, m);
            g.transform.localScale = Vector3.one * size;
            balls.Add(new Joint { at = at, size = size, offsetUp = offsetUp, t = g.transform });
        }

        void Prepare(GameObject g, Material m)
        {
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(visuals, false);
            var r = g.GetComponent<MeshRenderer>();
            if (m != null) r.sharedMaterial = m;
            r.enabled = showInFirstPerson && !InsideHead(g.name);
        }

        /// <summary>The head and neck sit around your eyes: never drawn for you (only in the mirror).</summary>
        static bool InsideHead(string n) => n.StartsWith("Head") || n.StartsWith("Neck");

        void LateUpdate()
        {
            bool valid = skeleton != null && skeleton.IsInitialized && skeleton.IsDataValid;
            if (visuals.gameObject.activeSelf != valid) visuals.gameObject.SetActive(valid);
            if (!valid) return;

            if (bonesVersion != skeleton.SkeletonChangedCount)
            {
                bones.Clear();
                foreach (var bone in skeleton.Bones) bones[bone.Id] = bone.Transform;
                bonesVersion = skeleton.SkeletonChangedCount;
            }

            foreach (var s in segments)
            {
                if (!bones.TryGetValue(s.a, out var a) || !bones.TryGetValue(s.b, out var b)) continue;
                Vector3 pa = a.position, pb = b.position, d = pb - pa;
                float len = d.magnitude;
                if (len < 1e-4f) { s.t.gameObject.SetActive(false); continue; }
                s.t.gameObject.SetActive(true);
                s.t.SetPositionAndRotation((pa + pb) * 0.5f, Quaternion.FromToRotation(Vector3.up, d / len));
                // Capsule primitive: 2 m tall, 1 m wide. Keep it at least as long as it is thick.
                s.t.localScale = new Vector3(s.thickness, Mathf.Max(len * 0.5f, s.thickness * 0.5f), s.thickness);
            }
            foreach (var ball in balls)
            {
                if (!bones.TryGetValue(ball.at, out var at)) continue;
                ball.t.position = at.position + ball.offsetUp;
                ball.t.rotation = at.rotation;
            }
        }

        public void SetShowInFirstPerson(bool show)
        {
            showInFirstPerson = show;
            foreach (var r in visuals.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = show && !InsideHead(r.name);
        }
    }
}

using UnityEngine;

namespace Reachoreo
{
    /// <summary>
    /// One Reachy Mini: Pollen's rig (Robots/reachy_mini.glb), driven with the same SDK units as the
    /// real robot (head pose in the robot's base frame, body yaw, two antennas). A port of the
    /// Reachoreo web app's xr/src/robot.ts, which is tested against the Encendido preview.
    ///
    /// Bones (docs/RIG_MAPPING.md in pollen-robotics/reachy_mini_blender): Core = body yaw;
    /// Core.001 carries the head (in Blender it follows Head.001 through constraints, which glTF
    /// drops, so it is placed here); Antenna.L/R.002 = the antennas (local Z hinge).
    /// The six Stewart-platform rods are drawn as thin cylinders between their two joints.
    ///
    /// Put this on an empty object; the model goes in as a child. The robot faces this object's +Z.
    /// </summary>
    public class ReachyRig : MonoBehaviour
    {
        /// <summary>The rig is 2.19x real size; this brings it to the real robot's size.</summary>
        public const float RigScale = 0.4575f;

        [Tooltip("The imported reachy_mini.glb instance (a child of this object).")]
        public Transform model;

        [Header("Pose (SDK units), used when 'Drive From Inspector' is on")]
        public bool driveFromInspector;
        [Range(-35, 35)] public float headRollDeg;
        [Range(-35, 35)] public float headPitchDeg;
        [Range(-180, 180)] public float headYawDeg;
        [Range(-12, 12)] public float headZmm;
        [Range(-2.8f, 2.8f)] public float bodyYawRad;
        [Range(-0.3f, 1.45f)] public float antennaRightRad;
        [Range(-1.45f, 0.3f)] public float antennaLeftRad;

        [Header("Look")]
        public Material rodMaterial;
        public float rodRadius = 0.0022f;

        Transform core, head, antL, antR;
        Quaternion coreRest, antLRest, antRRest;
        Matrix4x4 headRest;          // Core.001 in this object's space, at rest
        Vector3 pivot;               // the head's rotation centre (Head.001), this object's space
        float bodySign = 1f, antSignL = 1f, antSignR = 1f;
        readonly Transform[] rodBottom = new Transform[6];
        readonly Vector3[] rodTopRest = new Vector3[6];
        readonly Transform[] rods = new Transform[6];
        bool ready;

        // Robot frame (x forward, y left, z up) -> this object's space (forward +Z, left -X, up +Y).
        static readonly Matrix4x4 R2S = new Matrix4x4(
            new Vector4(0, 0, 1, 0),    // robot x -> +Z
            new Vector4(-1, 0, 0, 0),   // robot y -> -X
            new Vector4(0, 1, 0, 0),    // robot z -> +Y
            new Vector4(0, 0, 0, 1));

        void Awake() { Setup(); }

        public bool IsReady => ready;

        public void Setup()
        {
            if (ready) return;
            if (model == null && transform.childCount > 0) model = transform.GetChild(0);
            if (model == null) { Debug.LogError($"{name}: ReachyRig has no model"); return; }
            core = Find("Core"); head = Find("Core.001");
            antL = Find("Antenna.L.002"); antR = Find("Antenna.R.002");
            if (core == null || head == null || antL == null || antR == null) { Debug.LogError($"{name}: Reachy rig bones missing"); return; }
            coreRest = core.localRotation; antLRest = antL.localRotation; antRRest = antR.localRotation;
            headRest = Rel(head);
            pivot = Rel(Find("Head.001")).GetColumn(3);
            string[] k = { "A", "B", "C", "D", "E", "F" };
            for (int i = 0; i < 6; i++)
            {
                rodBottom[i] = Find("Neck." + k[i] + ".003");
                rodTopRest[i] = Rel(Find("Head." + k[i])).GetColumn(3);
                var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rod.name = "Rod_" + k[i];
                Destroy(rod.GetComponent<Collider>());
                rod.transform.SetParent(transform, false);
                if (rodMaterial != null) rod.GetComponent<MeshRenderer>().sharedMaterial = rodMaterial;
                rods[i] = rod.transform;
            }
            CalibrateSigns();
            ready = true;
            SetPose(0, 0, 0, 0, 0, 0, 0);
        }

        Transform Find(string bone)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == bone) return t;
            return null;
        }

        /// <summary>A transform's matrix in this object's space.</summary>
        Matrix4x4 Rel(Transform t) => transform.worldToLocalMatrix * t.localToWorldMatrix;

        /// <summary>Which way each hinge turns is read off the geometry once:
        /// +body yaw turns the robot's front toward its left (-X here); +antenna "out" swings the tip away from the head.</summary>
        void CalibrateSigns()
        {
            var front = new GameObject("probe").transform;
            front.SetParent(core, false);
            front.position = transform.TransformPoint(pivot + new Vector3(0, 0, 0.05f));
            float x0 = transform.InverseTransformPoint(front.position).x;
            core.localRotation = coreRest * Quaternion.AngleAxis(0.3f * Mathf.Rad2Deg, Vector3.up);
            float x1 = transform.InverseTransformPoint(front.position).x;
            bodySign = x1 < x0 ? 1f : -1f;
            core.localRotation = coreRest;
            Destroy(front.gameObject);

            antSignL = AntennaSign(antL, antLRest, Find("Antenna.L.003"), -1f);
            antSignR = AntennaSign(antR, antRRest, Find("Antenna.R.003"), 1f);
        }

        float AntennaSign(Transform node, Quaternion rest, Transform tip, float outX)
        {
            node.localRotation = rest;
            float a = transform.InverseTransformPoint(tip.position).x;
            node.localRotation = rest * Quaternion.AngleAxis(0.4f * Mathf.Rad2Deg, Vector3.forward);
            float b = transform.InverseTransformPoint(tip.position).x;
            node.localRotation = rest;
            float s = Mathf.Sign((b - a) * outX);
            return s == 0f ? 1f : s;
        }

        void LateUpdate()
        {
            if (ready && driveFromInspector)
                SetPose(headRollDeg, headPitchDeg, headYawDeg, headZmm, bodyYawRad, antennaRightRad, antennaLeftRad);
        }

        public void SetPose(SdkCommand c) =>
            SetPose(c.headRollDeg, c.headPitchDeg, c.headYawDeg, c.headZmm, c.bodyYawRad, c.antennaRightRad, c.antennaLeftRad);

        /// <summary>Pose the robot from SDK units. The head pose is in the robot's base frame
        /// (create_head_pose: R.from_euler("xyz", [roll, pitch, yaw]) plus z up), so head yaw includes the body yaw.</summary>
        /// <summary>The last pose given to this robot, in SDK units (what a real Reachy Mini would be sent).</summary>
        public SdkCommand LastCommand { get; private set; }

        public void SetPose(float rollDeg, float pitchDeg, float yawDeg, float zMm, float bodyYaw, float antRight, float antLeft)
        {
            LastCommand = new SdkCommand { headRollDeg = rollDeg, headPitchDeg = pitchDeg, headYawDeg = yawDeg, headZmm = zMm,
                                           bodyYawRad = bodyYaw, antennaRightRad = antRight, antennaLeftRad = antLeft };
            if (!ready) return;
            core.localRotation = coreRest * Quaternion.AngleAxis(bodySign * bodyYaw * Mathf.Rad2Deg, Vector3.up);

            Matrix4x4 rRobot = RotZ(yawDeg) * RotY(pitchDeg) * RotX(rollDeg);   // extrinsic x, y, z
            Matrix4x4 rScene = R2S * rRobot * R2S.inverse;
            Vector3 lift = new Vector3(0, zMm / 1000f, 0);
            Matrix4x4 m = Matrix4x4.Translate(pivot + lift) * rScene * Matrix4x4.Translate(-pivot) * headRest;
            Matrix4x4 local = Rel(head.parent).inverse * m;
            head.localPosition = local.GetColumn(3);
            head.localRotation = local.rotation;

            // Rods: bottom joints ride on the body; top joints follow the head's motion from rest.
            Matrix4x4 delta = m * headRest.inverse;
            for (int i = 0; i < 6; i++)
            {
                Vector3 a = transform.InverseTransformPoint(rodBottom[i].position);
                Vector3 b = delta.MultiplyPoint3x4(rodTopRest[i]);
                Vector3 dir = b - a; float len = dir.magnitude;
                rods[i].localPosition = (a + b) * 0.5f;
                rods[i].localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                rods[i].localScale = new Vector3(rodRadius * 2f, len * 0.5f, rodRadius * 2f);
            }

            // Antennas: on the real robot +right and -left are "out".
            antR.localRotation = antRRest * Quaternion.AngleAxis(antSignR * antRight * Mathf.Rad2Deg, Vector3.forward);
            antL.localRotation = antLRest * Quaternion.AngleAxis(antSignL * -antLeft * Mathf.Rad2Deg, Vector3.forward);
        }

        // Right-handed rotation matrices in the robot's own frame.
        static Matrix4x4 RotX(float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); var m = Matrix4x4.identity; m.m11 = c; m.m12 = -s; m.m21 = s; m.m22 = c; return m; }
        static Matrix4x4 RotY(float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); var m = Matrix4x4.identity; m.m00 = c; m.m02 = s; m.m20 = -s; m.m22 = c; return m; }
        static Matrix4x4 RotZ(float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); var m = Matrix4x4.identity; m.m00 = c; m.m01 = -s; m.m10 = s; m.m11 = c; return m; }
    }

    /// <summary>One Reachy Mini command in SDK units.</summary>
    public struct SdkCommand
    {
        public float headRollDeg, headPitchDeg, headYawDeg, headZmm, bodyYawRad, antennaRightRad, antennaLeftRad;
    }
}

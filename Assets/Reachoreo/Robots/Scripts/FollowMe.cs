using UnityEngine;

namespace Reachoreo
{
    /// <summary>
    /// "Follow me": the Reachys copy you. Your head turns, tilts and nods the robot's head, your shoulders turn
    /// its body, each hand raises the antenna on that side, crouching lowers its head. The Reachys stand beside
    /// you facing the same way you do, so your left is their left. Works with body tracking, or with the
    /// headset and Touch controllers when the body isn't tracked. ReachyLink sends it to the real robots.
    /// </summary>
    [DefaultExecutionOrder(950)]
    public class FollowMe : MonoBehaviour
    {
        public enum Who { ReachyA, ReachyB, Both }

        public bool active;
        public Who who = Who.Both;

        PerformanceDirector director;
        Transform head, origin, leftCtrl, rightCtrl;
        float standHeight;
        SdkCommand smooth; bool haveSmooth;

        void Awake()
        {
            director = GetComponent<PerformanceDirector>();
            var cam = Camera.main;
            if (cam != null) { head = cam.transform; origin = head.parent != null ? head.parent.parent : null; }
            if (head != null && head.parent != null)
            {
                leftCtrl = head.parent.Find("Left Controller");
                rightCtrl = head.parent.Find("Right Controller");
            }
        }

        public void Begin(Who w)
        {
            who = w; active = true; haveSmooth = false;
            standHeight = GameSession.Calibrated && GameSession.Height > 1f ? GameSession.Height - 0.11f : (head != null ? head.position.y : 1.6f);
        }

        public void End() { active = false; }

        void Update()
        {
            if (!active || head == null || director == null || director.robots == null) return;
            var c = Measure();
            if (!haveSmooth) { smooth = c; haveSmooth = true; }
            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);   // ~12 Hz low-pass: steady, still quick
            smooth = Lerp(smooth, c, k);
            var robots = director.robots;
            if ((who == Who.ReachyA || who == Who.Both) && robots.reachyA != null && robots.reachyA.isActiveAndEnabled) robots.reachyA.SetPose(smooth);
            if ((who == Who.ReachyB || who == Who.Both) && robots.reachyB != null && robots.reachyB.isActiveAndEnabled) robots.reachyB.SetPose(smooth);
        }

        SdkCommand Measure()
        {
            Quaternion rigRot = origin != null ? origin.rotation : Quaternion.identity;
            Vector3 e = (Quaternion.Inverse(rigRot) * head.rotation).eulerAngles;
            float yaw = Mathf.DeltaAngle(0, e.y), pitch = Mathf.DeltaAngle(0, e.x), roll = Mathf.DeltaAngle(0, e.z);

            // Your shoulders turn the robot's body; without body tracking, the body follows half your head turn.
            float bodyYaw = yaw * 0.5f;
            Transform sl = director.Bone("FullBody_LeftArmUpper"), sr = director.Bone("FullBody_RightArmUpper");
            if (director.BodyTracked && sl != null && sr != null)
            {
                Vector3 across = Quaternion.Inverse(rigRot) * (sr.position - sl.position);
                bodyYaw = -Mathf.Atan2(across.z, across.x) * Mathf.Rad2Deg;   // shoulders square to the mirror = 0
            }

            // Hands: each hand's height above the shoulder line raises that side's antenna (down = 0, up = full).
            Vector3? lh = Hand(true), rh = Hand(false);
            float shoulderY = sl != null && director.BodyTracked ? (sl.position.y + sr.position.y) * 0.5f : head.position.y - 0.25f;
            float Ant(Vector3? h) => h.HasValue ? Mathf.Clamp01((h.Value.y - (shoulderY - 0.55f)) / 0.95f) * 1.4f : 0.35f;

            // Robot frame: + yaw = to its left (your head turning left is negative in Unity), + pitch = down, + roll = right ear down.
            float bodyDeg = Mathf.Clamp(-bodyYaw, -150f, 150f);
            return new SdkCommand
            {
                bodyYawRad = bodyDeg * Mathf.Deg2Rad,
                headYawDeg = bodyDeg + Mathf.Clamp(-yaw - bodyDeg, -60f, 60f),
                headPitchDeg = Mathf.Clamp(pitch, -30f, 30f),
                headRollDeg = Mathf.Clamp(-roll, -30f, 30f),
                headZmm = Mathf.Clamp((head.position.y - standHeight) * 40f, -12f, 12f),
                antennaLeftRad = -Ant(lh),      // on the robot, -left is "out"
                antennaRightRad = Ant(rh),
            };
        }

        Vector3? Hand(bool left)
        {
            var b = director.Bone(left ? "FullBody_LeftHandWrist" : "FullBody_RightHandWrist");
            if (director.BodyTracked && b != null) return b.position;
            var ctrl = left ? leftCtrl : rightCtrl;
            if (ctrl != null && ctrl.gameObject.activeInHierarchy) return ctrl.position;
            return null;
        }

        static SdkCommand Lerp(SdkCommand a, SdkCommand b, float k) => new SdkCommand
        {
            headRollDeg = Mathf.Lerp(a.headRollDeg, b.headRollDeg, k), headPitchDeg = Mathf.Lerp(a.headPitchDeg, b.headPitchDeg, k),
            headYawDeg = Mathf.Lerp(a.headYawDeg, b.headYawDeg, k), headZmm = Mathf.Lerp(a.headZmm, b.headZmm, k),
            bodyYawRad = Mathf.Lerp(a.bodyYawRad, b.bodyYawRad, k),
            antennaRightRad = Mathf.Lerp(a.antennaRightRad, b.antennaRightRad, k), antennaLeftRad = Mathf.Lerp(a.antennaLeftRad, b.antennaLeftRad, k),
        };
    }
}

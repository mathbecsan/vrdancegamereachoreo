using UnityEngine;

namespace Reachoreo
{
    /// <summary>
    /// The dance coach: the figure in front of you whose moves you follow.
    ///
    /// Two ways to follow (switch any time):
    /// - BackToYou: the coach faces the mirror, like a teacher at the front of a studio class.
    ///   Their left is your left, so you copy them directly, and you see their face in the mirror.
    /// - FacingYou: the coach turns around and dances as your mirror image (they lift their right
    ///   arm when you should lift your left), the usual way to follow someone facing you.
    ///
    /// For now the coach plays a simple warm-up groove at the set tempo so the room is alive.
    /// Later the moves come from the dance itself (Encendido, or dances made by the Reachoreo app).
    /// </summary>
    public class DanceCoach : MonoBehaviour
    {
        public enum Facing { BackToYou, FacingYou }

        [Tooltip("BackToYou: copy them directly. FacingYou: they dance as your mirror image.")]
        public Facing facing = Facing.BackToYou;

        [Tooltip("Beats per minute of the warm-up groove.")]
        public float bpm = 120f;

        public bool playWarmup = true;

        [Tooltip("Press A on the right controller to switch between BackToYou and FacingYou.")]
        public bool toggleWithAButton = true;

        [Header("Joints (filled in by the coach prefab)")]
        public Transform hips;
        public Transform spine;
        public Transform head;
        public Transform upperArmL, lowerArmL, upperArmR, lowerArmR;
        public Transform upperLegL, lowerLegL, upperLegR, lowerLegR;

        Vector3 hipsRest;
        Quaternion baseRotation;
        bool cached;

        void OnEnable() { Cache(); ApplyFacing(); }
        void OnValidate() { if (Application.isPlaying) ApplyFacing(); }

        void Cache()
        {
            if (cached || hips == null) return;
            hipsRest = hips.localPosition;
            baseRotation = transform.localRotation;
            cached = true;
        }

        /// <summary>Turns the coach to face away from you (toward the mirror) or toward you.</summary>
        public void ApplyFacing()
        {
            if (!cached) return;
            transform.localRotation = facing == Facing.FacingYou ? baseRotation * Quaternion.Euler(0f, 180f, 0f) : baseRotation;
        }

        public void SetFacing(Facing f) { facing = f; ApplyFacing(); }

        // ---------- Dancing a dance file's dancer part ----------

        DanceFile poseSource; int[] jix; Vector3 restLocalPos; bool restPosCached;
        const float RestPelvisY = 210f * 1.75f / 480f;   // the dance file's figure, standing (xr/src/body.ts)

        /// <summary>Pose the coach from the dance's dancer joints at a dance time. Facing you, the coach is the
        /// mirror image (left and right swapped), so you can follow it like a reflection.</summary>
        public void PoseFromDance(DanceFile d, float time)
        {
            if (!cached || d == null || d.dancer == null || d.dancerJoints == null || d.frames <= 0) return;
            playWarmup = false;
            if (!restPosCached) { restLocalPos = transform.localPosition; restPosCached = true; }
            if (poseSource != d)
            {
                poseSource = d;
                string[] want = { "pelvis", "spine1", "spine2", "neck1", "neck2", "head",
                                  "L_shoulder", "L_elbow", "L_wrist", "R_shoulder", "R_elbow", "R_wrist",
                                  "L_hip", "L_knee", "L_foot", "R_hip", "R_knee", "R_foot" };
                jix = new int[want.Length];
                for (int k = 0; k < want.Length; k++) jix[k] = System.Array.IndexOf(d.dancerJoints, want[k]);
            }
            int J = d.dancerJoints.Length;
            float fx = Mathf.Clamp(time * d.frameRate, 0, d.frames - 1);
            int i = Mathf.FloorToInt(fx), j = Mathf.Min(i + 1, d.frames - 1); float u = fx - i;
            bool mirror = facing == Facing.FacingYou;

            Vector3 P(int which)
            {
                int k = jix[which]; if (k < 0) return Vector3.zero;
                int a = (i * J + k) * 3, b = (j * J + k) * 3;
                float x = Mathf.Lerp(d.dancer[a], d.dancer[b], u), y = Mathf.Lerp(d.dancer[a + 1], d.dancer[b + 1], u), z = Mathf.Lerp(d.dancer[a + 2], d.dancer[b + 2], u);
                // File: x = audience's right, z = toward the audience (the dancer's front). Coach: +Z is its front.
                return new Vector3(mirror ? x : -x, y, z);
            }
            float rootX = Mathf.Lerp(d.dancerRoot[i * 3], d.dancerRoot[j * 3], u);
            float face = Mathf.Lerp(d.dancerRoot[i * 3 + 2], d.dancerRoot[j * 3 + 2], u);

            // Root: stay near the mark (travel is scaled down), and spin with the dancer.
            transform.localPosition = restLocalPos + new Vector3(Mathf.Clamp(-rootX * 0.5f, -0.6f, 0.6f), 0f, 0f);
            Quaternion facingRot = facing == Facing.FacingYou ? baseRotation * Quaternion.Euler(0f, 180f, 0f) : baseRotation;
            transform.localRotation = facingRot * Quaternion.Euler(0f, (mirror ? 1f : -1f) * face * Mathf.Rad2Deg, 0f);

            Vector3 pelvis = P(0), spine1 = P(1), neck1 = P(3), neck2 = P(4), headP = P(5);
            hips.localPosition = hipsRest + new Vector3(pelvis.x, pelvis.y - RestPelvisY, 0f);
            Aim(hips, Vector3.up, neck1 - pelvis, 0.35f);
            Aim(spine, Vector3.up, neck1 - spine1, 1f);
            if (head.parent != null) Aim(head.parent, Vector3.up, headP - neck2, 1f);

            // The file's L side is the dancer's right. Facing away (copy directly) it drives the coach's right
            // limbs; facing you (mirror image) it drives the coach's left limbs.
            int sR = mirror ? 9 : 6, sL = mirror ? 6 : 9;          // shoulder indices in 'want'
            int hR = mirror ? 15 : 12, hL = mirror ? 12 : 15;      // hip indices
            Aim(upperArmR, Vector3.down, P(sR + 1) - P(sR), 1f); Aim(lowerArmR, Vector3.down, P(sR + 2) - P(sR + 1), 1f);
            Aim(upperArmL, Vector3.down, P(sL + 1) - P(sL), 1f); Aim(lowerArmL, Vector3.down, P(sL + 2) - P(sL + 1), 1f);
            Aim(upperLegR, Vector3.down, P(hR + 1) - P(hR), 1f); Aim(lowerLegR, Vector3.down, P(hR + 2) - P(hR + 1), 1f);
            Aim(upperLegL, Vector3.down, P(hL + 1) - P(hL), 1f); Aim(lowerLegL, Vector3.down, P(hL + 2) - P(hL + 1), 1f);
        }

        /// <summary>Turn a bone so its rest direction points along dirLocal (in the coach's own space).</summary>
        void Aim(Transform bone, Vector3 restDir, Vector3 dirLocal, float amount)
        {
            if (bone == null || dirLocal.sqrMagnitude < 1e-8f) return;
            Quaternion q = Quaternion.FromToRotation(restDir, dirLocal.normalized);
            if (amount < 1f) q = Quaternion.Slerp(Quaternion.identity, q, amount);
            bone.rotation = transform.rotation * q;
        }

        /// <summary>Back to the warm-up groove, standing on the mark.</summary>
        public void StopDancePose()
        {
            if (restPosCached) transform.localPosition = restLocalPos;
            if (hips != null) { hips.localRotation = Quaternion.identity; hips.localPosition = hipsRest; }
            ApplyFacing();
            playWarmup = true;
        }

        void Update()
        {
            if (toggleWithAButton && cached)
            {
                var rightHand = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRController>(UnityEngine.InputSystem.CommonUsages.RightHand);
                var a = rightHand != null ? rightHand.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton") : null;
                if (a != null && a.wasPressedThisFrame)
                    SetFacing(facing == Facing.BackToYou ? Facing.FacingYou : Facing.BackToYou);
            }

            if (!playWarmup || !cached) return;
            float beat = Time.time * bpm / 60f;          // beats since start
            int bar = Mathf.FloorToInt(beat / 4f);       // 4 beats per bar
            float inBeat = beat - Mathf.Floor(beat);     // 0..1 within the beat

            // When facing you, the coach mirrors: "your left" becomes their right.
            float side = facing == Facing.FacingYou ? -1f : 1f;

            // Bounce: down on every beat.
            float bounce = -0.035f * Mathf.Pow(1f - inBeat, 2f);
            // Step-touch: sway to one side over two beats, then the other.
            float sway = 0.09f * Mathf.Sin(beat * Mathf.PI) * side;
            hips.localPosition = hipsRest + new Vector3(sway, bounce, 0f);
            float legLean = -Mathf.Atan2(sway, 0.87f) * Mathf.Rad2Deg; // keeps the feet planted
            upperLegL.localRotation = Quaternion.Euler(0f, 0f, legLean);
            upperLegR.localRotation = Quaternion.Euler(0f, 0f, legLean);
            float knee = 12f + 18f * Mathf.Pow(1f - inBeat, 2f);
            upperLegL.localRotation *= Quaternion.Euler(-knee * 0.5f, 0f, 0f);
            upperLegR.localRotation *= Quaternion.Euler(-knee * 0.5f, 0f, 0f);
            lowerLegL.localRotation = Quaternion.Euler(knee, 0f, 0f);
            lowerLegR.localRotation = Quaternion.Euler(knee, 0f, 0f);

            spine.localRotation = Quaternion.Euler(4f, 0f, -sway * 40f);
            head.localRotation = Quaternion.Euler(-6f * Mathf.Pow(1f - inBeat, 2f), sway * 60f, 0f);

            // Arms: a different move each bar, in a 4-bar loop.
            float pulse = Mathf.Pow(1f - inBeat, 2f);
            Quaternion uL, lL, uR, lR;
            switch (bar % 4)
            {
                case 0: // arm swing, opposite to the step
                {
                    float s = 30f * Mathf.Sin(beat * Mathf.PI);
                    uL = Quaternion.Euler(s, 0f, -8f); uR = Quaternion.Euler(-s, 0f, 8f);
                    lL = lR = Quaternion.Euler(-25f, 0f, 0f);
                    break;
                }
                case 1: // claps in front of the chest, on every beat
                {
                    float open = 25f * (1f - pulse);
                    uL = Quaternion.Euler(-70f, 0f, -open); uR = Quaternion.Euler(-70f, 0f, open);
                    lL = Quaternion.Euler(-35f, 0f, 20f); lR = Quaternion.Euler(-35f, 0f, -20f);
                    break;
                }
                case 2: // point up, alternating sides every two beats
                {
                    bool leftUp = (Mathf.FloorToInt(beat / 2f) % 2 == 0) == (side > 0f);
                    Quaternion up = Quaternion.Euler(0f, 0f, 160f), rest = Quaternion.Euler(10f, 0f, 0f);
                    uL = leftUp ? Quaternion.Euler(0f, 0f, -160f) : rest;
                    uR = leftUp ? rest : up;
                    lL = lR = Quaternion.Euler(-10f, 0f, 0f);
                    break;
                }
                default: // arms out wide, lifting on the beat
                {
                    float lift = 70f + 15f * pulse;
                    uL = Quaternion.Euler(0f, 0f, -lift); uR = Quaternion.Euler(0f, 0f, lift);
                    lL = lR = Quaternion.Euler(0f, 0f, 0f);
                    break;
                }
            }
            upperArmL.localRotation = uL; lowerArmL.localRotation = lL;
            upperArmR.localRotation = uR; lowerArmR.localRotation = lR;
        }
    }
}

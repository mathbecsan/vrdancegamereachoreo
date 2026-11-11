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

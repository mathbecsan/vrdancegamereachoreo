using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Reachoreo
{
    /// <summary>
    /// Plays Encendido's robot parts on Reachy A and B, from the recorded score
    /// (reachy-dance tests/golden/encendido_robots.json: the preview's exact robot motion, 20 frames a second).
    /// Score units go through the same conversion and safety limits as the real robots
    /// (a port of reachy_dance/pose.py to_sdk, via the web app's xr/src/sdk.ts).
    /// </summary>
    public class EncendidoRobotPlayer : MonoBehaviour
    {
        public TextAsset score;
        public ReachyRig reachyA;
        public ReachyRig reachyB;
        public bool loop = true;
        [Tooltip("1 = real speed, 0.5 = half speed for practice.")]
        public float speed = 1f;

        [Header("Calibration (pose.py DEFAULT, same as the real robots)")]
        public float yawDeg = 40, bodyDeg = 30, pitchDeg = 25, zMmPerPx = 0.5f;
        public float yawLeftSign = 1, rollSign = 1, pitchDownSign = 1, antRightOutSign = 1, antLeftOutSign = -1;
        public bool antSwap;
        public float maxPitchRollDeg = 35, maxBodyDeg = 155, maxHeadBodyDeltaDeg = 60, maxAntennaRad = 1.45f, maxZmm = 12;

        float step = 0.05f;
        float[][] framesA, framesB;   // per frame: yaw, pitch, roll, body, turn, antL, antR, bob
        float t;

        public float Duration => framesA == null ? 0f : (framesA.Length - 1) * step;
        public float Time01 => Duration > 0f ? t / Duration : 0f;

        void Awake()
        {
            // In any place, use the Reachys on the PlaceRig's A and B spots unless linked by hand.
            var place = GetComponentInParent<PlaceRig>();
            if (place != null)
            {
                if (reachyA == null && place.reachyA != null) reachyA = place.reachyA.GetComponentInChildren<ReachyRig>(true);
                if (reachyB == null && place.reachyB != null) reachyB = place.reachyB.GetComponentInChildren<ReachyRig>(true);
            }
            if (score == null) { Debug.LogError("EncendidoRobotPlayer: no score"); return; }
            Parse(score.text);
        }

        void Update()
        {
            if (framesA == null) return;
            t += Time.deltaTime * speed;
            if (t > Duration) t = loop ? t % Duration : Duration;
            if (reachyA != null) reachyA.SetPose(ToSdk(Sample(framesA, t)));
            if (reachyB != null) reachyB.SetPose(ToSdk(Sample(framesB, t)));
        }

        public void Restart() { t = 0f; }

        float[] Sample(float[][] f, float time)
        {
            float x = time / step; int i = Mathf.Clamp(Mathf.FloorToInt(x), 0, f.Length - 1); int j = Mathf.Min(i + 1, f.Length - 1);
            float u = Mathf.Clamp01(x - i);
            var o = new float[8];
            for (int k = 0; k < 8; k++) o[k] = Mathf.Lerp(f[i][k], f[j][k], u);
            return o;
        }

        static float Clip(float x, float lim) => Mathf.Clamp(x, -lim, lim);

        /// <summary>Score units -> SDK units (pose.py to_sdk).</summary>
        public SdkCommand ToSdk(float[] p)
        {
            float yaw = p[0], pitch = p[1], roll = p[2], body = p[3], turn = p[4], antL = p[5], antR = p[6], bob = p[7];
            float bodyDegV = Clip(body * bodyDeg + turn * Mathf.Rad2Deg, maxBodyDeg);
            float relYaw = Clip(yaw * yawDeg, maxHeadBodyDeltaDeg);
            var c = new SdkCommand();
            c.headYawDeg = yawLeftSign * (bodyDegV + relYaw);
            c.bodyYawRad = yawLeftSign * bodyDegV * Mathf.Deg2Rad;
            c.headRollDeg = rollSign * Clip(roll * Mathf.Rad2Deg, maxPitchRollDeg);
            c.headPitchDeg = pitchDownSign * Clip(-pitch * pitchDeg, maxPitchRollDeg);
            c.headZmm = Clip(-bob * zMmPerPx, maxZmm);
            float camL = antSwap ? antR : antL, camR = antSwap ? antL : antR;
            c.antennaRightRad = antRightOutSign * Mathf.Clamp(camL, -0.3f, maxAntennaRad);
            c.antennaLeftRad = antLeftOutSign * Mathf.Clamp(camR, -0.3f, maxAntennaRad);
            return c;
        }

        // A small reader for {"step":0.05,"keys":[...],"A":[[...],...],"B":[[...],...]}.
        void Parse(string json)
        {
            int si = json.IndexOf("\"step\"");
            if (si >= 0) { int c = json.IndexOf(':', si) + 1; int e = json.IndexOfAny(new[] { ',', '}' }, c); step = float.Parse(json.Substring(c, e - c).Trim(), CultureInfo.InvariantCulture); }
            framesA = ReadFrames(json, "\"A\"");
            framesB = ReadFrames(json, "\"B\"");
        }

        static float[][] ReadFrames(string json, string key)
        {
            int k = json.IndexOf(key); if (k < 0) return null;
            int i = json.IndexOf('[', k) + 1;   // outer array
            var frames = new List<float[]>();
            var row = new List<float>();
            var num = new System.Text.StringBuilder();
            int depth = 1;
            for (; i < json.Length && depth > 0; i++)
            {
                char ch = json[i];
                if (ch == '[') { depth++; row.Clear(); }
                else if (ch == ']' || ch == ',')
                {
                    if (num.Length > 0) { row.Add(float.Parse(num.ToString(), CultureInfo.InvariantCulture)); num.Clear(); }
                    if (ch == ']') { if (depth == 2) frames.Add(row.ToArray()); depth--; }
                }
                else if (!char.IsWhiteSpace(ch)) num.Append(ch);
            }
            return frames.ToArray();
        }
    }
}

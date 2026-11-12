using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Reachoreo
{
    /// <summary>
    /// Sends the Reachys' moves to the real Reachy Minis, 30 times a second, through each robot's daemon
    /// (reachy-mini-daemon, WebSocket /api/move/ws/set_target: head pose in the robot's base frame in metres and
    /// radians, two antennas [right, left], body yaw). The same message the HRI-EU reachy-vr-quest teleop sends.
    ///
    /// Your Reachys are the USB "Lite" version, so their daemons run on the Mac (ports 8000 and 8001). With the
    /// headset plugged into the Mac, `adb reverse` makes the headset's localhost reach them (Tools/reachy_link.sh).
    /// Over Wi-Fi, put the Mac's address in persistentDataPath/reachy_link.json: {"hostA":"10.0.0.5","hostB":"10.0.0.5"}.
    ///
    /// Safety: every value is clamped to the robot's limits and speed-limited, and nothing is sent unless you
    /// turn "Real robots" on.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public class ReachyLink : MonoBehaviour
    {
        [Serializable]
        public class Robot
        {
            public string name = "Reachy A";
            public string host = "127.0.0.1";   // with adb reverse, the headset reaches the Mac through the USB cable
            public int port = 8000;
            [Tooltip("Send moves this much early (s): the robot's motor delay (reachy-dance show.yaml latency_s).")]
            public float latency = 0.16f;
            [NonSerialized] public ReachyRig rig;
            [NonSerialized] public ClientWebSocket ws;
            [NonSerialized] public volatile string status = "off";
            [NonSerialized] public volatile bool connected, connecting;
            [NonSerialized] public SdkCommand sent; [NonSerialized] public bool haveSent;
            [NonSerialized] public readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
            [NonSerialized] public float retryAt;
            [NonSerialized] public volatile bool warned;
        }

        public Robot a = new Robot { name = "Reachy A", port = 8000, latency = 0.16f };
        public Robot b = new Robot { name = "Reachy B", port = 8001, latency = 0.22f };
        public float sendRate = 30f;

        /// <summary>Optional: gives the pose to send for a robot (0 = A, 1 = B) and how early to send it.
        /// Set by the dance director while a dance plays; otherwise the virtual Reachy's current pose is sent.</summary>
        public Func<int, float, SdkCommand?> poseSource;

        public static bool Enabled { get => GameSession.RealRobots; set { GameSession.RealRobots = value; GameSession.Save(); } }
        public Robot[] Robots => new[] { a, b };

        float timer;
        CancellationTokenSource cts;

        [Serializable] class HostFile { public string hostA, hostB; public int portA, portB; }

        void Awake()
        {
            var place = GetComponentInParent<PlaceRig>();
            if (place != null)
            {
                a.rig = place.reachyA != null ? place.reachyA.GetComponentInChildren<ReachyRig>(true) : null;
                b.rig = place.reachyB != null ? place.reachyB.GetComponentInChildren<ReachyRig>(true) : null;
            }
            try
            {
                var f = Path.Combine(Application.persistentDataPath, "reachy_link.json");
                if (File.Exists(f))
                {
                    var h = JsonUtility.FromJson<HostFile>(File.ReadAllText(f));
                    if (!string.IsNullOrEmpty(h.hostA)) a.host = h.hostA;
                    if (!string.IsNullOrEmpty(h.hostB)) b.host = h.hostB;
                    if (h.portA > 0) a.port = h.portA;
                    if (h.portB > 0) b.port = h.portB;
                }
            }
            catch (Exception e) { Debug.LogWarning("Reachoreo: reachy_link.json: " + e.Message); }
            cts = new CancellationTokenSource();
        }

        void OnDestroy() { cts?.Cancel(); foreach (var r in Robots) Close(r); }
        void OnApplicationPause(bool paused) { if (paused) foreach (var r in Robots) Close(r); }

        void LateUpdate()
        {
            if (!Enabled)
            {
                foreach (var r in Robots) if (r.ws != null) { Close(r); r.status = "off"; }
                return;
            }
            foreach (var r in Robots)
                if (!r.connected && !r.connecting && Time.unscaledTime >= r.retryAt && r.rig != null && r.rig.isActiveAndEnabled) Connect(r);

            timer += Time.unscaledDeltaTime;
            if (timer < 1f / sendRate) return;
            float dt = timer; timer = 0f;
            for (int i = 0; i < 2; i++)
            {
                var r = i == 0 ? a : b;
                if (!r.connected || r.rig == null || !r.rig.isActiveAndEnabled) continue;
                var want = (poseSource != null ? poseSource(i, r.latency) : null) ?? r.rig.LastCommand;
                var c = Limit(want, r.haveSent ? r.sent : want, dt);
                r.sent = c; r.haveSent = true;
                if (r.outbox.Count < 3) r.outbox.Enqueue(Json(c));
            }
        }

        // ---------- Safety ----------

        static float Toward(float from, float to, float maxStep) => from + Mathf.Clamp(to - from, -maxStep, maxStep);

        /// <summary>Clamp to the robot's limits (as reachy_dance/pose.py) and limit speed, so a jump never reaches the motors.</summary>
        static SdkCommand Limit(SdkCommand w, SdkCommand prev, float dt)
        {
            float degStep = 400f * dt, radStep = 7f * dt, mmStep = 60f * dt;
            w.headRollDeg = Mathf.Clamp(w.headRollDeg, -35f, 35f);
            w.headPitchDeg = Mathf.Clamp(w.headPitchDeg, -35f, 35f);
            w.bodyYawRad = Mathf.Clamp(w.bodyYawRad, -155f * Mathf.Deg2Rad, 155f * Mathf.Deg2Rad);
            float bodyDeg = w.bodyYawRad * Mathf.Rad2Deg;
            w.headYawDeg = Mathf.Clamp(w.headYawDeg, bodyDeg - 60f, bodyDeg + 60f);
            w.headZmm = Mathf.Clamp(w.headZmm, -12f, 12f);
            w.antennaRightRad = Mathf.Clamp(w.antennaRightRad, -0.3f, 1.45f);
            w.antennaLeftRad = Mathf.Clamp(w.antennaLeftRad, -1.45f, 0.3f);
            return new SdkCommand
            {
                headRollDeg = Toward(prev.headRollDeg, w.headRollDeg, degStep),
                headPitchDeg = Toward(prev.headPitchDeg, w.headPitchDeg, degStep),
                headYawDeg = Toward(prev.headYawDeg, w.headYawDeg, degStep),
                headZmm = Toward(prev.headZmm, w.headZmm, mmStep),
                bodyYawRad = Toward(prev.bodyYawRad, w.bodyYawRad, radStep * 0.6f),
                antennaRightRad = Toward(prev.antennaRightRad, w.antennaRightRad, radStep),
                antennaLeftRad = Toward(prev.antennaLeftRad, w.antennaLeftRad, radStep),
            };
        }

        static string F(float x) => x.ToString("0.#####", CultureInfo.InvariantCulture);

        /// <summary>FullBodyTarget: head as XYZRPYPose (metres, radians, R.from_euler("xyz", [roll, pitch, yaw])).</summary>
        static string Json(SdkCommand c) =>
            "{\"target_head_pose\":{\"x\":0,\"y\":0,\"z\":" + F(c.headZmm / 1000f) +
            ",\"roll\":" + F(c.headRollDeg * Mathf.Deg2Rad) + ",\"pitch\":" + F(c.headPitchDeg * Mathf.Deg2Rad) + ",\"yaw\":" + F(c.headYawDeg * Mathf.Deg2Rad) +
            "},\"target_antennas\":[" + F(c.antennaRightRad) + "," + F(c.antennaLeftRad) + "],\"target_body_yaw\":" + F(c.bodyYawRad) + "}";

        // ---------- Connection ----------

        void Connect(Robot r)
        {
            r.connecting = true; r.status = "connecting...";
            var token = cts.Token;
            Task.Run(async () =>
            {
                var ws = new ClientWebSocket();
                try
                {
                    var uri = new Uri($"ws://{r.host}:{r.port}/api/move/ws/set_target");
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        timeout.CancelAfter(3000);
                        await ws.ConnectAsync(uri, timeout.Token);
                    }
                    r.ws = ws; r.connected = true; r.haveSent = false; r.status = $"connected ({r.host}:{r.port})";
                    r.warned = false; Debug.Log($"Reachoreo: {r.name} connected at {r.host}:{r.port}");
                    _ = Task.Run(() => Drain(ws, token));
                    while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
                    {
                        if (r.outbox.TryDequeue(out var msg))
                            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, token);
                        else await Task.Delay(5, token);
                    }
                    r.status = "disconnected";
                }
                catch (Exception e)
                {
                    r.status = e is OperationCanceledException ? $"not found at {r.host}:{r.port}" : $"not reachable at {r.host}:{r.port}";
                    if (!r.warned) { r.warned = true; Debug.LogWarning($"Reachoreo: {r.name} link failed ({r.host}:{r.port}): {e.GetType().Name}: {e.Message}"); }
                }
                finally
                {
                    r.connected = false; r.connecting = false;
                    if (r.ws == ws) r.ws = null;
                    try { ws.Dispose(); } catch { }
                }
            }, token);
            r.retryAt = Time.unscaledTime + 3f;
        }

        /// <summary>Read and drop the daemon's replies (errors are logged once).</summary>
        static async Task Drain(ClientWebSocket ws, CancellationToken token)
        {
            var buf = new byte[2048]; bool warned = false;
            try
            {
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), token);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    if (!warned) { warned = true; Debug.LogWarning("Reachoreo: robot replied: " + Encoding.UTF8.GetString(buf, 0, res.Count)); }
                }
            }
            catch { }
        }

        static void Close(Robot r)
        {
            var ws = r.ws; r.ws = null; r.connected = false;
            while (r.outbox.TryDequeue(out _)) { }
            if (ws == null) return;
            try { ws.Abort(); ws.Dispose(); } catch { }
        }
    }
}

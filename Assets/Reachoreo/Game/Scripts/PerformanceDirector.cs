using System;
using System.Collections;
using UnityEngine;

namespace Reachoreo
{
    /// <summary>
    /// Runs a dance: the dance clock, the count-in, the click track, the Reachys and the coach,
    /// pause and resume, and the sync score.
    ///
    /// Sync score: on every beat your tracked arms (upper arm and forearm directions, in your own
    /// body's frame) are compared with the coach's. Facing you, the coach is your mirror image, so
    /// your left arm is compared with their right. Each beat is judged Perfect / Good / OK / Miss.
    /// </summary>
    public class PerformanceDirector : MonoBehaviour
    {
        public enum State { Menu, Preview, Loading, CountIn, Playing, Paused, FreeDance, FollowMe, Finished }

        public EncendidoRobotPlayer robots;
        public DanceCoach coach;
        public OVRSkeleton body;
        public SelfAvatar avatar;

        public State state { get; private set; } = State.Menu;
        public DanceInfo song { get; private set; }
        public DanceSection section { get; private set; }

        // Clock
        float danceTime;          // seconds of dance time (0 = the first downbeat)
        float countIn;            // seconds of count-in left
        float elapsed;            // seconds performed
        float loopStart, loopEnd; // previews and free dance
        State resumeTo;

        // Results
        public float SyncPercent => judgedBeats > 0 ? 100f * scoreSum / judgedBeats : 0f;
        public float TrackedPercent => totalBeats > 0 ? 100f * trackedBeats / totalBeats : 0f;
        public int Perfect, GoodCount, Ok, Miss, Streak, BestStreak;
        public string LastJudgement = ""; public float LastJudgementAge = 99f;
        public float Elapsed => elapsed;
        public float CountInLeft => countIn;
        public float DanceTime => danceTime;
        public float Progress01 => section == null ? 0f : Mathf.Clamp01((danceTime - section.start) / Mathf.Max(0.01f, section.end - section.start));
        public bool BodyTracked => body != null && body.IsInitialized && body.IsDataValid;

        /// <summary>Plain-language body tracking status for the ready check (and the headset log).</summary>
        public string TrackingStatus
        {
            get
            {
                if (body == null) return "not set up in this place";
                var ovrBody = body.GetComponent<OVRBody>();
                if (ovrBody != null && !ovrBody.enabled) return "could not start (allow body tracking in the headset's settings)";
                if (!body.IsInitialized) return "starting...";
                if (!body.IsDataValid) return "waiting to see you: stand on the cross, arms in view";
                return "tracking you";
            }
        }
        string lastStatus; float statusTimer;
        public bool PlayingRealSong => music.clip != null && music.isPlaying;
        public bool Following => state == State.FollowMe || (state == State.Paused && resumeTo == State.FollowMe);
        public event Action Finished;

        float scoreSum; int judgedBeats, totalBeats, trackedBeats;
        float beatScoreSum; int beatFrames, beatTrackedFrames; int lastBeat = -1;

        AudioSource click, music;
        public ReachyLink link { get; private set; }
        public FollowMe follow { get; private set; }

        void Awake()
        {
            var place = GetComponentInParent<PlaceRig>();
            var root = place != null ? place.transform : transform;
            if (robots == null) robots = root.GetComponentInChildren<EncendidoRobotPlayer>(true);
            if (coach == null) coach = root.GetComponentInChildren<DanceCoach>(true);
            if (body == null) body = FindFirstObjectByType<OVRSkeleton>();
            if (avatar == null) avatar = FindFirstObjectByType<SelfAvatar>();
            click = gameObject.AddComponent<AudioSource>();
            click.playOnAwake = false; click.loop = true; click.spatialBlend = 0f;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false; music.loop = false; music.spatialBlend = 0f;
            link = GetComponent<ReachyLink>() ?? gameObject.AddComponent<ReachyLink>();
            follow = GetComponent<FollowMe>() ?? gameObject.AddComponent<FollowMe>();
            // The real robots get each move a little early (their motor delay) while a dance plays.
            link.poseSource = (side, early) =>
                state == State.Playing || state == State.Preview || state == State.FreeDance || state == State.CountIn
                    ? robots.HasFrames && !(side == 0 ? robots.externalA : robots.externalB) ? robots.SampleSdk(side, danceTime + early) : (SdkCommand?)null
                    : null;
        }

        void Start()
        {
            GameSession.Load();
            ApplySettings();
            ToMenu();
        }

        public void ApplySettings()
        {
            click.volume = GameSession.MusicVolume * 0.7f;
            music.volume = GameSession.MusicVolume;
            if (avatar != null) avatar.SetView(GameSession.BodyView);
        }

        float Downbeat => song != null && song.hasMusicFile ? song.data.song.firstDownbeat : 0f;
        float Bpm => song == null ? 120f : song.data != null && song.data.countBpm > 0 ? song.data.countBpm : song.bpm;

        // ---------- States ----------

        /// <summary>Follow me: the chosen Reachys copy your head and hands; the music plays if the song has one.</summary>
        public void StartFollowMe()
        {
            var d = GameSession.Song ?? (DanceLibrary.All.Count > 0 ? DanceLibrary.All[0] : null);
            if (d == null) { Debug.LogWarning("Reachoreo: Follow me could not start: no dances loaded yet"); return; }
            Use(d); section = null;
            Time.timeScale = 1f; StopAudio();
            robots.SetEnabled(true, true); robots.bothDanceA = false; robots.Idle();
            robots.externalA = GameSession.Leader != 1; robots.externalB = GameSession.Leader != 0;
            follow.Begin((FollowMe.Who)GameSession.Leader);
            if (!ReachyLink.Enabled) ReachyLink.Enabled = true;   // Follow me is for leading the real robots too
            if (coach != null) coach.gameObject.SetActive(false);
            loopStart = 0f; loopEnd = d.durationSec; danceTime = 0f; elapsed = 0f;
            state = State.FollowMe;
            if (d.hasMusicFile) StartCoroutine(WhenSongReady(d, () => { if (state == State.FollowMe && song == d) PlayMusicAt(danceTime); }));
            else PlayClick(Bpm);
        }

        void StopFollow() { if (follow != null) follow.End(); robots.externalA = robots.externalB = false; }

        public void ToMenu()
        {
            StopFollow();
            Time.timeScale = 1f;
            StopAudio();
            state = State.Menu;
            robots.SetEnabled(true, true); robots.bothDanceA = false; robots.Idle();
            if (coach != null) { coach.gameObject.SetActive(true); coach.StopDancePose(); }
        }

        public void Preview(DanceInfo d)
        {
            if (d == null) { ToMenu(); return; }
            Use(d); state = State.Preview;
            robots.SetEnabled(true, true); robots.bothDanceA = false;
            loopStart = d.previewStart; loopEnd = Mathf.Min(d.durationSec, d.previewStart + d.previewLength);
            danceTime = loopStart;
            StopAudio();
            if (d.hasMusicFile) StartCoroutine(WhenSongReady(d, () => { if (state == State.Preview && song == d) PlayMusicAt(danceTime); }));
            else PlayClick(Bpm);
        }

        public void StartDance(DanceInfo d, int sectionIndex)
        {
            StopFollow();
            Use(d); section = d.sections[Mathf.Clamp(sectionIndex, 0, d.sections.Count - 1)];
            Time.timeScale = 1f;
            StopAudio();
            robots.SetEnabled(GameSession.LeftReachy, GameSession.RightReachy);
            robots.bothDanceA = GameSession.ReachysInSync;
            robots.Drive(section.start);
            if (coach != null) coach.gameObject.SetActive(GameSession.ShowCoach);
            ResetScore();
            danceTime = section.start; elapsed = 0f;
            if (d.hasMusicFile && d.songClip == null) { state = State.Loading; StartCoroutine(WhenSongReady(d, () => { if (state == State.Loading && song == d) BeginCountIn(); })); }
            else BeginCountIn();
        }

        void BeginCountIn()
        {
            countIn = GameSession.CountIn ? 4f * 60f / Bpm : 0f;
            PlayClick(Bpm);
            if (countIn > 0f) state = State.CountIn; else StartPlaying();
        }

        void StartPlaying()
        {
            state = State.Playing;
            if (song.hasMusicFile && song.songClip != null) { click.Stop(); PlayMusicAt(danceTime); }
        }

        public void StartFreeDance()
        {
            var d = GameSession.Song ?? (DanceLibrary.All.Count > 0 ? DanceLibrary.All[0] : null);
            if (d == null) return;
            StopFollow();
            Use(d); section = null;
            Time.timeScale = 1f;
            StopAudio();
            robots.SetEnabled(GameSession.LeftReachy, GameSession.RightReachy);
            robots.bothDanceA = GameSession.ReachysInSync;
            if (coach != null) coach.gameObject.SetActive(false);   // free dance: nobody to follow
            loopStart = 0f; loopEnd = d.durationSec; danceTime = 0f; elapsed = 0f;
            state = State.FreeDance;
            if (d.hasMusicFile) StartCoroutine(WhenSongReady(d, () => { if (state == State.FreeDance && song == d) PlayMusicAt(danceTime); }));
            else PlayClick(Bpm);
        }

        public void Pause()
        {
            if (state != State.Playing && state != State.CountIn && state != State.FreeDance && state != State.FollowMe) return;
            resumeTo = state; state = State.Paused;
            Time.timeScale = 0f; click.Pause(); music.Pause();
        }

        public void Resume()
        {
            if (state != State.Paused) return;
            state = resumeTo; Time.timeScale = 1f; click.UnPause(); music.UnPause();
        }

        public void Restart()
        {
            if (song == null) return;
            if (state == State.FollowMe || (state == State.Paused && resumeTo == State.FollowMe)) StartFollowMe();
            else if (section == null) StartFreeDance(); else StartDance(song, song.sections.IndexOf(section));
        }

        public void Finish()
        {
            Time.timeScale = 1f;
            StopAudio();
            state = State.Finished;
            robots.Idle();
            if (coach != null) coach.StopDancePose();
            Finished?.Invoke();
        }

        void Use(DanceInfo d)
        {
            if (song != d) robots.LoadDance(d.data);
            song = d;
        }

        IEnumerator WhenSongReady(DanceInfo d, Action then)
        {
            yield return DanceLibrary.LoadSong(d);
            while (d.songLoading) yield return null;
            then();
        }

        void PlayMusicAt(float t)
        {
            if (song.songClip == null) { PlayClick(Bpm); return; }
            click.Stop();
            music.clip = song.songClip;
            music.volume = GameSession.MusicVolume;
            music.time = Mathf.Clamp(t + Downbeat, 0f, song.songClip.length - 0.05f);
            music.Play();
        }

        void StopAudio() { click.Stop(); music.Stop(); }

        void PlayClick(float bpm)
        {
            click.clip = MakeClickBar(bpm);
            click.volume = GameSession.MusicVolume * 0.7f;
            click.Play();
        }

        // ---------- Every frame ----------

        void Update()
        {
            float dt = Time.deltaTime;
            statusTimer += Time.unscaledDeltaTime;
            if (statusTimer > 3f) { statusTimer = 0f; var st = TrackingStatus; if (st != lastStatus) { lastStatus = st; Debug.Log("Reachoreo: body tracking " + st); } }
            switch (state)
            {
                case State.FollowMe:
                    if (music.isPlaying) danceTime = music.time - Downbeat; else danceTime += dt;
                    elapsed += dt;
                    if (danceTime >= loopEnd) { danceTime = loopStart; if (song.songClip != null) PlayMusicAt(danceTime); }
                    return;
                case State.Preview:
                case State.FreeDance:
                    // Follow the music when it plays, else our own clock; loop the stretch.
                    if (music.isPlaying) danceTime = music.time - Downbeat; else danceTime += dt;
                    elapsed += dt;
                    if (danceTime >= loopEnd || danceTime < loopStart - 1f)
                    {
                        danceTime = loopStart;
                        if (music.clip != null && (music.isPlaying || song.songClip != null)) PlayMusicAt(danceTime);
                    }
                    robots.Drive(danceTime);
                    if (state == State.Preview && coach != null && song.data.dancer != null && coach.isActiveAndEnabled) coach.PoseFromDance(song.data, danceTime);
                    return;
                case State.CountIn:
                    countIn -= dt;
                    if (coach != null && coach.isActiveAndEnabled) coach.PoseFromDance(song.data, section.start);
                    if (countIn <= 0f) StartPlaying();
                    return;
                case State.Playing:
                    break;
                default:
                    return;
            }

            danceTime = music.isPlaying ? music.time - Downbeat : danceTime + dt;
            elapsed += dt;
            robots.Drive(danceTime);
            if (coach != null && coach.isActiveAndEnabled) coach.PoseFromDance(song.data, danceTime);
            Score(dt);
            LastJudgementAge += dt;
            bool songOver = song.hasMusicFile && music.clip != null && !music.isPlaying && Time.timeScale > 0f && elapsed > 1f;
            if (danceTime >= section.end || songOver) Finish();
        }

        // ---------- Sync score ----------
        //
        // Every frame we compare your arms with the coach's: the direction of each upper arm and forearm, in each
        // body's own frame (so turning doesn't matter), allowing for the time it takes to follow someone (0 to
        // 0.3 s behind the coach). Every count we judge it, and when the coach moves you must move too: standing
        // still while the coach dances is a Miss. Counts where the coach just stands still are not rated.

        public float LiveMatch { get; private set; }     // 0..1, smoothed: how close your arms are right now
        public float LiveMove { get; private set; }      // 0..1: how much you move compared with the coach
        public bool CoachResting { get; private set; }

        struct ArmSample { public float t; public Vector3 a, b, c, d; public Vector3 wl, wr; }
        readonly System.Collections.Generic.List<ArmSample> coachHist = new System.Collections.Generic.List<ArmSample>();
        float beatMatchSum, beatPlayerMove, beatCoachMove; bool beatCoachPosed;
        Vector3 prevPWl, prevPWr, prevCWl, prevCWr; bool havePrevP, havePrevC;
        float lastScore;

        void ResetScore()
        {
            scoreSum = 0; judgedBeats = totalBeats = trackedBeats = 0;
            Perfect = GoodCount = Ok = Miss = Streak = BestStreak = 0;
            ResetBeat(); lastBeat = -1;
            coachHist.Clear(); havePrevP = havePrevC = false;
            LastJudgement = ""; LastJudgementAge = 99f; LiveMatch = 0f; LiveMove = 0f;
        }

        /// <summary>Which count of the dance a time falls in, using the song's tracked counts when it has them.</summary>
        int CountIndex(float t)
        {
            var c = song.data != null ? song.data.counts : null;
            if (c == null || c.Length < 2) return Mathf.FloorToInt(t * Bpm / 60f);
            int lo = 0, hi = c.Length - 1;
            if (t <= c[0]) return 0;
            if (t >= c[hi]) return hi;
            while (hi - lo > 1) { int m = (lo + hi) >> 1; if (c[m] <= t) lo = m; else hi = m; }
            return lo;
        }

        void ResetBeat() { beatScoreSum = beatMatchSum = beatPlayerMove = beatCoachMove = 0; beatFrames = beatTrackedFrames = 0; beatCoachPosed = false; }

        void Score(float dt)
        {
            int beat = CountIndex(danceTime);
            if (lastBeat < 0) lastBeat = beat;
            if (beat != lastBeat) { JudgeBeat(); lastBeat = beat; }
            beatFrames++;
            if (coach == null || !coach.gameObject.activeInHierarchy) return;

            // The coach, every frame (kept for 0.5 s so we can match you against where it was a moment ago).
            if (!CoachArms(out var cs)) return;
            cs.t = danceTime;
            coachHist.Add(cs);
            while (coachHist.Count > 0 && coachHist[0].t < danceTime - 0.5f) coachHist.RemoveAt(0);
            if (havePrevC) beatCoachMove += (cs.wl - prevCWl).magnitude + (cs.wr - prevCWr).magnitude;
            prevCWl = cs.wl; prevCWr = cs.wr; havePrevC = true;
            if (!ArmsDown(cs)) beatCoachPosed = true;

            if (!BodyTracked || !PlayerArms(out var ps)) { havePrevP = false; return; }
            if (havePrevP) beatPlayerMove += (ps.wl - prevPWl).magnitude + (ps.wr - prevPWr).magnitude;
            prevPWl = ps.wl; prevPWr = ps.wr; havePrevP = true;

            float best = 0f;
            foreach (float lag in new[] { 0f, 0.1f, 0.2f, 0.3f })
            {
                var c = CoachAt(danceTime - lag);
                float m = (Seg(ps.a, c.a) + Seg(ps.b, c.b) + Seg(ps.c, c.c) + Seg(ps.d, c.d)) / 4f;
                if (m > best) best = m;
            }
            beatTrackedFrames++; beatMatchSum += best;
            LiveMatch = Mathf.Lerp(LiveMatch, best, 0.15f);
        }

        void JudgeBeat()
        {
            totalBeats++;
            bool tracked = beatFrames > 0 && beatTrackedFrames >= beatFrames / 2;
            // The coach is resting: arms down and hardly moving. Nothing to copy, so this count isn't rated.
            CoachResting = !beatCoachPosed && beatCoachMove < 0.12f;
            if (!tracked)
            {
                LastJudgement = CoachResting ? "" : BodyTracked ? "" : "Not tracked";
                LastJudgementAge = 0f; ResetBeat(); return;
            }
            trackedBeats++;
            if (CoachResting) { LastJudgement = ""; ResetBeat(); return; }

            float pose = beatMatchSum / beatTrackedFrames;
            // Movement: if the coach moves, you have to move too (at least 60% as much, measured at the wrists).
            float move = beatCoachMove > 0.12f ? Mathf.Clamp01(beatPlayerMove / (0.6f * beatCoachMove)) : 1f;
            LiveMove = move;
            float s = pose * (0.35f + 0.65f * move);
            lastScore = s;
            judgedBeats++; scoreSum += s;
            Thresholds(out float perfect, out float good, out float ok);
            if (s >= perfect) { Perfect++; Streak++; LastJudgement = "Perfect"; Buzz(0.5f, 0.08f); }
            else if (s >= good) { GoodCount++; Streak++; LastJudgement = "Good"; }
            else if (s >= ok) { Ok++; Streak = 0; LastJudgement = "OK"; }
            else { Miss++; Streak = 0; LastJudgement = move < 0.3f ? "Miss: move with the coach" : "Miss"; }
            BestStreak = Mathf.Max(BestStreak, Streak);
            LastJudgementAge = 0f;
            ResetBeat();
        }

        public void BuzzBoth(float amplitude, float seconds) => Buzz(amplitude, seconds);

        /// <summary>A short controller vibration (nothing happens with hand tracking).</summary>
        void Buzz(float amplitude, float seconds)
        {
            foreach (var h in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics.HapticImpulsePlayer>(FindObjectsSortMode.None))
                if (h.isActiveAndEnabled) h.SendHapticImpulse(amplitude, seconds);
        }

        static void Thresholds(out float perfect, out float good, out float ok)
        {
            switch (GameSession.Tolerance)
            {
                case SyncTolerance.Relaxed: perfect = 0.75f; good = 0.55f; ok = 0.38f; break;
                case SyncTolerance.Strict: perfect = 0.88f; good = 0.74f; ok = 0.55f; break;
                default: perfect = 0.82f; good = 0.65f; ok = 0.45f; break;
            }
        }

        /// <summary>1 when two arm directions agree within ~20 degrees (Normal), 0 beyond ~70.</summary>
        static float Seg(Vector3 you, Vector3 them)
        {
            float lo = 20f, hi = 70f;
            if (GameSession.Tolerance == SyncTolerance.Relaxed) { lo = 28f; hi = 85f; }
            else if (GameSession.Tolerance == SyncTolerance.Strict) { lo = 14f; hi = 55f; }
            float ang = Vector3.Angle(you, them);
            return Mathf.Clamp01((hi - ang) / (hi - lo));
        }

        static bool ArmsDown(ArmSample c) => Vector3.Dot(c.a, Vector3.down) > 0.85f && Vector3.Dot(c.c, Vector3.down) > 0.85f
                                              && Vector3.Dot(c.b, Vector3.down) > 0.75f && Vector3.Dot(c.d, Vector3.down) > 0.75f;

        ArmSample CoachAt(float t)
        {
            if (coachHist.Count == 0) return default;
            var best = coachHist[coachHist.Count - 1];
            foreach (var c in coachHist) if (Mathf.Abs(c.t - t) < Mathf.Abs(best.t - t)) best = c;
            return best;
        }

        /// <summary>Your arms in your body's frame: a/b = left upper arm/forearm, c/d = right; wrists in arm lengths.</summary>
        bool PlayerArms(out ArmSample s)
        {
            s = default;
            Transform sl = Bone("FullBody_LeftArmUpper"), el = Bone("FullBody_LeftArmLower"), wl = Bone("FullBody_LeftHandWrist");
            Transform sr = Bone("FullBody_RightArmUpper"), er = Bone("FullBody_RightArmLower"), wr = Bone("FullBody_RightHandWrist");
            if (sl == null || el == null || wl == null || sr == null || er == null || wr == null) return false;
            return Arms(sl.position, el.position, wl.position, sr.position, er.position, wr.position, false, out s);
        }

        /// <summary>The coach's arms in its own frame, as the arms you should copy: facing you it is your mirror
        /// image, so its right arm is what your left arm should do (with left and right flipped).</summary>
        bool CoachArms(out ArmSample s)
        {
            s = default;
            Transform sl = coach.upperArmL, el = coach.lowerArmL, sr = coach.upperArmR, er = coach.lowerArmR;
            Transform wl = el != null ? el.Find("Hand_L") : null, wr = er != null ? er.Find("Hand_R") : null;
            if (sl == null || el == null || wl == null || sr == null || er == null || wr == null) return false;
            return Arms(sl.position, el.position, wl.position, sr.position, er.position, wr.position, coach.facing == DanceCoach.Facing.FacingYou, out s);
        }

        /// <summary>Arm directions in the body's own frame (x = its right, y = up, z = its front), wrists relative
        /// to the chest in arm lengths. Mirrored: sides swapped and x flipped, as seen in a mirror.</summary>
        static bool Arms(Vector3 sl, Vector3 el, Vector3 wl, Vector3 sr, Vector3 er, Vector3 wr, bool mirror, out ArmSample s)
        {
            s = default;
            Vector3 right = Vector3.ProjectOnPlane(sr - sl, Vector3.up);
            if (right.sqrMagnitude < 1e-6f) return false;
            right.Normalize();
            Vector3 fwd = Vector3.Cross(right, Vector3.up);
            Vector3 L(Vector3 d) => new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, Vector3.up), Vector3.Dot(d, fwd));
            Vector3 F(Vector3 v) => new Vector3(-v.x, v.y, v.z);
            float arm = GameSession.ArmLength > 0.3f ? GameSession.ArmLength
                      : Mathf.Max(0.3f, ((el - sl).magnitude + (wl - el).magnitude + (er - sr).magnitude + (wr - er).magnitude) / 2f);
            Vector3 chest = (sl + sr) * 0.5f;
            Vector3 A = L(el - sl).normalized, B = L(wl - el).normalized, C = L(er - sr).normalized, D = L(wr - er).normalized;
            Vector3 WL = L(wl - chest) / arm, WR = L(wr - chest) / arm;
            s = mirror ? new ArmSample { a = F(C), b = F(D), c = F(A), d = F(B), wl = F(WR), wr = F(WL) }
                       : new ArmSample { a = A, b = B, c = C, d = D, wl = WL, wr = WR };
            return true;
        }

        public Transform Bone(string id)
        {
            if (body == null || body.Bones == null) return null;
            var want = (OVRSkeleton.BoneId)Enum.Parse(typeof(OVRSkeleton.BoneId), id);
            foreach (var b in body.Bones) if (b.Id == want) return b.Transform;
            return null;
        }

        // ---------- Click track ----------

        /// <summary>One bar of clicks at the tempo, accent on beat 1. Made in code (no audio files).</summary>
        static AudioClip MakeClickBar(float bpm)
        {
            const int rate = 44100;
            float beat = 60f / bpm;
            int n = Mathf.RoundToInt(rate * beat * 4f);
            var data = new float[n];
            for (int b = 0; b < 4; b++)
            {
                int s0 = Mathf.RoundToInt(b * beat * rate);
                float freq = b == 0 ? 1568f : 1046f, amp = b == 0 ? 0.55f : 0.35f;
                for (int i = 0; i < rate * 0.04f && s0 + i < n; i++)
                    data[s0 + i] = amp * Mathf.Sin(2f * Mathf.PI * freq * i / rate) * Mathf.Exp(-i / (rate * 0.008f));
            }
            var clip = AudioClip.Create("Click " + bpm + " BPM", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

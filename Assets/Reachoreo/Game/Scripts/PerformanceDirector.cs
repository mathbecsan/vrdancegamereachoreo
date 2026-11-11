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
        public enum State { Menu, Preview, Loading, CountIn, Playing, Paused, FreeDance, Finished }

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
        public event Action Finished;

        float scoreSum; int judgedBeats, totalBeats, trackedBeats;
        float beatScoreSum; int beatFrames, beatTrackedFrames; int lastBeat = -1;

        AudioSource click, music;

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
            if (avatar != null) avatar.SetShowInFirstPerson(GameSession.ShowBodyFirstPerson);
        }

        float Downbeat => song != null && song.hasMusicFile ? song.data.song.firstDownbeat : 0f;
        float Bpm => song == null ? 120f : song.data != null && song.data.countBpm > 0 ? song.data.countBpm : song.bpm;

        // ---------- States ----------

        public void ToMenu()
        {
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
            if (state != State.Playing && state != State.CountIn && state != State.FreeDance) return;
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
            if (section == null) StartFreeDance(); else StartDance(song, song.sections.IndexOf(section));
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

        void ResetScore()
        {
            scoreSum = 0; judgedBeats = totalBeats = trackedBeats = 0;
            Perfect = GoodCount = Ok = Miss = Streak = BestStreak = 0;
            beatScoreSum = 0; beatFrames = beatTrackedFrames = 0; lastBeat = -1;
            LastJudgement = ""; LastJudgementAge = 99f;
        }

        void Score(float dt)
        {
            int beat = CountIndex(danceTime);
            if (lastBeat < 0) lastBeat = beat;
            if (beat != lastBeat) { JudgeBeat(); lastBeat = beat; }

            beatFrames++;
            if (!BodyTracked || coach == null || !coach.gameObject.activeInHierarchy) return;
            if (TryFrameScore(out float s)) { beatTrackedFrames++; beatScoreSum += s; }
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

        void JudgeBeat()
        {
            totalBeats++;
            if (beatFrames == 0) return;
            bool tracked = beatTrackedFrames >= beatFrames / 2;
            if (tracked)
            {
                trackedBeats++;
                float s = beatScoreSum / Mathf.Max(1, beatTrackedFrames);
                judgedBeats++; scoreSum += s;
                Thresholds(out float perfect, out float good, out float ok);
                if (s >= perfect) { Perfect++; Streak++; LastJudgement = "Perfect"; Buzz(0.5f, 0.08f); }
                else if (s >= good) { GoodCount++; Streak++; LastJudgement = "Good"; }
                else if (s >= ok) { Ok++; Streak = 0; LastJudgement = "OK"; }
                else { Miss++; Streak = 0; LastJudgement = "Miss"; }
                BestStreak = Mathf.Max(BestStreak, Streak);
            }
            else LastJudgement = BodyTracked ? "" : "Not tracked";
            LastJudgementAge = 0f;
            beatScoreSum = 0; beatFrames = beatTrackedFrames = 0;
        }

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
                case SyncTolerance.Relaxed: perfect = 0.86f; good = 0.74f; ok = 0.60f; break;
                case SyncTolerance.Strict: perfect = 0.94f; good = 0.87f; ok = 0.75f; break;
                default: perfect = 0.91f; good = 0.81f; ok = 0.67f; break;
            }
        }

        bool TryFrameScore(out float score)
        {
            score = 0f;
            Transform pSL = Bone("FullBody_LeftArmUpper"), pEL = Bone("FullBody_LeftArmLower"), pWL = Bone("FullBody_LeftHandWrist");
            Transform pSR = Bone("FullBody_RightArmUpper"), pER = Bone("FullBody_RightArmLower"), pWR = Bone("FullBody_RightHandWrist");
            if (pSL == null || pSR == null || pEL == null || pER == null || pWL == null || pWR == null) return false;
            Transform cSL = coach.upperArmL, cEL = coach.lowerArmL, cSR = coach.upperArmR, cER = coach.lowerArmR;
            Transform cWL = cEL.Find("Hand_L"), cWR = cER.Find("Hand_R");
            if (cWL == null || cWR == null) return false;

            Frame(pSL.position, pSR.position, out var pr, out var pf);
            Frame(cSL.position, cSR.position, out var cr, out var cf);
            bool mirror = coach.facing == DanceCoach.Facing.FacingYou;

            // Your left arm vs. their left (or, facing you, their right with left/right flipped).
            float s = 0f;
            s += Match(Local(pEL.position - pSL.position, pr, pf), Local(mirror ? cER.position - cSR.position : cEL.position - cSL.position, cr, cf), mirror);
            s += Match(Local(pWL.position - pEL.position, pr, pf), Local(mirror ? cWR.position - cER.position : cWL.position - cEL.position, cr, cf), mirror);
            s += Match(Local(pER.position - pSR.position, pr, pf), Local(mirror ? cEL.position - cSL.position : cER.position - cSR.position, cr, cf), mirror);
            s += Match(Local(pWR.position - pER.position, pr, pf), Local(mirror ? cWL.position - cEL.position : cWR.position - cER.position, cr, cf), mirror);
            score = s / 4f;
            return true;
        }

        static void Frame(Vector3 leftShoulder, Vector3 rightShoulder, out Vector3 right, out Vector3 forward)
        {
            right = Vector3.ProjectOnPlane(rightShoulder - leftShoulder, Vector3.up).normalized;
            forward = Vector3.Cross(right, Vector3.up);
        }

        static Vector3 Local(Vector3 d, Vector3 right, Vector3 forward) =>
            new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, Vector3.up), Vector3.Dot(d, forward)).normalized;

        static float Match(Vector3 you, Vector3 them, bool mirror)
        {
            if (mirror) them.x = -them.x;
            return (Vector3.Dot(you, them) + 1f) * 0.5f;
        }

        Transform Bone(string id)
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

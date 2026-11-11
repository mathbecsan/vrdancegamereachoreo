using System;
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
        public enum State { Menu, Preview, CountIn, Playing, Paused, FreeDance, Finished }

        public EncendidoRobotPlayer robots;
        public DanceCoach coach;
        public OVRSkeleton body;
        public SelfAvatar avatar;

        public State state { get; private set; } = State.Menu;
        public DanceInfo song { get; private set; }
        public DanceSection section { get; private set; }

        // Clock
        float danceTime;          // seconds into the dance (score time)
        float countIn;            // seconds of count-in left
        float elapsed;            // seconds performed
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
        public event Action Finished;

        float scoreSum; int judgedBeats, totalBeats, trackedBeats;
        float beatScoreSum; int beatFrames, beatTrackedFrames; int lastBeat = -1;

        AudioSource click;

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
        }

        void Start()
        {
            GameSession.Load();
            ApplySettings();
            ToMenu();
        }

        public void ApplySettings()
        {
            click.volume = GameSession.MusicVolume;
            if (avatar != null) avatar.SetShowInFirstPerson(GameSession.ShowBodyFirstPerson);
        }

        // ---------- States ----------

        public void ToMenu()
        {
            Time.timeScale = 1f;
            click.Stop();
            state = State.Menu;
            robots.SetEnabled(true, true); robots.bothDanceA = false; robots.Idle();
            if (coach != null) { coach.gameObject.SetActive(true); coach.playWarmup = true; }
        }

        public void Preview(DanceInfo d)
        {
            if (d == null) { ToMenu(); return; }
            song = d; state = State.Preview;
            robots.SetEnabled(true, true); robots.bothDanceA = false;
            robots.PlayLoop(d.previewStart, d.previewStart + d.previewLength);
            PlayClick(d.bpm);
        }

        public void StartDance(DanceInfo d, int sectionIndex)
        {
            song = d; section = d.sections[Mathf.Clamp(sectionIndex, 0, d.sections.Count - 1)];
            Time.timeScale = 1f;
            robots.SetEnabled(GameSession.LeftReachy, GameSession.RightReachy);
            robots.bothDanceA = GameSession.ReachysInSync;
            robots.Drive(section.start);
            if (coach != null) { coach.gameObject.SetActive(GameSession.ShowCoach); coach.playWarmup = true; }
            ResetScore();
            danceTime = section.start; elapsed = 0f;
            countIn = GameSession.CountIn ? 4f * 60f / d.bpm : 0f;
            state = countIn > 0f ? State.CountIn : State.Playing;
            PlayClick(d.bpm);
        }

        public void StartFreeDance()
        {
            song = DanceLibrary.All[0]; section = null;
            Time.timeScale = 1f;
            robots.SetEnabled(GameSession.LeftReachy, GameSession.RightReachy);
            robots.bothDanceA = GameSession.ReachysInSync;
            var energetic = song.sections[song.sections.Count - 1];
            robots.PlayLoop(energetic.start, energetic.end);
            if (coach != null) coach.gameObject.SetActive(false);   // free dance: nobody to follow
            elapsed = 0f;
            state = State.FreeDance;
            PlayClick(song.bpm);
        }

        public void Pause()
        {
            if (state != State.Playing && state != State.CountIn && state != State.FreeDance) return;
            resumeTo = state; state = State.Paused;
            Time.timeScale = 0f; click.Pause();
        }

        public void Resume()
        {
            if (state != State.Paused) return;
            state = resumeTo; Time.timeScale = 1f; click.UnPause();
        }

        public void Restart()
        {
            if (song == null) return;
            if (section == null) StartFreeDance(); else StartDance(song, song.sections.IndexOf(section));
        }

        public void Finish()
        {
            Time.timeScale = 1f;
            click.Stop();
            state = State.Finished;
            robots.Idle();
            Finished?.Invoke();
        }

        void PlayClick(float bpm)
        {
            click.clip = MakeClickBar(bpm);
            click.volume = GameSession.MusicVolume;
            click.Play();
        }

        // ---------- Every frame ----------

        void Update()
        {
            float dt = Time.deltaTime;
            if (state == State.CountIn)
            {
                countIn -= dt;
                if (countIn <= 0f) { state = State.Playing; dt = -countIn; countIn = 0f; } else return;
            }
            if (state == State.FreeDance) { elapsed += dt; return; }
            if (state != State.Playing) return;

            danceTime += dt; elapsed += dt;
            robots.Drive(danceTime);
            Score(dt);
            LastJudgementAge += dt;
            if (danceTime >= section.end) Finish();
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
            int beat = Mathf.FloorToInt((danceTime - section.start) * song.bpm / 60f);
            if (lastBeat < 0) lastBeat = beat;
            if (beat != lastBeat) { JudgeBeat(); lastBeat = beat; }

            beatFrames++;
            if (!BodyTracked || coach == null || !coach.gameObject.activeInHierarchy) return;
            if (TryFrameScore(out float s)) { beatTrackedFrames++; beatScoreSum += s; }
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
                if (s >= perfect) { Perfect++; Streak++; LastJudgement = "Perfect"; }
                else if (s >= good) { GoodCount++; Streak++; LastJudgement = "Good"; }
                else if (s >= ok) { Ok++; Streak = 0; LastJudgement = "OK"; }
                else { Miss++; Streak = 0; LastJudgement = "Miss"; }
                BestStreak = Mathf.Max(BestStreak, Streak);
            }
            else LastJudgement = BodyTracked ? "" : "Not tracked";
            LastJudgementAge = 0f;
            beatScoreSum = 0; beatFrames = beatTrackedFrames = 0;
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

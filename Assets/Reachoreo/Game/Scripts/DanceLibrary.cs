using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Reachoreo
{
    // ---------- The dance file (schema "reachoreo.dance/1", written by reachy-dance tools/export_dance.py) ----------

    [Serializable] public class DanceLicense { public string name, url, credit; }
    [Serializable] public class DanceSong { public string file; public float bpm, firstDownbeat, duration; }
    [Serializable] public class DancePhrase { public int n; public string name, act; public float start, end; public string[] pieces; }

    [Serializable]
    public class DanceFile
    {
        public string schema, id, title, artist, style;
        public string[] tags;
        public int difficulty;
        public DanceLicense license;
        public DanceSong song;
        public float countBpm, beatsPerCount, duration;
        public float[] counts;
        public DancePhrase[] phrases;
        public float frameRate;
        public int frames;
        public string[] robotKeys;
        public float[] robotA, robotB;      // frames x 8, score units
        public string[] dancerJoints;
        public float[] dancer;              // frames x joints x 3
        public float[] dancerRoot;          // frames x 3: x_m, depth, face_rad

        public bool HasSong => song != null && !string.IsNullOrEmpty(song.file);
        public bool HasLicense => license != null && !string.IsNullOrEmpty(license.name);
    }

    /// <summary>A part of a dance the player can choose to perform.</summary>
    public class DanceSection
    {
        public string name;
        public float start, end;   // seconds of dance time
        public DanceSection(string name, float start, float end) { this.name = name; this.start = start; this.end = end; }
    }

    /// <summary>One dance as the menus see it, plus where its data and song live.</summary>
    public class DanceInfo
    {
        public string id, title, artist, style, styleLabel;
        public float bpm, durationSec;
        public int difficulty;
        public Color color;
        public bool personal;              // from "My songs": the player's own track, never shipped
        public string folderUrl;           // where dance.json and the song are
        public DanceFile data;
        public AudioClip songClip;         // loaded when first needed
        public bool songLoading;
        public float previewStart, previewLength = 8f;
        public List<DanceSection> sections = new List<DanceSection>();

        public bool hasMusicFile => data != null && data.HasSong;
        public string StyleGroup => DanceLibrary.GroupOf(style);
        public string DifficultyLabel => difficulty <= 1 ? "Easy" : difficulty == 2 ? "Medium" : "Hard";
        public string Credit => data != null && data.HasLicense ? data.license.credit : null;

        public string PhraseAt(float t)
        {
            var ph = data?.phrases;
            if (ph == null || ph.Length == 0) return "";
            int i = 0;
            while (i < ph.Length - 1 && t >= ph[i + 1].start) i++;
            var p = ph[i];
            return (string.IsNullOrEmpty(p.act) ? "" : p.act + " · ") + p.n + ". " + p.name;
        }
    }

    /// <summary>
    /// All dances in the game: the built-in ones shipped in StreamingAssets/Dances (listed in index.json), and the
    /// player's own ("My songs") in the app's storage on the headset (persistentDataPath/Dances/&lt;id&gt;/dance.json).
    /// On a Quest that is /sdcard/Android/data/com.reachoreo.vr/files/Dances/.
    /// </summary>
    public static class DanceLibrary
    {
        public static readonly List<DanceInfo> All = new List<DanceInfo>();
        public static bool Loaded { get; private set; }
        public static string PersonalFolder => Path.Combine(Application.persistentDataPath, "Dances");

        // Filters shown on the song screen, and the styles each one covers.
        public static readonly (string label, string[] styles)[] Groups =
        {
            ("Latin", new[] { "salsa", "cumbia", "bachata", "marinera", "merengue", "reggaeton", "samba", "tango", "bossa-nova", "latin" }),
            ("Pop", new[] { "pop", "pop-rock", "rock" }),
            ("Electronic", new[] { "electronic", "edm", "house", "techno", "robot-electro", "synthwave" }),
            ("Hip-hop", new[] { "hip-hop", "urban", "rap", "trap" }),
            ("Funk & disco", new[] { "funk", "disco", "soul", "jazz" }),
            ("World", new[] { "afrobeat", "west-african", "reggae", "ska", "celtic", "folk", "world", "bollywood" }),
        };

        public static string GroupOf(string style)
        {
            foreach (var (label, styles) in Groups) if (Array.IndexOf(styles, style) >= 0) return label;
            return "Other";
        }

        public static string StyleLabel(string style)
        {
            if (string.IsNullOrEmpty(style) || style == "unknown") return "Your song";
            var s = style.Replace('-', ' ');
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        static readonly Color[] Palette =
        {
            new Color(0.30f, 0.36f, 0.62f), new Color(0.62f, 0.33f, 0.30f), new Color(0.27f, 0.52f, 0.46f),
            new Color(0.55f, 0.42f, 0.22f), new Color(0.45f, 0.30f, 0.55f), new Color(0.25f, 0.45f, 0.60f),
        };

        public static IEnumerator LoadAll()
        {
            All.Clear();
            // Built-in dances
            string baseUrl = Url(Application.streamingAssetsPath) + "/Dances";
            string index = null;
            yield return ReadText(baseUrl + "/index.json", t => index = t);
            if (index != null)
                foreach (var id in JsonUtility.FromJson<IndexFile>(index).dances)
                {
                    string folder = baseUrl + "/" + id, json = null;
                    yield return ReadText(folder + "/dance.json", t => json = t);
                    if (json != null) Add(json, folder, false);
                }
            // The player's own dances
            if (Directory.Exists(PersonalFolder))
                foreach (var dir in Directory.GetDirectories(PersonalFolder))
                {
                    var f = Path.Combine(dir, "dance.json");
                    if (!File.Exists(f)) continue;
                    try { Add(File.ReadAllText(f), Url(dir), true); }
                    catch (Exception e) { Debug.LogWarning($"Reachoreo: could not read {f}: {e.Message}"); }
                }
            Loaded = true;
            Debug.Log($"Reachoreo: {All.Count} dances loaded");
        }

        [Serializable] class IndexFile { public string[] dances; }

        static void Add(string json, string folderUrl, bool personal)
        {
            var d = JsonUtility.FromJson<DanceFile>(json);
            if (d == null || d.schema != "reachoreo.dance/1" || d.frames <= 0) return;
            var info = new DanceInfo
            {
                id = d.id, title = d.title, artist = string.IsNullOrEmpty(d.artist) ? (personal ? "My song" : "") : d.artist,
                style = string.IsNullOrEmpty(d.style) ? "unknown" : d.style, bpm = d.HasSong ? d.song.bpm : d.countBpm,
                durationSec = d.duration, difficulty = Mathf.Clamp(d.difficulty, 1, 3), personal = personal || !d.HasLicense && d.HasSong,
                folderUrl = folderUrl, data = d, color = Palette[Mathf.Abs(d.id.GetHashCode()) % Palette.Length],
            };
            if (d.id == "encendido") info.color = Palette[0];
            info.styleLabel = StyleLabel(info.style);
            BuildSections(info);
            All.Add(info);
        }

        /// <summary>Full dance, then its acts (if it has them) or three parts, cut on phrase lines.</summary>
        static void BuildSections(DanceInfo info)
        {
            var ph = info.data.phrases;
            info.sections.Add(new DanceSection("Full dance", 0f, info.durationSec));
            if (ph == null || ph.Length < 3) { info.previewStart = 0f; return; }
            var acts = new List<(string name, int from, int to)>();
            for (int i = 0; i < ph.Length; i++)
            {
                string a = ph[i].act ?? "";
                if (acts.Count == 0 || acts[acts.Count - 1].name != a) acts.Add((a, i, i)); else acts[acts.Count - 1] = (a, acts[acts.Count - 1].from, i);
            }
            if (acts.Count >= 2 && acts.Count <= 6 && !string.IsNullOrEmpty(acts[0].name))
                foreach (var (name, from, to) in acts) info.sections.Add(new DanceSection("Act " + name, ph[from].start, ph[to].end));
            else
            {
                int third = Mathf.Max(1, ph.Length / 3);
                string[] names = { "Beginning", "Middle", "End" };
                for (int k = 0; k < 3; k++)
                {
                    int from = k * third, to = k == 2 ? ph.Length - 1 : (k + 1) * third - 1;
                    info.sections.Add(new DanceSection(names[k], ph[from].start, ph[to].end));
                }
            }
            // Preview: a phrase about 60% of the way in, where songs are usually going strong.
            info.previewStart = ph[Mathf.Clamp(Mathf.RoundToInt(ph.Length * 0.6f), 0, ph.Length - 1)].start;
        }

        /// <summary>Loads the song the first time it is needed.</summary>
        public static IEnumerator LoadSong(DanceInfo d)
        {
            if (d == null || !d.hasMusicFile || d.songClip != null || d.songLoading) yield break;
            d.songLoading = true;
            string url = d.folderUrl + "/" + UnityWebRequest.EscapeURL(d.data.song.file).Replace("+", "%20");
            var type = d.data.song.file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? AudioType.WAV
                     : d.data.song.file.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS : AudioType.MPEG;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) d.songClip = DownloadHandlerAudioClip.GetContent(req);
                else Debug.LogWarning($"Reachoreo: could not load the song for {d.id}: {req.error}");
            }
            d.songLoading = false;
        }

        static string Url(string path) => path.Contains("://") ? path : "file://" + path;

        static IEnumerator ReadText(string url, Action<string> done)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                done(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null);
            }
        }
    }
}

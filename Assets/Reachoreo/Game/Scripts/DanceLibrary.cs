using System.Collections.Generic;
using UnityEngine;

namespace Reachoreo
{
    public enum DanceStyle { RobotElectro, Salsa, Cumbia, Bachata, Marinera }

    /// <summary>A part of a dance the player can choose to perform.</summary>
    public class DanceSection
    {
        public string name;
        public float start, end;   // seconds into the dance
        public DanceSection(string name, float start, float end) { this.name = name; this.start = start; this.end = end; }
    }

    /// <summary>One dance: its music facts, its parts, and where its Reachy score comes from.</summary>
    public class DanceInfo
    {
        public string id, title, artist, styleLabel;
        public DanceStyle style;
        public float bpm, durationSec;
        public int difficulty;          // 1 easy, 2 medium, 3 hard
        public Color color;
        public bool hasMusicFile;
        public float previewStart, previewLength = 8f;
        public float phraseSeconds = 4f;
        public string[] phrases;        // phrase names, one per phraseSeconds
        public string[] acts;           // act names, with the phrase index each act starts at
        public int[] actStarts;
        public List<DanceSection> sections = new List<DanceSection>();

        public bool IsLatin => style != DanceStyle.RobotElectro;
        public string DifficultyLabel => difficulty <= 1 ? "Easy" : difficulty == 2 ? "Medium" : "Hard";

        public string PhraseAt(float t)
        {
            if (phrases == null || phrases.Length == 0) return "";
            int i = Mathf.Clamp(Mathf.FloorToInt(t / phraseSeconds), 0, phrases.Length - 1);
            string act = "";
            if (acts != null) for (int a = 0; a < acts.Length; a++) if (i >= actStarts[a]) act = acts[a];
            return (act.Length > 0 ? act + " · " : "") + (i + 1) + ". " + phrases[i];
        }
    }

    /// <summary>
    /// The dances in the game. For now only Encendido, whose Reachy parts are recorded.
    /// Dances made with the Reachoreo app (salsa, cumbia, bachata, marinera...) will be added here
    /// as they are exported from the app.
    /// </summary>
    public static class DanceLibrary
    {
        public static readonly List<DanceInfo> All = new List<DanceInfo>();

        static DanceLibrary()
        {
            var encendido = new DanceInfo
            {
                id = "encendido",
                title = "Encendido",
                artist = "Reachoreo original",
                style = DanceStyle.RobotElectro,
                styleLabel = "Robot electro",
                bpm = 120f,
                durationSec = 76f,
                difficulty = 2,
                color = new Color(0.30f, 0.36f, 0.62f),
                hasMusicFile = false,
                previewStart = 48f,
                phrases = new[] { "Powered off", "A flicker", "Anyone there?", "Lonely", "Charging", "Reboot", "First steps", "Peekaboo",
                                  "Hood off", "Chicken peck", "Circle", "Spin", "Running man", "Running man turn", "Electro arms",
                                  "Robot square", "The wave", "Everybody", "Full power" },
                acts = new[] { "I. Off", "II. The reboot", "III. The spark", "IV. Switched on" },
                actStarts = new[] { 0, 4, 8, 12 },
            };
            encendido.sections.Add(new DanceSection("Full dance", 0f, 76f));
            encendido.sections.Add(new DanceSection("Acts I-II: Off and reboot", 0f, 32f));
            encendido.sections.Add(new DanceSection("Acts III-IV: Spark and switched on", 32f, 76f));
            encendido.sections.Add(new DanceSection("Act IV only: Switched on", 48f, 76f));
            All.Add(encendido);
        }

        public static string StyleName(DanceStyle s) => s == DanceStyle.RobotElectro ? "Robot electro" : s.ToString();
    }
}

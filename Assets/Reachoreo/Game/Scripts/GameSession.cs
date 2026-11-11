using System.Collections.Generic;
using UnityEngine;

namespace Reachoreo
{
    public enum SyncTolerance { Relaxed, Normal, Strict }

    /// <summary>
    /// Everything the player has chosen, kept while moving between screens (song, choreography,
    /// place, Reachys, guides) plus settings that are saved on the headset between sessions.
    /// </summary>
    public static class GameSession
    {
        // This visit's choices
        public static DanceInfo Song;
        public static int Section;               // index into Song.sections
        public static string PlaceId = "studio";
        public static bool LeftReachy = true;
        public static bool RightReachy = true;
        public static bool ReachysInSync;        // both dance Reachy A's part
        public static bool ShowCoach = true;
        public static bool CountIn = true;
        public static bool SeatedUpperBody;      // score the upper body only

        // Saved settings
        public static float MusicVolume = 0.8f;
        public static float RobotVolume = 0.6f;
        public static SyncTolerance Tolerance = SyncTolerance.Normal;
        public static bool ShowBodyFirstPerson;

        static readonly HashSet<string> favorites = new HashSet<string>();
        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            MusicVolume = PlayerPrefs.GetFloat("music_volume", MusicVolume);
            RobotVolume = PlayerPrefs.GetFloat("robot_volume", RobotVolume);
            Tolerance = (SyncTolerance)PlayerPrefs.GetInt("sync_tolerance", (int)Tolerance);
            ShowBodyFirstPerson = PlayerPrefs.GetInt("body_first_person", 0) == 1;
            foreach (var id in PlayerPrefs.GetString("favorites", "").Split(','))
                if (!string.IsNullOrEmpty(id)) favorites.Add(id);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("music_volume", MusicVolume);
            PlayerPrefs.SetFloat("robot_volume", RobotVolume);
            PlayerPrefs.SetInt("sync_tolerance", (int)Tolerance);
            PlayerPrefs.SetInt("body_first_person", ShowBodyFirstPerson ? 1 : 0);
            PlayerPrefs.SetString("favorites", string.Join(",", favorites));
            PlayerPrefs.Save();
        }

        public static bool IsFavorite(DanceInfo d) => d != null && favorites.Contains(d.id);

        public static void SetFavorite(DanceInfo d, bool on)
        {
            if (d == null) return;
            if (on) favorites.Add(d.id); else favorites.Remove(d.id);
            Save();
        }
    }
}

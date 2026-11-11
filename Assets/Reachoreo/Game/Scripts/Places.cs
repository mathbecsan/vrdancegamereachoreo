using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reachoreo
{
    public class PlaceInfo
    {
        public string id, name, scene, blurb;
        public bool ready;
        public Color color;
    }

    /// <summary>
    /// The Stanford places you can dance in. Each one is its own scene with the same PlaceRig (dance mark,
    /// coach, mirror, Reachy spots, your player rig), so everything travels with you. Your song, choices and
    /// settings stay in GameSession while you travel.
    /// </summary>
    public static class Places
    {
        public static readonly List<PlaceInfo> All = new List<PlaceInfo>
        {
            new PlaceInfo { id = "studio", name = "SRC dance studio", scene = "Place_DanceStudio", ready = true,
                            blurb = "The studio with the mirror wall.", color = new Color(0.36f, 0.38f, 0.42f) },
            new PlaceInfo { id = "hoover", name = "Hoover Tower", scene = "Place_HooverTower", ready = true,
                            blurb = "The plaza by the fountain, under the tower.", color = new Color(0.62f, 0.50f, 0.34f) },
            new PlaceInfo { id = "memchu", name = "Memorial Church", scene = "Place_MemorialChurch", ready = true,
                            blurb = "The Main Quad, facing the church.", color = new Color(0.70f, 0.55f, 0.30f) },
            new PlaceInfo { id = "mcmurtry", name = "McMurtry Building", scene = "Place_McMurtry", ready = true,
                            blurb = "Under the wooden canopy of the art building.", color = new Color(0.45f, 0.33f, 0.24f) },
            new PlaceInfo { id = "kitchen", name = "SRC kitchen", scene = "Place_SRCKitchen", ready = false,
                            blurb = "Coming soon.", color = new Color(0.40f, 0.40f, 0.40f) },
        };

        public static PlaceInfo Current
        {
            get
            {
                string s = SceneManager.GetActiveScene().name;
                foreach (var p in All) if (p.scene == s) return p;
                return All[0];
            }
        }

        public static PlaceInfo Get(string id) { foreach (var p in All) if (p.id == id) return p; return null; }

        /// <summary>Go to another place; the menu reopens on 'resumeScreen' there.</summary>
        public static void Travel(string id, string resumeScreen)
        {
            var p = Get(id);
            if (p == null || !p.ready) return;
            GameSession.PlaceId = id;
            GameSession.ResumeScreen = resumeScreen;
            GameSession.Save();
            Time.timeScale = 1f;
            SceneManager.LoadScene(p.scene);
        }
    }
}

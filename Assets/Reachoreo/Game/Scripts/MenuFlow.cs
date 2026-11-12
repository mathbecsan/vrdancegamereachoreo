using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reachoreo
{
    /// <summary>
    /// The game's screens, built from code on a panel in front of you:
    /// 01 Main menu, 02 Song selection, 03 Dance configuration, 04 Pre-performance,
    /// 05 Live dance (a small panel at waist height), 06 Performance summary,
    /// plus How to play, Settings and Exit.
    /// Every screen has the same bar: Back, Home, Settings, Continue. Your choices live in
    /// GameSession, so nothing is lost when you move between screens.
    /// </summary>
    public class MenuFlow : MonoBehaviour
    {
        public PerformanceDirector director;

        [Header("Placement (in the PlaceRig's space; you stand at the origin facing +Z)")]
        public Vector3 menuPosition = new Vector3(0f, 1.32f, 1.05f);
        public Vector3 hudPosition = new Vector3(0f, 0.92f, 0.72f);
        public float hudTilt = 35f;

        class Screen
        {
            public string id, title;
            public RectTransform root;
            public readonly List<Action> refresh = new List<Action>();
            public bool back = true, home = true, settings = true, live;
            public string continueLabel;            // null: no Continue button
            public Func<bool> canContinue;
            public Action onContinue;
        }

        readonly Dictionary<string, Screen> screens = new Dictionary<string, Screen>();
        readonly Stack<string> history = new Stack<string>();
        string current;

        Canvas menu, hud;
        RectTransform content;
        TextMeshProUGUI headerTitle;
        Button navBack, navHome, navSettings, navContinue;
        RectTransform exitConfirm;
        float liveTimer;

        // Song screen state
        string filter = "All songs";
        RectTransform songGrid; TextMeshProUGUI songEmpty;
        readonly List<(Button card, DanceInfo song)> songCards = new List<(Button, DanceInfo)>();

        // HUD
        TextMeshProUGUI hudPhrase, hudTime, hudBig, hudSmall;
        RectTransform hudFill, hudPlayRow, hudPauseRow, hudMeter; Image hudMeterImg;
        Button hudEnd;

        void Awake()
        {
            if (director == null) director = GetComponent<PerformanceDirector>() ?? FindFirstObjectByType<PerformanceDirector>();
        }

        System.Collections.IEnumerator Start()
        {
            GameSession.Load();
            if (!DanceLibrary.Loaded) yield return DanceLibrary.LoadAll();
            if (GameSession.Song == null && DanceLibrary.All.Count > 0) GameSession.Song = DanceLibrary.All[0];
            var oldPanel = transform.parent != null ? transform.parent.Find("Coach_View_Panel") : null;
            if (oldPanel != null) oldPanel.gameObject.SetActive(false);   // the coach view now lives in Settings and Configuration

            var place = GetComponentInParent<PlaceRig>();
            var anchor = place != null ? place.transform : transform;
            BuildMenu(anchor);
            BuildHud(anchor);
            director.Finished += OnDanceFinished;
            GameSession.PlaceId = Places.Current.id;
            string resume = GameSession.ResumeScreen; GameSession.ResumeScreen = null;
            if (!string.IsNullOrEmpty(resume) && screens.ContainsKey(resume))
            {
                history.Push("main");
                if (resume == "config") history.Push("songs");
                Show(resume);
            }
            else if (!GameSession.Calibrated) { history.Push("main"); StartCalibration("main"); }
            else Show("main");
        }

        // ================= Navigation =================

        void Go(string id) { if (current != null) history.Push(current); Show(id); }

        void Back() { if (history.Count > 0) Show(history.Pop()); else Show("main"); }

        void Home() { history.Clear(); director.ToMenu(); Show("main"); }

        void Show(string id)
        {
            current = id;
            menu.gameObject.SetActive(true); hud.gameObject.SetActive(false);
            exitConfirm.gameObject.SetActive(false);
            foreach (var s in screens.Values) s.root.gameObject.SetActive(s.id == id);
            var sc = screens[id];
            headerTitle.text = sc.title;
            navBack.gameObject.SetActive(sc.back && history.Count > 0);
            navHome.gameObject.SetActive(sc.home);
            navSettings.gameObject.SetActive(sc.settings && id != "settings");
            navContinue.gameObject.SetActive(sc.continueLabel != null);
            if (sc.continueLabel != null) UiKit.SetLabel(navContinue, sc.continueLabel);
            Refresh();

            // What the Reachys do behind the menu
            if (id == "main") director.ToMenu();
            else if ((id == "songs" || id == "config" || id == "pre") && GameSession.Song != null &&
                     (director.state != PerformanceDirector.State.Preview || director.song != GameSession.Song))
                director.Preview(GameSession.Song);
        }

        void Refresh()
        {
            if (current == null) return;
            var sc = screens[current];
            foreach (var r in sc.refresh) r();
            navContinue.interactable = sc.canContinue == null || sc.canContinue();
        }

        void Update()
        {
            if (menu != null && menu.gameObject.activeSelf && current != null && screens[current].live)
            {
                liveTimer += Time.unscaledDeltaTime;
                if (liveTimer > 0.25f) { liveTimer = 0f; Refresh(); }
            }
            if (hud != null && hud.gameObject.activeSelf) UpdateHud();
            if (current == "calibrate" && menu.gameObject.activeSelf) CalibrationTick(Time.unscaledDeltaTime);
        }

        // ================= Menu panel =================

        void BuildMenu(Transform anchor)
        {
            menu = UiKit.WorldCanvas(anchor, "Menu", new Vector2(1120, 700), menuPosition, Quaternion.identity);
            var bg = UiKit.Box(menu.transform, "Background"); UiKit.Stretch(bg); UiKit.Fill(bg, UiKit.Bg);
            var col = UiKit.VStack(menu.transform, "Layout", 14, 28); UiKit.Stretch(col);

            var header = UiKit.HStack(col, "Header", 12); UiKit.Size(header, h: 48);
            headerTitle = UiKit.Text(header, "", 30, UiKit.Ink, true, TextAlignmentOptions.MidlineLeft); UiKit.Size(headerTitle, flexW: 1, h: 48);
            var mark = UiKit.Text(header, "REACHOREO", 20, UiKit.Coral, true, TextAlignmentOptions.MidlineRight); UiKit.Size(mark, 220, 48);

            content = UiKit.Box(col, "Content"); UiKit.Size(content, flexW: 1, flexH: 1);

            var nav = UiKit.HStack(col, "Nav", 12); UiKit.Size(nav, h: 64);
            navBack = UiKit.Button(nav, "Back", Back, UiKit.Style.Secondary, 150);
            navHome = UiKit.Button(nav, "Home", Home, UiKit.Style.Secondary, 150);
            navSettings = UiKit.Button(nav, "Settings", () => Go("settings"), UiKit.Style.Secondary, 170);
            UiKit.Spacer(nav);
            navContinue = UiKit.Button(nav, "Continue", () => { var s = screens[current]; s.onContinue?.Invoke(); }, UiKit.Style.Primary, 260);

            BuildMain(); BuildSongs(); BuildConfig(); BuildPre(); BuildHowTo(); BuildSettings(); BuildSummary(); BuildPlaces(); BuildCalibration();
            BuildExitConfirm();
        }

        Screen NewScreen(string id, string title)
        {
            var root = UiKit.Box(content, "Screen " + id); UiKit.Stretch(root);
            var s = new Screen { id = id, title = title, root = root };
            screens[id] = s;
            return s;
        }

        static TextMeshProUGUI Heading(Transform parent, string text)
        {
            var t = UiKit.Text(parent, text.ToUpperInvariant(), 17, UiKit.Mute, true);
            UiKit.Size(t, h: 26);
            return t;
        }

        // ---------- 01 Main menu ----------
        void BuildMain()
        {
            var s = NewScreen("main", "");
            s.back = false; s.home = false; s.settings = false;
            var row = UiKit.HStack(s.root, "Main", 40); UiKit.Stretch(row);
            var left = UiKit.VStack(row, "Title", 10, 10, TextAnchor.MiddleLeft); UiKit.Size(left, 520, flexH: 1);
            UiKit.Size(UiKit.Text(left, "Reachoreo", 92, UiKit.Ink, true), h: 110);
            UiKit.Size(UiKit.Text(left, "Dance with two Reachy Minis, in real places at Stanford.", 26, UiKit.Mute), h: 70);
            UiKit.Size(UiKit.Text(left, "Follow the coach in front of you, watch yourself in the mirror, and keep in sync with the robots beside you.", 21, UiKit.Mute), h: 80);

            var buttons = UiKit.VStack(row, "Buttons", 14, 10, TextAnchor.MiddleCenter); UiKit.Size(buttons, flexW: 1, flexH: 1);
            UiKit.Button(buttons, "Start dancing", () => { if (GameSession.Calibrated) Go("songs"); else { history.Push("main"); StartCalibration("songs"); } }, UiKit.Style.Primary, -1, 72, 28);
            UiKit.Button(buttons, "Free dance", StartFreeDance, UiKit.Style.Secondary, -1, 58, 24);
            UiKit.Button(buttons, "Follow me: lead the Reachys", StartFollowMe, UiKit.Style.Secondary, -1, 58, 22);
            UiKit.Button(buttons, "Places", () => Go("places"), UiKit.Style.Secondary, -1, 58, 24);
            UiKit.Button(buttons, "How to play", () => Go("howto"), UiKit.Style.Secondary, -1, 58, 24);
            UiKit.Button(buttons, "Settings", () => Go("settings"), UiKit.Style.Secondary, -1, 58, 24);
            UiKit.Button(buttons, "Exit", () => exitConfirm.gameObject.SetActive(true), UiKit.Style.Quiet, -1, 60, 24);
        }

        // ---------- 02 Song selection ----------
        void BuildSongs()
        {
            var s = NewScreen("songs", "Choose a song");
            s.live = true;
            s.continueLabel = "Continue"; s.canContinue = () => GameSession.Song != null; s.onContinue = () => Go("config");
            var row = UiKit.HStack(s.root, "Songs", 18); UiKit.Stretch(row);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;

            var filters = UiKit.VStack(row, "Filters", 8); UiKit.Size(filters, 210, flexW: 0, flexH: 1);
            var filterButtons = new List<(Button, string)>();
            var labels = new List<string> { "All songs", "My songs" };
            foreach (var (label, _) in DanceLibrary.Groups) labels.Add(label);
            labels.Add("Favorites");
            foreach (var f in labels)
            {
                var name = f;
                var b = UiKit.Button(filters, f, () => { filter = name; Refresh(); }, UiKit.Style.Secondary, -1, 46, 20);
                filterButtons.Add((b, f));
            }
            s.refresh.Add(() => { foreach (var (b, f) in filterButtons) UiKit.SetSelected(b, f == filter); });

            var middle = UiKit.Box(row, "Grid Area"); UiKit.Size(middle, flexW: 1, flexH: 1);
            songGrid = UiKit.Box(middle, "Grid"); UiKit.Stretch(songGrid);
            var grid = songGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(250, 270); grid.spacing = new Vector2(14, 14);
            songEmpty = UiKit.Text(middle, "", 22, UiKit.Mute, false, TextAlignmentOptions.Center); UiKit.Stretch((RectTransform)songEmpty.transform, 30);
            foreach (var d in DanceLibrary.All) songCards.Add((SongCard(songGrid, d), d));
            s.refresh.Add(RefreshSongGrid);

            var info = UiKit.Panel(row, "Info", UiKit.CardDim, 18, 8); UiKit.Size(info, 290, flexW: 0, flexH: 1);
            var title = UiKit.Text(info, "", 27, UiKit.Ink, true); UiKit.Size(title, h: 70);
            var artist = UiKit.Text(info, "", 20, UiKit.Mute); UiKit.Size(artist, h: 28);
            var facts = UiKit.Text(info, "", 19, UiKit.Ink); UiKit.Size(facts, h: 90);
            var preview = UiKit.Text(info, "", 18, UiKit.Mute); UiKit.Size(preview, h: 120);
            UiKit.Spacer(info, true);
            var fav = UiKit.Button(info, "Save to favorites", () => { var d = GameSession.Song; GameSession.SetFavorite(d, !GameSession.IsFavorite(d)); Refresh(); }, UiKit.Style.Secondary, -1, 54, 21);
            s.refresh.Add(() =>
            {
                var d = GameSession.Song;
                info.gameObject.SetActive(d != null);
                if (d == null) return;
                title.text = d.title; artist.text = d.artist;
                facts.text = $"{d.styleLabel}  ·  {d.bpm:0} BPM\nDuration {Clock(d.durationSec)}\nDifficulty: {d.DifficultyLabel} {Dots(d.difficulty)}";
                preview.text = "The Reachys and the coach are previewing the choreography (" + d.PhraseAt(d.previewStart) + ")." +
                               (!d.hasMusicFile ? "\nMusic: click track at the dance's tempo." : d.songLoading ? "\nLoading the song..." : "") +
                               (d.personal ? "\nYour song: it stays on this headset." : d.Credit != null ? "\nMusic: " + d.Credit : "");
                UiKit.SetLabel(fav, GameSession.IsFavorite(d) ? "In favorites" : "Save to favorites");
                UiKit.SetSelected(fav, GameSession.IsFavorite(d), UiKit.Teal);
            });
        }

        Button SongCard(Transform parent, DanceInfo d)
        {
            Button card = null;
            card = UiKit.Button(parent, "", () => { GameSession.Song = d; GameSession.Section = 0; director.Preview(d); Refresh(); });
            card.transform.Find("Label").gameObject.SetActive(false);
            var col = UiKit.VStack(card.transform, "Card", 6, 12); UiKit.Stretch(col);
            var tile = UiKit.Box(col, "Art"); UiKit.Size(tile, h: 120); UiKit.Fill(tile, d.color);
            var initial = UiKit.Text(tile, d.title.Substring(0, 1), 64, new Color(1, 1, 1, 0.85f), true, TextAlignmentOptions.Center); UiKit.Stretch((RectTransform)initial.transform);
            var tag = UiKit.Text(tile, d.styleLabel.ToUpperInvariant(), 14, new Color(1, 1, 1, 0.8f), true, TextAlignmentOptions.BottomLeft); UiKit.Stretch((RectTransform)tag.transform, 10);
            var cardTitle = UiKit.Text(col, d.title, 24, UiKit.Ink, true); UiKit.Size(cardTitle, h: 30);
            cardTitle.textWrappingMode = TextWrappingModes.NoWrap; cardTitle.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Size(UiKit.Text(col, d.artist, 17, UiKit.Mute), h: 22);
            UiKit.Size(UiKit.Text(col, $"{d.bpm:0} BPM · {Clock(d.durationSec)} · {d.DifficultyLabel}", 16, UiKit.Mute), h: 22);
            return card;
        }

        void RefreshSongGrid()
        {
            int shown = 0;
            foreach (var (card, d) in songCards)
            {
                bool match = filter == "All songs" || (filter == "My songs" && d.personal) || (filter == "Favorites" && GameSession.IsFavorite(d)) ||
                             d.StyleGroup == filter;
                card.gameObject.SetActive(match);
                UiKit.SetSelected(card, d == GameSession.Song, new Color(0.34f, 0.22f, 0.19f, 1f));
                if (match) shown++;
            }
            songEmpty.text = shown > 0 ? "" : filter == "Favorites"
                ? "No favorites yet. Select a song and press \"Save to favorites\"."
                : filter == "My songs" ? "No songs of your own yet.\nMake a dance from your song in the Reachoreo app and send it to the headset: it will appear here and stay private."
                : "No " + filter.ToLowerInvariant() + " dances yet.\nMore songs are coming to the library.";
        }

        // ---------- 03 Dance configuration ----------
        void BuildConfig()
        {
            var s = NewScreen("config", "Set up your dance");
            s.continueLabel = "Continue"; s.onContinue = () => Go("pre");
            var row = UiKit.HStack(s.root, "Columns", 22); UiKit.Stretch(row);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;

            // Column 1: choreography and place
            var c1 = UiKit.VStack(row, "Col1", 8); UiKit.Size(c1, 340, flexW: 0, flexH: 1);
            Heading(c1, "Choreography");
            var sectionButtons = new List<Button>();
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var b = UiKit.Button(c1, "", () => { GameSession.Section = k; Refresh(); }, UiKit.Style.Secondary, -1, 46, 18);
                sectionButtons.Add(b);
            }
            s.refresh.Add(() =>
            {
                var d = GameSession.Song;
                for (int i = 0; i < sectionButtons.Count; i++)
                {
                    bool has = d != null && i < d.sections.Count;
                    sectionButtons[i].gameObject.SetActive(has);
                    if (!has) continue;
                    var sec = d.sections[i];
                    UiKit.SetLabel(sectionButtons[i], $"{sec.name}  ({Clock(sec.end - sec.start)})");
                    UiKit.SetSelected(sectionButtons[i], GameSession.Section == i);
                }
            });
            Heading(c1, "Place");
            var placeGrid = UiKit.Box(c1, "Places"); UiKit.Size(placeGrid, h: 112);
            var pg = placeGrid.gameObject.AddComponent<GridLayoutGroup>(); pg.cellSize = new Vector2(166, 52); pg.spacing = new Vector2(8, 8);
            foreach (var place in Places.All)
            {
                if (!place.ready) continue;
                var pid = place.id;
                var b = UiKit.Button(placeGrid, place.name, () => { if (pid != Places.Current.id) Places.Travel(pid, "config"); }, UiKit.Style.Secondary, -1, 52, 16);
                s.refresh.Add(() => UiKit.SetSelected(b, GameSession.PlaceId == pid));
            }

            // Column 2: Reachys and tracking
            var c2 = UiKit.VStack(row, "Col2", 8); UiKit.Size(c2, 340, flexW: 0, flexH: 1);
            Heading(c2, "Reachys");
            Toggle(s, c2, "Left Reachy", () => GameSession.LeftReachy, v => GameSession.LeftReachy = v);
            Toggle(s, c2, "Right Reachy", () => GameSession.RightReachy, v => GameSession.RightReachy = v);
            Toggle(s, c2, "Both in sync", () => GameSession.ReachysInSync, v => GameSession.ReachysInSync = v);
            UiKit.Size(UiKit.Text(c2, "In sync: both dance the same part instead of their own.", 15, UiKit.Faint), h: 22);
            Heading(c2, "Tracking");
            Choice(s, c2, "Body", new[] { "Standing", "Seated" }, () => GameSession.SeatedUpperBody ? 1 : 0, v => GameSession.SeatedUpperBody = v == 1);
            UiKit.Size(UiKit.Text(c2, "Seated: only your upper body counts.", 15, UiKit.Faint), h: 22);

            // Column 3: guides and audio
            var c3 = UiKit.VStack(row, "Col3", 8); UiKit.Size(c3, 340, flexW: 0, flexH: 1);
            Heading(c3, "Guides");
            Toggle(s, c3, "Coach", () => GameSession.ShowCoach, v => GameSession.ShowCoach = v);
            Choice(s, c3, "View", new[] { "Back to you", "Facing you" }, () => director.coach != null && director.coach.facing == DanceCoach.Facing.FacingYou ? 1 : 0,
                   v => { if (director.coach != null) director.coach.SetFacing(v == 1 ? DanceCoach.Facing.FacingYou : DanceCoach.Facing.BackToYou); });
            Toggle(s, c3, "Count-in", () => GameSession.CountIn, v => GameSession.CountIn = v);
            Heading(c3, "Audio");
            VolumeRow(c3, "Music", () => GameSession.MusicVolume, v => { GameSession.MusicVolume = v; director.ApplySettings(); });
            VolumeRow(c3, "Reachy sounds", () => GameSession.RobotVolume, v => GameSession.RobotVolume = v);
        }

        void Toggle(Screen s, Transform parent, string label, Func<bool> get, Action<bool> set)
        {
            var row = UiKit.Row(parent, label, 150);
            Button b = null;
            b = UiKit.Button(row, "", () => { set(!get()); Refresh(); }, UiKit.Style.Secondary, -1, 48, 20);
            s.refresh.Add(() => { UiKit.SetLabel(b, get() ? "On" : "Off"); UiKit.SetSelected(b, get()); });
        }

        void Choice(Screen s, Transform parent, string label, string[] options, Func<int> get, Action<int> set, float labelWidth = 90)
        {
            var row = UiKit.Row(parent, label, labelWidth);
            var buttons = new List<Button>();
            for (int i = 0; i < options.Length; i++)
            {
                int k = i;
                buttons.Add(UiKit.Button(row, options[i], () => { set(k); Refresh(); }, UiKit.Style.Secondary, -1, 48, 18));
            }
            s.refresh.Add(() => { for (int i = 0; i < buttons.Count; i++) UiKit.SetSelected(buttons[i], get() == i); });
        }

        void VolumeRow(Transform parent, string label, Func<float> get, Action<float> set)
        {
            var row = UiKit.Row(parent, label, 150);
            UiKit.Slider(row, get(), v => { set(v); GameSession.Save(); });
        }

        // ---------- 04 Pre-performance ----------
        void BuildPre()
        {
            var s = NewScreen("pre", "Ready to dance?");
            s.live = true;
            s.continueLabel = "Start performance"; s.onContinue = StartPerformance;
            var row = UiKit.HStack(s.root, "Columns", 22); UiKit.Stretch(row);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;

            var left = UiKit.Panel(row, "Your dance", UiKit.CardDim, 22, 10); UiKit.Size(left, flexW: 1, flexH: 1);
            Heading(left, "Your dance");
            var summary = UiKit.Text(left, "", 22, UiKit.Ink); UiKit.Size(summary, h: 260);
            s.refresh.Add(() =>
            {
                var d = GameSession.Song; if (d == null) { summary.text = ""; return; }
                var sec = d.sections[Mathf.Clamp(GameSession.Section, 0, d.sections.Count - 1)];
                string robots = GameSession.LeftReachy && GameSession.RightReachy ? "Both Reachys" : GameSession.LeftReachy ? "Left Reachy only" : GameSession.RightReachy ? "Right Reachy only" : "No Reachys";
                summary.text = $"<b>{d.title}</b>  ·  {d.artist}\n{sec.name}, {Clock(sec.end - sec.start)}\n{Places.Current.name}\n{robots}{(GameSession.ReachysInSync ? ", in sync" : "")}\n" +
                               $"Coach: {(GameSession.ShowCoach ? (director.coach != null && director.coach.facing == DanceCoach.Facing.FacingYou ? "facing you" : "back to you") : "hidden")}\n" +
                               $"{(GameSession.SeatedUpperBody ? "Seated" : "Standing")}, count-in {(GameSession.CountIn ? "on" : "off")}";
            });

            var right = UiKit.Panel(row, "Checks", UiKit.CardDim, 22, 12); UiKit.Size(right, flexW: 1, flexH: 1);
            Heading(right, "Ready check");
            Check(s, right, () =>
                director.BodyTracked ? (UiKit.Good, "Body tracking: tracking you") : (UiKit.Warn, "Body tracking: " + director.TrackingStatus));
            Check(s, right, () => (UiKit.Good, "Reachys: virtual Reachys ready"));
            Check(s, right, () => (UiKit.Faint, "Real robots: not connected (comes with the Reachoreo app link)"));
            Check(s, right, () => (UiKit.Good, GameSession.Song != null && GameSession.Song.hasMusicFile
                ? "Sync: coach and Reachys follow the song's tracked counts" : $"Sync: coach and Reachys follow the same {(GameSession.Song != null ? GameSession.Song.bpm : 120):0} BPM clock"));
            Check(s, right, () => GameSession.Song == null ? (UiKit.Faint, "Audio: no song selected")
                : !GameSession.Song.hasMusicFile ? (UiKit.Warn, "Audio: click track at the dance's tempo (this dance has no song file)")
                : GameSession.Song.songClip != null ? (UiKit.Good, "Audio: song ready, the dance follows its real beats")
                : (UiKit.Warn, "Audio: loading the song..."));
            UiKit.Spacer(right, true);
            UiKit.Size(UiKit.Text(right, "Give yourself about 2 x 2 m of clear space. You stay on your mark the whole dance.", 17, UiKit.Faint), h: 50);
        }

        void Check(Screen s, Transform parent, Func<(Color, string)> get)
        {
            var row = UiKit.HStack(parent, "Check", 12); UiKit.Size(row, h: 54);
            var dot = UiKit.Dot(row, UiKit.Good, 18);
            var t = UiKit.Text(row, "", 19, UiKit.Ink, false, TextAlignmentOptions.MidlineLeft); UiKit.Size(t, flexW: 1, h: 54);
            s.refresh.Add(() => { var (c, text) = get(); dot.color = c; t.text = text; });
        }

        // ---------- How to play ----------
        void BuildHowTo()
        {
            var s = NewScreen("howto", "How to play");
            var grid = UiKit.Box(s.root, "Tips"); UiKit.Stretch(grid);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>(); g.cellSize = new Vector2(344, 250); g.spacing = new Vector2(16, 16);
            Tip(grid, "Hands or controllers", "Use either, and switch any time: pick up the Touch controllers or put them down. Poke a button, or point the ray at it and pull the trigger (or pinch).");
            Tip(grid, "Stay on your mark", "Stand on the orange cross with about 2 x 2 m of clear space. During a dance you never move artificially.");
            Tip(grid, "Follow the coach", "Copy the coral coach in front of you. \"Back to you\": copy directly. \"Facing you\": they are your mirror image. A on the right controller switches.");
            Tip(grid, "See yourself", "Your tracked body is the blue-teal figure in the mirror. Reachy A dances on your left, Reachy B on your right.");
            Tip(grid, "Your score", "Every count, your upper arms and forearms are compared with the coach's (a short delay is fine), and you must move when the coach moves. Standing still is a Miss. When the coach rests, nothing is rated.");
            Tip(grid, "Pause anytime", "The small panel at your waist shows the time and your rating. Press Pause to resume, restart or quit.");
        }

        void Tip(Transform parent, string title, string text)
        {
            var p = UiKit.Panel(parent, title, UiKit.CardDim, 20, 8);
            UiKit.Size(UiKit.Text(p, title, 24, UiKit.Coral, true), h: 32);
            UiKit.Size(UiKit.Text(p, text, 19, UiKit.Ink), h: 170);
        }

        // ---------- Settings ----------
        void BuildSettings()
        {
            var s = NewScreen("settings", "Settings");
            var row = UiKit.HStack(s.root, "Columns", 26); UiKit.Stretch(row);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            var c1 = UiKit.VStack(row, "Col1", 8); UiKit.Size(c1, 620, flexW: 0, flexH: 1);
            Heading(c1, "Audio");
            VolumeRow(c1, "Music", () => GameSession.MusicVolume, v => { GameSession.MusicVolume = v; director.ApplySettings(); });
            VolumeRow(c1, "Reachy sounds", () => GameSession.RobotVolume, v => GameSession.RobotVolume = v);
            Heading(c1, "Movement");
            Choice(s, c1, "Sync tolerance", new[] { "Relaxed", "Normal", "Strict" }, () => (int)GameSession.Tolerance, v => { GameSession.Tolerance = (SyncTolerance)v; GameSession.Save(); }, 200);
            Choice(s, c1, "Coach view", new[] { "Back to you", "Facing you" }, () => director.coach != null && director.coach.facing == DanceCoach.Facing.FacingYou ? 1 : 0,
                   v => { if (director.coach != null) director.coach.SetFacing(v == 1 ? DanceCoach.Facing.FacingYou : DanceCoach.Facing.BackToYou); }, 200);
            Heading(c1, "Body");
            var calRow = UiKit.Row(c1, "Calibration", 150);
            var calBtn = UiKit.Button(calRow, "Calibrate again", () => { history.Push(current); StartCalibration("settings"); }, UiKit.Style.Secondary, -1, 48, 19);
            s.refresh.Add(() => UiKit.SetLabel(calBtn, GameSession.Calibrated ? $"Calibrate again ({GameSession.Height:0.00} m)" : "Calibrate now"));
            Heading(c1, "Visuals");
            Choice(s, c1, "My body (looking down)", new[] { "Off", "Arms", "Full" }, () => GameSession.BodyView,
                   v => { GameSession.BodyView = v; GameSession.Save(); director.ApplySettings(); }, 200);

            var c2 = UiKit.Panel(row, "Robots", UiKit.CardDim, 20, 8); UiKit.Size(c2, 418, flexW: 0, flexH: 1);
            Heading(c2, "Real Reachy Minis");
            Toggle(s, c2, "Send moves", () => ReachyLink.Enabled, v => ReachyLink.Enabled = v);
            Choice(s, c2, "Follow me", new[] { "A", "B", "Both" }, () => GameSession.Leader, v => { GameSession.Leader = v; GameSession.Save(); }, 120);
            var robotStatus = UiKit.Text(c2, "", 16, UiKit.Ink); UiKit.Size(robotStatus, h: 92);
            s.refresh.Add(() =>
            {
                var l = director.link;
                robotStatus.text = l == null ? "" : !ReachyLink.Enabled ? "Off. Turn on to make your real Reachys move with the game." :
                    $"Reachy A: {l.a.status}\nReachy B: {l.b.status}\nPlug the headset into the Mac and run Tools/reachy_link.sh.";
            });
            s.live = true;
            Heading(c2, "About");
            UiKit.Size(UiKit.Text(c2,
                "Reachoreo VR, early build.\n\n" +
                "Reachy Mini 3D model and rig by Clement Plays, published by Pollen Robotics (Apache-2.0).\n\n" +
                "Coach, avatar and rooms: made for Reachoreo.\n" + MusicCredits(), 14, UiKit.Ink), h: 150);
        }

        // ---------- Calibration (required before dancing) ----------
        int calStep; float calHold, calNoTrack; string calThen = "main";
        float calHeight, calShoulders, calSpan;
        TextMeshProUGUI calTitle, calText, calStatus, calSteps; Image calDot; RectTransform calBar; Button calSkip;

        void StartCalibration(string then) { calThen = then; calStep = 0; calHold = 0; calNoTrack = 0; Show("calibrate"); }

        void BuildCalibration()
        {
            var s = NewScreen("calibrate", "Set up your body");
            s.home = false; s.settings = false;
            s.continueLabel = "Continue"; s.canContinue = () => calStep == 0 || calStep == 3;
            s.onContinue = () => { if (calStep == 0) { calStep = 1; calHold = 0; } else if (calStep == 3) FinishCalibration(); Refresh(); };
            var col = UiKit.Panel(s.root, "Calibration", UiKit.CardDim, 28, 14); UiKit.Stretch(col);
            calSteps = UiKit.Text(col, "", 18, UiKit.Mute, true); UiKit.Size(calSteps, h: 26);
            calTitle = UiKit.Text(col, "", 34, UiKit.Ink, true); UiKit.Size(calTitle, h: 46);
            calText = UiKit.Text(col, "", 22, UiKit.Ink); UiKit.Size(calText, h: 190);
            var row = UiKit.HStack(col, "Status", 12); UiKit.Size(row, h: 40);
            calDot = UiKit.Dot(row, UiKit.Warn, 20);
            calStatus = UiKit.Text(row, "", 20, UiKit.Ink, false, TextAlignmentOptions.MidlineLeft); UiKit.Size(calStatus, flexW: 1, h: 40);
            var track = UiKit.Box(col, "Hold"); UiKit.Size(track, h: 14); UiKit.Fill(track, UiKit.Card);
            calBar = UiKit.Box(track, "Fill"); calBar.anchorMin = Vector2.zero; calBar.anchorMax = new Vector2(0, 1); calBar.offsetMin = calBar.offsetMax = Vector2.zero;
            UiKit.Fill(calBar, UiKit.Teal);
            UiKit.Spacer(col, true);
            calSkip = UiKit.Button(col, "Continue without calibration (scores less accurate)", () => { GameSession.Calibrated = false; Leave(); }, UiKit.Style.Quiet, -1, 52, 18);
            s.refresh.Add(RefreshCalibration);
        }

        void RefreshCalibration()
        {
            if (calTitle == null) return;
            calSteps.text = $"STEP {calStep + 1} OF 4";
            calBar.parent.gameObject.SetActive(calStep == 1 || calStep == 2);
            calBar.anchorMax = new Vector2(Mathf.Clamp01(calHold / (calStep == 1 ? 2f : 1.5f)), 1);
            calSkip.gameObject.SetActive(calStep > 0 && calNoTrack > 15f);
            switch (calStep)
            {
                case 0:
                    calTitle.text = "Hands or controllers";
                    calText.text = "You can play with your hands or with the Touch controllers, and switch any time.\n\n" +
                                   "<b>To use the controllers:</b> press the Meta button (the round one) on each controller until it lights up. " +
                                   "The game switches to them as soon as you pick them up: point the ray at a button and pull the trigger.\n" +
                                   "If they don't wake up: check the AA batteries, or pair them in the Meta Horizon app (Devices > your headset > Controllers).";
                    var mode = UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager.currentInputMode.Value;
                    bool hands = mode == UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager.InputMode.TrackedHand;
                    bool ctrl = mode == UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager.InputMode.MotionController;
                    calDot.color = hands || ctrl ? UiKit.Good : UiKit.Warn;
                    calStatus.text = ctrl ? "Using: Touch controllers" : hands ? "Using: your hands (pick up a controller to switch)" : "No hands or controllers seen yet";
                    break;
                case 1:
                    calTitle.text = "Stand still";
                    calText.text = "Stand on the orange cross, facing the mirror. Stand tall, arms relaxed at your sides, and hold still for 2 seconds.\nThis measures your height and shoulders.";
                    break;
                case 2:
                    calTitle.text = "Make a T";
                    calText.text = "Stretch both arms straight out to the sides, level with your shoulders, palms down. Hold for 1.5 seconds.\nThis measures your arm span, so the score fits your body.";
                    break;
                default:
                    calTitle.text = "All set";
                    calText.text = $"Height: about {calHeight:0.00} m\nArm span: {calSpan:0.00} m\nArm length: {Mathf.Max(0.3f, (calSpan - calShoulders) / 2f):0.00} m\n\nYou can redo this any time in Settings.";
                    calDot.color = UiKit.Good; calStatus.text = "Calibrated";
                    break;
            }
        }

        void CalibrationTick(float dt)
        {
            if (calStep == 0 || calStep == 3) { liveTimer += 0f; if (Time.frameCount % 15 == 0) RefreshCalibration(); return; }
            if (!director.BodyTracked)
            {
                calNoTrack += dt; calHold = 0;
                calDot.color = UiKit.Warn; calStatus.text = "Body tracking: " + director.TrackingStatus;
                RefreshCalibration(); return;
            }
            calNoTrack = 0;
            Transform head = director.Bone("FullBody_Head"), sl = director.Bone("FullBody_LeftArmUpper"), sr = director.Bone("FullBody_RightArmUpper");
            Transform el = director.Bone("FullBody_LeftArmLower"), er = director.Bone("FullBody_RightArmLower");
            Transform wl = director.Bone("FullBody_LeftHandWrist"), wr = director.Bone("FullBody_RightHandWrist");
            Transform pl = director.Bone("FullBody_LeftHandPalm"), pr = director.Bone("FullBody_RightHandPalm");
            if (head == null || sl == null || sr == null || el == null || er == null || wl == null || wr == null) return;
            Vector3 ual = (el.position - sl.position).normalized, uar = (er.position - sr.position).normalized;
            Vector3 fal = (wl.position - el.position).normalized, far = (wr.position - er.position).normalized;
            bool ok;
            if (calStep == 1)
            {
                ok = Vector3.Dot(ual, Vector3.down) > 0.8f && Vector3.Dot(uar, Vector3.down) > 0.8f;
                calStatus.text = ok ? "Hold still..." : "Let your arms hang at your sides";
                if (ok) { calHeight = Mathf.Lerp(calHeight > 0 ? calHeight : head.position.y + 0.11f, head.position.y + 0.11f, 0.1f); calShoulders = (sr.position - sl.position).magnitude; }
            }
            else
            {
                bool level = Mathf.Abs(ual.y) < 0.35f && Mathf.Abs(uar.y) < 0.35f;
                bool straight = Vector3.Dot(ual, fal) > 0.85f && Vector3.Dot(uar, far) > 0.85f;
                bool apart = Vector3.Dot(ual, uar) < -0.6f;
                ok = level && straight && apart;
                calStatus.text = ok ? "Hold it..." : !apart || !level ? "Arms straight out to the sides, at shoulder height" : "Straighten your elbows";
                if (ok) calSpan = ((pl != null ? pl.position : wl.position) - (pr != null ? pr.position : wr.position)).magnitude + (pl != null ? 0.12f : 0.2f);
            }
            calDot.color = ok ? UiKit.Good : UiKit.Warn;
            calHold = ok ? calHold + dt : 0f;
            if (calHold >= (calStep == 1 ? 2f : 1.5f)) { calStep++; calHold = 0; director.BuzzBoth(0.4f, 0.1f); }
            RefreshCalibration();
            navContinue.interactable = calStep == 0 || calStep == 3;
        }

        void FinishCalibration()
        {
            GameSession.Calibrated = true;
            GameSession.Height = calHeight; GameSession.ShoulderWidth = calShoulders; GameSession.ArmSpan = calSpan;
            GameSession.ArmLength = Mathf.Max(0.3f, (calSpan - calShoulders) / 2f);
            GameSession.Save();
            Leave();
        }

        void Leave()
        {
            if (calThen == "songs") Go("songs");
            else if (calThen == "settings") Back();
            else { history.Clear(); Show("main"); }
        }

        // ---------- Places ----------
        void BuildPlaces()
        {
            var s = NewScreen("places", "Choose a place");
            var grid = UiKit.Box(s.root, "Places"); UiKit.Stretch(grid);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>(); g.cellSize = new Vector2(344, 250); g.spacing = new Vector2(16, 16);
            foreach (var place in Places.All)
            {
                var p = place;
                Button card = UiKit.Button(grid, "", () => { if (p.ready && p.id != Places.Current.id) Places.Travel(p.id, "main"); });
                card.transform.Find("Label").gameObject.SetActive(false);
                card.interactable = p.ready;
                var col = UiKit.VStack(card.transform, "Card", 8, 14); UiKit.Stretch(col);
                var tile = UiKit.Box(col, "Art"); UiKit.Size(tile, h: 120); UiKit.Fill(tile, p.color);
                UiKit.Size(UiKit.Text(col, p.name, 24, UiKit.Ink, true), h: 30);
                var sub = UiKit.Text(col, p.blurb, 17, UiKit.Mute); UiKit.Size(sub, h: 44);
                s.refresh.Add(() => { bool here = p.id == Places.Current.id; UiKit.SetSelected(card, here, new Color(0.34f, 0.22f, 0.19f, 1f)); sub.text = here ? "You are here." : p.blurb; });
            }
        }

        // ---------- 06 Summary ----------
        void BuildSummary()
        {
            var s = NewScreen("summary", "Your performance");
            s.back = false; s.settings = false;
            var col = UiKit.VStack(s.root, "Summary", 16); UiKit.Stretch(col);
            var headline = UiKit.Text(col, "", 40, UiKit.Ink, true); UiKit.Size(headline, h: 54);
            var grid = UiKit.Box(col, "Stats"); UiKit.Size(grid, h: 300);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>(); g.cellSize = new Vector2(339, 140); g.spacing = new Vector2(14, 14);
            var stats = new List<(TextMeshProUGUI value, Func<string> get)>();
            void Stat(string label, Func<string> get)
            {
                var p = UiKit.Panel(grid, label, UiKit.CardDim, 18, 4);
                UiKit.Size(UiKit.Text(p, label.ToUpperInvariant(), 16, UiKit.Mute, true), h: 22);
                var v = UiKit.Text(p, "", 38, UiKit.Ink, true); UiKit.Size(v, h: 80);
                stats.Add((v, get));
            }
            Stat("Sync accuracy", () => director.Perfect + director.GoodCount + director.Ok + director.Miss > 0 ? $"{director.SyncPercent:0}%" : "No score");
            Stat("Moves", () => $"<size=26>{director.Perfect} Perfect · {director.GoodCount} Good\n{director.Ok} OK · {director.Miss} Miss</size>");
            Stat("Best streak", () => $"{director.BestStreak} beats");
            Stat("Duration", () => Clock(director.Elapsed));
            Stat("Body tracked", () => $"{director.TrackedPercent:0}% of beats");
            Stat("Completed", () => $"{director.Progress01 * 100f:0}%");
            var buttons = UiKit.HStack(col, "Buttons", 14); UiKit.Size(buttons, h: 76);
            UiKit.Button(buttons, "Replay", () => { HideMenuForDance(); director.Restart(); }, UiKit.Style.Primary, 300, 72, 26);
            UiKit.Button(buttons, "Choose another song", () => { history.Clear(); history.Push("main"); Show("songs"); }, UiKit.Style.Secondary, 360, 72, 24);
            UiKit.Button(buttons, "Main menu", Home, UiKit.Style.Secondary, 260, 72, 24);
            s.refresh.Add(() =>
            {
                float sync = director.SyncPercent;
                bool scored = director.Perfect + director.GoodCount + director.Ok + director.Miss > 0;
                headline.text = director.Progress01 < 0.99f ? "Dance ended early" : !scored ? "Dance complete" : sync >= 85 ? "In perfect sync!" : sync >= 70 ? "Great dancing!" : sync >= 50 ? "Nice work, keep going" : "Good start, try again";
                foreach (var (v, get) in stats) v.text = get();
            });
        }

        void BuildExitConfirm()
        {
            exitConfirm = UiKit.Box(menu.transform, "Exit Confirm"); UiKit.Stretch(exitConfirm);
            UiKit.Fill(exitConfirm, new Color(0, 0, 0, 0.6f), false);
            var p = UiKit.Panel(exitConfirm, "Dialog", UiKit.Card, 30, 18);
            p.anchorMin = p.anchorMax = new Vector2(0.5f, 0.5f); p.sizeDelta = new Vector2(520, 250);
            UiKit.Size(UiKit.Text(p, "Leave Reachoreo?", 32, UiKit.Ink, true, TextAlignmentOptions.Center), h: 50);
            UiKit.Size(UiKit.Text(p, "Your settings and favorites are saved.", 20, UiKit.Mute, false, TextAlignmentOptions.Center), h: 30);
            var b = UiKit.HStack(p, "Buttons", 14, 0, TextAnchor.MiddleCenter); UiKit.Size(b, h: 70);
            UiKit.Button(b, "Stay", () => exitConfirm.gameObject.SetActive(false), UiKit.Style.Secondary, 200, 64);
            UiKit.Button(b, "Exit", () => { GameSession.Save(); Application.Quit(); }, UiKit.Style.Primary, 200, 64);
            exitConfirm.gameObject.SetActive(false);
        }

        // ================= 05 Live dance (HUD) =================

        void BuildHud(Transform anchor)
        {
            hud = UiKit.WorldCanvas(anchor, "Dance HUD", new Vector2(640, 250), hudPosition, Quaternion.Euler(hudTilt, 0, 0));
            var bg = UiKit.Box(hud.transform, "Background"); UiKit.Stretch(bg); UiKit.Fill(bg, new Color(0.09f, 0.09f, 0.10f, 0.82f));
            var col = UiKit.VStack(hud.transform, "Layout", 8, 18); UiKit.Stretch(col);
            var top = UiKit.HStack(col, "Top", 10); UiKit.Size(top, h: 30);
            hudPhrase = UiKit.Text(top, "", 19, UiKit.Mute, false, TextAlignmentOptions.MidlineLeft); UiKit.Size(hudPhrase, flexW: 1, h: 30);
            hudTime = UiKit.Text(top, "", 19, UiKit.Mute, false, TextAlignmentOptions.MidlineRight); UiKit.Size(hudTime, 160, 30);
            var track = UiKit.Box(col, "Progress"); UiKit.Size(track, h: 10); UiKit.Fill(track, UiKit.CardDim);
            hudFill = UiKit.Box(track, "Fill"); hudFill.anchorMin = Vector2.zero; hudFill.anchorMax = new Vector2(0, 1); hudFill.offsetMin = hudFill.offsetMax = Vector2.zero;
            UiKit.Fill(hudFill, UiKit.Coral);
            var mid = UiKit.HStack(col, "Mid", 10); UiKit.Size(mid, h: 80);
            hudBig = UiKit.Text(mid, "", 52, UiKit.Ink, true, TextAlignmentOptions.MidlineLeft); UiKit.Size(hudBig, flexW: 1, h: 80);
            hudSmall = UiKit.Text(mid, "", 20, UiKit.Mute, false, TextAlignmentOptions.MidlineRight); UiKit.Size(hudSmall, 200, 80);
            var meter = UiKit.HStack(col, "Meter", 10); UiKit.Size(meter, h: 18);
            UiKit.Size(UiKit.Text(meter, "ARM MATCH", 14, UiKit.Mute, true, TextAlignmentOptions.MidlineLeft), 110, 18);
            var mtrack = UiKit.Box(meter, "Track"); UiKit.Size(mtrack, flexW: 1, h: 10); UiKit.Fill(mtrack, UiKit.CardDim);
            hudMeter = UiKit.Box(mtrack, "Fill"); hudMeter.anchorMin = Vector2.zero; hudMeter.anchorMax = new Vector2(0, 1); hudMeter.offsetMin = hudMeter.offsetMax = Vector2.zero;
            hudMeterImg = UiKit.Fill(hudMeter, UiKit.Teal);

            hudPlayRow = UiKit.HStack(col, "Play Row", 10); UiKit.Size(hudPlayRow, h: 60);
            UiKit.Button(hudPlayRow, "Pause", () => { director.Pause(); }, UiKit.Style.Secondary, 180, 56);
            UiKit.Spacer(hudPlayRow);
            hudEnd = UiKit.Button(hudPlayRow, "End free dance", Home, UiKit.Style.Secondary, 240, 56);

            hudPauseRow = UiKit.HStack(col, "Pause Row", 10); UiKit.Size(hudPauseRow, h: 60);
            UiKit.Button(hudPauseRow, "Resume", () => director.Resume(), UiKit.Style.Primary, 180, 56);
            UiKit.Button(hudPauseRow, "Restart", () => director.Restart(), UiKit.Style.Secondary, 180, 56);
            UiKit.Button(hudPauseRow, "Quit to menu", Home, UiKit.Style.Secondary, 220, 56);
            hud.gameObject.SetActive(false);
        }

        void HideMenuForDance()
        {
            menu.gameObject.SetActive(false);
            hud.gameObject.SetActive(true);
        }

        void StartPerformance()
        {
            if (GameSession.Song == null) return;
            HideMenuForDance();
            director.StartDance(GameSession.Song, GameSession.Section);
        }

        void StartFollowMe()
        {
            history.Clear(); history.Push("main");
            HideMenuForDance();
            director.StartFollowMe();
        }

        void StartFreeDance()
        {
            history.Clear(); history.Push("main");
            HideMenuForDance();
            director.StartFreeDance();
        }

        void OnDanceFinished()
        {
            history.Clear(); history.Push("main");
            Show("summary");
        }

        void UpdateHud()
        {
            var st = director.state;
            bool paused = st == PerformanceDirector.State.Paused;
            bool free = director.section == null;
            hudPlayRow.gameObject.SetActive(!paused);
            hudPauseRow.gameObject.SetActive(paused);
            hudEnd.gameObject.SetActive(free);
            UiKit.SetLabel(hudEnd, director.Following ? "End follow me" : "End free dance");

            if (free)
            {
                hudPhrase.text = director.Following ? "Follow me: the Reachys copy your head and hands" + (ReachyLink.Enabled ? " (real robots too)" : "")
                               : "Free dance: " + (director.song != null ? director.song.title : "") + " · move however you like";
                hudTime.text = Clock(director.Elapsed);
                hudFill.anchorMax = new Vector2(0, 1);
                hudBig.text = paused ? "Paused" : "";
                hudSmall.text = "";
                if (director.Following && director.link != null)
                {
                    hudBig.fontSize = 22; hudBig.color = UiKit.Mute;
                    hudBig.text = paused ? "Paused" : "Real Reachy A: " + director.link.a.status + "\nReal Reachy B: " + director.link.b.status;
                }
                return;
            }
            var song = director.song; var sec = director.section;
            hudPhrase.text = song.PhraseAt(director.DanceTime);
            hudTime.text = Clock(director.DanceTime - sec.start) + " / " + Clock(sec.end - sec.start);
            hudFill.anchorMax = new Vector2(director.Progress01, 1);
            if (paused) { hudBig.text = "Paused"; hudBig.color = UiKit.Ink; hudSmall.text = ""; return; }
            if (st == PerformanceDirector.State.Loading) { hudBig.text = "Loading song"; hudBig.color = UiKit.Mute; hudSmall.text = ""; return; }
            if (st == PerformanceDirector.State.CountIn)
            {
                int n = Mathf.CeilToInt(director.CountInLeft * song.bpm / 60f);
                hudBig.text = n.ToString(); hudBig.color = UiKit.Coral; hudSmall.text = "Get ready";
                return;
            }
            hudMeter.anchorMax = new Vector2(director.BodyTracked ? director.LiveMatch : 0f, 1);
            hudMeterImg.color = director.LiveMatch > 0.75f ? UiKit.Coral : UiKit.Teal;
            string j = director.LastJudgementAge < 0.6f ? director.LastJudgement : "";
            hudBig.text = j; hudBig.fontSize = j.Length > 10 ? 34 : 52;
            hudBig.color = j == "Perfect" ? UiKit.Coral : j == "Good" ? UiKit.Teal : j == "Miss" || j == "Not tracked" ? UiKit.Faint : UiKit.Ink;
            hudSmall.text = director.CoachResting ? "Rest, get ready" : director.Streak >= 3 ? $"Streak {director.Streak}" : "";
        }

        // ================= Helpers =================

        static string Clock(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            return $"{(int)(seconds / 60)}:{(int)(seconds % 60):00}";
        }

        static string MusicCredits()
        {
            var lines = new List<string>();
            foreach (var d in DanceLibrary.All) if (d.Credit != null) lines.Add("\u2022 " + d.Credit);
            return lines.Count > 0 ? "Music:\n" + string.Join("\n", lines) : "Music: Encendido uses a click track. Your own songs stay on this headset.";
        }

        static string Dots(int n) => new string('●', Mathf.Clamp(n, 0, 3)) + new string('○', 3 - Mathf.Clamp(n, 0, 3));
    }
}

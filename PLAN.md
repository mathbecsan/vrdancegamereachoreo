# Reachoreo VR: game plan

**What it is:** a VR dance game for Meta Quest 3/3S. You pick a real place at Stanford, pick a dance, and dance it. A mirror in front of you shows your own body dancing, and two Reachy Mini robots perform the choreography with you, one on each side. Then you get a score. Later it connects to the Reachoreo app (`~/Downloads/reachy-dance`), which already makes dances from any song.

Revised 2026-11-10. This replaces the earlier "recreate one kitchen" plan; that greybox kitchen is kept as one place.

> It's an original game in the rhythm-dance genre. We don't copy any existing game's name, characters, UI look, songs or routines. Its identity comes from the robots and the real Stanford places.

Tech: Unity 2022.3.62f2, URP 14, XR Interaction Toolkit 3.1.2, OpenXR 1.14.3, Meta OpenXR 1.0.4, XR Hands 1.5.1 (all already installed).

---

## The core loop

1. **Lobby:** a small hub where you choose a **place** and a **dance**.
2. **Place:** you're standing in a real Stanford spot, on a dance mark.
3. **Dance:** the music starts. In front of you there's a **mirror** with your avatar copying your movements live. **Reachy A and B** dance their parts beside you, as in Encendido: each gets its own moment, and they're never static once the beat is in. **Move cues** (small pictograms of the next move, on the beat) scroll in.
4. **Score:** every count compares your tracked pose with the target pose. You get feedback per move and a total score at the end.
5. **Results**, then dance again or change place.

## Places (the community part)

The community is the places: dancing somewhere you know at Stanford. The first set:

| Place | Kind | Reachys sit on | Build approach |
|---|---|---|---|
| **SRC human dance studio** | indoor | low plinths or the barre side | Model the room from photos and measurements, with a real mirror wall (it doubles as the game mirror) |
| **McMurtry Building** | indoor/outdoor (atrium or front plaza) | benches or ledges | Phone scan of a small area plus your own 360° photo as the backdrop |
| **Meyer Green** | outdoor lawn | a picnic table or rolling cases | Ground patch and props modeled; your own 360° photo for the sky and buildings |
| SRC kitchen mockup | indoor (greybox already exists) | the counter | Keep as the Encendido place |

**How each place is built, so it's authentic and runs on a Quest:**
- A **playable area** of about 4 × 4 m around you, built accurately: the floor, nearby walls and props, from your photos and measurements, or a cleaned-up phone scan (Polycam or Scaniverse).
- **Everything far away is a 360° photo you take yourself** at that exact spot (phone panorama, or a 360 camera), used as the skybox. That's cheap on the Quest and 100% real.
- **The lighting is matched to the photo.** Meyer Green at golden hour, the studio under its real overheads.
- Every place is a **separate scene** with the same `PlaceRig` prefab (dance mark, mirror spot, Reachy A/B spots, light setup), so adding a new place is a small, repeatable job.
- No AI-generated assets (same rule as the app's `ASSETS.md`). Take photos, scans and 360s yourself. Ask permission before shooting inside SRC rooms, and don't use Stanford logos or the Tree as branding unless the university approves it.

## Seeing yourself: body tracking and the mirror

- **Quest body tracking** through Meta's **Movement SDK**: the upper body is tracked and the legs are estimated (Quest 3/3S). It drives a full-body avatar.
- **Mirror:** a render-texture mirror (a second camera at low resolution and 30-45 fps, to protect the frame rate), or the avatar mirrored directly in front of you. In the dance studio it's the real mirror wall.
- **Avatar:** a simple, stylized body in Reachoreo's own look, not a photoreal human. Start with Mixamo Y Bot (kept out of git) or a model of our own.
- Setup check: Meta Movement SDK needs the **Meta XR Core SDK**. Confirm it works alongside the Unity OpenXR Meta packages already in the project, or switch to Meta's OpenXR path before building on it.

## Scoring

- Target poses per count come from the choreography itself (Encendido's dancer part is in the app's preview and score, and song-made dances come from the app).
- Compare joint angles of the arms, torso and head (the legs are estimated, so they count less), with a timing window around each beat.
- Feedback per move: *miss / ok / good / perfect*, a combo counter, and the total at the end.
- Later: when the app is connected, its camera-based scoring can make this more accurate.

## Gameplay systems (genre mechanics, our own versions)

Studied 2026-11-10 from the public help pages for Just Dance VR: Welcome to Dancity (ubisoft.com/en-gb/help/just-dance-vr/gameplay). We take **mechanics**, which are common to the whole VR rhythm-dance genre, and none of their art, characters, city, songs, routines or UI look.

| Genre mechanic | What it does there | Reachoreo's version |
|---|---|---|
| Body calibration | Height and arm-length calibration so moves register accurately | `Calibration` step before the first dance (T-pose: measure height and arm span), redo it from the menu; feeds scoring and the avatar |
| Seated / standing | Re-calibrate to play seated | Seated mode: only the upper body is scored, and the Reachys adapt their cues |
| Difficulty per song | Songs rated by how hard they are | Each dance rated 1-3 by move density and speed (the app's song-made dances already know their move count per bar) |
| Pause and quit anytime | Pause during songs; quitting loses progress for that song | Wrist menu: pause / restart / quit; a quit dance isn't counted as completed |
| Visual move feedback | Effects appear when you hit certain moves | Hit feedback in the world: the Reachys react (antenna pops, cheers, a "look at me" turn); place lights pulse on *perfect* |
| Haptics | Controller vibration, adjustable | A short pulse on each scored move and a stronger one on *perfect* (hand tracking: no haptics, visual only) |
| Social hub with scoreboards | A shared hub to meet players and see scoreboards | **The places are the hub:** each Stanford place has its own board ("top dancers at Meyer Green this week") |
| Quickplay | Jump straight into a song with others | "Dance now": a random dance in your last place, one tap from the lobby |
| Multiplayer, up to six | Groups dance together, locally or online | Later (Phase 5): friends in the same place, each with their own avatar and mirror slot |
| Private home space | Dance with your group in your own space | Your **dorm room** as a private place (optional, from your own photos) |
| Avatar customization and unlocks | Wardrobe of body, face, hair, outfits; more unlock as you play | Simple outfits and colors, plus Stanford-themed unlocks earned by dancing in each place (e.g. dance 5 times at McMurtry) |
| XP and auto-save | Experience points, a single auto-saved profile | XP per dance, place "stamps", one auto-saved profile on the headset |
| Comfort options | Teleport or free movement, snap/smooth turning, vignette | In the lobby only: teleport plus snap turn, and a vignette when moving. During a dance you never move artificially: you stay on your mark |
| Replayable tutorial | The onboarding can be replayed | "First dance" tutorial with the Reachys teaching the basics (step, clap, point), replayable from the menu |

**Not taken:** their characters and avatars, the city and its buildings, their songs and choreographies, the "coach" look, their UI style and fonts, and their name. Our identity is the Reachys, the real Stanford places, and dances made by the Reachoreo app.

---

## Build phases

### Phase 0: Setup (the user does this, with step-by-step help)
- [ ] Install **Android Build Support** (with OpenJDK and Android SDK & NDK) for 2022.3.62f2 in Unity Hub. It isn't installed yet.
- [ ] Switch to Android, turn on OpenXR → Meta Quest support, Quest developer mode, and test that one build runs on the headset.
- [ ] Add the **Meta XR Core SDK** and **Movement SDK**, and get body tracking working in a test scene.

### Phase 1: Vertical slice in one place (the dance studio)
- [ ] `PlaceRig` prefab: dance mark, mirror, Reachy A/B spots.
- [ ] Greybox the SRC dance studio at real scale, with the mirror wall.
- [ ] Body-tracked avatar in the mirror. Test it in the headset: does it feel like *you*?
- [ ] Calibration step (height and arm span) and a seated/standing switch.
- [ ] Import the Reachy Mini (`~/Downloads/reachy-dance/xr/public/models/reachy_mini.glb`, Apache-2.0, needs the glTFast package) with a `ReachyRig.cs` (head pose, body yaw within ±160°, antennas).
- [ ] `DanceClock.cs` (BPM, phrases, counts) and robot playback from `~/Downloads/reachy-dance/tests/golden/encendido_robots.json`.
- [ ] Move cues and a first version of scoring with Encendido.
- **Done when:** you can dance Encendido in the studio, see yourself in the mirror, the Reachys dance with you, and you get a score.

### Phase 2: Make it feel good
- [ ] Beat-synced feedback (light pulses on "perfect", Reachy reactions when you do well: antenna pops, cheers).
- [ ] The results screen.
- [ ] Comfort: no forced movement, you stay on your mark, and seated or limited-space options.
- [ ] Performance pass: steady 72 fps on a Quest 3S.

### Phase 3: More places
- [ ] Lobby / place picker.
- [ ] Meyer Green, McMurtry, and the SRC kitchen (reuses the existing greybox), each with its `PlaceRig`, 360° backdrop and matched lighting.
- [ ] Write the "how to add a place" checklist, so new spots are easy to add.

### Phase 4: More dances (connect the app)
- [ ] Export format: the app writes a dance (song, beats, robot parts, dancer target poses) as JSON, and Unity loads it.
- [ ] Later, live mode: a `ReachoreoBridge.cs` WebSocket to the dashboard. Note the **port conflict**: the dashboard and MCP for Unity both default to `8080`, so move one of them.

### Phase 5: Progression and dancing together
- [ ] Profile: XP, place stamps, unlockable outfits, auto-save.
- [ ] Per-place scoreboards (local first, online later).
- [ ] "Dance now" quick start and a replayable first-dance tutorial.
- [ ] Multiplayer in the same place (Unity Netcode for GameObjects, or Photon). This is the biggest piece, so it comes last.

---

## Folder layout

```
Assets/Reachoreo/
  Scenes/  Lobby.unity, Place_DanceStudio.unity, Place_MeyerGreen.unity, Place_McMurtry.unity, Place_SRCKitchen.unity (the current Kitchen.unity)
  Places/  shared PlaceRig prefab + per-place art
  Player/  avatar, body tracking, mirror
  Robots/  reachy_mini.glb, ReachyRig prefab, NOTICE
  Dance/   DanceClock, choreography data, move cues, scoring
  UI/      lobby, results
References/<place>/   photos, measurements, 360s, scans (not imported)
CREDITS.md
```

## What we need from you

1. **Photos and measurements of the SRC dance studio** (room size, mirror wall size). This is the first place.
2. **A 360° photo** standing at your chosen dance spot on Meyer Green and at McMurtry.
3. Is Encendido's song final, or should we use a click track for now?

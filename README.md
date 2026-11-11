# Reachoreo VR

A VR dance game for Meta Quest 3/3S. You stand in a real place at Stanford (first: the SRC dance studio), follow a
dance coach in front of you, see yourself in the mirror through Quest body tracking, and dance with two Reachy Mini
robots, one on each side. Every beat compares your arms with the coach's: Perfect, Good, OK or Miss.

It is the VR player of [Reachoreo](https://github.com/mathbecsan/Reachoreo): the web app analyses a song and makes the
dance; this game plays the dance files it exports (`reachoreo.dance/1`, see the web app's
`docs/reachoreo-architecture.md`).

## What's in it

- **Screens**: main menu, song selection (All, My songs, Latin, Pop, Electronic, Hip-hop, Funk & disco, World,
  Favorites), dance setup (part of the dance, place, Reachys, standing or seated, coach view, count-in, audio),
  ready check, a small in-dance panel (phrase, time, rating, pause), and a performance summary.
- **Coach**: dances the dance file's dancer part. "Back to you" (copy directly) or "Facing you" (mirror image).
- **Reachy Minis**: Pollen Robotics' model, driven in the real robots' SDK units with the same limits.
- **Mirror**: a reflected copy of the room behind a see-through mirror, with correct depth in each eye.
- **Your body**: Meta Movement SDK full-body tracking drives a simple mannequin you see in the mirror.

## Run it

Unity 6000.3.28f1 (6.3 LTS) with Android Build Support. Open the project, switch to Android if needed, and build
`Assets/Reachoreo/Scenes/Place_DanceStudio.unity` to a Quest in developer mode.

Dances: built-in ones go in `Assets/StreamingAssets/Dances/<id>/` (listed in `index.json`). Your own songs go only on
the headset: `/sdcard/Android/data/com.reachoreo.vr/files/Dances/<id>/` (`dance.json` + `song.ogg`), made with
`tools/export_dance.py` in the web app. They are never committed.

## Folders

| Path | What |
|---|---|
| `Assets/Reachoreo/Scenes` | `Place_DanceStudio` (the game), `Place_SRCKitchen` (greybox) |
| `Assets/Reachoreo/Game` | menus and screens (`MenuFlow`), dance clock and scoring (`PerformanceDirector`), dance files (`DanceLibrary`) |
| `Assets/Reachoreo/Places` | `PlaceRig` (dance mark, coach, mirror, Reachy spots), `MirrorWorld` |
| `Assets/Reachoreo/Coach`, `Player`, `Robots` | coach, body-tracked avatar, Reachy rig |
| `References/` | measurements and photos of the real places (not imported by Unity) |
| `PLAN.md` | the game plan |

## Credits

Reachy Mini 3D model and rig by Clément Plays, published by Pollen Robotics (Apache-2.0,
`Assets/Reachoreo/Robots/NOTICE.txt`). Built on Unity's Mixed Reality template, XR Interaction Toolkit and the Meta XR
SDKs. No AI-generated assets.

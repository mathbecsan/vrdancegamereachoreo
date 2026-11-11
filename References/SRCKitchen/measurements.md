# Kitchen measurements (real room vs. greybox guess)

Every size in `Assets/Reachoreo/Scenes/Place_SRCKitchen.unity` is a **guess** until this table has real numbers.
Take a tape measure to the Robotics Center kitchen mockup, write the real value in the right-hand column,
and we update the greybox to match.

In Unity, every guessed piece carries an **Estimated Dimension** component (orange outline when selected).
To list them all, type `t:EstimatedDimension` in the Hierarchy search box.

Layout used for the guess: you (the audience) face the counter. The counter runs along the back wall.
Reachy A sits on its left end, Reachy B on its right end, and the dancer stands in front of the middle.

| What | Guess in the greybox | Real (measure) |
|---|---|---|
| Room width (left wall to right wall) | 5.00 m | |
| Room depth (audience wall to counter wall) | 5.50 m | |
| Ceiling height | 2.70 m | |
| Is the counter against a wall, or an island? | against the back wall | |
| Counter length | 3.20 m | |
| **Counter height (floor to top surface)** | **0.90 m** | |
| Counter depth (front edge to wall) | 0.62 m | |
| Countertop thickness | 4 cm | |
| Toe kick (gap at the floor) height / depth | 10 cm / 8 cm | |
| Upper cabinets: do they exist? Bottom height, height, depth | yes: 1.45 m, 0.75 m, 0.35 m | |
| Window: which wall, width, sill height, top height | left wall, 1.40 m, 1.00 m, 2.20 m | |
| Door: which wall, width, height | right wall, 0.90 m, 2.10 m | |
| Lamp: type and position (pendant? spots? strips?) | one pendant over the middle of the counter, shade 30 cm, bottom at 1.92 m | |
| Reachy A position on counter (distance from the left end, from the front edge) | 1.25 m left of center, 31 cm from the front edge | |
| Reachy B position | 1.25 m right of center, 31 cm from the front edge | |
| Distance between the two robots | 2.50 m | |
| Dancer's spot (distance from the counter edge) | 0.78 m | |
| Audience / camera spot (distance from the dancer) | 3.10 m | |
| Floor material | (unknown) | |
| Counter material | (unknown, web version uses marble) | |
| Cabinet colour / finish | (unknown) | |

Photos: put them next to this file in `References/` (Unity doesn't import this folder).

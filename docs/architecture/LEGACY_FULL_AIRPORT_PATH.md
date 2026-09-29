# The legacy `-airsideFullAirport` path

The default game draws the real Adelaide field (`AirsideBareField.Enabled`). Launching with `-airsideFullAirport`
switches to the old miniature "full airport" QA scene (Kingscote, 1:20 scale, a Unity Terrain tile, greybox
buildings). It is not the game any more, but it is not dead code either, so it was not deleted in the code-quality
pass. This note records what it is and what retiring it would take.

## What belongs to it

| Piece | Lines | Used by |
|---|---|---|
| `AirsideTerrainField` (1:20 landform maths) | 495 | `AirsideTerrainGround`, `AirsidePrototype` (`TerrainFieldTests`) |
| `AirsideTerrainGround` (Unity Terrain bake) | 138 | `AirsideStaticWorld`, `AirsidePrototype` |
| `AirsidePrototype` branches on `AirsideBareField.Enabled` | 39 sites | camera framing, lights, fog, props, buildings |
| `AirsidePrototype.BuildLandsideLife` and the Kingscote builders | several hundred | only when not bare |
| `AirsideAdelaidePerimeter` | 189 | **still needed**: its fence constants feed `AdelaideBoundaryFence` |

## What is not legacy, despite the names

- `AirportTaxiNetwork` (218) is used by `AirportSimulation`, `AirlineOperations` and `CommercialFlight`: it is the
  segment-reservation network the simulation runs on. `AirportLayout` (46) is only its 1:20 constants.
- `AirsideBareField` is the real-field extents and camera framing.

## To retire it

1. Decide with Bailey that the `-airsideFullAirport` QA scene is finished (ADR).
2. Delete the `!AirsideBareField.Enabled` arms (keep the bare arm) and the Kingscote builders; drop
   `AirsideTerrainField`, `AirsideTerrainGround`, `AirsideStaticWorld` terrain hooks and their tests.
3. Move `AirportLayout`'s constants into `AirportTaxiNetwork` and delete the file.
4. Turn `AirsideBareField.Enabled` into a constant `true` and remove the flag.
5. Run `scripts/test-unity.sh` and compare overview/follow captures with the current build: this touches lighting and
   camera code that cannot be judged from tests alone.

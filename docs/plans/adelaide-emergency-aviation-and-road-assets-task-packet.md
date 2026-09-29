# Adelaide emergency aviation and road assets — task packet

**Date:** 30 September 2026
**Owner:** Codex
**Branch:** `codex/adelaide-helicopter-road-assets`

## Player-visible outcome

- Adelaide's mapped western rescue/retrieval precinct has its real circular
  `Helipad West`, including a contrasting perimeter, centre `H`, touchdown ring,
  edge lamps and a parked, original Bell 412EP-class medical-rescue helicopter.
- The thousands of parked landside vehicles read as a varied Australian car park
  rather than one repeated two-box car: sedan, hatch, SUV, ute and van silhouettes
  remain deterministic and inexpensive.
- Mapped traffic lights read as roadside equipment at overview and close range,
  with a footing, pole, mast arm, backing board, three lamps and lamp visors.
  Existing stop/give-way placement and road paint stay tied to the OSM road data.

## Files and modules in scope

- `scripts/generate-air-017-bell-412.py`
- `game/Airside/Assets/Airside/Art/Models/Aircraft/mdl_bell_412_rescue_v01.*`
- `game/Airside/Assets/StreamingAssets/Airside/Art/Models/Aircraft/mdl_bell_412_rescue_v01.*`
- `game/Airside/Assets/Airside/Presentation/AdelaideEmergencyAviationGeometry.cs`
- `game/Airside/Assets/Airside/Presentation/AirsideAdelaideEmergencyAviation.cs`
- `game/Airside/Assets/Airside/Presentation/AdelaideCarParkGeometry.cs`
- `game/Airside/Assets/Airside/Presentation/AdelaideRoadFurnitureGeometry.cs`
- the Adelaide field-build hook and matching EditMode tests
- art manifest, asset/data register, ADR, `GAME.md` and `CHANGELOG.md`

## Decisions and invariants

- The pad outline and centre come from committed OSM way `1229789628`,
  `Helipad West`: centre `(-302.737, 697.242)` in the existing runway frame and
  an approximately `18.94 m` radius. This is the pad beside the mapped SA
  Ambulance rescue/retrieval aviation base, not an invented pad at the SAPOL
  hangar.
- At the current project date, the appropriate parked medical-rescue silhouette
  is a Bell 412EP-class helicopter. Babcock records the Bell 412EP for SAAS; the
  South Australian Government says the replacement AW139 fleet begins in
  October 2027. Bell's current 412 literature supplies the dimensional envelope.
- AIR-017 is project-authored, unbranded geometry. Real service marks, registration,
  logos and exact livery are excluded. Broad red/white rescue colour blocking is
  representational only.
- The helicopter and pad are presentation-only static world detail. They do not
  enter fleet ownership, traffic sequencing, reservations, missions, saves or
  deterministic simulation.
- Road-vehicle variety is derived only from the existing deterministic bay hash.
  Car count, occupancy, placement, parking direction and simulation remain
  unchanged.
- Signal state remains the existing deterministic presentation split. This pass
  improves hardware only; it does not add road-traffic simulation.
- Existing primitive/material fallbacks remain available if AIR-017 cannot load.

## Acceptance criteria

1. AIR-017 loads through the supported project glTF path, stays within the
   published 412 envelope, has four main-rotor blades, twin engine housings,
   skid gear, a tail rotor, glazing and a readable rescue-aircraft silhouette.
2. The helipad uses the committed OSM centre and outline within `0.25 m`, draws
   an `H`, touchdown ring, perimeter and at least eight edge lamps, and places
   AIR-017 wholly inside the pad's rotor-clear circle.
3. Parked-car generation includes all five deterministic body classes in the
   built Adelaide set, preserves the current car-count bounds and stays within
   the expanded mesh budget.
4. Every mapped signal approach remains on the left kerb and gains a footing,
   mast arm, backing board, three distinct lamps and visors. Road-furniture mesh
   output stays within its explicit budget and keeps correct winding.
5. Missing AIR-017 art leaves an intentional procedural helicopter fallback and
   cannot affect simulation state.

## Tests and playtest

- Run the AIR-017 generator check and inspect its reported dimensions/part count.
- Run `scripts/test-unity.sh` and `scripts/audit-unity-assets.py`.
- Build with `scripts/build-mac.sh`.
- Inspect packaged Adelaide captures at default overview and a close western-
  precinct camera in day and night lighting; confirm pad placement, helicopter
  proportions, car-class readability, signal silhouettes and no clipping.

## Must remain unchanged

- Runway, taxiway, stand, road, car-park and building coordinates.
- Aircraft schedules, ground routing, road data, traffic behaviour, time,
  weather, economy, progression and saves.
- Existing turnaround GSE assets and their task choreography.
- Existing OSM snapshot and attribution; no live network dependency at runtime.

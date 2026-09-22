# 0097 — Aircraft surface detail: metre UVs, a real skin, and a 737 that reads as one

Date: 22 September 2026.

## Decision

### 1. Aircraft kits unwrap in metres

The aircraft glTFs carry `POSITION` only — no `TEXCOORD_0` — so `ArtGltfLoader` generates UVs.
The generic kit unwrap (`BuildPlanarUvs`) normalises each part's own bounding box to 0..1, and
its axis choice drops the part's **longest** axis. On an airframe that meant two things:

- Texel density varied by over a hundred times across one aircraft: a 39.47 m fuselage got a
  single texture repeat across its whole length, a 0.36 m cabin window got one across 36 cm.
- The fuselage was unwrapped looking straight down its own length, so every ring of the tube
  landed on the same UV and the maps could only ever read as a lengthwise smear.

Aircraft kits now use `BuildMetreUvs`: a cylindrical unwrap about each part's longest axis,
with V in metres along that axis and U in metres of arc around it. Density is then identical
on every part of every aircraft. Building, vehicle and prop kits keep the bounding-box unwrap
their surface tiling was tuned against — this is scoped by path (`Models/Aircraft`).

`AircraftSkin` and `AircraftGlazing` tiling changes meaning accordingly: it is now repeats per
metre (0.5 = one repeat every two metres), not repeats per part.

### 2. `tx_aircraft_skin_*_v02`

The v01 set is 256×256 with a near-flat basecolor (std 6.5/255) and a **constant** mask — no
metallic or smoothness variation anywhere. It carried no surface information, and under the old
UVs it could not have.

v02 is 1024², authored as exactly 2.032 m square, and placed in real units: frames at 0.508 m
(the cabin frame pitch the generators already use), stringers at 0.254 m, rivets at 0.0635 m
along each frame, and a coarser 1.016 m lap-joint grid. Every pitch is a harmonic of the frame
pitch and divides the tile a whole number of times, so it wraps without a seam.
`AirsideMaterialLibrary.PreferAuthoredMap` already prefers `_v02`, so no code change binds it.

### 3. AIR-005 nose, flight deck and nacelle

The 737-8 is the source mesh for four other narrowbodies (A320, 737-800, E190, A220-300 are
axis-scaled copies of it — see `generate-air-adelaide-fleet.py`), so its shape errors read five
times over:

- **Nose.** A 0.30 m tip with only 0.16 m of droop across the whole nose read as a long cone.
  The tip is now 0.46 m and the nose centreline sits 0.24 m below the cabin axis, giving the
  type its chin.
- **A 4 cm waist** at station z=15.00 (1.86 → 1.82 → 1.88) that no airframe has, removed.
- **Flight deck.** The panes were small and far apart, reading as two dark specks near the nose.
  Each is larger and the side windows move forward and further round, so the assembly reads as
  one wraparound band. Panes stay inside the 0.70 m z-span the AIR-005 check enforces.
- **Nacelle.** The lower cowl now flattens toward a chord and widens slightly below the
  centreline, instead of being a plain ellipse — the flat-bottomed shape the type is known for.

## Rationale

Items 1 and 2 are why the aircraft read as flat plastic regardless of lighting: the material
system was fully wired up and doing nothing, because it had no usable UVs and no authored
detail to sample. Fixing the unwrap is what makes any skin possible; the skin is what makes
the fix visible. Item 3 is a shape error rather than a missing feature, and it is the single
most-looked-at part of the most-used airframe family.

## Affected systems

Presentation and art only. No simulation, schedule, reservation or save behaviour changes.
The regenerated models keep their exact published envelopes — `scripts/test-air-005-737-8.py`
and `scripts/test-air-adelaide-fleet.py` both enforce them and both pass.

## Evidence

- `scripts/test-domain.sh` — 687 passed. The unwrap's density contract is covered by
  `AircraftSkinUvTests` against the real fuselage and window dimensions.
- `scripts/test-air-005-737-8.py` and `scripts/test-air-adelaide-fleet.py` pass; envelopes,
  fitted glass, open inlets and mesh integrity unchanged.
- `scripts/test-aircraft-connectivity.py` — all 13 aircraft still chain to the fuselage.
- All four v02 maps verified to tile seamlessly (wrap step equals the neighbouring step).
- Multi-view renders of the regenerated AIR-005 inspected: blunt drooped radome, wraparound
  flight-deck band, flattened lower cowl.

Not verified: the Unity compile and any in-engine appearance. `scripts/test-unity.sh` cannot run
on this Mac (batchmode licensing loop, `docs/build-mac-batchmode-dead-end`). The UV change,
the tiling change and the new skin all need an Editor GUI compile and a packaged look at
day/dusk/night before merge — this is the change most likely to need tuning by eye.

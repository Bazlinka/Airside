# 0098 — The E190 and A220-300 get their own geometry

Date: 22 September 2026.

## Decision

`scripts/generate-air-adelaide-fleet.py` built the E190 and the A220-300 by taking the AIR-005
737-8 mesh and applying a three-axis scale (`_narrowbody`). Both types now have dedicated
generators lofted from their own dimension tables:

- `scripts/generate-air-013-e190.py` → AIR-013
- `scripts/generate-air-014-a220-300.py` → AIR-014

The fleet script still regenerates all six Adelaide types from one entry point, but for these
two it now calls the dedicated builders instead of scaling.

## Rationale

An axis scale reproduces a type's bounding box and nothing else. A 737 squashed to 28.72 m of
span still has a 737's six-abreast cross-section, a 737's wing planform and taper, 737 nacelle
proportions and 737 split-scimitar winglets. The things that actually identify these two
aircraft are exactly the things a scale factor cannot change:

| | E190 (AIR-013) | A220-300 (AIR-014) | 737-8 (AIR-005) |
|---|---|---|---|
| fuselage | 3.01 m, four abreast, shallow double bubble | 3.50 m, five abreast | 3.76 m, six abreast |
| nose | short, rounded | long, finely pointed, strongly drooped | short, blunt, drooped |
| wing | modest sweep, set well aft, deep root fairing | high aspect ratio, slender, long | moderate |
| wingtip | small canted winglet fence | raked tip, no vertical fence | split scimitar |
| engines | CF34-class: 1.16 m fan, slim cowl | PW1500G-class: 1.85 m fan, short fat cowl | flat-bottomed high-bypass |
| frame pitch | 0.787 m | 0.787 m | 0.508 m |

The raked A220 tip in particular is a different mesh, not a different scale — there is no
vertical surface at the tip at all, which is the clearest way to tell it from the other two at
distance.

Both generators reuse the shared lofting primitives (`oval_lathe_fuselage`, `lofted_aerofoil`,
`aircraft_skin`, `box`, `cylinder`) the other per-type generators use. That is shared
machinery, not shared shape: every station table, planform, nacelle profile and tip device is
the type's own.

## Affected systems

Art only. Both models keep their exact published envelopes — 36.24 × 28.72 × 10.55 m and
38.70 × 35.10 × 11.50 m — and the nose-stop datum at local z=0, so
`AircraftVisualProfiles.EmbraerE190` and `AirbusA220300` (pick box, shadow, follow distance,
visual centre −18.12 m and −19.35 m) remain correct without a C# change. Main and nose tyre
radii are authored to match those profiles so the wheel roll stays right.

Part counts fall (E190 179 → 154, A220 179 → 162) because the scaled copy inherited 737
furniture these types do not have; triangles fall with them (35,488 → 27,044 and 29,492).

## Placement is computed, not guessed

The first pass hand-placed the small furniture — wick, nav, landing and taxi lights, nose gear
door — at literal coordinates, and `scripts/test-aircraft-connectivity.py` failed with **11
floating parts**: the E190's wicks and landing lights adrift, and on the A220 a tail nav light
floating inside its own tail cone, a gear door and taxi light about 45 cm up inside the
fuselage, and nav lights 32 cm above the raked tip.

Every one of those parts is now positioned from the surface it attaches to — `_belly_y(z)`
samples the fuselage underside, wing furniture interpolates the planform, and tip furniture
interpolates the winglet or rake stations. That also surfaced a second-order trap: `np.interp`
clamps past the last station, so reading the A220's tip position out of `WING_STATIONS` (which
stops at x=16.60) silently returned the un-raked value and would have left the nav light
floating a second time. The rake has its own station table for exactly this reason.

## Evidence

- `scripts/test-aircraft-connectivity.py` — clean on all 13 after the placement fix, having
  failed with 11 floating parts before it.
- `scripts/test-air-adelaide-fleet.py` passes: all six envelopes exact, geometry valid.
- Each generator validates its own envelope, tyres at y=0, nose datum at z=0, tail at
  −length and fuselage half-width, and refuses to write otherwise.
- Top-down renders of the E190, A220-300 and 737-8 compared: three distinct planforms,
  spans, fuselage widths, nacelle sizes and tip devices.
- Thumbnails regenerated for both types.

Not verified: the Unity compile and in-engine appearance. `scripts/test-unity.sh` cannot run on
this Mac (batchmode licensing loop, `docs/build-mac-batchmode-dead-end`). Nothing here changes
C#, but the models still want a packaged look before merge.

## Still open

The A320, A330-900 and 787-9 remain axis-scaled copies (of the 737-8, the A350-900 and the
787-10 respectively). The same treatment applies to them one type at a time.

# 0062 — Contracts card fill, terminal roof fixes, weather fog tint, and a documented go-around bug

Date: 2026-09-20

## Decision

1. **Contracts offer cards fill the column instead of leaving most of it empty.** The market
   only ever runs three offers at a time (ADR 0056), but `ContractsWorkspaceLayout.OfferCard`
   pinned every card to a fixed 86 px height regardless of the column's actual height, so a
   tall window showed three small cards and a large blank void below them — the single
   clearest "dead space" example found while looking for what a HUD rework should fix first
   (see the mockup renders this session compared before/after). New
   `OfferCardHeight(shown)` grows each card to fill the room up to a 172 px cap, and the
   card's own text/button block centres in whatever extra height that gives it rather than
   stretching; every card also gets a subtle outline now (not only the highlighted one), for a
   touch more definition than a flat fill. Verified with the real mockup renderer
   (`scripts/hud-mockup` + `scripts/render-hud-mockups.py`), not just by reading the layout math.
2. **Terminal roof brow no longer cantilevers past the building.** `AdelaideTerminalArchitecture
   .RoofBrow()`'s last segment was centred at X=1584 with width 98, spanning to X=1633; the
   terminal's own real OSM footprint (`AdelaideLayout.Terminals`) ends at X=1615.8 — the brow
   hung 17.2 m past the building's own south-east corner into open air. Narrowed to end with a
   small margin inside that corner, keeping the ~4 m gap pattern the rest of the brow already
   uses between segments.
3. **Terminal roof plant/equipment screens get the same metal texture as the brow beside
   them.** `RoofDetails()`'s plant/skylight loop applied a texture only to skylights implicitly
   (via flat glass-tint colour, correct for glass) — the "roof plant" entries, the same
   roof-furniture category as the brow and arguably more plausibly corrugated-metal-clad,
   rendered as flat solid colour while the textured-metal brow sat right next to them. Now
   shares the brow's `tx_corrugated_metal` material.
4. **Fog now actually tints, not just thins.** `UpdateWeatherPresentation`'s fog-colour branch
   fires for every weather kind but Clear (`wet || Gloom > 0.12`, true for Cloudy through
   Storm) and set `fogColor` from daylight alone — every one of those five kinds rendered the
   *identical* colour, differing only in density. Now blends toward a slate grey by `Gloom`
   (Storm/Rain/Overcast darken correctly) with one deliberate exception: Fog blends toward a
   pale, near-white haze instead, driven by how much its `Visibility` loss outruns what its own
   `Gloom` would explain — the one WeatherLook value where that gap is real (real fog scatters
   light into a bright haze even as it dims the sun; Storm's gloom and visibility loss are
   nearly matched, so it stays grey).

## What was found but deliberately not fixed here

- **Go-around → holding-for-landing teleport (real, verified, not attempted).** A go-around
  flies a full racetrack (`CircuitTraffic.GoAround`) ending near short final at ~305 m circuit
  height, then transitions straight to `HoldingForLanding`, which is drawn pinned to a queue
  slot on the ordinary final-approach path (`ApproachHold.HoldingFinalProgress`) — a
  fundamentally different position/altitude with no relation to where the circuit actually
  left the aircraft. Confirmed with real numbers: a 400–1,100 m horizontal jump and a
  ~210–250 m altitude drop in one tick, landing gear included (`GoAround` forces gear up;
  `HoldingForLanding`'s queue-pinned approach progress is usually well past the point gear
  deploys). This is a real bug, but a correct fix means blending two structurally different
  flight-path systems (a fixed racetrack vs. a queue-pinned glideslope) without a way to watch
  the result — attempting it blind risked replacing one visible defect with another. Left
  fully documented rather than half-fixed; see GAME.md for a suggested approach.
- **Terminal airside glazing sits 0.28–2.14 m off the real wall, not flush.** The 28 glazing
  bays and their mullions use a constant Z (435.55/435.6), but the terminal's real OSM wall is
  a curving polyline whose Z runs 437.7→435.8 along its length — every bay floats in front of
  the wall by an amount that shrinks from 2.14 m (west end) to 0.28 m (east end), never
  reaching zero. The correct fix interpolates each bay's Z from the actual wall polyline
  instead of a constant; doing that safely means picking the right edge of a 64-point polygon
  correctly, which is easy to get subtly wrong (e.g. the wrong side of the building) without
  being able to look at the result. Flagged, not guessed at.
- **No wind-driven rain/cloud direction; cloud "coverage" only changes alpha, not count/spread.**
  Rain drop angle and drift, and cloud drift, are all fixed constants regardless of
  `RunwayWeather`'s actual wind direction — the windsock is the only wind-reactive visual in
  the game. `BuildCloudBands`' cluster count and layout are also fixed regardless of
  `CloudCover`; only tint/alpha respond. Both are real, larger design/implementation efforts
  (wind-driven drift math; a variable-density cloud layer) better done with Unity available to
  tune by eye, not guessed at blind.

## Reason

Bailey asked for more aircraft-behaviour bug fixes, terminal realism, better rain/cloud types,
and separately said the HUD "needs a massive rework." Investigated all four with dedicated
research passes before touching code, per this session's running discipline, and used the
existing offline HUD mockup renderer (`scripts/hud-mockup` + `render-hud-mockups.py`, which
this session set up Pillow for) to get *real* rendered images of the current HUD — the first
time this session could see a Presentation change rather than reason about it blind. That
rendering is what turned "massive rework" into a concrete, prioritised first finding (offer
cards leaving most of the column empty) rather than a guess. A "massive rework" of all four
workspaces is a much larger, iterative design project than one pass can respons­ibly finish;
this is a real, verified start on the clearest problem the renders showed, not the whole job.

## Consequences

- `ContractsWorkspace.cs` is in the headless harness (UnityEngine-free) —
  `scripts/test-domain.sh` **499/499**, unchanged count (no new tests added; the change is
  layout math with no new branch worth a dedicated unit test, and it was verified visually via
  the mockup renderer instead, which is the tool built for exactly this).
- `AirsideAdelaidePavement.cs` (terminal geometry) is also in the harness; the same 499/499 run
  covers it, and no `AdelaideTerminalArchitectureTests` assertion needed updating (the existing
  test only checks bay Z is under a threshold, which the brow-width change does not touch).
- `AirsidePrototype.YpadPavement.cs` (terminal material change) and the fog-colour change in
  `AirsidePrototype.cs` are both Unity-only, outside the harness, reviewed by inspection only.
- No save, simulation, or timing change anywhere in this pass.

## Tests

`scripts/test-domain.sh`: 499/499 (unchanged — no new Simulation-layer behaviour). Contracts
card fill verified visually via `scripts/hud-mockup` + `scripts/render-hud-mockups.py`.
Terminal and fog-colour changes are Unity-only and reviewed by inspection; `scripts/test-unity.sh`
and a Play-mode look remain owed, same as the rest of this session's Presentation work.

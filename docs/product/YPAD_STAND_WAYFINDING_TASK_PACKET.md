# YPAD stand wayfinding — implementation task packet

**Status:** Approved for implementation by Bailey's 2026-09-14 continuation request
**Branch:** `codex/ypad-wayfinding`
**Base:** `feature/ypad-real-layout` at `ae12aa9`

## Player-visible outcome

The four starter-airline parking positions read as real Adelaide regional bays:
each has a yellow lead-in, a stop bar and its real 50A–50D identifier. From the
normal overview and aircraft-follow views, the player can connect a stand choice
to a physical place on the apron.

## Files and module in scope

- `Presentation/AdelaideStandMarkings.cs`: deterministic marking geometry derived
  from generated YPAD bay data.
- `Presentation/AirsidePrototype.YpadPavement.cs`: procedural paint and Unity-rendered
  world labels.
- Focused EditMode tests for all four bays.
- `GAME.md` and `CHANGELOG.md` handoff evidence.

## Decisions and invariants

- `AdelaideLayout` remains the sole source of bay position, heading and reference.
- This is presentation only. It does not change aircraft state, timing, routes,
  runway or stand reservation, saves, commands or HUD layout.
- Text is rendered by Unity; no text is baked into an image.
- No new external asset or licence is introduced.

## Acceptance criteria

1. Exactly four marking sets are generated for BAY-1 through BAY-4.
2. The visible references are exactly 50D, 50C, 50B and 50A.
3. Every lead-in ends and every stop bar centres on its generated bay stop point.
4. Markings remain derived from `AdelaideLayout`; no copied stand coordinates.
5. The full Unity EditMode suite compiles and passes with no failed or skipped tests.

## Verification

- Run `scripts/test-unity.sh`.
- Inspect generated geometry tests for references, stop positions and finite values.
- Packaged visual inspection remains part of the later camera-matrix pass; the
  external-player playtest is deferred by Bailey.

## Must remain unchanged

- Claude's OSM source snapshot, generator and generated `AdelaideLayout`.
- Ground speed profiles and ground-leg durations.
- Fleet state transitions, reservations, save schema and airline UI.

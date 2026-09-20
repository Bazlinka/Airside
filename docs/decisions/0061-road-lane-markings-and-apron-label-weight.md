# 0061 — Road lane markings, bolder apron labels, runway left as-is

Date: 2026-09-20

## Decision

1. **Roads gain a dashed centreline.** `AirsideAdelaideRoads` built one flat, vertex-coloured
   asphalt ribbon per OSM road, rendered through `Airside/Surroundings` — the same shader the
   surrounding land/sea uses, which blends each vertex 92% toward the real Adelaide satellite
   photo underneath it (`_SatelliteStrength`). That made the road read as a grey band cut out
   of the aerial photo, with no lane discipline at all. A new `BuildLaneMarkingMesh` walks the
   same road data and lays a thin (0.32 m) dashed white strip (5 m dash / 5 m gap) down each
   road's centreline, on a *separate* mesh using `AirsideMaterialLibrary`'s `PaintedLine`
   material (a real Lit material with its own `tx_painted_line` texture, already used
   elsewhere) instead of the satellite-blended shader — painted-line white would otherwise be
   diluted back toward the satellite colour if drawn with the road's own material. Only drawn
   on roads at or above the same width threshold that already gates whether a road ribbon is
   drawn at all, so a landside laneway does not get a highway-grade centreline.
2. **Apron/stand identifier labels are bold.** `TextMesh.fontStyle = FontStyle.Bold` on the
   painted stand/gate reference labels (`AirsidePrototype.YpadPavement.cs`).
3. **Runway appearance: left unchanged.** Checked for a concrete gap before touching anything
   and did not find one worth forcing — real ICAO-style stroke-drawn paint (edges, centreline,
   threshold stripes, aiming points, TDZ marks, designation numerals), a genuine asphalt
   texture (`tx_asphalt_runway`) via `AirsideMaterialLibrary`, rubber-streak wear
   (`RunwayRubberMarks`), taxiway edge wear (`TaxiwayEdgeWear`), and threshold/PAPI/ALS/edge
   lighting are all already in place. Apron slab-joint geometry (`ApronSlabJoints`) does not
   extend to the runway, and should not — Adelaide's runway is asphalt, not concrete, so it
   has no slab grid to draw.

## Reason

Bailey asked to "improve visual and ground appearance... including roads around the airport,"
and separately for runway appearance and more natural apron labels. Reading the actual
generators before touching anything (per this session's running discipline) found the real,
specific gaps rather than guessing broadly: roads had no lane markings at all next to a runway
that has extensive, deliberate ICAO paint; apron labels use Unity's default `TextMesh` font,
visibly thinner than the runway's own purpose-built stroke numerals. The runway itself did not
turn up an equivalent gap on inspection, so it was left alone rather than changed for its own
sake.

## What was deliberately not attempted here

A full stroke-based glyph alphabet for stand/gate references (matching how
`AirsideStripMarkings.DesignationNumerals` draws 05/23/12/30 as real segment geometry, not
font text) would be the thorough fix for apron labels — but stand references need letters too
(50D, 18L), not just digits, and building and tuning a matching letter set without being able
to see it rendered is a much larger, riskier undertaking than a one-line bold-weight change.
Flagged as the real follow-up, not silently dropped.

## Consequences

- Presentation-only, Unity-only files (`AirsideAdelaideRoads.cs`, one shader-adjacent-but-
  unchanged reference in `AirsidePrototype.YpadPavement.cs`) — none of this compiles in the
  headless harness, so `scripts/test-domain.sh` is unaffected by design (499/499, unchanged)
  and this is reviewed by inspection only. No Unity editor was available in this session to
  actually see the dashed lines or the bolder labels rendered.
- No simulation, save, or timing change of any kind — pure additive geometry/material.
- The lane-marking mesh building is a one-time startup cost (built once when the field loads,
  not per frame), sized by real OSM road length, capped by the same `MaxRoads` the road ribbon
  itself already caps at.

## Tests

None possible — both files are UnityEngine-dependent and outside the headless harness.
`scripts/test-unity.sh` and a Play-mode look at the landside roads (dashes should read at
follow and overview distance, not swim or alias) and the apron stand labels (bold, still
legible, not overlapping the lead-in paint) are the real verification still owed.

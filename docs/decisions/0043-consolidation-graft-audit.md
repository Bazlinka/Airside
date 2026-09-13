# 0043 — Audit of the branch-consolidation graft

Date: 2026-09-13. Requested by Bailey after the wheel-spin fix (#203) turned out
to be a *lost* fix rather than a new bug: "do that audit".

## What the consolidation actually did

`c23cfa1` "Consolidate all Airside branch history into main" is a **74-parent
octopus merge**. Its first parent is `d17df53`, and:

```
git diff --name-only d17df53 c23cfa1   ->   0 files
```

The merge tree *is* parent 1's tree. All 73 other branch tips contributed
**zero content** — functionally `-s ours` across 73 branches. Every commit on
those branches became reachable history, and every tree was discarded.

This is why `git merge-base --is-ancestor 177c075 HEAD` succeeds while
`RebakeWheelPivots` is nowhere in the codebase: the commit is an ancestor of
main, its content never was. Ancestry proves nothing about content here, and
`git log` reads as though the work landed.

## Method, and what it cannot see

For each grafted tip, diff it against its own merge-base with parent 1, collect
the C# declarations it **added**, and ask whether that identifier exists
anywhere in the tree. 87 declared symbols came back absent, concentrated in two
tips: `adff81f` (66) and `0a4ae95` (17).

Two limits, both material:

- It matches on **added declarations**. A lost *edit to an existing function* —
  a behaviour change introducing no new name — is invisible to it. The
  `Landing(a)` / `Landing(a, 0f)` compile break that Codex fixed in #200 was
  exactly that shape. So this is a floor on the loss, not a ceiling.
- A renamed successor reads as a loss. Four did:
  `Pavement_PlateauCoversCrossStripAndTaxiF` → `…CrossStripTaxiAndAprons`,
  `StripMarkings_TaxiGuideHasEdgesAndDashes` → `…IsSolidCentrelineAndDoubleEdges`,
  `DistanceToRunway` → `DistanceToPavement`, and
  `BuildBareAdelaideRunway` → `BuildBareMainRunway` / `BuildBareCrossRunway`.

## Findings

### Pass A — wheel pivot rebake (`177c075`). Real loss. Fixed.

Genuinely lost, and the bug was live: every tyre/wheel/rim node has an identity
transform with its mesh baked in aircraft space, so the roll pass swept each
part around the fuselage centreline (0.37 m radius forward mains, 0.84 m aft,
8.35 m nose). Restored in #203 with the name contract locked by
`AircraftPartsTests`.

### Pass B — gear strut hinge (`a10edcd`). Superseded. No action.

The branch's `RebakeGearStrutPivots` is absent, but the fix is not: the later,
more general `RebakeAircraftArticulatedPivots` already special-cases
`"Gear nose" / "Gear L" / "Gear R"` with `pivot.y = bounds.max.y` and applies
`RebakePartPivot`, which clones the mesh, shifts the vertices and moves the
transform — the same top-hinge rebake by a different name. Restoring Pass B
would add a second pass that immediately early-outs.

### Pass C — cabin window recess (`50bb41f`). Does not apply to v02. No action.

Pass C recessed panes that the **v06** kit baked flat at the fuselage's widest
half-width. The v02 generator builds each pane with `surface_quad`, which
samples the analytic fuselage surface (`surface(z, theta, offset)`) at every
corner, so the panes follow the body curve. Measured against that surface, all
26 panes sit a uniform **1.6–3.2 cm** proud, which is the intended construction:
`offset=.021` plus an 8 mm front face.

An earlier measurement in this session reported up to 19 cm of protrusion. That
was wrong. It interpolated fuselage *vertices* by height across a wide z-window,
but the section is an ellipse whose centre height `cy` varies by station, so
"max |x| among vertices bracketing y" is not the skin half-width at that
(z, θ) — it under-reads near the top of the section and manufactures a
protrusion. The v02 rebuild solved this class of problem structurally.

### Pass D — gear-door hinge trap (`0a4ae95`). Moot on v02. No action.

The lost note warned that a naive hinge rebake sends the flat belly door ~0.9 m
below the fuselage and through the runway. `RebakeAircraftArticulatedPivots`
does perform that naive rebake today (`Gear door*` → `pivot.y = bounds.max.y`),
but on the v02 kit the geometry no longer reaches the ground. Rotating every
door vertex 78° about the rebaked hinge:

| door | span from hinge | lowest point after swing | drop |
|---|---|---|---|
| `gear_door_left` / `_right` (flank panels) | 0.93 m | y = 0.88 | 0.18 m |
| `gear_door_nose` (belly panel) | 0.62 m | y = 0.78 | 0.56 m |

The wheel contact plane is y = 0, so nothing penetrates the runway. The nose
door still hinges about its centre station rather than an edge, so it seesaws
rather than swinging like a real door — cosmetic, worth tidying whenever the
doors are next revisited, not a trap.

### `adff81f` — 66 symbols, lost, moot

A large Adelaide landside branch: suburban houses, CBD tower glow, hills ridge
trees, kerbside coach, terminal glass banding, navaids, parked-fleet liveries.
All of it sits behind `ShowBuildings` / `ShowEnvironment` / `ShowWorldProps`,
which are false on the bare field (ADR 0041), so none of it would render. The
only aircraft-facing casualty is `UpdateClimbVapor` (wingtip vapour on climb),
a nice-to-have rather than a defect.

## Decision

Restore nothing further. Of the four Passes, only Pass A was a real, live loss,
and it is already fixed. B is superseded, C and D are made moot by the v02
rebuild, and the scenery branch is moot under the bare-field direction.

Record it here so the same four commits are not re-chased: they read, from
`git log`, exactly like work that landed.

## Residual risk

The audit cannot see lost edits to existing functions. If another defect of the
`Landing(a)` shape surfaces, suspect the graft first and diff the relevant
branch tip against its merge-base rather than trusting ancestry. A future
consolidation should merge trees or cherry-pick, never graft history whose
content is discarded — reachable commits that contain nothing are worse than
deleted branches, because they look like delivered work.

## Evidence

`scripts/test-domain.sh` **121 passed, 0 failed** (unchanged; this ADR is
documentation only and ships no code). Geometry figures above were computed from
the shipped `mdl_atr42_starter_v02.gltf` and `.bin` and the `STATIONS` profile in
`scripts/generate-air-001-atr42-v02.py`.

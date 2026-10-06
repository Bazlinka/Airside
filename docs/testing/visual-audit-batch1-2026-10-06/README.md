# Visual audit fixes — first code-only batch, 6 October 2026

## Authorisation and scope

Bailey approved parallel implementation of the first batch from draft PR #534,
then explicitly excluded all Unity testing, builds and player execution. This
user constraint overrides the repository's usual native verification requirement.
Code review and Unity-free headless checks are the acceptance scope for this PR;
native compilation, runtime transforms, visual quality and performance remain
unverified. No historical manual acceptance is reopened by this work.

Baseline: main `000ab56b`. Report: PR #534 at
`2ed8a1d67d600fe3acc4ac454ca0a297aa66cd90`.

## Exclusive ownership

- MAP: MAP-01 scheduled first departures, MAP-02 inspector transitions, MAP-03
  north-up projected aircraft headings. Owns Airline partial and map helpers/tests.
- GROUND: GND-01 service wheel pivots/axes/roles, GND-02 stair/apron datum and
  passenger apron paths. Owns GroundService/Boarding partials and ground helpers/tests.
- AIRCRAFT: AIR-01 hub-local fan-disc coverage, AIR-03 type-specific touchdown
  effects. Owns AircraftVisuals partial and aircraft helpers/tests.
- INTEGRATOR: shared harness, GAME.md, CHANGELOG.md and this evidence.

Each worker uses its own branch/worktree. No simulation commands, scheduling,
save schema, aircraft paint/geometry, asset provenance or optional design work
are changed. Shared-file changes require an explicit handoff.

## Acceptance

Tests must exercise pure helpers called by production code rather than copied
formulas. Source review confirms caller wiring; that is not a native compile.
Map tests cover first scheduled/AI flights, planning vs inspection transitions,
and headings under field rotation/projection. Ground checks cover road-wheel
selection, stationary axle centers, original geometry preservation and apron vs
motion-root datum. Aircraft checks cover hub radial distances, type-specific
contact timing, rotorcraft exclusion and once-per-landing event semantics.

## Verification

- Baseline full headless: **1,511 passed / 0 failed**; see [baseline](baseline-headless.txt).
- MAP worker `aeb1f159`: **17/17 focused** headless map regressions.
- GROUND worker `6199b14e`: **20/20 focused**, including 6 fuel, 10 baggage and
  8 bus tyre/hub parts read from shipped glTF/bin data; see [ground](ground-focused.txt).
- AIRCRAFT worker `196de0c6`: full headless **1,520 passed / 0 failed**.
- Integrated asset-only fan probe: all 8 fan sides contain the blade-tip radius
  with 2.5% radial margin; see [probe](fan-asset-probe.txt), rerun with
  `python3 scripts/audit-jet-fan-disc-coverage.py`.
- Static metadata/mirror audit: **1,749 unique GUIDs**, **386 byte-identical
  mirrors**, **70 committed character materials**; see [audit](asset-audit.txt).
  This Python filesystem audit does not open Unity or validate imports.
- Integrated harness generation: 130 pure Presentation files and 189 test files.
- Independent worker cross-review of integrated ground/aircraft code found no
  actionable symbol, coordinate or lifecycle issue; source review only.
- Combined full headless: **1,557 passed / 0 failed**, including all 46 new
  regression cases; see [integrated run](integrated-headless.txt). No Unity commands are authorised.

Production caller wiring was source-reviewed. Headless compilation does not
include the changed Unity-dependent prototype partials. Actual Unity transform
composition, native C# compilation, shader/material rendering, passenger foot
poses and performance remain unverified. The wheel fixture verifies shipped
positions and pure geometry; the runtime pivot hierarchy itself was not executed.
The fan probe covers baked identity-node glTFs, not alternative imported prefabs.

The ground review also corrected bus root placement when the walking/bus-stop
datum moved to pavement; it measures road-tyre contact without modifying meshes.
Map selection exits inspection deliberately, while direct inspection preserves
planning identity. A cancelled AtStand schedule is hidden; a live new schedule
replaces the route label from a retained previous trip.

## Remaining audit scope

Nine concrete findings remain pending: CORE-01, AIR-02, CK-01, WLD-01,
AST-01/02, WX-01 and SKY-01/02. Optional lighting, tree/scenery design and
performance work are unchanged. Draft PR #534 remains separate from this batch.

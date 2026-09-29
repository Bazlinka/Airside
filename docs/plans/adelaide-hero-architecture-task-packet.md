# Adelaide hero architecture — implementation task packet

Date: 30 September 2026
Owner: Codex
Branch: `codex/adelaide-hero-architecture`

## Player-visible outcome

Adelaide Airport reads as a specific, finished place at overview and follow distance: Terminal 1 has a stronger roofline and airside facade rhythm, the operational hangars have recognisable roof silhouettes rather than flat prisms, and every working aerobridge has visible structure, glazing, cab detail and running gear.

## Scope

- `Presentation/AirsideAdelaidePavement.cs`
- `Presentation/AirsidePrototype.YpadPavement.cs`
- `Presentation/BuildingDetail.cs`
- `Presentation/AirsidePrototype.Aerobridges.cs`
- focused EditMode tests for the terminal, hangar-roof and bridge presentation contracts
- `GAME.md`, `CHANGELOG.md`, the asset/data register and a dated ADR

No simulation, airport coordinates, stands, routes, reservations, saves, fleet economics or schedules are in scope.

## Relevant decisions and invariants

- ADR 0068: the visual airport uses real Adelaide coordinates.
- ADR 0113: aerobridges are presentation-only and never decide boarding or reservations.
- ADR 0124: surveyed building footprints remain authoritative; detail is merged by material.
- ADR 0184: use the existing OSM-derived airport and precinct data, not a parallel layout.
- Procedural shells remain the fallback and all new presentation remains deterministic.

## Acceptance criteria

1. Every surveyed operational hangar receives a closed, non-flat roof silhouette wholly supported by its existing footprint.
2. Current hangars include at least two visibly different roof profiles.
3. Terminal 1 gains raised skylight/lantern forms and a consistent airside structural rhythm without extending beyond the surveyed terminal bar.
4. Aerobridges retain their existing dock/retract positions and timing while gaining rotunda glazing, tunnel frames, cab glazing/roof detail and a recognisable wheeled bogie.
5. No new external asset or licence obligation is introduced.
6. The complete Unity EditMode suite, asset audit and Mac build pass.
7. Day and night packaged captures show the new silhouettes without blocking gates, aircraft or markings.

## Tests and playtest

- Add deterministic geometry tests for roof profiles, bounds and terminal hero elements.
- Run `scripts/audit-unity-assets.py`.
- Run `scripts/test-unity.sh`.
- Run `scripts/build-mac.sh`.
- Capture packaged overview by day and night; inspect the terminal/hangar/bridge regions and Player log.

## Must remain unchanged

- Adelaide footprints, terminal wall polyline and gate coordinates.
- Aerobridge service gates, door targeting, movement timeline and boarding logic.
- Simulation determinism, saves and route reservations.
- Existing material and primitive fallbacks.

## Completion evidence

- Unity EditMode: 1,426 passed, 0 failed, 2 precondition-based inconclusive.
- Unity asset audit: 1,449 unique GUIDs, 343 mirrored runtime-art files, 70 character materials.
- Mac build: `work/builds/Airside.app`, successful dirty feature-branch build.
- Packaged captures inspected: `adelaide-hero-day.png`, `adelaide-hero-night.png`,
  `adelaide-hero-terminal-overview.png`, and `adelaide-hero-aerobridges-airside.png`.
- No exception/error match in the four corresponding Player logs.
- Supplementary .NET harness unavailable because the .NET SDK is not installed on this Mac.

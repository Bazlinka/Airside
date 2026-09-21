# 0076 — Aircraft-specific reach and an honest live-rivals map

Date: 2026-09-21

## Decision

Replace the three generic 500/1,000/2,000 km rings with one geodesic ring for the selected
aircraft's actual practical range. Draw only the selected destination route by default; the existing
runtime hover route remains available for comparison. Keep every destination dot, but reveal its code
at 2x zoom and full name at 4x, while always naming the selected destination.

Add a `RIVALS n · ON/OFF` control for actual airborne AI-airline aircraft. Hide those routes, icons
and hit targets together when off. Remove ambient `SkyTraffic` icons from this strategic map; they are
world atmosphere, not competitors, and labelling them as rivals would be dishonest.

## Reason

The former all-spokes network, generic rings and ambient traffic created visual density without
helping a decision. The player needs three answers: where can this aircraft reach, which route am I
considering, and where are the simulated competitors right now. The revised hierarchy answers those
directly from existing simulation data.

## Consequences

- Range changes immediately when the player cycles aircraft.
- Rival presence is competitive information, not invented market share or a fake score.
- Zooming progressively reveals destination detail rather than showing every full label at once.
- Ambient sky traffic remains in the airport world and no longer competes with strategic map data.

## Verification

- Domain suite: 555/555, including selected-aircraft range and declutter tests.
- Shared 1440 x 900 Route Map render inspected: one selected route, destination dots and restrained
  labels remain legible.
- Full Unity player rebuild deferred to avoid consuming the user's final weekly usage window; the
  branch changes compile through the shared domain/painter harness.

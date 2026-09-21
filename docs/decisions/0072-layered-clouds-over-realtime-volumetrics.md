# 0072 — Layered cloud meshes over realtime volumetrics

Date: 2026-09-21

## Decision

Build every procedural cloud from three independently tinted combined meshes: a wide, flattened
underside; an irregular bank of body lobes; and a smaller raised crown. Continue to reveal clusters
according to simulated cloud cover, move them with the real surface wind and move their ground
umbras with them.

Add `-airsideReviewWeather <kind>` as a presentation-only packaged-build override so every weather
state can be inspected without changing the simulation clock or save.

## Reason

The old clouds were only one or two stretched spheres. At the game's normal high overview they
read as separate translucent discs rather than coherent cloud banks. Full realtime volumetric
clouds are a poor fit for Airside's broad airport view and current procedural presentation stack:
they would add a large GPU and integration cost to solve a silhouette and shading problem.

Three low-poly layers provide the useful visual cues—flat darker bases, uneven mass and sunlit
vertical development—while retaining deterministic layout and predictable performance.

## Consequences

- Adelaide renders at most 16 clusters × 3 renderers (48 cloud renderers), with each layer's lobes
  already combined into one mesh.
- Clouds remain stylised geometry rather than physically simulated vapour.
- Weather tinting now updates every layer with role-specific brightness; it no longer
  expects a renderer on the cluster root.
- Cloud lobes use opaque lit depth rather than transparency. Transparent intersections exposed
  every individual sphere as a white disc from the normal overview camera; solid depth joins them
  into one readable mass, while cover thresholds control which clusters are visible.
- Cloud ground shadows rotate with their cluster footprint instead of resetting north/south.
- The review-weather override affects presentation only and is intended for visual QA.

## Verification

- `scripts/test-domain.sh`: 550/550.
- Unity EditMode: 784/786; the same two unrelated baseline failures recorded in the session handoff.
- A fresh packaged Mac build was captured under forced Cloudy weather and inspected at 1920×1080.

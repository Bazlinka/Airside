# 0074 — Distance-aware ground clarity without fake resolution

Date: 2026-09-21

## Decision

Treat the Sentinel-2 surroundings image as broad geographic context rather than one uniform
surface layer. Blend it at 78% beside the airport, rise to 92% beyond 5.2 km, and restore a small
rotated procedural detail signal in the middle distance. Use the exact near-field equation at the
authored-ground boundary so reducing satellite influence does not expose a rectangular seam.

Regenerate the same 2048 x 2048 runtime image from a 4096 x 4096 WMS request before runway
alignment. Keep the runtime dimensions and coverage unchanged. Apply a conservative negative mip
bias to the authored ground layers on high/medium quality because the airport is usually viewed at
an oblique angle.

## Reason

The shipped image covers 24 km and its Sentinel-2 source is roughly 10 m per source pixel. Upscaling
that image or applying AI sharpening would produce confident-looking false detail and shimmer. The
visible blur instead needs scale separation: authored texture detail near operations, satellite
colour and geography at distance, and one high-quality resampling step before the runtime asset.

## Consequences

- Runtime satellite texture dimensions and GPU memory remain unchanged.
- Near-ground detail is authored and repeatable; it is not presented as aerial-photography detail.
- The satellite still cannot show sub-metre features it never captured.
- QGIS/GDAL remain sensible free tools for inspecting and preparing future legal source imagery,
  but no external editor is required to reproduce this asset.

## Verification

- Domain suite passes.
- Unity EditMode: 787/789, with only the same two unrelated baseline failures.
- Fresh Mac build completed and the final 1920 x 1080 overview was inspected after regeneration.

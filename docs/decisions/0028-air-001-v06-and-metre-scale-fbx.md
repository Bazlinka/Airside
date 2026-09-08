# 0028 — AIR-001 v06 and metre-scale FBX imports

**Date:** 2026-09-08
**Status:** Accepted
**Decision owner:** Bailey

## Decision

Replace the preferred AIR-001 presentation with a distinct v06 high-wing regional
turboprop. Retain v05 and all prior aircraft as ordered fallbacks. Generated ASCII
FBX files declare 100 centimetres per authored unit because Airside procedural
geometry is authored in metres. Meshes without usable UV coordinates receive flat
lit materials and do not sample authored texture maps.

## Reason

v05 still read as assembled blocks beside the approved aircraft reference. During
v06 packaged validation, Unity exposed two broader pipeline defects: the exporter
declared metre coordinates as centimetres and imported the plane at 1% scale, while
UV-less meshes sampled a single dark texture texel and rendered black. Both defects
made model quality impossible to assess in the game.

## Impact

The change is presentation-only. Simulation dimensions, paths, reservations, saves
and timings do not change. v06 keeps the existing animation part-name contract.

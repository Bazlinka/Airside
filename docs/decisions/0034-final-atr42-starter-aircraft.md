# 0034 — Final ATR 42-class starter aircraft

**Date:** 2026-09-10
**Status:** Accepted
**Decision owner:** Bailey (product), Codex (implementation)

## Decision

AIR-001 is replaced by `mdl_atr42_starter_v01`, a production-identity asset
rather than another revision of the underscale regional-turboprop placeholder.
It is a fictional, unbranded ATR 42-600-class aircraft at the manufacturer's
published three-view dimensions: 22.67 m length, 24.57 m wingspan, 7.59 m
height and 3.93 m propeller diameter.

The gameplay articulation set is deliberately restrained: two six-blade
propellers, flaps, ailerons, elevators, rudder, spoilers, cabin and cargo doors,
three retracting gear assemblies, gear doors and six rolling wheels. Each part
has a runtime-rebaked hinge/pivot. Decorative parts do not animate.

The livery remains Airside-owned and fictional. `mdl_regional_turboprop_01_v06`
is retained only as a compatibility fallback.

## Reason

The old 15.09 m × 10.70 m model read as a small generic commuter next to the
real-metre 3,100 m × 45 m runway. The ATR 42 is large enough to be legible from
the management camera, credible for a small regional airport, and leaves clear
future progression to larger ATR 72-class turboprops and regional jets.

## Affected systems

- AIR-001 FBX, glTF/`.bin`, StreamingAssets mirror and Resources prefab
- Aircraft material/name mapping, articulated pivots and gear hierarchy
- Follow framing, propeller blur, touchdown wheel spacing and ground shadow
- AIR-001 dimension and articulation EditMode tests

Save schema and deterministic simulation are unchanged.

## Evidence

- ATR official product page and ATR 42-600 factsheet (three-view dimensions)
- Generator reports 158 named parts, 19,824 vertices and 8,712 triangles
- Unity Resources prefab bake and AIR-001 targeted EditMode tests
- Packaged macOS follow-camera inspection

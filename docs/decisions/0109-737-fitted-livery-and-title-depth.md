# 0109 — 737 fitted livery and depth-tested fuselage titles

Date: 23 September 2026. Bailey: Virgin/Qantas 737s on the apron still looked
barcoded while Hangar previews for fitted types looked clean; fuselage wordmarks
also read through the wings.

## Decision

### 737-8 and 737-800 operator sash

AIR-005 (737-8) replaces its buried box stripes with the shared
`aircraft_skin.livery_ribbon` sash used by A320/A330/turboprops. The Adelaide
737-800 kit is regenerated from that base. Presentation treats both types as
fitted liveries: no repeating traffic decal; `Livery*` parts take the operator
accent colour.

### Fuselage titles depth

World-space `TextMesh` titles used the GUI/Text Shader (ZTest Always, ZWrite Off)
and `sortingOrder = 2`, so glyphs drew on top of opaque wings. Titles now use a
URP Unlit cutout material that depth-tests and writes depth; sorting order stays
at 0. Daylight tint updates `_BaseColor` as well as `TextMesh.color`.

## Affected systems

`generate-air-005-narrowbody-737-8.py`, Adelaide fleet regeneration, StreamingAssets
kits and Hangar thumbnails, `AirsidePrototype.FleetVisuals` (`hasFittedLivery`),
`DepthTestStandLabel` in `AirsidePrototype.YpadPavement.cs`.

## Migration

None. Art kits and presentation only; saves and simulation unchanged.

## Guardrails

- Still no real airline trademarks or copied paint schemes.
- E190 / A220 / A321neo / 787 family remain on the generic decal until their own
  fitted passes.
- Keep titles as paint-like cutout geometry, not screen overlays.

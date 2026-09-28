# 0160 — Trees where the satellite sees tree canopy

Date: 28 September 2026. Author: Claude, at Bailey's request ("improve the surroundings … trees").

## Context

The normal Adelaide field had no trees at all. The eucalyptus kit is used only by the legacy
compact scene. OpenStreetMap maps few individual trees around the airport, so it cannot say where
they stand.

## Decision

`scripts/generate-adelaide-trees.py` finds canopy in the same nine clear summer Sentinel-2 L2A scenes
as the ground imagery (ADR 0157), using the near-infrared band:

- **Measure.** Per-pixel median NDVI and visible brightness.
- **Classify.** In an Adelaide summer, evergreen canopy is NDVI > 0.35 and dark (visible reflectance
  < 0.055). Irrigated turf is as green but bright, and dry grass is neither. The mask follows the
  Sturt River and Patawalonga lines, the airport's perimeter drain, golf-course roughs and street
  trees.
- **Place.** A 10 m canopy pixel holds a tree with 70 % chance, jittered inside it.
- **Exclusions.** Dropped if on an airport or suburb building (ADR 0159), on a road carriageway
  (verge trees stay), on water, airside, or in the landside precinct. Trees thin out with the
  suburbs, 1.1–1.8 km out.
- **Result:** 11 428 trees, 7–14 m tall with 2.6–4.4 m crowns, in four eucalypt greens (ATRE v1,
  190 KB).

`AirsideAdelaideSuburbs` builds them into the same 1 km tile meshes and material as the buildings:
a three-sided trunk and a six-sided faceted crown with a low dome top, about 18 triangles each. The
graphics-test switch becomes **Suburbs and trees**.

## Evidence

- `docs/testing/surroundings-2026-09-28/suburbs-trees-overview.jpg` and `suburbs-trees-low.jpg`:
  rough offline renders (not Unity).
- `AdelaideSuburbDataTests.Trees_StandWhereTheSatelliteSeesCanopy`: count, sizes, colours, none in
  the landside precinct or on the runway strip, truncated files rejected.

## Costs

- About 205 k triangles on top of the buildings' 212 k, still one material and about 12 tiles.
- Both come off with the switch.

## Not verified

**Not seen in Unity.**

Data: Contains modified Copernicus Sentinel data 2024–2026. Output SHA-256 `5bff07ef05629cdb8e932420cb45cc925750666efd028bf899ccfc5b2198aa74`.

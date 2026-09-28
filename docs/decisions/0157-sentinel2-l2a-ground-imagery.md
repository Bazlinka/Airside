# 0157 — Ground imagery rebuilt from native Sentinel-2 L2A

Date: 28 September 2026. Author: Claude, at Bailey's request ("improve the surroundings … quality
of satellite imagery especially").

## Context

The normal Adelaide field drapes a runway-aligned satellite image over the airfield ground and the
coastal-plain surroundings (ADR 0074, `docs/plans/ypad-surroundings-plan.md` P7). v01 was a 2048 px
bake of the ESA WorldCover 2021 composite fetched over WMS. Looking at it:

- a large smeared wedge across the Gulf where the WMS returned a tile gap;
- flat, hazy tone, and about 12 m output pixels from a 4096 px request rotated and downsampled;
- no on-screen credit, although it is CC BY 4.0.

Sharper imagery is not available on open terms. Google, Bing, Apple, Mapbox, Esri and Nearmap
forbid redistribution. South Australia's open portal (data.sa.gov.au, checked again today) lists
only the 1949 black-and-white metro mosaic; current state orthophotos are supplied through Mapland
on unconfirmed terms (plan §1a). Sentinel-2's 10 m is therefore the ceiling, and what can improve
is the processing.

## Decision

`scripts/generate-adelaide-satellite-s2.py` builds v02 from the source directly:

- **Source.** Nine cloud-free (< 1 %) summer scenes of tile 54HTG, 2024–2026, as Sentinel-2 L2A
  cloud-optimised GeoTIFFs from Element 84 Earth Search on the AWS Registry of Open Data. Only the
  area's window is read.
- **Median.** Per pixel, with cloud, shadow, cirrus, saturated and no-data pixels removed from the
  scene classification. This leaves no gaps, no single-day cars or boats, and one even summer light.
- **Tone.** Earth Search's COGs already have the +1000 DN offset removed, although the metadata
  says otherwise: subtracting it again crushed green and blue (found and fixed here). One brightness
  curve for land and a plain exposure scale for water are applied equally to all three channels, so
  hues stay true. Both are fitted to v01, whose tone the ground and surroundings shaders were tuned
  against. Land gets ×1.12 saturation and ×1.05 contrast over v01, and water/land are blended on a
  softened water mask, so there is no seam at the beach or the Patawalonga.
- **Frame.** The same ±12 km runway-local square and the same local↔lon/lat convention as v01, so
  every shader constant and UV is unchanged. Output is a 4096 px JPEG (5.9 m/px; bicubic from 10 m),
  7 MB against v01's 8 MB PNG.
- **Memory.** `AirsideArtTextures.Load` reads the image size from the PNG/JPEG header so a 4096²
  image can be recognised. ADR 0157 compressed that texture on load. ADR 0162 stops that: the
  compress froze the game on open. The satellite now stays uncompressed (about 85 MB of VRAM
  with mips).
- **Credit.** `MapAttribution.Sentinel` ("Contains modified Copernicus Sentinel data 2024–2026") is
  added to the field credit line. `HudLayout.CreditWidth` is 470 → 760 so both credits fit.

v01 moves out of the shipped art into `docs/data/esa-worldcover/` as the tone reference.

## Evidence

`docs/testing/surroundings-2026-09-28/satellite-v01-v02.jpg`: whole square, airport, West Beach
coast and a suburb, v01 against v02. Taxiway edges, apron stands, terminal and hangar roofs, golf
fairways and street grids resolve in v02 where v01 was soft; the Gulf smear is gone.

Domain suite 936/936, including the extended attribution test. The header reader was checked on
the shipped JPEG (4096², large) and PNGs (1254², 2048², not large). The asset audit output is
unchanged from `main`.

**Not seen in Unity** when this record was written. A Mac overview and a close airfield view are
needed to confirm the blend and the colour on the live ground. The on-load compress described
above was later removed (ADR 0162) because it stalled the first frame.

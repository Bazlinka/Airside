# 0190 — Far land cover for the zoomed-out map

Status: accepted (Unity look not yet verified)

Beyond the satellite image (30 km) the outer terrain (ADR 0185) was one blend of two colours chosen by height. It is now
coloured by land cover from ESA WorldCover 2021 (CC BY 4.0), baked by `scripts/generate-adelaide-landcover.py` into
`landcover_adelaide_far_v01.bin`: one class per 250 m cell on the far DEM's grid (769 x 769, +-96 km), the majority of a
5 x 5 sub-grid of a ~45 m read, raw-deflated (56 KB). Suburbs read grey-tan, crop land is a patchwork of green, straw and
fallow paddocks, woodland dark green, the Gulfs and inland water blue, hillsides a little darker by slope.

`AdelaideFarLandCover` (pure) parses and colours it; `AdelaideOuterTerrainGeometry.Build` takes it as an optional argument, so
without the file the ring falls back to the height blend and nothing else changes. Attribution is in the Flight Manual credits
and the data register. Tests check the grid matches the far DEM, the class shares are plausible, land cover and DEM agree on
the coast, and the coloured mesh keeps its shape.

Not done: a finer mesh mid-range (a change of cell size would need crack-free seams), a blend at the 30 km satellite seam,
and roads or coastline lines on the far ground. Known: colour is per 500 m mesh vertex, so it is soft up close; that only
matters when the camera is very high.

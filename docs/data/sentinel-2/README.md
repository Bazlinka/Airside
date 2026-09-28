# Adelaide Sentinel-2 L2A surroundings (v02)

`tx_adelaide_sentinel2_l2a_v02.jpg` is the ground and surroundings imagery for the normal
Adelaide field. It covers the same runway-aligned 24 km × 24 km square as v01 (±12 km about the
05/23 midpoint), at 4096 × 4096 px (5.9 m per pixel; the source is 10 m).

It is a per-pixel median of nine cloud-free summer Sentinel-2 Level-2A scenes of MGRS tile
54HTG, 2024–2026, listed with their cloud cover in `adelaide-l2a-v02-scenes.json`. Cloud,
cloud shadow, cirrus, saturated and no-data pixels (scene classification) are dropped before the
median, which also removes cars, boats and glint and gives one even summer light.

Tone: one brightness curve for land and a plain exposure scale for water, each applied equally to
red, green and blue so hues stay true, fitted to the v01 texture so the ground and surroundings
shaders (tuned against v01) keep their look; saturation ×1.12 and contrast ×1.05 over v01 on land.
v01 (`../esa-worldcover/tx_adelaide_sentinel2_2021_v01.png`) is kept outside the build as that
reference.

Source and access: Copernicus Sentinel-2 L2A cloud-optimised GeoTIFFs, Element 84 Earth Search on
the AWS Registry of Open Data — https://registry.opendata.aws/sentinel-2-l2a-cogs/

Note: Earth Search's COGs already have the processing-baseline 04.00 +1000 DN offset removed even
though the item metadata still lists `offset: -0.1`; the generator uses offset 0.

Licence: Copernicus Sentinel data are free to use, modify and redistribute with attribution
(Legal notice on the use of Copernicus Sentinel Data and Service Information).

Attribution (shown on screen with the map credit): Contains modified Copernicus Sentinel data
2024–2026.

Regenerate with `python3 scripts/generate-adelaide-satellite-s2.py` (downloads the band windows
into `work/cache/sentinel-2/`, about 2 minutes). `--scenes ID ...` pins the scene list.

Shipped JPEG SHA-256: `d80280f594df5a257f659b0d090dcce8ced0b63099b08111b3b53ae44ac127bd`; the authored and StreamingAssets copies match.

# Adelaide Sentinel-2 surroundings

`tx_adelaide_sentinel2_2021_v01.png` is a runway-aligned extract of the ESA
WorldCover 2021 Sentinel-2 RGB median composite. It covers 24 km x 24 km around
Adelaide Airport at approximately 12 m per output pixel.

Source and access: https://esa-worldcover.org/en/data-access

Attribution: © ESA WorldCover project 2021 / Contains modified Copernicus
Sentinel data (2021) processed by ESA WorldCover consortium.

Licence: Creative Commons Attribution 4.0 International (CC BY 4.0).

Regenerate with `python3 scripts/generate-adelaide-satellite.py`. An existing WMS
download can be supplied using `--source path/to/image.png`.

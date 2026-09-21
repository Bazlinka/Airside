# Adelaide Sentinel-2 surroundings

`tx_adelaide_sentinel2_2021_v01.png` is a runway-aligned extract of the ESA
WorldCover 2021 Sentinel-2 RGB median composite. It covers 24 km x 24 km around
Adelaide Airport at approximately 12 m per shipped output pixel. The generator
requests a 4096 x 4096 source image before runway alignment and downsamples once
to the 2048 x 2048 runtime texture; this preserves more edge information through
rotation without increasing runtime texture memory or inventing source detail.

Source and access: https://esa-worldcover.org/en/data-access

Attribution: © ESA WorldCover project 2021 / Contains modified Copernicus
Sentinel data (2021) processed by ESA WorldCover consortium.

Licence: Creative Commons Attribution 4.0 International (CC BY 4.0).

Regenerate with `python3 scripts/generate-adelaide-satellite.py`. An existing WMS
download can be supplied using `--source path/to/image.png`.

Current shipped PNG SHA-256:
`500166901613b1cf7b9049e6df5e65e45988b9e1e0b13c2b0bc112473f0657f3`.

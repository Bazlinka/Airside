# TEX-CLOUD-001 cumulus atlas generation — 2026-09-21

Tool: OpenAI built-in image generation.

Selected output: `tx_cloud_atlas_cumulus_v01.png`, 1254 x 1254 RGBA, SHA-256
`1efb2285cc8aab71aa301a6b872cb2f7b8a7f1c631b238a4694c3614993f019c`.

## Initial prompt

> Create a production-ready 2048 x 2048 transparent PNG sprite atlas for an airport management
> strategy game viewed from a high oblique aerial camera. Arrange exactly 16 distinct realistic
> fair-weather cumulus cloud clusters in a clean 4 by 4 grid. Every cloud cluster must be fully
> isolated inside its own equal square cell with generous transparent padding on all sides; no cloud
> may touch or cross a cell boundary. Show each cloud from a consistent high three-quarter aerial
> angle, with natural soft cauliflower structure, subtle cool grey undersides, soft white sunlit tops,
> believable feathered semi-transparent edges, and midday sunlight from the upper left. Vary the
> silhouettes and density while keeping scale and lighting consistent. Genuine alpha transparency
> everywhere outside the clouds: no sky, no checkerboard, no coloured backdrop, no horizon, no
> ground, no shadows, no text, no labels, no borders, no grid lines, no logos, no watermark. This is
> a functional sprite sheet, not concept art.

## Cleanup edit prompt

> Clean this cloud sprite atlas into a genuinely production-ready transparent PNG. Preserve the
> realistic cloud character but remove every cyan, blue, black, white-speckled, chromatic-fringe,
> halo and background artifact outside the clouds. Recompose as exactly 16 distinct cloud clusters
> in a precise 4 by 4 grid. Each cluster must be scaled down enough to have at least 12 percent fully
> transparent padding on every side of its own equal cell. No pixels from any cloud or feathered edge
> may touch or cross a cell boundary. Keep only neutral white and subtle cool-grey cloud colour, with
> clean soft antialiased alpha edges. Genuine alpha transparency outside clouds, not black or white
> fill. No visible grid, no sky, no shadows, no text, no labels, no watermark. Output a square 2048 x
> 2048 PNG sprite sheet.

The service returned 1254 x 1254 rather than the requested size. It is intentionally shipped at that
native size instead of being upscaled. The runtime shader rejects sub-visible low-alpha fringe and
neutralises saturated edge RGB before blending.

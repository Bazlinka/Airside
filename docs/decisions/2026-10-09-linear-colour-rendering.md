# Linear colour rendering

Date: 2026-10-09
Status: Accepted — Bailey authorised the recommended first renderer improvement.

## Decision and reason

Use Linear colour space in Unity 6.3/URP to perform lighting and transparent blending
in linear light. This is a renderer foundation change, not a promise of a dramatic
visual upgrade. Keep current HDR, ACES, grading, MSAA/SMAA, shadow and cloud budgets.
A visual comparison may motivate a separate exposure/material tuning pass.

Existing terrain/suburb mesh palettes already store linear RGB. Retain that contract
and the WorldSurfaceLighting Gamma fallback branch so comparison builds remain possible.
Colour textures and Color material properties are authored in sRGB; normal, occlusion
and mask textures remain linear data. The land-cover calibration previously multiplied
encoded satellite RGB by encoded tint. Decode both before averaging and regenerate
its eight class colours. Green/fallow crop variants retain their prior channel ratios
relative to the recalibrated crop mean. Water geometry retains the existing sea palette.

## Affected systems and migration

Project render settings, far/state-wide land-cover presentation and its offline palette
calibrator. No simulation, RNG, saves, camera mechanics, native integration, new assets
or third-party costs. No save migration. Reverting the colour setting also requires
reverting the palette/calibrator together to restore the previous rendering baseline.

## Evidence and remaining verification

Palette generation from the existing satellite image/land-cover grid, Python syntax
and scoped diff/source checks. No broad suite, Unity launch or packaged build requested.
Native shader compilation, visual improvement and performance are unverified. Compare
identical day/dusk/night overview and aircraft-follow scenes, including white fuselage,
glass, night lamps, rain/wet pavement, clouds and terrain around the 15–30 km drape
handover. Exposure and existing hard-coded shader colours may need later visual tuning.

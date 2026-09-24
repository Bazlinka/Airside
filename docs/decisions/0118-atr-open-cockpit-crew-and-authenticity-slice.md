# ADR 0118 — ATR open cockpit and authenticity slice

Date: 2026-09-24

## Decision

Make AIR-001 the first aircraft with a genuinely transparent flight deck. Cut four sloped openings in its own fuselage shell, fit single-surface tinted panes and separate frames, and model an original dark cockpit with two seated low-detail pilots. Keep cabin windows and every other fleet type opaque until their own interiors and shell cutouts are ready. Preserve the ATR's 22.67 × 24.57 × 7.59 m envelope and 3.93 m six-blade propeller disc, but give each already-pitched blade a subtle outer sweep. Use the player/operator accent on all 13 existing fuselage sashes and lightly tint the ATR spinners; keep the fictional identity and neutral lifting surfaces.

## Reason

Making the prior window material transparent without modifying the sealed fuselage would show white skin behind it, not a cockpit. The cutout, structural frame, interior and crew must travel together. A limited ATR vertical slice establishes a repeatable source and presentation pattern without exposing empty cabins on the other 12 aircraft or changing their approved art. Sloped pane outlines, subtle prop sweep and consistent operator colour improve recognisability without a copied livery or branded part.

## References and rights

Proportions and 3.93 m propeller diameter were checked against the [ATR 42-600 factsheet](https://www.atr-aircraft.com/wp-content/uploads/2020/07/Factsheets_-_ATR_42-600.pdf); the two-crew cockpit arrangement was cross-checked against [ATR's cockpit overview](https://www.atr-aircraft.com/innovation/cockpit/). These are visual and dimensional references only. No manufacturer or airline photograph, logo, texture, CAD data or 3D asset is distributed. Geometry, crew silhouettes and paint remain original Airside project work.

## Affected systems and migration

`scripts/atr_aircraft_authenticity.py` adds the ATR-only finish before the shared glazing pass in `scripts/polish-aircraft-glazing.py`. The existing v03 model, editable FBX, Hangar thumbnail and StreamingAssets mirror are revised in place; v02/v01 remain runtime fallbacks. `AirsidePrototype` gives only the new ATR pane names transparent glass colours and preserves the other fleet's opaque glazing. No save schema, aircraft dimensions or simulation behaviour changes. If presentation fails, revert this art revision or use the existing v02 fallback.

## Verification

The candidate passed four-pane, two-pilot, six-blade, exact-envelope and triangle tests; every part chains to the fuselage within 5 cm. The 13-aircraft fitted-paint/title-layout validator and Unity EditMode (1055/1055) pass. A packaged close-view remains required before final visual approval.

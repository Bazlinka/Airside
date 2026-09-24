# ADR 0117 — Fitted aircraft glazing across the fleet

Date: 2026-09-24

## Decision

Keep each aircraft's existing window shape and add a small, fitted geometry layer to every cabin and flight-deck pane: a pale metal surround, a dark recessed gasket, and a restrained upper-edge reflection. Generate this layer from the same project-owned procedural source as each of the 13 genuine fleet models. Render Hangar thumbnails from the updated glTF kits and mirror those same kits into StreamingAssets.

## Reason

The existing panes read as flat dark marks at close range. Separate fitted edges give the windows depth and a more believable transition into the curved fuselage, while preserving each aircraft's dimensions, window rhythm, operator paint and distinct cockpit shape. The details are merged by role and side to limit draw-call growth.

## Consequences

The shared source is `scripts/polish-aircraft-glazing.py`, with per-type source generators unchanged. Regenerating the fleet requires running that script before the thumbnail renderer and StreamingAssets sync. Unity classifies trim as painted metal, gasket as rubber, and reflection as opaque aircraft glazing; neither transparency nor cabin-light timing changes. This is original project geometry, with no external model or texture rights. The previous git revision is the fallback.

Validation: all 13 generated models pass connected-mesh checks and the asset mirror audit; Unity material classification is covered by EditMode regression tests. Close renders were inspected for A320, A220, Saab 340 and A350. A packaged aircraft close-view remains a separate presentation check.

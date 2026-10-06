# ADR 0237: Full-map flight inspection and shared UI controls

Date: 6 October 2026. Bailey corrected the earlier mini-map request: flight status
and camera actions belong on the normal full map. He also requested the whole
game's icons and buttons be refreshed, with map screens first.

Click a simulated flight on the full map to open a live flight inspector in its
right-hand pane. Keep the map open; do not switch the planning aircraft or conflate
an airborne flight with destination booking. The inspector reuses the existing
status/journey/card data and cockpit, window-seat and exterior actions. Selecting
a destination restores planning details. All simulated operators are visible by
default and a South Australia shortcut avoids repeated zoom/pan setup.

Make the map surface opaque, reserve planning/inspection space, show fewer
unselected labels and limit route emphasis. External real-feed decoration remains
separate from simulated status/camera access. Existing SA journey eligibility and
render-origin handling from ADR 0235 are retained. The mini map returns to its
airport scope; its frame and traffic markers are polished.

Shared icons are 37 original line glyphs, authored in a 32-unit grid with uniform
round strokes. `scripts/generate-ui-icons.py` keeps editable SVG sources and
supersampled runtime PNGs; old v01 icons remain fallback. Shared buttons use slate
secondary fills and Coastal Blue primary actions, reserving amber for operational
attention. No external artwork, library or asset licence is introduced.

Acceptance: full-map click retains map context and planning identity while
exposing status/view actions; destination booking remains intact; native/headless
regression and asset audits; clean Mac build; inspect actual packaged map,
inspector and airport mini map. Capture evidence is distinct from hardware input,
full-flight weather and performance acceptance. No save or simulation changes.

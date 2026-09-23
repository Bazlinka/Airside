# 0106 — A330-900 nose and fitted livery

Date: 23 September 2026.

## Decision

AIR-015 retains its existing ID, stand datum, published planning envelope,
running gear, wing and engine animation nodes. Its forward fuselage is now
rounder and fuller instead of the scaled A350 spear; four individually fitted
flight-deck panes replace the A350 six-pane mask. A shallow geometry ribbon
conforms to the actual fuselage and receives the existing operator colour.
The A330 alone stops receiving the generic repeating fuselage decal, which
painted barcode-like bands at overview distance. Pylons no longer project
as rectangular blocks above the wing. The Hangar thumbnail is regenerated
from the runtime model.

## Reason

At the miniature camera scale the nose, glazing, wing/engine relationship and
colour block are the most recognisable features. These changes improve the
A330 read without risking the established operational loop or silently
overwriting the source A350 kit. Further wing and nacelle fidelity remains
a separate reviewable slice.

## Affected systems and migration

AIR-015 art and presentation material selection only. No simulation rule,
command, save format, route, control, data source or fallback changes. No
migration. Geometry and colour are original Airside procedural work, without
downloaded models, logos or copied liveries.

## Acceptance

Pass the fleet envelope and A330 family-cue tests; every part must connect
within 5 cm. Verify the packaged Mac model and livery at Gate 25 in day,
dusk and night, and confirm Unity compiles with no new EditMode failure.

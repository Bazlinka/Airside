# ADR 0118 — Cut-through aircraft glazing and operator paint

Date: 2026-09-25

## Decision

Cut a bounded opening in the authored skin behind every cabin and flight-deck pane on all 13 runtime aircraft. Use one translucent outer lite, a recessed dark interior, fitted seal and restrained highlight. Add two simple cockpit crew silhouettes. Keep each type's original window outline, proportions and moving parts. Split oversized widebody fuselages into port/starboard 16-bit mesh nodes to satisfy the existing glTF loader.

Use the operator's existing livery colour for fitted side markings instead of a universal blue, and restrict accent paint to the fin, rudder and winglets. Return the main wings, horizontal stabilisers and engines to neutral paint. Correct Virgin Australia's overly pink palette to red. Keep the game's original unbranded geometry and text; do not copy real logos, artwork or exact trade dress.

## Reason

ADR 0117's sealed skin and opaque, double-sided pane read as dark paint, not glass. Real aircraft show dark cabin depth, subtle reflections and crew through the flight deck at close range. Operator-colour side markings are more recognisable than the same blue stripe on every airline. Airbus, Boeing and ATR type imagery and airline fleet pages are visual references only; no third-party art is embedded.

## Affected systems and migration

`scripts/polish-aircraft-glazing.py`, 13 aircraft glTF/FBX kits, Hangar thumbnails, StreamingAssets mirrors, material classification, aircraft colour mapping and the operator palette. Save schema, aircraft dimensions, route balance and flight behaviour are unchanged. Regenerate the kits before thumbnails and mirrors. The previous git revision remains the rollback. Packaged close-view visual approval is still required before calling the art final.

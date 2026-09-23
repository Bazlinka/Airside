# 0107 — Fitted starter-turboprop liveries

Date: 23 September 2026.

## Decision and reason

AIR-001 ATR 42, AIR-006 Dash 8 Q400 and AIR-007 Saab 340B retain their
existing IDs, envelopes, silhouettes, moving parts and fallback models. Their
inset rectangular livery bars are replaced by shallow, curved ribbons following
each fuselage skin. The existing fictional operator colour paints these parts.
These three types no longer receive the generic repeated traffic decal, whose
metre-based UVs produced barcode-like bands in the packaged game. Hangar
thumbnails are regenerated from the resulting runtime geometry.

This is a visual repair at normal miniature viewing distance, not a claim of
dimension-perfect aircraft or a new livery identity. The remaining nose,
wing and engine fidelity can be improved in later aircraft-specific slices.

## Affected systems and migration

Procedural source, aircraft art, Hangar thumbnails and fleet presentation
material selection only. No simulation rule, command, control, save format,
route, real data source or migration. No external model, logo or copied airline
design is introduced; the original kits and primitive fallbacks remain in git.

## Acceptance

Three generator checks, strict 5 cm connected-part check and inspected
multi-view renders; packaged Mac build compiles and loads the updated art.

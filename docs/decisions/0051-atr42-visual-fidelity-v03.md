# 0051 — AIR-001 ATR 42-600 visual fidelity pass (v03)

Date: 2026-09-15

## Decision

Author a new `mdl_atr42_starter_v03` kit for AIR-001 rather than overwrite the
approved v02 asset. Catalogue and runtime prefer v03, then v02, then v01, then
the older regional turboprop v06 primitive kit.

## Why

After the Cursor Q400 / Saab / 737 visual revisions, the Hangar render of the ATR
v02 looked less finished. Bailey asked for one controlled fidelity pass that
keeps the stocky ATR character and the exact 22.67 × 24.57 × 7.59 m envelope.

## What changed

- Four clean fitted cockpit panes with credible body-colour pillars
- Even, Hangar-readable cabin window rows
- Smoother blunt nose into the cabin without lengthening the airframe
- Compact nacelles blended into the high wing
- Fuselage-side main-gear sponsons kept clearly distinct from Q400 nacelle gear
- Improved wing-root, fin and T-tail joins
- Six readable 3.93 m propeller blades with mechanically connected hubs
- Fewer floating panels / coplanar tiles / block fairings

## Invariants

Presentation-only. Exact dimensions, centred regional-aircraft root, tyre
contact at y=0, animation/part names, schedules, stands, reservations and save
schema are unchanged. Q400, Saab and 737 kits were not modified.

## Evidence

- Generator: `scripts/generate-air-001-atr42-v03.py`
- Geometry check: `scripts/test-air-001-atr42-v03.py`
- Review board: `docs/art/candidates/air_001_atr42_v03_mesh_review.png`
- Multi-angle stills: `work/review/air-001-atr42-v03-*.png`
- glTF SHA-256 `813063c6c681a0027bac7b5f33d439aaad8fae99d0dc95804d44317ff5266eea`; bin `cef96226f22ced6aee3366b9001154b5f32c7b95ad6d0572922474327dee1cbb`; FBX `ec70c066e27542031610494e798a8f9003a9151a89839032b4a7abccf680b729`
- Hangar thumb SHA-256 `992ed02a08d52c6d067a704882bcfbd16aff917d0d55b22b75af48eb8a6cbbf9`

## Follow-up

Unity EditMode / packaged overview-follow day/dusk/night QA on Mac when an
editor is available (`scripts/test-unity.sh`).

# 0228 — Cockpit immersion presentation

Date: 2026-10-01. Status: first pass merged with Bailey’s authorisation; native acceptance open.

Bailey requested rumble, better cockpit navigation and flight detail, and cloud
transitions. Add bounded angular vibration with an off switch, eased FOV and
five seat orientation shortcuts; keep the eye at the fitted pilot station.
Derive displayed vertical speed and distance from rendered poses with the existing
floating origin. Use the existing stratus altitude for a smooth presentation-only
cloud envelope and precipitation cutoff. Preserve existing aircraft sound voices,
fixing interior filtering to select the watched registration.

Affected systems: camera, cockpit HUD, sky/rain and sound emitter selection.
Migration: none; no simulation or persisted data changes. No new external assets.
Native Unity, packaged visual/audio and performance acceptance remain open.

## Continued sound and controls

Following Bailey’s explicit merge/continue instruction, correct the non-spatial
apron/PA leak into cockpit and disable Doppler on its own aircraft voices. Add an
original deterministic filtered-noise airflow bed at a restrained speed-dependent
level; retain type-specific engine/reverse/tyre/contact sources. The airflow clip
belongs to the current interior and is destroyed at rebind/exit/teardown.
Add arrow/+−/Home navigation, eased head turns and a saved vibration preference.
One optional PlayerPrefs key defaults on; airline-save schema is unchanged.
No external asset or claim of recorded interior fidelity is introduced.

## Observer sky and fog at cloud breakout

Unify cockpit sky colour, fog and celestial occlusion using a cover-aware cloud-top
transition. Ground/deck weather remains unchanged; a clear sky has no artificial
altitude-triggered breakout. This corrects the first pass's mismatch where the
background became clear but storm fog and hidden celestial bodies persisted.
Included with #521 after GitHub access recovery. No data migration.

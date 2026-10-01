# 0228 — Cockpit immersion presentation

Date: 2026-10-01. Status: candidate branch, pending native acceptance.

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

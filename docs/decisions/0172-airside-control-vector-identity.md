# 0172 — Airside control-vector identity

Date: 28 September 2026. Author: Codex, at Bailey's request for a complete logo redesign.

## Decision

Replace the approach-runway A across Airside's title, launch sequence and Standalone icon with one
coherent v03 identity:

- an overlapping **AS** monogram, so the symbol belongs to Airside rather than to aviation in
  general;
- one Cloud route rising across the monogram to one amber destination, representing the player's
  route-planning and airline-growth loop;
- Glass Cockpit aqua `#3FD0C9`, Cloud `#EEF1EC`, amber `#FFB547` and Runway Ink `#17242A`;
- a heavier AIR/SIDE wordmark that remains readable on the dawn title art and at reduced sizes.

BRD-004 is the wordmark, BRD-005 the app icon and BRD-006 the transparent launch mark. Their SVG
sources and deterministic Pillow exporter are committed. The v01/v02 files remain only as history
and rollback assets.

## Reason

The old mark was a generic letter A wrapped around a perspective runway. It communicated
"airport" but did not create an ownable Airside identity, and its narrow details weakened at small
sizes. The AS monogram is specific to the game while the single route and destination express the
actual management fantasy without a literal aircraft, wing or tower.

## Affected systems

Presentation only: `SplashScreen`, `AirsideTheme`, Standalone Player icon settings, runtime brand
art, packaged mirrors and brand documentation. No simulation, save, camera or input behaviour
changes.

## Migration and fallback

No save migration. Missing v03 art falls back through the existing texture loader; the previous
v01/v02 assets remain committed for an immediate visual rollback.

## Evidence

See `docs/testing/brand-identity-2026-09-28/title-preview.jpg` and
`docs/art/prompts/airside-brand-v03-2026-09-28.md`. Runtime and StreamingAssets files have matching
hashes. Unity imported the six runtime/StreamingAssets v03 files without an import error; the
bounded full EditMode run ended during project import, so a completed test result and packaged
Dock/Finder appearance remain platform checks.

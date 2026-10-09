# Welcome weather isolation and small comfort fixes

Status: Accepted
Date: 2026-10-09
Task: #706

## Decision

The full-screen welcome illustration/setup/title Options are a separate interface
surface. Live world weather rendering is enabled only after entry to the airport
and while Weather Layers is on. The existing gameplay weather still drives
operations and material wetness. The opening glide reveals that live world.

Keep title shortcuts consistent with their printed hints, expose backup recovery
on the returning card, retain complete compact action cards, and use full-height
Options rows with scrolling only when required. Options Views starts at the
current camera view. Flight codes retain their existing ASCII validation.

## Reason

Live camera fog/cloud/rain/lightning has no place over the opening illustration.
Several isolated input/layout mismatches made the opening less dependable without
adding depth to airline play. These fixes preserve the game's existing systems.

## Affected systems and migration

Presentation weather, opening/setup, Options and title input only. No save schema,
settings key, operations, clock, reservation, economic or asset migration. No
external assets added. Existing player preferences remain supported.

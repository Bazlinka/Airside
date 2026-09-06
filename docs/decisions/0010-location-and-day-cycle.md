# Decision 0010: airport location and a day/night cycle

Date: 2026-09-06

## Decision

The airport now sits at a named real-world location (`AirportLocation` in the
domain layer). The prototype ships with three South Australian regional airfields
— Kingscote (Kangaroo Island, the default), Port Lincoln and Coober Pedy — each
carrying an id, name, region, UTC offset and latitude. Only the name and the
day/night cycle are used yet; the offset and latitude are recorded for the
companion app and for climate/daylight-length work later.

`DayCycle` derives the local time of day purely from the simulation clock: the
clock is treated as local time, one simulated day every 1200 seconds (20 real
minutes), starting at 08:00 on day one. It reports the hour, the day number, a
day phase (Night / Dawn / Day / Dusk) and a 0..1 daylight value. The presentation
layer drives the sun angle, colour and intensity and the ambient light from it,
and the HUD shows the location, day number, local clock and phase.

## Save schema → version 2

`AirsideSaveData.CurrentSchemaVersion` is now 2, adding `locationId`. Schema 1
saves are migrated on load (`AirsideSaveData.Migrate`): the location is set to the
default and the version is bumped. `Validate` accepts versions 1 and 2 so a v1
file loads, migrates, and is rewritten as v2. A regression test covers a hand
-written v1 save.

## Reason

"First playable airport" needs a place. Location is the smallest piece of that and
unlocks the day/night cycle, which makes a real-time game readable — you can tell
at a glance roughly what time it is and how long you have been away. Keeping the
day cycle a pure function of the sim clock keeps it deterministic and free to
reconstruct on load, consistent with every other simulation rule.

Establishing the migration pattern now, on a trivial additive change, means the
harder schema changes later have a worked example to follow.

## Consequences

- `AirportSimulation` gains `Location` and `TimeOfDay`; a new constructor takes a
  location, the old one defaults to Kingscote.
- Real-world wall-clock alignment (so the in-game time matches the player's
  timezone) is deferred to the companion-app work.
- Latitude-driven daylight length, seasons and climate are future work; the field
  is already carried.
- Next location work: a picker at new-game time, and demand/served-routes that
  vary by location.

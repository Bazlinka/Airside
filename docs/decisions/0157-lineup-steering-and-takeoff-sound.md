# 0157 — Line up without the tail sliding, and a takeoff roll that sounds like one

Date: 28 September 2026. Author: Cursor, at Bailey's request.

## Context

Turning onto the runway looked like the aircraft was drifting. The nose followed the
lineup path, but the fuselage was the chord between the nose and a main-gear point on
that same path. In the turn the rear wheels were travelling sideways, so the tail
stepped out. The drawn heading then lagged that chord, which swung the tail further.
The path itself was a quadratic through the corner, tighter in the middle than the
radius it claimed.

The takeoff then started with the engines already at full power. Taxi and the roll
did not share a note, and the acceleration was a straight ramp off a hard stop.

## Decision

- Lineup is a circular fillet, the widest arc that still leaves about a wheelbase of
  straight before the roll.
- On that leg the nose stays on the path and the main gear trails with no sideslip.
  The fuselage is not yawed onto the runway ahead of the gear — that slide was the
  drift. A long aircraft keeps rolling straight until the trailed gear is within 2°
  of the centreline, and the takeoff roll starts from that further point. Roll length
  is unchanged. Taxi and pushback keep the previous path-chord tracking, so stand
  clearance is unchanged.
- The drawn airframe follows that heading promptly on the ground, and the lineup
  no longer adds a heading wobble.
- The ground roll eases off the stop and is still accelerating at rotate. Average
  speed is unchanged, so the rotate point and the scheduled roll time stay put.
- Engine loudness and pitch follow thrust plus airspeed. Full power is no longer
  forced on the first frame of takeoff; the note rises through the roll.

## Affected systems

`LineupGeometry`, `GroundPath` / `GroundLeg`, `AdelaideGround` lineups,
`AircraftPerformanceProfile` takeoff speed, propeller and jet thrust schedules,
`EngineVoice`, fleet aircraft attitude and engine audio.

## Migration

None. Not saved. Lineup lasts longer for a long wheelbase (the extra centreline, at
taxi speed). The takeoff roll starts that many metres further down the same runway.

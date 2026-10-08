# Established arrivals retain physical presence when estimates are postponed

Date: 2026-10-09
Status: Accepted — Bailey's no-disappearing-aircraft instruction
Revises estimate-loss handling in the extended final presentation.

An already established rendered arrival must not be removed or returned to an earlier
route pose when its ETA is temporarily unavailable or moves beyond the 32 km final
window. Keep its identity and actual world pose in a continuous tangent holding orbit.
Use reference approach speed, radius at least 1.8 km (larger for fast aircraft to limit
bank to roughly 25°), and a single 5 m/s climb to at least 1.5 km. Repeated laps do not
reset height or position. A usable ETA rejoins using the existing bounded final-pose
slew. Actual clearance/state changes retain the held pose with a rate-bounded handoff;
large offsets no longer bypass the blend or get forced through six seconds.

Use Circuit visual phase and heading-derived bank/pitch for the held model. Ordinary
follow/cockpit terrain origin, mini-map and selected status/altitude use the actual
pose, rather than the deadline-derived route. Rotorcraft keep their own track.

This represents delays the simulation already owns. It never chooses a landing,
stand, curfew exception, weather release or resource reservation. There is no new
FleetState or saved schema/migration. The orbit is transient; a reload rebuilds it
from the saved operational state rather than restoring the exact phase. It is not a
complete fuel/diversion/ATC system. Native hold/rejoin and clearance timing are
unverified. Focused pure geometry/boundary checks and C# syntax are supplementary.

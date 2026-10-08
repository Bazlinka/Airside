# Parked aircraft activity window

Date: 2026-10-09
Status: Accepted — Bailey's parked-aircraft proposal and instruction to continue

Keep parked models, identity lookup, Fleet inventory, planning/timetable rows and
explicit selection/follow available. Only activity counts and automatic camera cycling
exclude idle parked aircraft until two hours before their published departure.
Use published time so a long delay cannot return an already active movement to idle.
Overdue departures waiting on weather remain active. Cancelled/unbooked stand aircraft
are inactive. Moving/airborne aircraft and maintenance taxi/position/return movements
stay active. Stationary maintenance is inactive; known outstation parked models enter
the window before their return departure. No simulation or save schema changes for
this policy. Native HUD/camera appearance remains unverified.

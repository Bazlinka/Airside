# 0186 — Checks are done in a hangar

Status: accepted (Unity look not yet verified)

While a player aircraft is in its routine check (ADR 0085) it is towed along the taxiways to a hangar that can hold it, waits
inside, and is towed back so it is on its stand when the check ends. Turboprops go to Regional Express, jets to Cobham, else
the nearest hangar that fits (outline, wing tips and length checked against the OSM footprint).

Presentation only. The simulation still holds the stand and the check timer, so no stand can be double-booked, saves are
unchanged and nothing needs versioning. `HangarTow` (Simulation, Unity-free, tested) plans the route with
`AdelaideTaxiRouter`, times it at tug speed, and gives the pose at any second of the check; `AirsidePrototype.FleetVisuals`
uses it for a parked player aircraft in check. A check too short to tow (under two tows plus 15 minutes) stays put.

Not done yet: a tug model on the tow, hangar door animation, and a real hangar-occupancy limit (two aircraft in one
hangar share the same spot). Known: the route joins the stand from the nearest taxiway node, so the tow may clip apron
edges; the heading slews over the first 18 m at the stand.

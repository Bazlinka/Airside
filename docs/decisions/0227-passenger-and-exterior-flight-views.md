# ADR 0227 — Passenger and exterior journey views

1 October 2026. Requested by Bailey.

Add fitted left/right window seats to all 13 existing passenger aircraft and an
orbiting exterior camera. Share the aircraft registration, pose ownership and
South Australia terrain window across the spectator modes. Switching a view must
not restore the Adelaide origin; Overview/Esc explicitly leaves the journey.

Cabins are original simplified five-row sections with representative economy seat
groups. They use each runtime kit's window bounds and existing aircraft surfaces;
no branded airline replica, copied photos or new external assets. Window-seat
selection reuses the cabin; combined meshes keep material draw counts bounded.
Cockpit/passenger audio uses the interior mix; exterior uses exterior audio.

Simulation, schedules, reservations and save format remain unchanged. Geographic
coverage and the previously recorded long-flight performance limits remain.

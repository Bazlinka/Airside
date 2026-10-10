# Flight crews and ground vehicle finish

Status: Accepted
Date: 2026-10-10

## Decision
Bailey requested more crew/pilots and improved ground vehicles. Extend automatic provision: two pilots plus one regional, two jet or four widebody cabin attendants. The captain walks a safe aircraft-specific perimeter at 1.3 m/s, pausing for inspection. Player Fuel/preparation cannot complete before the inspection and clearing budget; subsequent baggage/boarding times use the same job plan. Flight/cabin crews board via existing stairs or aerobridges. Jet inspection crews return to a terminal doorway; other briefing takes place inside the terminal. Regional crews join the stair route from their waiting/inspection positions.

No hiring, duty roster, airline economics or new saved fields. Other airlines receive visible crews on inferred preparation windows; their existing dispatch timing is retained. Crew figures are clock-derived and bounded, with no persistent NPC state. Cockpit occupant modelling and terminal interiors are outside this slice.

Reuse the licensed CC0 suit/ramp figures and vehicle models. Original navy/teal uniform palettes and pilot caps identify flight crews; original seated bone poses put drivers into service cabs. Vehicle body shading scales RGB while preserving full opacity; only explicit glass remains translucent. Front axle steering is separate from wheel roll. Vehicle movement now follows advancement of the same presentation simulation clock, with a 1-second catch-up step cap; stationary/frozen clock produces no driving, steering or beacon animation. Wheel roll assumes a nominal 35 cm radius. Small unshadowed amber beacon/safety-headlamp sources improve visibility on existing lamps.

## Reason
Provide identifiable people with specific jobs and readable vehicle structure. A captain inspection must not complete by rendering an animation faster, and a darkened paint colour must not become a translucent body.

## Affected systems and migration
FlightCrewWork, DeparturePrep, FlightCrew/Uniform rendering, GroundService/VehicleDetails and the existing native service review. Existing prep timestamps reconstruct the work; pending old bookings may need additional preparation. Saves, reservations, licence sources and economy contracts are retained. Older capture/test fixtures must sample the new actual work window rather than fixed old times.

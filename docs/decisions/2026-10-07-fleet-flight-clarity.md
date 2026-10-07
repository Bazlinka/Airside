# Fleet and flight clarity

Date: 2026-10-07. Status: implemented; native Unity verification pending.

The player could not efficiently compare available aircraft, switching to Melbourne left stale Adelaide selection, and the network fleet explicitly hid every 3D camera. Flight pricing mixed the cost and gross return in prose, making comparison difficult. Bailey requested implementation and direct merge, without another prototype/approval stage.

Use the existing Unity interface and real simulation data. Fleet defaults to operations with optional market and fuller details. Available excludes booked aircraft, maintenance and overdue checks across every base. Airport filters show all statuses and select a visible player aircraft; an empty base clears details. Each row keeps aircraft type and base visible. Track follows the network position on the map.

The planner opens its route comparison, sorts reachable routes by expected round-trip profit, keeps career markers, and separates pay now, expected return and profit. Estimates use the same reliability, eligible contract (including final reward), demand, difficulty and freight rules as settlement. Fees remain automatic; this does not add a fare-setting system or change balance. Costs already paid are still credited when updating an Adelaide booking.

Network bookings receive an explicit review and confirmation. Pending network services can be cancelled: a single refund, the same eligible contract cancellation penalty as Adelaide, and pause any repeat plan. A repeated cancellation refuses; a departed service refuses. Commands remain owned by simulation. No arrival, rotation or logbook credit is awarded for cancellation.

Airborne network aircraft can be watched from exterior, cockpit or supported passenger windows using existing aircraft assets and interiors. The read-only presentation follows the persisted timetable and great-circle route with a floating origin, retaining actual registration/type/livery. It does not create a live Melbourne airport, simulate new remote ground reservations or change flights. Ground phases return to Fleet; unsupported remote terrain and airport detail remain outside this scope. This amends ADR 0239's camera limitation without replacing its unified fleet or ferry design.

Affected systems: FleetBoard/Workspace, FlightPlanner/RouteMapWorkspace, AirsidePrototype fleet/map/camera integration, OutstationJourney and network cancellation. No new external assets or dependencies. No save schema change or migration: cancellation clears existing booking fields; camera/filter/review state is presentation only. No changes to live time, Adelaide runway/stand reservations, settlement identity or repeat generation while away.

Verification and native checklist: [fleet flight clarity](../testing/fleet-flight-clarity-2026-10-07/README.md).

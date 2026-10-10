# Airport-provided working ground teams

Status: Accepted
Date: 2026-10-09

## Decision
Bailey requested more ground crew doing visible jobs that also affect readiness, and delegated the provisioning choice. Provide automatic airport teams first; no hiring UI or economy change. Regional baggage teams have two carriers and one hold attendant, jets three carriers plus attendant, widebodies four plus attendant. Fuel/catering have three people, arrival/pushback three, boarding two supervisors plus a regional planeside handler. Reuse the shipped licensed characters and original hand tools.

Baggage work uses a deterministic handling allowance of 65% of typical seats (rounded up). This is an explicit game abstraction, not actual passenger/checked-bag manifest data. Partition it once between carriers. Physical bag trip lengths at the existing carrying pace, staggered starts, belt transit and 12-second setup/clear windows establish a minimum service duration. Existing base upgrades retain their faster other stages, but cannot make baggage workers move faster than this minimum. The shared pure job timing drives player readiness, vehicle/stair/boarding timelines and crew poses. Rendering does not authorise departure. Fuel and catering retain existing stage budgets with setup/clear windows reserved within them.

Other airlines receive larger visible teams on their existing ambient service windows; their scheduling remains unchanged. Crew walking is stand-local equipment staging, not airport-wide navigation. Pushback wing walkers attend both wingtips before release; following a moving tug is outside this slice.

## Reason
A longer arbitrary timer should not create more luggage. Work determines its budget, and the player aircraft waits for that budget to complete. Automatic provision adds active airport life without prematurely adding staffing management.

## Affected systems
DeparturePrep, RampCrew, TurnaroundCrewWork, ServiceChoreography, existing Boarding/HandTools rendering and native ServiceWorkReview.

## Migration impact
No persisted fields or schema change. Work is reconstructed from existing aircraft type, base level, prep start and simulation clock. Pending old bookings may need additional baggage time; completed/in-flight journeys retain their normal states. Cancelled jobs produce no active player service team. Reservations and random/economy rules are unchanged.

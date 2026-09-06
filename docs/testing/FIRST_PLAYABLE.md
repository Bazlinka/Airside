# First playable acceptance check

## Current automated evidence

- The complete aircraft cycle produces the same result with one large update or one-second updates.
- Fifty consecutive cycles finish without a runway, taxiway or stand reservation conflict.
- Seeded stand selection repeats across runs.
- Reservation acquisition is atomic when a resource is already occupied.
- Backward simulation time is rejected.
- Turnaround tasks respect their dependencies and report the cause of a delay.
- Priority crew can only be bought once per turnaround and changes both time and cash.
- Flight revenue and delay costs reconcile in the airport economy.
- Reopening after elapsed real time matches an uninterrupted deterministic run.
- A damaged latest save recovers from its previous complete snapshot.
- Backward clock changes cannot reverse the airport, and catch-up is capped at thirty days.

## Remaining observed-play checks

- Confirm orbit, zoom, pan, follow and overview controls feel comfortable.
- Measure frame pacing on the target Mac during a fifty-cycle soak.
- Confirm pause and 1×/4× controls remain clear without instructions.
- Confirm active fuel, baggage and passenger vehicles make turnaround progress readable.
- Confirm the priority-crew choice and its $300 cost are understandable at a glance.
- Add a visual diagnostic for any future deadlock.
- Confirm the welcome-back report remains readable for short and long absences.

The greybox build passes when:

- one aircraft completes landing, taxi-in, turnaround, pushback, taxi-out and departure fifty times without manual intervention;
- no aircraft enters an occupied runway segment or stand;
- pausing stops simulation time without corrupting the active cycle;
- changing frame rate does not change state transition times;
- the same seed produces the same event sequence;
- the current aircraft state is understandable without a debug window;
- aircraft and camera motion are smooth on the development Mac.

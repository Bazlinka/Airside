# First playable acceptance check

## Current automated evidence

- The complete aircraft cycle produces the same result with one large update or one-second updates.
- Fifty consecutive cycles finish without a runway, taxiway or stand reservation conflict.
- Seeded stand selection repeats across runs.
- Reservation acquisition is atomic when a resource is already occupied.
- Backward simulation time is rejected.

## Remaining observed-play checks

- Confirm orbit, zoom, pan, follow and overview controls feel comfortable.
- Measure frame pacing on the target Mac during a fifty-cycle soak.
- Confirm pause and 1×/4× controls remain clear without instructions.
- Add a visual diagnostic for any future deadlock.

The greybox build passes when:

- one aircraft completes landing, taxi-in, turnaround, pushback, taxi-out and departure fifty times without manual intervention;
- no aircraft enters an occupied runway segment or stand;
- pausing stops simulation time without corrupting the active cycle;
- changing frame rate does not change state transition times;
- the same seed produces the same event sequence;
- the current aircraft state is understandable without a debug window;
- aircraft and camera motion are smooth on the development Mac.

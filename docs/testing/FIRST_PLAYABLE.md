# First playable acceptance check

The greybox build passes when:

- one aircraft completes landing, taxi-in, turnaround, pushback, taxi-out and departure fifty times without manual intervention;
- no aircraft enters an occupied runway segment or stand;
- pausing stops simulation time without corrupting the active cycle;
- changing frame rate does not change state transition times;
- the same seed produces the same event sequence;
- the current aircraft state is understandable without a debug window;
- aircraft and camera motion are smooth on the development Mac.

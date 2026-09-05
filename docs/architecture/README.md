# Architecture decisions

Airside separates simulation truth from Unity presentation.

- `Airside.Domain` contains identifiers, values and state definitions.
- `Airside.Simulation` contains deterministic rules and state transitions.
- `Airside.Presentation` will translate simulation state into Unity objects and animation.
- `Airside.Persistence` will own versioned snapshots, the event journal and migrations.
- CloudKit will exchange compact projections and idempotent commands after the local simulation and save format are stable.

The Mac game is the authoritative simulation writer for the first companion release. The phone reads projected status and writes commands; it does not rewrite the full airport snapshot.

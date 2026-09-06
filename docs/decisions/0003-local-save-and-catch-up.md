# Decision 0003: replayable local saves and offline catch-up

Date: 2026-09-06

## Decision

The first save schema stores the simulation time, wall-clock save time, original random seed, revision and an append-only journal of player commands. Loading recreates the airport from the seed, reapplies commands in stable order and then advances the same simulation rules by the elapsed real time.

The game autosaves every fifteen simulation seconds and when it is paused, backgrounded or closed. Each write is flushed to a temporary file and replaces the main snapshot while retaining the previous complete snapshot. If the latest file is missing, malformed or invalid, loading tries the previous copy.

Offline advancement is capped at thirty days in one load. A backward device-clock change adds no elapsed time and is explained to the player. The welcome-back report summarizes time away, completed flights, net cash and delay costs.

## Reason

Replaying the compact current prototype gives live and offline play exactly the same operational result while establishing the version, recovery and command rules needed by later snapshot migration and CloudKit work. It also makes player decisions part of persistent truth instead of saving only their financial outcome.

## Consequences

- Commands require stable identifiers, simulation timestamps and idempotent application.
- Changes to deterministic rules need an explicit save migration before release.
- The current replay implementation is intentionally simple; larger airports will add periodic materialized snapshots and discrete-event catch-up without changing the schema guarantees.
- CloudKit will exchange projections and commands after this local contract is proven, rather than becoming the authoritative simulator.

# Airside

Airside is a real-time, persistent airport management game for macOS with a focused iPhone companion app.

## Current target

The first playable build proves one complete aircraft cycle: land, taxi, occupy a stand, complete a timed turnaround, push back, taxi out and depart. The loop must remain deterministic, readable and stable for fifty consecutive cycles.

## Repository map

- `game/Airside/` — Unity 6.3 LTS macOS game
- `companion/AirsideCompanion/` — SwiftUI companion app, introduced after the save and sync contract is stable
- `docs/product/` — product plan and agreed scope
- `docs/architecture/` — technical decisions and data contracts
- `docs/testing/` — acceptance checks and test fixtures
- `tools/` — repeatable project utilities

## First implementation slice

1. Open `game/Airside` in Unity 6.3 LTS.
2. Build the greybox runway, taxi path and two stands.
3. Connect the domain clock and aircraft state machine to a scene presenter.
4. Add deterministic transition tests before expanding the simulation.

The full product plan is in `docs/product/Airside Project Plan.docx`.

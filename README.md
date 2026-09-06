# Airside

Airside is a real-time, persistent airport management game for macOS with a focused iPhone companion app.

## Current target

The first playable build proves one complete aircraft cycle: land, taxi, occupy a stand, coordinate visible ground services, push back, taxi out and depart. The loop remains deterministic, readable and stable for fifty consecutive cycles while delays affect the airport's cash balance.

## Repository map

- `game/Airside/` — Unity 6.3 LTS macOS game
- `companion/AirsideCompanion/` — SwiftUI companion app, introduced after the save and sync contract is stable
- `docs/product/` — product plan and agreed scope
- `docs/architecture/` — technical decisions and data contracts
- `docs/testing/` — acceptance checks and test fixtures
- `docs/art/` — approved art direction, asset manifest, prompts and visual references
- `scripts/` — repeatable project checks and local builds

## First implementation slice

1. Open `game/Airside` in Unity 6.3 LTS.
2. Press Play to run the repeating aircraft movement prototype.
3. Run `./scripts/test-unity.sh` for deterministic simulation checks.
4. Run `./scripts/build-mac.sh` for a local macOS application build.

Both scripts run under `bash` or `zsh`. They expect Unity 6.3 LTS at the default
Hub path; set `AIRSIDE_UNITY` to point elsewhere.

See `GAME.md` for the current milestone, controls, evidence and next work.
See `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` before creating or integrating any visual asset.

The full product plan is in `docs/product/Airside Project Plan.docx`.

# Airside Delivery Plan

## First playable execution plan

Version 2.0
7 September 2026

## Purpose

Airside is a real-time airport management game for macOS. The player runs a small regional airport while aircraft and ground services operate automatically. The immediate goal is a coherent, enjoyable first playable that a new player can launch, understand and play without developer help.

The project already has more systems than the first playable needs. Development now concentrates on joining those systems into one reliable player experience. New platforms, major simulation layers and additional asset batches wait until external players confirm that the current airport loop is understandable and fun.

## Delivery decision

The Mac first playable is the only active product target. One implementation branch owns the next player-visible outcome. Planning, documentation and asset work support that branch and do not become parallel milestones.

The next development step is:

1. Merge and verify PR 35, which restores Unity compilation and has local evidence for a successful macOS build and 107 passing EditMode tests.
2. Launch that exact build and complete a focused visual test at 1280 by 720, 1440 by 900 and the development Mac's Retina resolution.
3. Fix only failures that stop a new player from completing or understanding the first session.
4. Build the first-session flow described below and give it to an external player.

PR 34, cloud tooling and further art production are useful but are not on the critical path to the first playable.

## What is already built

The current game includes the core aircraft cycle, deterministic time, persistence and offline catch-up, route offers, cash, reputation, staffing, weather, daily reports, research, a third stand, insolvency, concurrent commercial flights, ground traffic, a scalable HUD, approved surfaces, UI art and integrated model kits with primitive fallbacks.

These systems are sufficient for the first playable. The current work is integration, onboarding, tuning and verification.

## First playable player journey

A new player starts at Kingscote with a small operating airport, a clear cash position and one immediate route opportunity. Within the first two minutes, the player sees an aircraft movement and understands the airport's current state. Within ten minutes, the player makes a useful decision about a route, staffing, priority turnaround or research. The consequences appear in the world and in the daily financial result.

The session should have a simple arc:

- Start or continue the airport.
- Read a short status summary.
- Watch an aircraft arrive, taxi and turn around.
- Make one operational or investment decision.
- See the effect on time, service, cash or reputation.
- Reach a daily report or another clear checkpoint.
- Quit and relaunch without losing or corrupting progress.

The player should never need to read a design document, debug log or raw event stream to know what to do.

## Immediate implementation slice

### Outcome

Turn the current prototype into a self-explanatory 15 to 30 minute first session.

### Work in scope

- A clean new-game path that does not depend on an old developer save.
- A brief opening explanation of the player's role and the first useful decision.
- Clear priority between status, alerts, offers and optional detail in the HUD.
- Tuning so a first-session decision produces a visible result within the session.
- Reliable save, quit, relaunch and away-summary behaviour.
- Visual correction of model placement, scale, lighting, panel layout and unreadable text.
- A packaged macOS build suitable for a small external playtest.

### Acceptance criteria

- The game compiles and builds in Unity 6000.3.23f1.
- All EditMode tests pass with no failed or skipped tests.
- A new player can start without an existing save.
- The first aircraft cycle is visible and understandable.
- The player can identify the next useful action within two minutes.
- At least one decision has a visible operational or financial consequence within 15 minutes.
- HUD panels do not overlap or clip at the three target display sizes.
- Aircraft and ground vehicles do not visibly share reserved space or slide through released space.
- Saving, quitting and relaunching preserve state and produce a readable away summary.
- A 30 minute soak completes without a crash, deadlock or unexplained stop.
- One external player completes the session without coaching and can explain what happened.

## Build sequence

### Step 1 Restore a trustworthy build

Merge PR 35 after reviewing its narrow compiler repair. Pull main, run the Unity tests, create the macOS build and launch the resulting app. Record the commit, Unity version, test count and screenshots from the exact build.

### Step 2 Verify the integrated presentation

Test the current models, surfaces, lights, weather effects and HUD together. Fix showstoppers such as missing assets, unreadable panels, overlaps, severe scale errors, broken cameras and misleading aircraft positions. Cosmetic polish that does not affect understanding moves to the backlog.

### Step 3 Make the first session playable

Add or tighten the new-game entry, opening guidance and decision pacing. Reuse the systems already present. Do not add another economy, research, passenger, construction or airline subsystem during this step.

### Step 4 External playtest and tune

Give the packaged build to one or two people who have not read the plan. Observe where they become confused or bored. Fix the three most serious problems, rebuild and repeat once. Evidence from players decides the next system, not the size of the long-term backlog.

## Working rules for speed

- Keep one active player-visible implementation slice.
- Prefer a working greybox or existing fallback over waiting for a perfect asset.
- Use the current approved art direction; do not run another approval round for work that clearly follows it.
- Stop creating new reference art until the integrated assets have been played and assessed.
- Write documentation only when it preserves a decision, explains a handoff or supports verification.
- Keep pull requests small enough to review quickly, but do not split one working outcome into artificial micro-PRs.
- Run focused tests while iterating and the full Unity suite before merging.
- Treat a build as complete only after the exact packaged app launches and is played.
- Put non-blocking defects in the backlog and continue toward the playable.

## Core rules that remain mandatory

Speed does not justify corrupt saves or inconsistent simulation. These rules protect the game and remain in force:

- Simulation time comes from an injected clock.
- Random choices come from a seeded source.
- Runways, taxiways and stands are reserved before use.
- Frame rate does not change simulation outcomes.
- State-changing commands are safe to apply exactly once.
- Persisted schema changes include a version and migration path.
- Offline catch-up produces results compatible with live play.
- External assets and data have recorded source, licence and fallback.
- Simulation state drives presentation; animation and effects do not decide operational outcomes.

## First playable scope

The first playable includes one regional airport, one runway network, two or three stands, commercial aircraft and supporting ground traffic, route decisions, staffing, two research choices, weather, reputation, daily finances, saving, offline catch-up and failure through insolvency.

The current integrated art may ship with procedural fallbacks where an asset fails. A consistent, readable airport is more valuable than complete asset replacement.

## Deferred until the first playable is proven

The following work remains part of the long-term direction but is not active now:

- iPhone companion and CloudKit synchronisation
- detailed individual passenger simulation
- modular terminal interiors and baggage networks
- cargo and general aviation operations
- multiple airports or save slots
- live weather, live schedules or other external services
- real airline names and liveries
- multiplayer and social features
- advanced construction staging and land acquisition
- maintenance, regulation and certification depth
- financing, monetisation and release operations
- additional broad art, animation and VFX batches

Any deferred item returns only when the first playable exposes a clear need for it or external testing proves the central loop works.

## Long-term direction

After the first playable succeeds, Airside can grow from a tiny regional airfield through domestic and international stages. The player will continue acting as the airport operator rather than directly controlling aircraft or vehicles. Expansion should add new operational pressure, visible consequences and meaningful choices without weakening save integrity or readability.

The long-term product remains a premium stylised miniature airport that continues operating while the player is away. The first playable is the evidence that this larger game is worth building.

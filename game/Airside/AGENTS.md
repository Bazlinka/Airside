# Airside game code — what to know before editing C#

Applies to everything under `game/Airside/`. The repo-wide contract is `../../AGENTS.md`; this adds what is specific to the Unity project.
(This file lives outside `Assets/` on purpose: Unity wants a `.meta` file for anything inside `Assets/`.)

## Layers (do not break)

| Folder (`Assets/Airside/…`) | Owns | May use `UnityEngine`? |
|---|---|---|
| `Domain/` | Pure rules: time, ids, destinations, tiers | No |
| `Simulation/` | The airport: deterministic, clock-injected, seeded randomness; reservations; saves | No |
| `Presentation/` | MonoBehaviours, camera, visuals, UI, audio | Yes — it only *represents* simulation state, never decides reservations, task completion or persistence |
| `Editor/` | Editor-only tools and review helpers | Yes |
| `Tests/EditMode/` | Deterministic NUnit tests | Whatever the code under test uses |
| `Art/` | Approved runtime models, textures, UI, animation, VFX | — |

`AirsidePrototype` (the main MonoBehaviour) is split across 39 partial files: `../../docs/architecture/PRESENTATION_MAP.md` says what each owns.
Most of its state lives in `AirsidePrototype.cs` and `AirsidePrototype.Airline.cs`, which makes them the files tools collide on most — declare your file scope
(`../../docs/ai/WORKFLOW.md`) and keep edits there small.

## Unity pitfalls learned the hard way

- **Every file under `Assets/` needs a committed `.meta`** with a unique GUID (`scripts/audit-unity-assets.py` checks). Runtime art is mirrored
  byte-for-byte into `StreamingAssets` (`scripts/sync-art-streaming-assets.sh`); the audit checks that too.
- **`JsonUtility` cannot write null.** An absent nested save object round-trips as an all-default object, so restore must treat a blank
  record as "none" (this made every v22 save fail to load). The headless tests never exercise `JsonUtility` — a new nested save field needs this
  handled and a test that runs in Unity. Any persisted schema change needs an explicit version and a migration.
- **Unity's NUnit is 3.5 and has no implicit usings.** `Is.AnyOf`, newer NUnit APIs and an unwritten `using System;` break Unity's compile while the
  .NET harness would accept them; `scripts/check-unity-nunit.sh` (CI) now catches both.
- **The headless harness skips every test that touches `UnityEngine`** (and Presentation files that need it). After adding a Presentation file or a test, run
  `python3 scripts/update-harness.py` (CI fails if the list is stale). A green headless run says nothing about those tests.
- **The project renders in Linear colour space**: colour textures and Color material properties use sRGB authoring; normals/masks stay linear. Mesh vertex palettes are already linear: do not convert them twice.
- Time comes from the injected clock, random choices from a seeded source; frame rate must never change simulation outcomes.
- Presentation-only systems must keep a safe fallback (no network, missing file, missing asset) — see how the land-cover and DEM loaders return
  `null`/`false` instead of throwing.

## Checks

Follow the repo-wide **Testing and merge policy** in `../../AGENTS.md`. These are
available tools, not a mandatory checklist:

- `scripts/test-quick.py --changed`: optional focused checks when useful.
- `scripts/test-domain.sh`: full headless suite, only when Bailey requests it.
- `scripts/test-unity.sh`: full native suite, only when Bailey requests it.
- `scripts/audit-unity-assets.py`: quick metadata/mirror check when assets change.

Use quick syntax/compile checks when readily available. Report unverified Unity
behaviour; do not start lengthy editor/build/playtest runs as a default merge gate.

New code goes in the matching folder; if nothing fits, add the folder and note it in `../../AGENTS.md`.

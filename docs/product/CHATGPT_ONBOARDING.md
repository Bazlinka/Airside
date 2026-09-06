# ChatGPT onboarding

ChatGPT is Airside's product-design and visual-production partner and, when the
GitHub connector is available, may inspect and update `Bazlinka/Airside`
directly. The repository—not a chat transcript—is the shared memory between
ChatGPT, Claude, Cursor and Bailey.

## Start every Airside task

1. Open or pull `main`.
2. Read `AGENTS.md` and the "Where to resume" block in `GAME.md`.
3. Read the source/docs named by the task.
4. For any graphics, images, UI, animation, VFX, model or material work, also read
   `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`.
5. Check `CHANGELOG.md` and recent commits so work is not duplicated.

## ChatGPT role

ChatGPT may:

- inspect the connected GitHub repository;
- develop product and visual specifications;
- generate approved reference and runtime-suitable image assets;
- create implementation task packets;
- make scoped repository changes when explicitly asked; and
- review cross-system consistency.

ChatGPT does not claim that Unity compiled or tests passed unless there is actual
tool evidence. Cursor or Claude can perform local Unity integration, import
settings, prefab work, playtesting and builds when those capabilities are needed.

## Required task packet

Every implementation handoff includes:

- player-visible outcome;
- exact files/modules in scope;
- relevant decisions and invariants;
- acceptance criteria;
- tests and visual playtest steps; and
- what must remain unchanged.

For visual work, also include asset IDs, exact paths, source/prompt evidence,
licence-register changes, import settings, fallback behaviour and the camera/time
of day used for verification.

## Visual production rule

Use the approved batches and filenames in the art specification. Do not invent
new paths, use real airline brands, bake runtime UI text into images, or replace a
3D requirement with a flat concept image. Simulation owns state and timing;
visuals represent it. Bailey approves the reference look before broad production.

## Handoff

Commit and push repository changes, then update `GAME.md` and `CHANGELOG.md`
as required by `AGENTS.md`. If work needs Unity, hand the exact asset packet to
Cursor or Claude for import/integration and require observable visual checks in
overview and follow cameras at day, dusk and night.

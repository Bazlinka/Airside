# Freight contracts and interface refresh — task packet

Approved by Bailey, 6 October 2026. Merged in PR #526 at `19fd9688` under
his explicit merge approval. Native Unity validation remains outstanding.

## Player-visible outcome

Freight contracts require a matching refitted freighter. Aircraft viewing uses a
clear, consistent set of camera controls. The interface shares quieter slate,
warm-white and sea-glass styling. Returning players see a structured summary of
flights, net funds, reliability and fleet status. Button ticks are softer and
quieter throughout the game.

## Scope and invariants

Domain contract role matching; simulation acceptance, deadlines, completion and
cancellation; contract HUD eligibility. Shared palette/theme/painter, navigation,
selected-aircraft viewing actions, flight-view HUD and return briefing. Generated
UI click waveform/playback only. Shared offline preview exporter/renderer.

Preserve seeded scheduling, reservations, exactly-once settlement, existing
save fields/version and watched-aircraft identity/camera bindings. No world art,
external audio samples, new economy or broad aircraft production. Native Unity
compile/EditMode checks remain the merge requirement in `AGENTS.md`.

## Acceptance and checks

- A passenger aircraft cannot accept or earn progress on freight work. A matching
  freighter can. Contract progress and refit role survive save/load.
- Deadline capacity, cancellation penalties, eligible-aircraft lists and offer
  locks use the same aircraft-role requirement.
- View switching, recenter, overview and vibration controls remain available and
  use shared HUD rendering. No overlapping controls at supported viewport sizes.
- Return briefing keeps metrics and return action visible while a large fleet
  scrolls separately. Report actual net change, including losses.
- Click audio has a smooth attack, bounded peak and faded tail; repeated
  acknowledgements within 75 ms cannot stack.
- Run focused checks, the complete headless suite, generated-harness check,
  whitespace checks, asset audit and C# syntax check. Export shared-layout
  previews at 1440×900 and 1280×720. Native compilation and runtime appearance/
  listening are recorded separately from offline evidence.

## Remaining freight backlog

AI freight carriers, cargo stands and outstation freight operations remain later
slices. The outstation model currently has no freighter role; its passenger
services cannot count towards a freight contract. Do not present the whole
freight milestone as complete.

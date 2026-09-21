# 0088 — Adelaide base capability roadmap

Date: 21 September 2026. Bailey asked to push beyond aircraft content into the map,
navigation/HUD, campaign/career, buildings and environment, and then said to do as much
as useful.

## Decision

The existing operating tiers also describe the player's Adelaide **base capability**.
For now this is a derived roadmap, not a second upgrade economy:

- **Provisional — Regional starter base:** regional apron, one-aircraft operation.
- **Regional — Expanded regional base:** multi-aircraft regional growth and Dash 8-class operation.
- **Domestic — Jet-gate operation:** terminal-gate jet fleet and domestic network.
- **International — International base:** widebody fleet and long-haul handling.

Career UI names the current base capability and states what the next operating tier unlocks.
The Route Map may use the same career truth to explain why destinations matter.

## Why derived first

The simulation already gates aircraft and routes through tier, rotations, reliability,
range and stand/gate compatibility. Adding a separate persistent facility currency now would
duplicate those rules and create fake depth before physical base expansion is proven fun.

This gives the player a coherent growth story immediately and creates one vocabulary for
later physical hangar, stand, terminal, handling and cargo upgrades.

## Persistence and simulation

No save-schema change. No new command, money sink, timer or reservation rule. The capability
description is recomputed from the existing career tier and therefore cannot drift across
reload or away catch-up.

## Next

When physical facility upgrades are introduced, they should extend this roadmap rather than
create a parallel level system. A facility must unlock or improve a real operating capability,
be visible in the airport world, and preserve stand/runway reservation invariants.

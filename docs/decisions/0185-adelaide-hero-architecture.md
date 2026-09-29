# 0185 — Adelaide hero architecture stays presentation-only

Date: 30 September 2026
Status: Accepted

## Decision

Strengthen Adelaide Airport's identity with deterministic presentation geometry derived from the existing surveyed footprints and gate sites: shaped hangar roofs, a more legible Terminal 1 roof/facade rhythm, and a detailed modular aerobridge presentation. Keep the existing OSM footprints, gate positions, movement timelines and procedural shells as the authoritative layout and fallback.

## Reason

The current airport is geographically strong but several major forms still read as flat or generic at play distance. Replacing the coordinate system or introducing independent static building models would risk visible misalignment with gates, roads and routing. Deriving the hero forms from the existing data produces the missing silhouettes without changing gameplay.

## Affected systems

- Adelaide building and terminal presentation
- Aerobridge presentation
- EditMode presentation geometry tests
- Art/data register and packaged visual review

## Migration impact

None. The change adds no saved data, simulation state, route, reservation or command changes. Removing the new detail leaves the existing shells and bridge movement contract intact.

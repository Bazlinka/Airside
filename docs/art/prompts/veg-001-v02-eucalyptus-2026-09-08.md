# VEG-001 v02 — eucalyptus densify (2026-09-08)

## Goal

Presentation-only upgrade so KI eucalyptus belts read as denser multi-lobe
crowns with tapered trunks at overview, not thin cylinders / single ellipsoids.
Simulation / economy / companion unchanged.

## Outputs

| Asset | Path | Notes |
|---|---|---|
| VEG-001 v02 kit | `Models/Environment/mdl_eucalyptus_kit_v02.gltf` (+ `.bin` / `.fbx`) | 30 named meshes (tree_a|b|c × 10); multi-lobe canopies, bark, fork, denser lod1 |
| Resources prefab | `Resources/Airside/Prefabs/mdl_eucalyptus_kit_v02.prefab` | Pipeline-proof; Mac bake overwrites from FBX |

## PreferArtKit order

`mdl_eucalyptus_kit_v02` → `v01` in `TryPlaceTreeFromKit`.

## Placement densify

- Place the full tree belt (all array slots) with authored silhouettes — no longer
  thin to 41 when the kit is present.
- Far trees (`|x|>55` or `|z|>50`) also place `{prefix}_lod1` as extra crown mass.

Extract names match v01 contract.

## Generators

- `scripts/generate-veg-001-v02.py`
- `scripts/generate-veg-001-v02-prefab.py`

## Evidence

- Evidence: `scripts/test-domain.sh` pending at commit time
- Mac Unity overview vs REF pending (no Mac editor in cloud agent)

## Unchanged

Domain, Simulation, Persistence, save schema, reservations, Companion. VEG-002 scrub remains v01.

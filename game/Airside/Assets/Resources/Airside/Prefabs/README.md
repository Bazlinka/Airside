# Airside Prefabs — Unity-import / Addressables drop target

Decision **0025** item 1–2: production presentation prefers Unity-imported
prefabs (then Addressables) over the interim StreamingAssets glTF parser.

## How runtime resolves art

`ArtPresentationLoader` looks up:

1. Addressables key `airside-prefab/<key>` — runtime locator
   (`AirsidePrefabAddressables` + `AirsideResourcesProvider`) exposes every
   prefab in this folder until Bailey builds Editor Addressables groups
2. `Resources.Load("Airside/Prefabs/<key>")` — direct fallback
3. StreamingAssets / Editor glTF via `ArtGltfLoader`
4. Caller procedural cuboid fallback

`<key>` is the kit basename, e.g. `mdl_terminal_regional_small_v03` or
`mdl_passenger_stairs_v01`.

## Prefabs on disk

| Key | Regenerator |
|---|---|
| `mdl_passenger_stairs_v01` | `scripts/generate-passenger-stairs-prefab.py` |
| `mdl_wheel_chocks_v01` | `scripts/generate-gse-prefabs.py` |
| `mdl_gpu_cart_v01` | `scripts/generate-gse-prefabs.py` |
| `mdl_pushback_tug_v01` | `scripts/generate-apron-gse-prefabs.py` |
| `mdl_safety_cone_v01` | `scripts/generate-apron-gse-prefabs.py` |
| `mdl_work_barrier_v01` | `scripts/generate-apron-gse-prefabs.py` |
| `mdl_airside_sign_v01` | `scripts/generate-apron-sign-dolly-prefabs.py` |
| `mdl_baggage_dolly_v01` | `scripts/generate-apron-sign-dolly-prefabs.py` |
| `mdl_windsock_pole_v01` | `scripts/generate-apron-sign-dolly-prefabs.py` |
| `mdl_fuel_farm_v01` | `scripts/generate-fuel-farm-prefab.py` |
| `mdl_parked_ga_v01` | `scripts/generate-parked-ga-prefab.py` |
| `mdl_hangar_bay_props_v01` | `scripts/generate-hangar-bay-props-prefab.py` |
| `mdl_arff_truck_v01` | `scripts/generate-arff-truck-prefab.py` |
| `mdl_parked_car_v01` | `scripts/generate-parked-car-prefab.py` |
| `mdl_luggage_trolley_v01` | `scripts/generate-landside-furniture-prefabs.py` |
| `mdl_landside_bench_v01` | `scripts/generate-landside-furniture-prefabs.py` |
| `mdl_coast_boat_v01` | `scripts/generate-landside-furniture-prefabs.py` |
| `mdl_fire_hydrant_v01` | `scripts/generate-apron-safety-prefabs.py` |
| `mdl_extinguisher_cabinet_v01` | `scripts/generate-apron-safety-prefabs.py` |
| `mdl_fod_bin_v01` | `scripts/generate-apron-safety-prefabs.py` |
| `mdl_arff_shed_v01` | `scripts/generate-arff-shed-prefab.py` |

All use built-in cube/cylinder meshes + `AirsideRuntimeMaterialBinder`. Bailey may
overwrite any with an authored FBX prefab of the same name.

## Authored FBX kits (turboprop + terminal)

Source FBX (Unity ModelImporter):

- `Assets/Airside/Art/Models/Aircraft/mdl_regional_turboprop_01_authored_v01.fbx`
- `Assets/Airside/Art/Models/Buildings/mdl_terminal_regional_small_authored_v01.fbx`

Companion StreamingAssets glTF is preferred at runtime until Resources prefabs
exist. On Mac, after FBX import: **Airside → Art → Bake Authored FBX Prefabs**
writes `mdl_*_authored_v01.prefab` here so `airside-prefab/<key>` serves
Unity-imported meshes.

## Workflow (Mac Unity)

1. Import authored FBX/glTF into `Assets/Airside/Art/` (or a Models import folder).
2. For AIR-001 / BLD-001 authored kits: run **Airside → Art → Bake Authored FBX Prefabs**.
3. Assign URP Lit materials from the material library / authored maps (or keep
   `AirsideRuntimeMaterialBinder` until materials are ready).
4. Optionally register the same asset in Addressables with key
   `airside-prefab/<key>` — the runtime locator already exposes Resources keys via
   `AirsideResourcesProvider`; Editor groups can replace that bridge when Bailey
   builds them. Verify with **Airside → Art → Verify Prefab Addressables Keys**.

Until a prefab is present for a kit, the existing glTF kits keep working
(including `mdl_regional_turboprop_01_authored_v01` ahead of lofted/v04/…).

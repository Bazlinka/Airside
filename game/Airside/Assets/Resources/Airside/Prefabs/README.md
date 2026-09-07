# Airside Prefabs — Unity-import / Addressables drop target

Decision **0025** item 1–2: production presentation prefers Unity-imported
prefabs (then Addressables) over the interim StreamingAssets glTF parser.

## How runtime resolves art

`ArtPresentationLoader` looks up:

1. `Resources.Load("Airside/Prefabs/<key>")` — place prefabs here
2. Addressables key `airside-prefab/<key>` (when a catalog locates it)
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

All use built-in cube/cylinder meshes + `AirsideRuntimeMaterialBinder`. Bailey may
overwrite any with an authored FBX prefab of the same name.

## Workflow (Mac Unity)

1. Import authored FBX/glTF into `Assets/Airside/Art/` (or a Models import folder).
2. Assign URP Lit materials from the material library / authored maps (or keep
   `AirsideRuntimeMaterialBinder` until materials are ready).
3. Create a prefab named exactly `<key>.prefab`.
4. Copy or move it under `Assets/Resources/Airside/Prefabs/`.
5. Optionally register the same asset in Addressables with key
   `airside-prefab/<key>` — the loader already probes that key.

Until a prefab is present for a kit, the existing v03/v02/v01 glTF kits keep working.

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

## First prefab on disk

| Key | File | Notes |
|---|---|---|
| `mdl_passenger_stairs_v01` | `mdl_passenger_stairs_v01.prefab` | Built-in cube hierarchy + `AirsideRuntimeMaterialBinder`; replaces procedural stairs when present. Regenerate via `scripts/generate-passenger-stairs-prefab.py`. Bailey may overwrite with an authored FBX prefab of the same name. |

## Workflow (Mac Unity)

1. Import authored FBX/glTF into `Assets/Airside/Art/` (or a Models import folder).
2. Assign URP Lit materials from the material library / authored maps (or keep
   `AirsideRuntimeMaterialBinder` until materials are ready).
3. Create a prefab named exactly `<key>.prefab`.
4. Copy or move it under `Assets/Resources/Airside/Prefabs/`.
5. Optionally register the same asset in Addressables with key
   `airside-prefab/<key>` — the loader already probes that key.

Until a prefab is present for a kit, the existing v03/v02/v01 glTF kits keep working.

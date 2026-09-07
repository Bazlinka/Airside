# Airside Prefabs — Unity-import / Addressables drop target

Decision **0025** item 1–2: production presentation prefers Unity-imported
prefabs (then Addressables) over the interim StreamingAssets glTF parser.

## How runtime resolves art

`ArtPresentationLoader` looks up:

1. `Resources.Load("Airside/Prefabs/<key>")` — place prefabs here
2. StreamingAssets / Editor glTF via `ArtGltfLoader`
3. Caller procedural cuboid fallback

`<key>` is the kit basename, e.g. `mdl_terminal_regional_small_v02`.

## Workflow (Mac Unity)

1. Import authored FBX/glTF into `Assets/Airside/Art/` (or a Models import folder).
2. Assign URP Lit materials from the material library / authored maps.
3. Create a prefab named exactly `<key>.prefab`.
4. Copy or move it under `Assets/Resources/Airside/Prefabs/`.
5. Optionally register the same asset in Addressables (package already listed in
   `Packages/manifest.json`) and swap the loader to `Addressables.LoadAssetAsync`
   once groups exist — keep this Resources bridge until then.

Until a prefab is present, the existing v02/v01 glTF kits keep working.

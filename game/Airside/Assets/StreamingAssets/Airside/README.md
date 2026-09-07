# StreamingAssets — Airside runtime art

Unity copies this folder into packaged players. The interim glTF/PNG loaders
(`ArtRuntimePaths`, `ArtGltfLoader`, `AirsideTheme`) read from here first.

**Do not edit files here by hand.** Author under `Assets/Airside/Art/`, then run:

```bash
scripts/sync-art-streaming-assets.sh
```

Long-term production path is Unity-imported prefabs / Addressables (decision 0025 /
ADR 0026). Drop prefabs into `Assets/Resources/Airside/Prefabs/` named after the
glTF basename; `ArtPresentationLoader` prefers those before StreamingAssets kits.

#!/usr/bin/env bash
set -euo pipefail

# One-shot Editor bake of the Kingscote airfield terrain.
#
# The packaged player must not build a 257x257 heightmap or a 256x256x4 splatmap
# during startup, so the bake happens here and the result is committed:
#
#   Assets/Airside/Art/Terrain/terrain_kingscote_first_playable_v01.asset
#   Assets/Airside/Art/Terrain/trn_ground_{drygrass,greengrass,worndirt,coastsand}_v01.terrainlayer
#   Assets/Airside/Art/Terrain/mat_kingscote_terrain_v01.mat
#   Assets/Resources/Airside/Prefabs/mdl_kingscote_terrain_v01.prefab
#
# Re-run this after changing AirsideTerrainField or regenerating the CC0 maps with
# scripts/generate-cc0-terrain-ground.py. Until it has run, the runtime falls back
# to the procedural "Airfield terrain base" slab, so the scene still renders.
#
# Needs a Mac Unity 6.3 LTS editor, same as scripts/test-unity.sh.

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
unity="${AIRSIDE_UNITY:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}"
log="$root/work/terrain-bake.log"

if [ ! -x "$unity" ]; then
  echo "Unity not found at: $unity" >&2
  echo "Set AIRSIDE_UNITY to your Unity 6.3 LTS executable and retry." >&2
  exit 1
fi

mkdir -p "$root/work"
"$unity" -batchmode -nographics \
  -projectPath "$root/game/Airside" \
  -executeMethod Airside.Editor.AirsideTerrainBakerMenu.BakeFromCommandLine \
  -logFile "$log"

echo "Terrain baked. Log: $log"
echo "Commit the new/updated assets under Assets/Airside/Art/Terrain and"
echo "Assets/Resources/Airside/Prefabs, including their .meta files."

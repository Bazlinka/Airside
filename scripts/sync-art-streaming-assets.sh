#!/usr/bin/env bash
# Sync runtime art files into StreamingAssets so packaged Unity builds can load them.
# Source of truth remains Assets/Airside/Art/. Re-run after adding/changing glTF/PNG art.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
src="$root/game/Airside/Assets/Airside/Art"
dst="$root/game/Airside/Assets/StreamingAssets/Airside/Art"

if [[ ! -d "$src" ]]; then
  echo "Missing art source: $src" >&2
  exit 1
fi

# Clear only the file types this script owns. A blanket rm -rf also destroyed the
# 282 committed .meta files Unity had generated for StreamingAssets, so simply
# running the sync deleted tracked files and would have had Unity reissue fresh
# GUIDs for every synced asset.
mkdir -p "$dst"
find "$dst" -type f \( -name '*.gltf' -o -name '*.bin' -o -name '*.png' \) -delete

# Runtime loaders only need glTF kits (+ bins) and UI/surface PNGs.
#
# Textures/Terrain is excluded on purpose. Those maps are referenced by imported
# TerrainLayer assets, which Unity packs into the build itself; they never go
# through ArtRuntimePaths, so copying them here would duplicate megabytes into the
# player for nothing.
while IFS= read -r -d '' file; do
  rel="${file#"$src"/}"
  mkdir -p "$dst/$(dirname "$rel")"
  cp -f "$file" "$dst/$rel"
done < <(find "$src" -type f \
  -not -path "$src/Textures/Terrain/*" \
  \( -name '*.gltf' -o -name '*.bin' -o -name '*.png' \) -print0)

count="$(find "$dst" -type f | wc -l | tr -d ' ')"
echo "Synced $count runtime art files → $dst"
echo "Remember: Unity will generate .meta files for StreamingAssets on next Editor open."

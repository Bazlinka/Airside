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
find "$dst" -type f \( -name '*.gltf' -o -name '*.bin' -o -name '*.png' -o -name '*.jpg' -o -name '*.jpeg' \) -delete

# Runtime loaders need glTF kits (+ bins), UI/surface PNGs, and the CC0 Terrain
# layer maps used by the Adelaide bare-field ground mesh (ArtRuntimePaths).
# TerrainLayer .terrainlayer assets still pack their own copies for the Kingscote
# Terrain path; the StreamingAssets PNGs are the mesh/shader runtime source.
while IFS= read -r -d '' file; do
  rel="${file#"$src"/}"
  mkdir -p "$dst/$(dirname "$rel")"
  cp -f "$file" "$dst/$rel"
done < <(find "$src" -type f \
  \( -name '*.gltf' -o -name '*.bin' -o -name '*.png' -o -name '*.jpg' -o -name '*.jpeg' \) -print0)

# A source that was deleted or renamed leaves its old .meta behind; Unity warns about every
# orphan on import. Remove metas for owned types whose file no longer exists.
while IFS= read -r -d '' meta; do
  [[ -e "${meta%.meta}" ]] || rm -f "$meta"
done < <(find "$dst" -type f \( -name '*.gltf.meta' -o -name '*.bin.meta' -o -name '*.png.meta' -o -name '*.jpg.meta' -o -name '*.jpeg.meta' \) -print0)

# Count the synced art only; the old count included every .meta file as well.
count="$(find "$dst" -type f \( -name '*.gltf' -o -name '*.bin' -o -name '*.png' -o -name '*.jpg' -o -name '*.jpeg' \) | wc -l | tr -d ' ')"
echo "Synced $count runtime art files → $dst"
echo "Remember: Unity will generate .meta files for StreamingAssets on next Editor open."

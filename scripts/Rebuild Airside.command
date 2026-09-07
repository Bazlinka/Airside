#!/bin/bash
# Double-click this in Finder on your Mac (or: open scripts/Rebuild\ Airside.command)
cd "$(dirname "$0")/.." || exit 1
exec bash ./scripts/rebuild-and-open-mac.sh

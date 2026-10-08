#!/usr/bin/env bash
# Compile the original universal macOS notification bundle for the Unity prebuild hook.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
destination="${1:?Usage: build-mac-notifications.sh output.bundle}"
if [[ "$(uname -s)" != Darwin ]]; then
  echo 'The notification bridge requires the macOS SDK and Xcode command-line tools.' >&2
  exit 1
fi
mkdir -p "$destination/Contents/MacOS"
xcrun --sdk macosx clang++ -std=c++17 -fobjc-arc -fblocks \
  -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -bundle \
  -framework Foundation -framework AppKit -framework UserNotifications \
  "$root/scripts/native/airside-notifications.mm" \
  -o "$destination/Contents/MacOS/AirsideNotifications"
cat > "$destination/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>AirsideNotifications</string>
<key>CFBundleIdentifier</key><string>com.airside.notifications.bridge</string>
<key>CFBundleName</key><string>AirsideNotifications</string>
<key>CFBundlePackageType</key><string>BNDL</string>
<key>CFBundleVersion</key><string>1</string>
</dict></plist>
PLIST
codesign --force --sign - "$destination"
echo "Built universal macOS notification bridge: $destination"

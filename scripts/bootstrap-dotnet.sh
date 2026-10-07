#!/usr/bin/env bash
set -euo pipefail

# Make the headless checks runnable on a fresh machine or cloud session: install the .NET 8 SDK if it is missing (no root needed)
# and pre-restore the test harness's NuGet packages (both the NUnit 3.14 run and the NUnit 3.5 compile check, ADR 0252).
# Idempotent: when `dotnet` 8 is already on PATH it only restores. Prints the PATH line to export when it installs.
#
#   bash scripts/bootstrap-dotnet.sh            install if needed, then restore
#   DOTNET_INSTALL_DIR=/opt/dotnet bash ...     choose the install folder (default ~/.dotnet)

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
dir="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

have_sdk8() { command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; }

if ! have_sdk8; then
  if [ -x "$dir/dotnet" ] && PATH="$dir:$PATH" DOTNET_ROOT="$dir" dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
    :
  else
    echo "Installing the .NET 8 SDK into $dir ..." >&2
    tmp="$(mktemp -d)"
    trap 'rm -rf "$tmp"' EXIT
    if curl -sSL --retry 3 "${DOTNET_INSTALL_URL:-https://dot.net/v1/dotnet-install.sh}" -o "$tmp/dotnet-install.sh" \
        && bash "$tmp/dotnet-install.sh" --channel 8.0 --install-dir "$dir" >&2; then
      :
    else
      # Same route Cursor's environment used before this script: the distro package. Needs root or passwordless sudo.
      echo "dotnet-install did not work; trying apt-get dotnet-sdk-8.0 ..." >&2
      sudo_cmd=""; [ "$(id -u)" -ne 0 ] && sudo_cmd="sudo"
      if command -v apt-get >/dev/null 2>&1 \
          && $sudo_cmd apt-get update -qq >&2 \
          && $sudo_cmd apt-get install -y --no-install-recommends dotnet-sdk-8.0 >&2 && have_sdk8; then
        echo ".NET installed from apt." >&2
        cd "$root/scripts/dotnet-harness"
        dotnet restore -p:UnityNUnit=true -v q >/dev/null
        dotnet restore -v q >/dev/null
        echo ".NET $(dotnet --version) ready; harness packages restored." >&2
        exit 0
      fi
      echo "Could not install the .NET 8 SDK (no network to dot.net and no usable apt package). Install it by hand: https://dotnet.microsoft.com/download" >&2
      exit 1
    fi
  fi
  export PATH="$dir:$PATH" DOTNET_ROOT="$dir"
  # Tools that run this once and keep no shell state (Cursor/Codex environment setup) lose the export, so also link `dotnet` onto the PATH when we can.
  if [ -w /usr/local/bin ] && [ ! -e /usr/local/bin/dotnet ]; then ln -s "$dir/dotnet" /usr/local/bin/dotnet 2>/dev/null || true; fi
  echo "export PATH=\"$dir:\$PATH\" DOTNET_ROOT=\"$dir\""
fi

cd "$root/scripts/dotnet-harness"
# Restore both package sets so later builds work offline; the default set goes last because `dotnet test` expects it.
dotnet restore -p:UnityNUnit=true -v q >/dev/null
dotnet restore -v q >/dev/null
echo ".NET $(dotnet --version) ready; harness packages restored." >&2

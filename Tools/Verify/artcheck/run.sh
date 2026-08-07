#!/bin/bash
# Build and render. Fatal on build failure: sips will happily convert a stale .bmp and
# print "rendered", which is how a broken build looks exactly like a working one.
set -euo pipefail
cd "$(dirname "$0")"
UNITY_VERSION="${UNITY_VERSION:-6000.5.7f1}"
UNITY_SCRIPTING="${UNITY_SCRIPTING:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/Resources/Scripting}"
export DOTNET_ROOT="$UNITY_SCRIPTING/DotNetSdk"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
out="$("$DOTNET_ROOT/dotnet" run -c Release 2>&1)" || { echo "$out" | grep -E "error CS" | head -20; exit 1; }
if echo "$out" | grep -q "error CS"; then echo "$out" | grep "error CS" | head -20; exit 1; fi
echo "$out" | grep -vE "^Microsoft|^Copyright|^$" || true
for f in *.bmp; do sips -s format png "$f" --out "${f%.bmp}.png" >/dev/null 2>&1; done
echo "renders written"

#!/bin/bash
# One entry point: rebuild the runtime DLL (which the simulator links against),
# typecheck the editor scripts, then run every suite.
#
# Every step is fatal. This used to pipe each build into `grep ... || true`, which
# discarded the compiler's exit status - so a self-check that failed to compile left
# the previous DLL in place and the suites happily reported ALL CHECKS PASSED against
# stale code. A harness that can pass while the build is broken is worse than none.
SP="$(cd "$(dirname "$0")" && pwd)"
UNITY_VERSION="${UNITY_VERSION:-6000.5.7f1}"
UNITY_SCRIPTING="${UNITY_SCRIPTING:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/Resources/Scripting}"
export UNITY_SCRIPTING UNITY_VERSION
export DOTNET_ROOT="$UNITY_SCRIPTING/DotNetSdk"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
set -euo pipefail

run_build() {
  local what="$1" script="$2" out
  if ! out="$("$script" 2>&1)"; then
    echo "=== $what FAILED TO BUILD ==="
    echo "$out" | grep -vE "^Microsoft|^Copyright" | head -30
    exit 1
  fi
  # Roslyn can exit 0 while still printing errors; treat any error line as fatal too.
  if echo "$out" | grep -q "error CS"; then
    echo "=== $what BUILT WITH ERRORS ==="
    echo "$out" | grep "error CS" | sort -u | head -30
    exit 1
  fi
  echo "$out" | grep -vE "^Microsoft|^Copyright|^$" || true
}

run_build "runtime"       "$SP/check.sh"
run_build "editor scripts" "$SP/check-editor.sh"
cd "$SP/storysim" && "$DOTNET_ROOT/dotnet" run -c Release 2>&1 | grep -vE "^$"

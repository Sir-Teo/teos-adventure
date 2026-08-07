#!/bin/bash
# Typecheck Eggverse scripts against Unity's reference assemblies using Unity's bundled Roslyn,
# and produce the DLL the offline suites link against.
#
# Exits non-zero when the build fails. It used to end with `csc ... | grep | head`, which made
# the script's exit status head's - always 0 - so `check.sh && echo OK` printed OK over a screen
# of compiler errors. And a failed build left the previous DLL in place for the suites to link,
# so they reported green against stale code. The output now goes to a temporary file that is
# only moved into place on success, and any previous DLL is removed: a broken build leaves no
# DLL at all rather than a stale one that would quietly produce a green run.
set -uo pipefail
# Override with UNITY_SCRIPTING=... if your editor lives elsewhere.
UNITY_VERSION="${UNITY_VERSION:-6000.5.7f1}"
S="${UNITY_SCRIPTING:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/Resources/Scripting}"
[ -d "$S" ] || { echo "Unity scripting tools not found at $S"; echo "Set UNITY_SCRIPTING or UNITY_VERSION."; exit 1; }
PROJ="$(cd "$(dirname "$0")/../.." && pwd)"
SP="$(cd "$(dirname "$0")" && pwd)"
REFS=()
for d in "$S/Managed/UnityEngine"/*.dll; do REFS+=(-r:"$d"); done
for d in "$PROJ/Library/ScriptAssemblies/UnityEngine.UI.dll" \
         "$PROJ/Library/ScriptAssemblies/Unity.InputSystem.dll" \
         "$PROJ/Library/ScriptAssemblies/Unity.InputSystem.ForUI.dll"; do
  [ -f "$d" ] && REFS+=(-r:"$d")
done
for d in "$S/NetStandard/ref/2.1.0"/*.dll; do REFS+=(-r:"$d"); done
SRC=()
while IFS= read -r f; do SRC+=("$f"); done < <(find "${1:-$PROJ/Assets/Scripts}" -name "*.cs")
[ ${#SRC[@]} -eq 0 ] && { echo "no sources"; exit 1; }

# Same *filename* in a scratch directory, not a different filename: Roslyn bakes the output
# file's stem into the assembly identity, so building to .Eggverse.build.dll produced an
# assembly called ".Eggverse.build" that no longer resolved once renamed to Eggverse.dll.
mkdir -p "$SP/.build"
TMP="$SP/.build/Eggverse.dll"
OUT="$("$S/DotNetSdk/dotnet" "$S/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" \
  -nostdlib -noconfig -target:library -langversion:9.0 -nowarn:CS0169,CS0414,CS0649 \
  -out:"$TMP" "${REFS[@]}" "${SRC[@]}" 2>&1)"
STATUS=$?
echo "$OUT" | grep -vE "^$|^Microsoft|^Copyright" | head -60
if [ $STATUS -ne 0 ] || echo "$OUT" | grep -q "error CS"; then
  # Remove the previous DLL too, not just the failed build. Leaving it means anyone who runs a
  # suite directly rather than through verify.sh links yesterday's code and gets a green run.
  # No DLL at all fails loudly, which is the only honest outcome of a broken build.
  rm -f "$TMP" "$SP/Eggverse.dll"
  exit 1
fi
mv -f "$TMP" "$SP/Eggverse.dll"

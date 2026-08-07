#!/bin/bash
# Typecheck runtime + editor scripts together. Same exit-status fix as check.sh.
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
         "$PROJ/Library/ScriptAssemblies/Unity.InputSystem.dll"; do
  [ -f "$d" ] && REFS+=(-r:"$d")
done
for d in "$S/NetStandard/ref/2.1.0"/*.dll; do REFS+=(-r:"$d"); done
SRC=()
while IFS= read -r f; do SRC+=("$f"); done < <(find "$PROJ/Assets/Scripts" "$PROJ/Assets/Editor" -name "*.cs")

OUT="$("$S/DotNetSdk/dotnet" "$S/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" \
  -nostdlib -noconfig -target:library -langversion:9.0 -nowarn:CS0169,CS0414,CS0649 \
  -out:"$SP/EggverseWithEditor.dll" "${REFS[@]}" "${SRC[@]}" 2>&1)"
STATUS=$?
echo "$OUT" | grep -vE "^$|^Microsoft|^Copyright" | head -30
if [ $STATUS -ne 0 ] || echo "$OUT" | grep -q "error CS"; then exit 1; fi

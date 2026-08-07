#!/bin/bash
# Plant a fault, confirm the suite catches it, put the file back.
#
#   Tools/Verify/plant.sh <file> <find> <replace> [expected substring of the failure]
#
# Exists because twice a hand-run "does this check have teeth?" test produced no failure and I
# read that as the check passing, when in fact the edit had matched nothing and the file was
# never touched. A no-op edit and a working check look identical from the outside.
#
# So this refuses to draw a conclusion unless it can prove the plant landed: it checks the
# replacement actually changed the file, and it restores the original whatever happens.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

[ $# -ge 3 ] || { echo "usage: plant.sh <file> <find> <replace> [expected]"; exit 2; }
FILE="$1"; FIND="$2"; REPL="$3"; EXPECT="${4:-}"
[ -f "$FILE" ] || { echo "no such file: $FILE"; exit 2; }

BACKUP="$(mktemp)"; cp "$FILE" "$BACKUP"

# Restoring the source is not enough. verify.sh builds before it checks, so a planted run
# leaves a DLL with the fault compiled into it, and artcheck links that DLL - it will happily
# render a defect that no longer exists in any source file. That cost real time to chase once.
# Put the source back, then rebuild so nothing downstream is looking at the plant.
restore() {
  cp "$BACKUP" "$FILE"; rm -f "$BACKUP"
  "$ROOT/Tools/Verify/check.sh" >/dev/null 2>&1 || echo "  WARNING: could not rebuild after restoring."
}
trap restore EXIT

# --- plant, and prove it landed ---
python3 - "$FILE" "$FIND" "$REPL" <<'PY'
import sys
path, find, repl = sys.argv[1], sys.argv[2], sys.argv[3]
src = open(path).read()
n = src.count(find)
print(f"  plant: {n} occurrence(s) of the target")
if n == 0:
    sys.exit(3)
open(path, "w").write(src.replace(find, repl, 1))
PY
case $? in
  3) echo "  ABORT: the pattern matched nothing, so nothing was tested."; exit 3 ;;
  0) ;;
  *) echo "  ABORT: could not plant."; exit 2 ;;
esac

if cmp -s "$FILE" "$BACKUP"; then
  echo "  ABORT: the file is unchanged after planting. Nothing was tested."
  exit 3
fi

# --- run, and report what was caught ---
OUT="$("$ROOT/Tools/Verify/verify.sh" 2>&1)"
FAILS="$(echo "$OUT" | grep -E "SELFCHECK FAIL|^  FAIL:" || true)"

# A plant that does not compile produces no failures either, and reporting that as "the suite
# still passes" is the exact mistake this script exists to prevent - it would read as a missing
# check when in fact nothing ran. Say so instead.
if echo "$OUT" | grep -q "error CS"; then
  echo "  ABORT: the planted code does not compile, so the suite never ran."
  echo "$OUT" | grep "error CS" | head -3 | sed 's/^/    /'
  exit 3
fi

# A suite that crashed produces no failures either. That is not "still passes" - it is "never
# finished", and reporting it as the former is how a check that took the whole run down with it
# looked like a check with no teeth.
if echo "$OUT" | grep -qE "Unhandled exception|SecurityException|NullReferenceException"; then
  echo "  ABORT: the suite crashed rather than finishing, so nothing was concluded."
  echo "$OUT" | grep -E "Unhandled exception|at Eggverse|at Sim" | head -4 | sed 's/^/    /'
  exit 3
fi

if [ -z "$FAILS" ]; then
  echo "  NOT CAUGHT: the suite still passes with the fault planted."
  exit 1
fi

echo "  caught:"
echo "$FAILS" | sed 's/^/    /' | head -6

if [ -n "$EXPECT" ]; then
  if echo "$FAILS" | grep -q "$EXPECT"; then
    echo "  and it names the expected problem."
  else
    echo "  WARNING: something failed, but not the check being tested ('$EXPECT' not mentioned)."
    exit 1
  fi
fi

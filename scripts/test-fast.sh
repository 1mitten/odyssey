#!/usr/bin/env bash
# Odyssey: the fast test tier. Runs the pure-C# simulation and interface-model tests with no Unity.
#
#   scripts/test-fast.sh                        run the default tier (everything but Long)
#   scripts/test-fast.sh --filter TestCategory=Long   run the slow ones
#   scripts/test-fast.sh --filter Name~Def      run a subset (dotnet test filter syntax)
#   ODYSSEY_TEST_ALL=1 scripts/test-fast.sh     run everything, Long included
#
# The default excludes TestCategory=Long, which is where soak runs, scale-target round trips and
# anything else measured in seconds lives. That boundary is the reason the default tier stays
# worth running after every commit: a tier nobody waits for is a tier nobody runs. Passing an
# explicit --filter replaces the default entirely, so a filter of your own sees every test.
#
# Two projects, run one after the other: the Sim tests and the Hud tests. Both mirror the Unity
# assemblies through tools/dotnet/, and a failure in either fails the run.
#
# Why this exists: Unity batchmode spends tens of seconds booting the editor, refreshing the
# asset database and reloading the script domain. The tests themselves take about 0.06 seconds.
# Filtering which tests Unity runs saves nothing; not starting Unity saves everything. This
# tier is for the inner loop. scripts/unity.sh test editmode remains the authoritative gate,
# because only Unity proves the assembly definitions and the editor-facing code.
#
# The Unity install ships a .NET *runtime* but not an SDK, so this needs a real SDK on the path
# or in the usual per-user location. Install one without admin rights:
#   powershell -c "& ([scriptblock]::Create((irm https://dot.net/v1/dotnet-install.ps1))) -Channel 8.0"

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

PROJECTS=(
  "tools/dotnet/Odyssey.Tests.Sim/Odyssey.Tests.Sim.csproj"
  "tools/dotnet/Odyssey.Tests.Hud/Odyssey.Tests.Hud.csproj"
)

has_sdk() {
  # A runtime-only install answers --list-sdks with an error on stdout and still exits 0, so
  # the exit code cannot be trusted. Require a real version line.
  local bin="$1"
  [[ -x "$bin" ]] || return 1
  "$bin" --list-sdks 2>/dev/null | grep -qE "^[0-9]+\.[0-9]+\.[0-9]+"
}

find_dotnet() {
  if [[ -n "${DOTNET:-}" ]] && has_sdk "$DOTNET"; then echo "$DOTNET"; return 0; fi

  local candidates=()
  if command -v dotnet >/dev/null 2>&1; then candidates+=("$(command -v dotnet)"); fi
  candidates+=(
    "${USERPROFILE:-$HOME}/.dotnet/dotnet.exe"
    "$HOME/.dotnet/dotnet.exe"
    "$HOME/.dotnet/dotnet"
    "${LOCALAPPDATA:-}/Microsoft/dotnet/dotnet.exe"
    "/c/Program Files/dotnet/dotnet.exe"
    "/usr/share/dotnet/dotnet"
    "/usr/local/share/dotnet/dotnet"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -n "$candidate" ]] && has_sdk "$candidate"; then
      echo "$candidate"
      return 0
    fi
  done

  echo "test-fast.sh: no .NET SDK found (a runtime alone is not enough)." >&2
  echo "              Install one with:" >&2
  echo "                powershell -c \"& ([scriptblock]::Create((irm https://dot.net/v1/dotnet-install.ps1))) -Channel 8.0\"" >&2
  echo "              or set DOTNET=/path/to/dotnet. Until then use: scripts/unity.sh test editmode" >&2
  return 2
}

DOTNET_BIN="$(find_dotnet)"

# dotnet test takes one --filter, so a caller's own filter replaces ours rather than joining it.
has_filter=0
for arg in "$@"; do
  case "$arg" in --filter|--filter=*) has_filter=1 ;; esac
done

DEFAULT_FILTER=()
if [[ $has_filter -eq 0 && -z "${ODYSSEY_TEST_ALL:-}" ]]; then
  DEFAULT_FILTER=(--filter "TestCategory!=Long")
fi

STATUS=0
for PROJECT in "${PROJECTS[@]}"; do
  "$DOTNET_BIN" test "$PROJECT" --nologo "${DEFAULT_FILTER[@]}" "$@" || STATUS=1
done
exit "$STATUS"

#!/usr/bin/env bash
# Odyssey: the fast test tier. Runs the pure-C# simulation tests with no Unity at all.
#
#   scripts/test-fast.sh                 run every Sim test
#   scripts/test-fast.sh --filter Name~Def   run a subset (dotnet test filter syntax)
#
# Why this exists: Unity batchmode spends tens of seconds booting the editor, refreshing the
# asset database and reloading the script domain. The tests themselves take about 0.06 seconds.
# Odyssey.Sim and Odyssey.Sim.Contracts carry no UnityEngine reference by design (ADR 0005), so
# they can be compiled and tested by a plain dotnet SDK, which turns the inner loop from minutes
# into seconds.
#
# This tier is fast, not authoritative. Unity remains the gate:
#   - only Unity proves the assembly-definition boundaries actually hold;
#   - only Unity can run anything that touches Unity (PlayMode, editor tooling, scenes).
# Run scripts/unity.sh test editmode before committing, and let CI run it on every push.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

PROJECT="tools/dotnet/Odyssey.Tests.Sim/Odyssey.Tests.Sim.csproj"

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
exec "$DOTNET_BIN" test "$PROJECT" --nologo "$@"

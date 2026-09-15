#!/usr/bin/env bash
# Odyssey: run the Unity editor headless from the repository root.
#
#   scripts/unity.sh which                      print the editor binary that will be used
#   scripts/unity.sh inventory                  run the Synty inventory (docs/research/synty-inventory.md)
#   scripts/unity.sh test editmode|playmode     run tests, results in TestResults/<Mode>.xml
#   scripts/unity.sh exec Ns.Class.Method       run a static editor method in batchmode, then quit
#   scripts/unity.sh open                       launch the editor GUI on this project
#
# Editor discovery: $UNITY_EDITOR if set, else the version in ProjectSettings/ProjectVersion.txt
# under $UNITY_HUB_EDITORS (default ~/Unity/Hub/Editor on Linux/macOS, C:\Program Files\Unity\Hub\Editor
# on Windows Git Bash), else the newest installed 6000.x.
# Extra arguments after the subcommand are passed to Unity. Set UNITY_EXTRA_ARGS for standing
# additions (e.g. UNITY_EXTRA_ARGS="-nographics" or licence flags for CI).

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

editor_bin() {
  # editor_bin <install dir>: print the editor binary inside it, if any (Unity on Linux/macOS, Unity.exe on Windows)
  local dir="$1"
  if [[ -x "$dir/Editor/Unity.exe" ]]; then echo "$dir/Editor/Unity.exe"; return 0; fi
  if [[ -x "$dir/Editor/Unity" ]]; then echo "$dir/Editor/Unity"; return 0; fi
  return 1
}

find_unity() {
  if [[ -n "${UNITY_EDITOR:-}" ]]; then
    if [[ -x "$UNITY_EDITOR" ]]; then echo "$UNITY_EDITOR"; return 0; fi
    echo "unity.sh: UNITY_EDITOR is set but not executable: $UNITY_EDITOR" >&2
    return 2
  fi
  local hub="${UNITY_HUB_EDITORS:-}"
  if [[ -z "$hub" ]]; then
    local cand
    for cand in "$HOME/Unity/Hub/Editor" "/c/Program Files/Unity/Hub/Editor"; do
      if [[ -d "$cand" ]]; then hub="$cand"; break; fi
    done
    hub="${hub:-$HOME/Unity/Hub/Editor}"
  fi
  local want=""
  if [[ -f ProjectSettings/ProjectVersion.txt ]]; then
    want="$(sed -n 's/^m_EditorVersion: *//p' ProjectSettings/ProjectVersion.txt | tr -d '[:space:]')"
  fi
  if [[ -n "$want" ]] && editor_bin "$hub/$want"; then return 0; fi
  local newest
  newest="$(ls -d "$hub"/6000.*/ 2>/dev/null | sort -V | tail -n 1 || true)"
  if [[ -n "$newest" ]] && editor_bin "${newest%/}"; then return 0; fi
  echo "unity.sh: no Unity editor found (wanted '${want:-any 6000.x}' under $hub)." >&2
  echo "          Install the project's Unity version via Unity Hub or set UNITY_EDITOR=/path/to/Editor/Unity." >&2
  return 2
}

run_batch() {
  # run_batch <logfile> <unity args...>; prints the exit code and log path, returns the exit code
  local log="$1"; shift
  mkdir -p Logs
  local rc=0
  # shellcheck disable=SC2086
  "$UNITY" -batchmode -projectPath "$ROOT" -logFile "$log" ${UNITY_EXTRA_ARGS:-} "$@" || rc=$?
  echo "unity.sh: exit $rc, log: $log"
  return "$rc"
}

cmd="${1:-help}"
if [[ $# -gt 0 ]]; then shift; fi

case "$cmd" in
  which)
    find_unity
    ;;
  inventory)
    UNITY="$(find_unity)"
    run_batch Logs/synty-inventory.log -nographics \
      -executeMethod Odyssey.EditorTools.SyntyInventory.Run -quit "$@"
    ;;
  test)
    UNITY="$(find_unity)"
    mode="${1:-editmode}"
    if [[ $# -gt 0 ]]; then shift; fi
    case "$mode" in
      editmode|EditMode) platform=EditMode ;;
      playmode|PlayMode) platform=PlayMode ;;
      *) echo "unity.sh: test editmode|playmode" >&2; exit 2 ;;
    esac
    mkdir -p TestResults
    # No -quit here: Unity exits on its own after -runTests and -quit would drop the results file.
    run_batch "Logs/test-$platform.log" -nographics -runTests -testPlatform "$platform" \
      -testResults "$ROOT/TestResults/$platform.xml" "$@"
    ;;
  exec)
    UNITY="$(find_unity)"
    method="${1:-}"
    if [[ -z "$method" ]]; then echo "unity.sh: exec Namespace.Class.Method" >&2; exit 2; fi
    shift
    run_batch Logs/exec.log -nographics -executeMethod "$method" -quit "$@"
    ;;
  open)
    UNITY="$(find_unity)"
    nohup "$UNITY" -projectPath "$ROOT" "$@" >/dev/null 2>&1 &
    echo "unity.sh: editor launched (pid $!)"
    ;;
  help|-h|--help)
    sed -n '2,13p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    ;;
  *)
    echo "unity.sh: unknown command '$cmd' (try: which, inventory, test, exec, open, help)" >&2
    exit 2
    ;;
esac

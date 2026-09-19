#!/usr/bin/env bash
# Odyssey: run the Unity editor headless from the repository root.
#
#   scripts/unity.sh which                      print the editor binary that will be used
#   scripts/unity.sh inventory                  run the Synty inventory (docs/research/synty-inventory.md)
#   scripts/unity.sh test editmode|playmode     run tests, results in TestResults/<Mode>.xml
#   scripts/unity.sh build                      build a Win64 player into Build/ (gitignored)
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

check_version_pin() {
  # Unity Hub opens a project with the NEWEST installed editor, not the one the project is
  # pinned to. A newer editor then upgrades the project in place without asking: it rewrites
  # ProjectVersion.txt and bumps URP, Timeline, uGUI and Burst to its own generation, after
  # which the code no longer compiles (obsolete APIs) and the benchmark numbers no longer apply.
  # This happened on 2026-09-15 and cost a diagnosis; ADR 0001 pinned the version deliberately.
  #
  # .unity-version is the committed pin. If ProjectVersion.txt has drifted from it, restore it
  # along with the package files, rather than letting unity.sh dutifully launch the wrong editor
  # because it read the upgraded file.
  [[ -f .unity-version && -f ProjectSettings/ProjectVersion.txt ]] || return 0

  local pinned actual
  pinned="$(tr -d '[:space:]' < .unity-version)"
  actual="$(sed -n 's/^m_EditorVersion: *//p' ProjectSettings/ProjectVersion.txt | tr -d '[:space:]')"
  [[ "$pinned" == "$actual" ]] && return 0

  echo "unity.sh: this project is pinned to $pinned but ProjectVersion.txt says $actual." >&2
  echo "          A newer editor has upgraded the project in place. Restoring the pin." >&2
  git checkout -- ProjectSettings/ProjectVersion.txt Packages/manifest.json Packages/packages-lock.json 2>/dev/null || {
    echo "unity.sh: could not restore automatically; fix ProjectSettings/ProjectVersion.txt by hand." >&2
    return 4
  }
  rm -rf Library/PackageCache Library/ScriptAssemblies 2>/dev/null || true
  echo "          Restored. Open this project with the $pinned entry in Unity Hub, or via" >&2
  echo "          scripts/unity.sh open, which always picks the pinned editor." >&2
  return 0
}

find_unity() {
  check_version_pin || return $?
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

check_project_lock() {
  # A batch run against a locked project dies instantly with exit 1 and a near-empty log, which
  # is genuinely hard to diagnose. Say what is wrong instead, and clear the lock when it is
  # merely stale (a previous batch run that did not exit leaves one behind).
  [[ -f Temp/UnityLockfile ]] || return 0

  # Scoped to *this* project, not to Unity in general, and matched on the whole path.
  #
  # The first version counted every Unity.exe on the machine. This machine runs more than one
  # Unity project at once, so an editor open on an unrelated project made every batch command
  # here fail with "close the editor" while the lock it was complaining about was in fact stale.
  # Worse, it failed in the direction that looks like a real conflict, so the obvious next move
  # would have been to kill an editor belonging to somebody else's work.
  #
  # The second version scoped it to the project but matched the path as a **substring**, which is
  # the same bug wearing a disguise: the sibling checkout `D:\code\odyssey-mines` contains
  # `D:\code\odyssey`, so a batch run over there blocked every command in here and said the
  # editor was open when no editor was open anywhere. The argument is now parsed out and compared
  # whole, case-insensitively and with the separators normalised, so a prefix is not a match.
  local live="" proj_win=""
  proj_win="$(cygpath -w "$ROOT" 2>/dev/null || echo "$ROOT")"
  if command -v powershell.exe >/dev/null 2>&1; then
    live="$(ODY_PROJ="$proj_win" powershell.exe -NoProfile -Command \
      '$want = ($env:ODY_PROJ -replace "/","\").TrimEnd("\").ToLowerInvariant();
       @(Get-CimInstance Win32_Process | Where-Object {
           $_.Name -eq "Unity.exe" -and
           $_.CommandLine -match "(?i)-projectpath\s+`"?([^`"]+?)`"?(\s|$)" -and
           (($Matches[1] -replace "/","\").TrimEnd("\").ToLowerInvariant() -eq $want)
         }).Count' \
      2>/dev/null | tr -d '[:space:]')"
  elif command -v pgrep >/dev/null 2>&1; then
    live="$(pgrep -c -f -- "-projectPath $ROOT\( \|\$\)" 2>/dev/null || true)"
  fi
  [[ "$live" =~ ^[0-9]+$ ]] || live=0

  if [[ "${live:-0}" -gt 0 ]]; then
    echo "unity.sh: this project is locked and a Unity process is running." >&2
    echo "          Close the editor before running a batch command (they cannot share a project)." >&2
    return 3
  fi

  echo "unity.sh: removing a stale Temp/UnityLockfile (no Unity process is running)." >&2
  rm -f Temp/UnityLockfile
  return 0
}

kill_tree() {
  local pid="$1"
  kill "$pid" 2>/dev/null || true
  sleep 2
  if kill -0 "$pid" 2>/dev/null; then
    kill -9 "$pid" 2>/dev/null || true
  fi
}

run_batch() {
  # run_batch <logfile> <unity args...>; prints the exit code and log path, returns the exit code
  local log="$1"; shift
  check_project_lock || return $?
  mkdir -p Logs
  local rc=0
  # shellcheck disable=SC2086
  "$UNITY" -batchmode -projectPath "$ROOT" -logFile "$log" ${UNITY_EXTRA_ARGS:-} "$@" || rc=$?
  echo "unity.sh: exit $rc, log: $log"
  return "$rc"
}

run_tests_watchdog() {
  # Unity is documented to exit on its own after -runTests, and -quit would cut the run short,
  # so neither option is available. In practice it sometimes writes its results and then keeps
  # running indefinitely (an editor package holding a background connection will do this). That
  # hangs a terminal and would hang CI outright, so the results file is treated as the authority:
  # once it is written, the run is over, and a lingering process is given a grace period and then
  # terminated.
  local log="$1" results="$2"; shift 2
  check_project_lock || return $?
  mkdir -p Logs TestResults
  rm -f "$results"

  local timeout="${UNITY_TEST_TIMEOUT:-1800}"
  local grace="${UNITY_TEST_GRACE:-25}"

  # shellcheck disable=SC2086
  "$UNITY" -batchmode -projectPath "$ROOT" -logFile "$log" ${UNITY_EXTRA_ARGS:-} \
    -runTests -testResults "$results" "$@" &
  local pid=$!

  local waited=0 settled=0
  while kill -0 "$pid" 2>/dev/null; do
    if [[ -s "$results" ]]; then
      settled=$((settled + 1))
      if [[ "$settled" -ge "$grace" ]]; then
        echo "unity.sh: results written but Unity is still running after ${grace}s; terminating it." >&2
        kill_tree "$pid"
        break
      fi
    fi
    if [[ "$waited" -ge "$timeout" ]]; then
      echo "unity.sh: timed out after ${timeout}s with no results; terminating Unity." >&2
      kill_tree "$pid"
      echo "unity.sh: see $log" >&2
      return 124
    fi
    sleep 1
    waited=$((waited + 1))
  done
  wait "$pid" 2>/dev/null || true

  if [[ ! -s "$results" ]]; then
    echo "unity.sh: no test results were written. See $log" >&2
    grep -aE "error CS" "$log" 2>/dev/null | sort -u | head -20 >&2 || true
    return 1
  fi

  # The results file, not the process exit code, decides pass or fail.
  local summary
  summary="$(grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' "$results" | head -1 || true)"
  echo "unity.sh: $summary  ($results)"
  if grep -qE 'result="Failed"' "$results"; then
    grep -oE 'name="[^"]+"[^>]*result="Failed"' "$results" | head -20 >&2
    return 1
  fi
  return 0
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
    # No -quit here: -quit can cut the run short before results are written. Unity is documented
    # to exit on its own after -runTests but sometimes does not, so the watchdog supervises it.
    # PlayMode needs a real graphics device, so -nographics is EditMode-only.
    if [[ "$platform" == "EditMode" ]]; then
      run_tests_watchdog "Logs/test-$platform.log" "$ROOT/TestResults/$platform.xml" \
        -nographics -testPlatform "$platform" "$@"
    else
      run_tests_watchdog "Logs/test-$platform.log" "$ROOT/TestResults/$platform.xml" \
        -testPlatform "$platform" "$@"
    fi
    ;;
  build)
    # A standalone player. The one thing that compiles the PLAYER assembly set -- no UnityEditor,
    # stripping as configured -- and the only thing that proves the scene and the shaders survive
    # packaging. A `using UnityEditor` in Presentation passes both test tiers and fails here.
    #
    # Output goes to Build/, which is gitignored: a player has the licensed Synty content baked
    # into it, so it is the one artefact that must never be committed.
    UNITY="$(find_unity)"
    run_batch Logs/build.log -nographics \
      -executeMethod Odyssey.EditorTools.PlayerBuild.Windows64 "$@"
    ;;
  exec)
    UNITY="$(find_unity)"
    method="${1:-}"
    if [[ -z "$method" ]]; then echo "unity.sh: exec Namespace.Class.Method" >&2; exit 2; fi
    shift
    run_batch Logs/exec.log -nographics -executeMethod "$method" -quit "$@"
    ;;
  shot)
    # Renders a picture to Logs/. Deliberately WITHOUT -nographics: this is the one family of
    # commands that needs a real graphics device, because its whole purpose is to produce a
    # picture a human (or Claude) can look at instead of reasoning about what the renderer
    # ought to be drawing.
    #
    # Takes an optional method, so any tool that makes an image gets the same device and the
    # same watchdog: `unity.sh shot` for the play view, or e.g.
    # `unity.sh shot Odyssey.EditorTools.ScatterSheet.Shoot` for a contact sheet of props.
    UNITY="$(find_unity)"
    shot_method="${1:-Odyssey.EditorTools.PlayScene.Screenshot}"
    if [[ $# -gt 0 ]]; then shift; fi
    run_batch Logs/shot.log -executeMethod "$shot_method" -quit "$@"
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
    echo "unity.sh: unknown command '$cmd' (try: which, inventory, test, build, exec, shot, open, help)" >&2
    exit 2
    ;;
esac

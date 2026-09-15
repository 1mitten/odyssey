# ADR 0001 — Engine: Unity 6.3 LTS (6000.3.x), URP, C#

Status: accepted (brief §2, agreed before Phase 0; enacted 2026-09-15).

## Context

The prototype needs an engine the owner already works in, that the Synty POLYGON packs target, that Claude Code can drive headless (batchmode tests, editor scripting, MCP), and that will stay supported for the project's life. The 2022.3-era Synty rebuilds are SRP-native. Unity 6.4–6.6 are Update releases, each supported only until the next ships; 6.3 is the current LTS.

## Decision

Unity **6.3 LTS**, tracking the newest 6000.3.x patch (6000.3.24f1 at creation), with **URP** and C# (nullable enabled, analysers on). One planned upgrade, to **6.7 LTS** when it lands later in 2026, and no other. The project was created from the Universal 3D template (URP 17.3.0) at the repository root.

The Windows dev machine had 6000.6.0f1 installed; it was not used — a first import attempt made on it was discarded and 6000.3.24f1 was installed instead, precisely because Update releases fall out of support mid-project.

## Consequences

- `ProjectSettings/ProjectVersion.txt` pins the version; `scripts/unity.sh` resolves the editor from it on both dev machines.
- Any editor other than 6000.3.x on a dev machine is a setup error, not a choice (`docs/setup/local-dev.md`).
- The 6.7 upgrade is a single planned event with its own verification pass (reimport, tests, headless day-run), not a rolling drift.

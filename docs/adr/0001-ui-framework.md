# ADR 0001 — UI Toolkit for the runtime interface, behind a Unity-free HUD assembly

- **Status:** accepted, pending one measurement
- **Date:** 2026-09-15
- **Deciders:** owner, with the research in `docs/research/g-02-unity-ui-framework.md`
- **Supersedes:** nothing
- **Related:** `docs/adr/0002-sim-ui-contract.md`, `docs/design/09-ui-and-input.md`

## Context

The interface has to carry a thousand-plus elements, a 50 × 25 priority grid, a 50-card roster
bar and an archive that may reach ten thousand rows, at 60 FPS with the simulation running at
3× speed, on a 2022 mid-range laptop. That laptop is the target; the RTX 5070 Ti dev machine
is not.

Unity 6.3 offers two supported runtime frameworks, UI Toolkit and uGUI. The reference game we
are studying uses a third approach, immediate mode, where every widget is rebuilt every frame
from a call that also contains the widget's logic.

Nothing has been compiled or profiled. There is no Unity project in the repository yet and the
container this was written in has neither Unity nor a dotnet SDK.

## Decision

**Use UI Toolkit for the runtime interface.** Author layout and skinning as markup and
stylesheets. Bind imperatively from our own view cache inside frequency buckets rather than
through the runtime data-binding system.

**And, more importantly: put every piece of interface state, decision-making and formatting in
`Odyssey.Hud`, an assembly with no UnityEngine reference at all.** UI Toolkit is a view driver
attached at the end. This is the part of the decision that carries the weight; the framework
choice is downstream of it.

Immediate mode is rejected for the interface and permitted for exactly one thing: a developer
diagnostic overlay in non-shipping builds.

## Rationale

Four mechanisms decide it, in order of weight:

1. **Per-frame allocation.** Immediate mode allocates every frame by construction. At a
   thousand-plus elements that is a permanent garbage treadmill, which is incompatible with the
   stated frame budget on the target machine. Both retained frameworks allocate nothing unless
   text or hierarchy is touched.
2. **List virtualisation.** UI Toolkit has it built in; uGUI needs it hand-rolled or bought.
   The archive, the work grid, the research list and the trade ledger all need it.
3. **Testability.** Immediate mode fuses logic and drawing in one method, so there is no seam
   to assert against. Our milestone gate requires headless tests, so this alone disqualifies it.
4. **Moddability.** Stylesheets and markup are text, so icon and theme overrides are trivially
   patchable. A uGUI mod must ship prefabs and therefore asset bundles built against a matching
   editor version, which is a high barrier for the day-one modding the brief requires.

The recommendation against the runtime data-binding system is deliberate and against the grain.
Our view layer already knows exactly what changed and when, so the binding system's change
detection duplicates work we are doing anyway; and imperative binding keeps the decision about
whether a widget updates this frame inside the Unity-free, testable assembly.

## Alternatives considered

**uGUI.** More battle-tested, larger third-party ecosystem, but no built-in virtualisation,
layout lives in prefabs rather than text, prefab-based modding needs asset bundles, and Unity
is steering away from it. Kept warm as the fallback: because the HUD assembly is Unity-free,
swapping the view driver is weeks of work, not a rewrite.

**Immediate mode, as the reference game does.** Rejected on allocation, testability and modding
surface, all three of which are structural rather than tunable. The structural ideas worth
taking from that game — a window stack, a selection-derived inspect pane, a command grid
attached to the selection — are architecture and are adopted in
`docs/design/09-ui-and-input.md` independently of the drawing model.

## Consequences

**Good.** Virtualisation, the dynamic icon atlas and text-based styling come for free. The
owner can author panel layout visually in the editor without touching C#, which matches the
working agreement. Roughly 22 of 28 interface subsystems end up free of Unity and therefore
unit-testable with no editor present — which, once the remote container gains a dotnet SDK,
means most of the interface test suite runs remotely in seconds.

**Bad.** View-level tests need a live panel, which may not work under `-batchmode -nographics`.
That is experiment R4 and it is the lowest-confidence assumption in the design. The blast
radius is contained: a negative result costs us only the thin view-level test suite, not the
interface test strategy, because the strategy lives in the Unity-free assembly.

**Constraining.** Dynamic atlas eligibility caps icon size and forbids compression and mips, so
icons are authored at 128 pixels, uncompressed, no mips. We author them, so this is free.
No Unity vector, rectangle or colour types may appear in the HUD assembly; equivalents live in
`Odyssey.Core`. A small tax.

**Unresolved.** There appears to be no public runtime markup parser, so a mod cannot drop a
markup file on disk without asset bundles. The mitigation is our own `UiLayoutDef` format,
parsed and validated in a Unity-free assembly, which turns the limitation into an advantage
because layout definitions become headless-testable. The underlying claim is experiment R11 and
takes ten minutes to settle on the dev machine.

## Flip conditions

Fall back to uGUI for the HUD shell if **any** of these hold, measured on the target laptop:

- **F1.** The dense stress case — the priority grid, the roster bar and a ten-thousand-row
  archive, all open — exceeds **3.5 ms** on the main thread, or allocates **anything** per
  frame after warm-up, and profiling attributes it to framework internals rather than our code.
- **F2.** About two hundred icons cannot be resolved into two atlas pages or fewer, producing
  more than about sixteen HUD draw calls.
- **F3.** Pointer events cannot be partitioned correctly between the interface and the world,
  for the eight enumerated drag cases, without fighting the framework.

Abandon UI Toolkit entirely rather than partially only if two of the three fail. In every case
`Odyssey.Hud` is untouched.

The experiments that test these are R1, R2 and R3 in `docs/research/g-02-unity-ui-framework.md`.
**None can run in the remote container**; all three are dev-machine work.

## Status of the evidence

This ADR is accepted on a structural argument, not on measurement. Every performance figure in
`docs/design/09-ui-and-input.md` §4 is a budget set to be slightly uncomfortable, not an
observation. If R1 comes back comfortable on the first attempt, the budget was set too loosely
and should be tightened rather than celebrated.

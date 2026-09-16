# ADR 0003 — UI Toolkit for the runtime interface, behind a Unity-free HUD assembly

- **Status:** accepted; F1 measured 2026-09-16 and does not fire, F2 and F3 still unmeasured
- **Date:** 2026-09-15
- **Deciders:** owner, with the research in `docs/research/g-02-unity-ui-framework.md`
- **Supersedes:** nothing
- **Related:** `docs/adr/0004-sim-ui-contract.md`, `docs/design/09-ui-and-input.md`

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

## Measurement, 2026-09-16 — F1 answered

R1 ran on the dev machine as `Odyssey.Tests.PlayMode.HudStressTests`, under the real player loop,
which `docs/lessons.md` records as the only place a frame number in this project means anything.
The dense case is synthetic — the priority grid, the roster bar and the archive do not exist yet —
and deliberately so: F1 asks about UI Toolkit, and real panels would answer with their own logic
mixed in. Text is assigned from a pool built during set-up, so nothing measured is our garbage.

| Arm | Elements | Labels retexted per frame | Cost over baseline |
|---|---|---|---|
| Priority grid | 1,250 | 25 at 60 Hz | 0.330 ms |
| Roster bar | 200 | 150 at 60 Hz | 2.368 ms |
| Archive (`ListView`) | 10,000 rows | 0 | −0.031 ms |
| All three | 11,450 | 175 at 60 Hz | 2.626 ms |
| **All three, bucket cadence** | 11,450 | ~11 at 4 Hz a label | **0.488 ms** |

Baseline, the shipped HUD alone, is 0.414 ms. Allocation is **zero bytes a frame with zero
generation-zero collections in every arm**, which is the half of F1 that mattered most: it is the
mechanism that disqualified immediate mode, and it survives the dense case intact.

**F1 does not fire.** 0.488 ms against a 1.167 ms budget — the 3.5 ms laptop figure divided by a
headroom factor of three, the owner's ruling of 2026-09-16 on measuring a laptop budget on a
desktop. The factor is **ASSUMED**: HUD work is main-thread, so the axis is single-thread speed,
and a 2022 mid-range laptop part is roughly half a Ryzen 7 9800X3D and throttles under sustained
load. It stands until a real laptop is measured.

### What the arms actually say

**Cost tracks text churn, not tree size.** The grid holds six times the roster's elements for a
seventh of its cost, and ten thousand archive rows cost nothing measurable at all. The predictor
is how many labels change text in a frame, at about **15 microseconds each** on this machine.
That number is more useful than the verdict: it lets a panel be budgeted before it is built.

**Virtualisation is confirmed.** −0.031 ms for ten thousand rows is noise around zero. That was
mechanism 2 of the rationale above and a main reason uGUI was set aside; it is now measured
rather than argued.

**The frequency buckets are load-bearing, not an optimisation.** Driving all 175 labels every
frame costs 2.626 ms and misses the budget on a machine three times faster than the target. The
same tree at a four-hertz cadence costs 0.488 ms. Design 09 already binds inside buckets, so the
design is correct — but it is now clear that it has to, and a future panel that rebinds on every
frame will break the budget without touching the framework. That is a constraint on us, and it
belongs beside F1 rather than inside it.

**One prediction in `g-02` was wrong.** Its note that the priority grid might need a single
element painting itself, added as R1's second arm, is unnecessary: the grid is the cheap part.
The roster bar carries 90% of the cost, and it does so because of how often it rebinds rather
than because of what it contains.

### Still open

F2 and F3 are not measured, and the fall-back rule is "if **any** of these hold". R2 (icon atlas
pages and draw calls) remains. R4's player-loop half is answered by `HudSmokeTests`; whether a
panel resolves under `-nographics` is still open.

**F3 was attempted on 2026-09-16 and could not be answered, for two reasons worth recording.**

The first is that the thing it tests was never built. Design 09 section 6 resolves all eight
pointer cases through an `InputRouter` with an explicit capture stack; what exists is a single
`Func<Vector2, bool>` on the camera rig, which is a one-bit answer. Four of the eight cases —
a drag begun on the world and released over a panel, the reverse, a modal swallowing everything,
and a tooltip never capturing — have nothing to test against, because drags, modals and tooltips
do not exist yet.

The second is that **mouse input cannot currently be driven in a PlayMode test at all**.
`InputSystem.QueueStateEvent` with a `MouseState` reaches neither the scroll nor the buttons of
`SliceCameraRig`. That was established the only way it could be: by a negative control, a scroll
over the world that must zoom the camera and did not. Three versions of a test were written
before that control existed, and all three produced confident, plausible, meaningless numbers —
first by sampling the camera while it was still drifting toward its start-up target, then by
demanding a tolerance tighter than the residue exponential smoothing leaves behind. They were
deleted rather than kept.

Both are `OQ-40`. Until the harness can be shown to fail when the input is withheld, nothing
asserted about pointer routing should be believed, and F3 stays open.

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

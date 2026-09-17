# ADR 0004 — Snapshot-read, intent-write: the contract between simulation and interface

- **Status:** accepted, and stated as a **constraint on the not-yet-written architecture ADR**;
  amended 2026-09-17, when the per-pawn half of the read contract was opened, and again the same
  day for the per-cell half (see the amendments below)
- **Date:** 2026-09-15
- **Deciders:** owner, with the design in `docs/design/09-ui-and-input.md` §2
- **Related:** `docs/adr/0003-ui-framework.md`; brief §5 Lane D1 and Lane D5

## Context

Brief Lane D1 has not chosen a simulation architecture. The three candidates are plain C#
simulation objects with Burst and Jobs on hot paths, full DOTS/ECS, and
MonoBehaviour-per-thing. Brief Lane D5 separately wants the simulation tick off the main thread.

The interface is the largest consumer of simulation state. If the architecture ADR is written
without a stated contract for how the interface reads and writes, the project inherits the
reference game's real structural problem: an interface that touches live simulation objects on
the main thread, which is precisely what makes moving the tick off-thread hard afterwards.

This ADR therefore lands **before** the architecture ADR, deliberately and out of phase order.

## Decision

Exactly two channels cross the boundary, and no others.

**Downward, the read contract.** The simulation publishes an immutable view of itself into a
**triple-buffered** slot at a tick-group boundary, when the main thread has requested it. The
interface acquires a lease on the ready slot at the top of its frame and reads nothing else for
the duration. Alongside it, a **bounded single-producer, single-consumer event ring** carries
discrete events that must never be dropped: a bulletin raised, an alert raised or cleared, a
thing destroyed, an intent rejected, a construction finished.

Things are addressed by **generational handles**, never by reference. The interface never
dereferences a handle; it uses it as a key into the current view.

Expensive detail is **subscription-scoped**: the interface declares interest when a panel opens
and the simulation fills only what is subscribed, so a closed panel costs nothing.

**Upward, the write contract.** The interface never calls a simulation mutator. Every action
becomes an immutable, serialisable intent pushed onto a queue, drained by the simulation at the
tick boundary before systems run, and applied or rejected with a machine-readable reason.
Intents flush while the clock is paused. Intents are logged, so an intent log plus a world seed
is a replayable test case.

## Amendment, 2026-09-17 — a feature may publish about a pawn without widening the contract

This ADR fixed the read contract as a published view that both sides reference. It did not say how
a *new* feature adds to it, and the answer in practice turned out to be "edit the struct everybody
reads". Felling added `Working` and `WorkCell` to `PawnView`; hauling added `Gesture` and its
serial; mining widened it again. Hauling water, sleeping in a bed and being injured would each do
the same. That is five features into a prototype and the contract file is already the busiest seam
in the project — `docs/plans/vertical-slice.md` counted it among six shared files that one feature
had to edit to add itself, of which five should have been extension points.

**So there is a second channel in the published frame: a sparse `PawnAspect` row, `(PawnId, key,
int)`, written through `SnapshotWriter.AddPawnAspect` and read back with
`WorldSnapshot.TryGetPawnAspect`.** A feature publishes what the interface needs to know about a
pawn without `PawnView`, `Sim.Contracts` or the composition root changing at all. No new
registration mechanism was added: the contributor seam that already existed for the frame
(`AddSnapshotContributor`) now reaches pawns, which is why this amendment is about sixty lines of
contract and not a subsystem.

**The key is a name, hashed, and not an enum.** An enum of aspect kinds would live in the contracts
assembly, which is the file we are trying to stop editing — the mechanism would recreate the
problem it exists to solve. A 64-bit FNV-1a of a symbolic name is the only form that lets a feature
mint its own vocabulary against no shared file, and it is the reasoning that already made interface
icons symbolic keys rather than filenames (ADR 0007). Sixty-four bits rather than thirty-two
because a collision here is not a crash but one feature silently reading another's number, and at
thirty-two that is roughly a one-in-two-hundred-thousand event across a few hundred keys — cheaper
to design out than to detect, and detecting it would have needed the central registry again.

**Where the line falls.** `PawnView` carries what every pawn always has and what the renderer needs
for every pawn every frame: where it is, where it is stepping, whether it is working and at what.
An aspect carries what one feature knows about some pawns sometimes. The test is whether the field
would be meaningfully populated for every pawn in every build; if not, it is an aspect. Moving the
existing fields out is explicitly *not* part of this — they pass that test, and a contract churned
for symmetry is worse than one that grew for a reason.

**Aspects are not state.** They are not saved and not hashed, exactly as `PawnGesture` is not. What
a feature publishes is a report derived from state it holds itself; that state is what belongs in
the save and the hash, and the feature is what owns it. `PawnViewContributorTests` proves the whole
loop on a fixture feature that lives outside both `Sim.Contracts` and `Sim.Pawns`: its numbers
enter the state hash and the save, its report does not, and a control that stops it publishing
leaves the hash identical.

**This is the unscoped form of something this ADR already specified and nobody built.** "Expensive
detail is subscription-scoped: the interface declares interest when a panel opens and the
simulation fills only what is subscribed" — there is no subscription mechanism in the code, and
there never was. Every installed feature publishes for every pawn it tracks, every frame. At the
prototype's scale that is tens of rows against a 62,500-cell slice already in the same buffer, so
it does not register; the honest statement is that the cheap thing was built and the specified
thing was deferred, not that the specification was met.

**Flip condition F1.** When the published set stops being sparse — a colony where aspect rows run
into the thousands per frame, or an interface doing a lookup per pawn per name in a roster redraw —
the retreat is in this order: sort the rows by pawn and hand a reader the span for one pawn, and
only then build the subscription scoping this ADR asked for. The measurement that triggers it is
the publish budget above: 0.8 ms per tick, against 0.186 ms measured for a full slice.

## Amendment 2, 2026-09-17 — the per-cell half: one row for the asked-about cell, answered while paused

The first amendment opened the read contract for pawns. Cells had a different gap: the pane could
be *clicked onto* a cell since U16's plumbing landed, but the frame's whole answer to "what is on
this layer" was one byte per cell of the active layer with four meanings — the deliberately minimal
slice channel. So a click on rock read identically to a click on grass, a water tile said nothing
about being water, and the pane shipped the placeholder "cell readout arrives with cell inspection"
for two milestones (owner, 2026-09-17: rocks indistinguishable, water silent, piles generic).

**There is now a sparse `CellDetail` row, one per asked-about cell**, written through
`SnapshotWriter.AddCellDetail` and read with `WorldSnapshot.TryGetCellDetail`, published by a
sim-side contributor (`CellDetailContributor`, registered in `ColonyComposition` beside the
designation and construction grids). It carries real values, not categories — terrain kind, what
stands in the cell, the floor's material, support, the work to clear, and the crossing cost in
thousandths of a clear crossing — by the same argument `SiteView` made: the player clicks and
expects to be told, and "a third speed" cannot be recovered from a byte.

**Selection-scoped rather than layer-shaped, deliberately.** The tempting alternative was widening
the slice channel to a terrain byte per cell. Rejected on the lesson `OrderView` paid for: a click
reaches above the slice (the outcrop the owner could see and not mine), so any layer-shaped channel
either misses those cells or publishes the whole drawn band every frame. A question is about one
cell; a row per question costs nothing while nothing is asked and answers on any layer.

**The question is an intent — `QueryCell` — and this is the part the original ADR had already
promised.** "Intents flush while the clock is paused," the Decision says, and the implementation
honoured that exactly never: a paused world spends a tick only for a speed change, so everything
else queued waits for an unpause that may not come. That was tolerable for commands (an order given
while paused applying on unpause is sensible) and wrong for the first intent that is a *question*,
because inspecting a stopped world is when inspection happens. So: a paused world answers a
`QueryCell` by republishing the view over the same settled world (`SimWorld.RepublishViews`) — no
tick spent, no system run, no hash computed, the counter unmoved — which the intent's kind permits
because a question changes nothing the simulation owns. `IntentBus.DrainWhere` exists for this and
nothing else; a command must never cross the boundary out of turn, because a command applied
off-boundary is a replay that cannot be reproduced.

**The line, restated for cells.** A per-cell channel of dense state (terrain for every cell, every
frame) would be the retreat, not the design: it is what the minimap and overlays of later
milestones may eventually justify, priced then. Until a reader wants more than the handful of rows
a selection asks for, the sparse question-and-answer shape holds, and the flip condition is the
same F1 as the pawn half.

## Rationale

**Triple, not double, buffering.** At 3× speed the simulation may publish between the interface
acquiring a buffer and releasing it. With two buffers the next write lands in the buffer being
read. A third slot with an explicit lease costs roughly 64 kB and removes the race outright.

**Frame-driven, not tick-driven, publishing.** At 3× speed the tick rate triples. Publishing per
tick would triple the interface's cost for no benefit, because intermediate states of a
continuous value such as a mood bar are not worth showing. Publishing about sixty times a
second is what the display can use. Discrete events cannot be sampled this way, which is
exactly why the event ring exists beside the frame.

**Handles rather than references.** This makes use-after-free structurally impossible rather
than merely avoided, and it gives a clean answer to the case that decides whether the contract
is real: a panel open on a colonist who dies mid-tick. Specified in
`docs/design/09-ui-and-input.md` §2.3.

**Intents rather than direct calls.** One mechanism buys four things: thread safety across an
off-thread tick, deterministic replay for tests, undo for designations, and the natural seam a
modder patches. Direct method calls buy none of them and cost less only in the first week.

## Alternatives considered

| Option | Verdict |
|---|---|
| Dirty flag plus frequency-bucketed pull | Viable. This is what the chosen design degenerates into if building the published view proves too expensive. Kept as the documented retreat |
| Push via events for everything | Fifty colonists times eight needs at 3× speed is tens of thousands of change events a second. Reserved for discrete events only |
| A full reactive or observable layer | Allocation pressure from subscription plumbing, and thread-affinity problems the moment the tick moves off the main thread. Rejected |
| Polling live simulation state per frame | **Forbidden.** A data race against a half-written world. Named explicitly because it is what happens by default when no contract is designed |

## Consequences, including the one that binds another decision

The interface requires five things of whichever architecture wins Lane D1:

1. A contracts assembly of plain blittable types that both sides reference, with neither
   referencing the other.
2. An end-of-tick-group publish hook able to write into a preallocated buffer.
3. Stable generational handles for every inspectable thing.
4. An intent drain point at the tick boundary, functional while paused.
5. The ability to answer bounded detail queries without exposing live state.

Measured against the three candidates:

- **Plain C# simulation objects with Burst and Jobs** satisfy all five most cheaply and preserve
  Harmony-style patchability. Favoured by the interface's requirements.
- **DOTS/ECS** satisfies requirements 1 and 3 naturally and makes 2 and 5 a snapshot job, which
  is fine provided that job does not fight structural change. It hurts modding, because
  patching Burst-compiled systems is unpleasant.
- **MonoBehaviour-per-thing is eliminated.** If things *are* Unity objects on the main thread
  there is no off-thread tick and no Unity-free contract, and the headless test strategy goes
  with it. Either that candidate goes or Lane D5 goes.

**This narrows Lane D1 from three candidates to two.** It is an argument, not a measurement, and
it rests on two premises: that the tick moves off the main thread, and that there is a contracts
assembly with no Unity dependency. Drop either premise and the conclusion goes with it. The
architecture ADR should accept or reject this on the record rather than inherit it silently.

**Also constraining.** Unity's mathematics and collections packages must not leak into the
contracts assembly. If DOTS wins, the simulation may reference them; the contracts stay plain
blittable structs and arrays.

## Verification

Four tests make this contract real rather than aspirational, all headless:

- `Architecture_Hud_DoesNotReferenceUnityEngine_OrSim` — the strongest structural guard in the
  design.
- `ViewCache_NeverDereferencesThingRef`.
- `Intent_AgainstDeadTarget_RejectedWithReason`.
- `Replay_IntentLog_ProducesIdenticalWorld` — simultaneously a determinism test and a
  bug-report format.

One measurement is outstanding: the cost of building and publishing a view at 3× speed, and
whether it stalls the main thread. That is experiment R5 in
`docs/research/g-02-unity-ui-framework.md`. It is the **only** performance experiment in this
work that could run in the remote container, and only once that container has a dotnet SDK —
which is a small argument for installing one sooner rather than later.

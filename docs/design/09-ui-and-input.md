# 09 — UI, HUD and input architecture

**Status:** design, written out of phase order. Nothing here is built. See *Why this exists
now* below.

**Scope:** the architecture of the interface — the subsystems, the contract between the
simulation and the interface, the performance budget, the assembly layout, input, icons and
the modding seams. The region-by-region specification of what each panel actually contains is
`docs/design/10-ui-panel-catalogue.md`. The cut-away camera, instanced world rendering and the
Synty tint strategy stay in `docs/design/06-rendering-and-camera.md`; this file cites it and
does not duplicate it.

**Research behind it:** `docs/research/g-01-ui-information-design.md` (the reference taxonomy
and what verticality does to it) and `docs/research/g-02-unity-ui-framework.md` (the framework
comparison and the twelve experiments that have not been run).

**Decisions recorded:** `docs/adr/0003-ui-framework.md`, `docs/adr/0004-sim-ui-contract.md`.

**Built, and measured elsewhere.** The HUD was rebuilt to an approved specification on 2026-09-16
and `docs/design/14-hud-layout.md` now owns every number on the screen — the type scale, the
palette, the spacing, where each region is anchored and how much of the viewport it covers. This
file keeps the architecture, the contract, the budget and the input model, all of which that
rebuild obeys and none of which it changed. Where the two touch: §7a's "words, not abbreviations"
stands and is now enforced by a test; §7's two-to-four character placeholder badge is **withdrawn**
in favour of an outlined square with no text in it (ADR 0007, amended twice); and §9 D4's 1080p
reference is now literal — the panel scales against 1920 x 1080 rather than the mockup's 1200 x 800,
because the specification's anchors are 1080p pixels.

---

## Why this exists now, ahead of its phase

Brief §6 puts design work in Phase 3, and Phase 2 research has not started. This file is
written early for one specific reason.

**Lane D1, the simulation architecture decision, is still open**, and Lane D5 wants the
simulation tick off the main thread. The interface is the largest consumer of simulation
state. If the architecture ADR is written without a stated contract for what the interface
must read and how it writes back, the project inherits the reference game's real structural
problem: an interface that reads live simulation objects on the main thread, which is exactly
what makes moving the tick off-thread hard afterwards.

So §2 of this file is an **input to** the architecture ADR rather than an output of it, and it
narrows Lane D1 from three candidates to two. That claim is made explicitly in §2.5 so the
ADR can accept or reject it on the record.

Everything else here could have waited. It is written now because it is cheaper to write in
one pass than in two.

---

## 1. Five commitments everything else follows from

1. **The interface is a plain C# program with a replaceable renderer.** All state, all
   decisions and all formatting live in `Odyssey.Hud`, an assembly with **no UnityEngine
   reference**. UI Toolkit is a view driver bolted on the end. This is what makes the
   interface testable in a container with no Unity, and what makes the framework choice
   reversible.
2. **Exactly two channels cross the simulation boundary.** Downward: a triple-buffered
   published frame plus a lock-free event ring. Upward: a single serialisable intent queue
   drained at tick boundaries. Nothing else, ever. No interface code holds a reference to a
   simulation object.
3. **Overlays are never UI elements.** One layer of the agreed footprint is 62,500 cells.
   Per-cell scalar fields become one texture per layer drawn on one quad.
4. **Zero bytes allocated per frame in the steady-state HUD**, asserted by a test, from M0.
   Retrofitting this is the same class of mistake as retrofitting the z axis.
5. **Icons, text and layout are Defs, not code.** This is simultaneously the modding surface
   and the reason the interface can exist before any art does.

---

## 2. The simulation-to-interface contract

This is the section that constrains Lane D1.

### 2.1 Ranked options

1. **Published frame, triple-buffered, plus a bounded event ring, plus subscription-scoped
   detail views.** Recommended.
2. Dirty flag plus frequency-bucketed pull. What option 1 degenerates into if building the
   published frame proves too expensive. The safe retreat.
3. Push via events for everything. Correct but unusable as a general mechanism: fifty
   colonists times eight needs at three times the base tick rate is tens of thousands of
   change events per second. Reserved for discrete events that must never be dropped.
4. A full reactive or observable layer. Allocation pressure from the subscription plumbing,
   and observables acquire thread-affinity problems the moment the simulation moves off the
   main thread. No.
5. Polling live simulation state per frame. **Forbidden.** With the tick off the main thread
   this is a data race against a half-written world. It is also the thing that happens by
   default if the contract is not designed, which is why it is named and banned here.

### 2.2 The read contract

```
Simulation thread, at a tick-group boundary, only when the main thread has set "publish due":

    writer = frames.AcquireWriteSlot()          // triple buffer: write / ready / in-use
    writer.WriteGlobals(clock, weather, ledger, threatLevel)
    writer.WriteRoster(colonistRows)            // bounded by colonist count
    writer.WriteAlertInputs(conditionFields)
    foreach (subscription in activeSubscriptions)   // only what the interface asked for
        writer.WriteDetail(subscription)
    frames.PublishReady(writer)                 // one atomic exchange of the ready index

Main thread, once at the top of the frame:

    frame = frames.Acquire()                    // takes a lease on the ready slot
    ... the entire HUD frame reads only `frame` ...
    frames.Release(frame)
```

**Triple, not double.** At 3× speed the simulation may publish between the interface acquiring
a buffer and releasing it. With two buffers the next write lands in the buffer being read.
Three slots with an explicit lease costs roughly 64 kB more and removes the race outright.

**Publishing is frame-driven, not tick-driven.** The main thread raises a flag; the simulation
publishes at the next tick boundary after it sees it. At 3× speed that is about sixty
publishes a second rather than one per tick. Intermediate states are skipped, which is correct
for continuous values such as a mood bar, and is precisely why the event ring exists for
discrete ones.

**Subscription-scoped detail.** The published frame's cost scales with what is on screen, not
with world size. The interface registers a subscription — colonist needs detail, the work
grid, a health tree, research state, room statistics, a trade ledger — and the simulation
fills only what is subscribed. Closing the work tab stops the simulation building a work grid
sixty times a second. Subscriptions travel **up the intent queue**, so there is still exactly
one write channel.

**Handles, never references.** A thing is addressed as `ThingRef { int Id; int Generation; }`.
The interface never dereferences one; it uses it as a key into the current frame. Use-after-free
is therefore structurally impossible rather than merely avoided.

Sizing, with everything open:

| Content | Size |
|---|---|
| Globals: clock, date, season, weather, temperature, about forty ledger counters | ~0.5 kB |
| Roster: 50 × (mood, health, four need summaries, eight status bits, job id, world ref) | ~4 kB |
| Alert condition inputs | ~1 kB |
| Selection detail, bounded at 32 subjects × ~256 B | ~8 kB |
| Work grid, 50 × 25 priority bytes, only when subscribed | ~1.3 kB |
| Health tree for one colonist, only when subscribed | ~4 kB |
| Cell field windows for overlays, current slice ± 2 | ~310 kB |
| **Per slot** | **~330 kB**, dominated by overlays |
| **Three slots** | **~1 MB**, allocated once at startup and never again |

Overlay cell fields are the only large item. If the copy cost shows up in R5 or R6 they move
out of the frame buffers into a separate single-writer ring with per-chunk sequence numbers.

**Events that must never be dropped** travel on a bounded single-producer, single-consumer
ring of small blittable structs, drained once per frame: a bulletin raised, an alert raised or
cleared, a thing destroyed, an intent rejected, a job completed (for audio), a construction
finished. Size it at 4,096 entries. On overflow: drop, increment a counter, surface it in the
developer overlay, and fail a test. A ring that overflows in normal play is a bug, not a
condition to be handled gracefully.

### 2.3 The subject that dies while its pane is open

This case decides whether the contract is real, so it has a specified behaviour rather than an
implementation detail.

1. The simulation pushes `ThingDestroyed { ref, reasonCode }` onto the event ring during the
   tick.
2. The next published frame writes that subject's detail slot with `Valid = false` and a
   tombstone reason.
3. On the next interface frame the view cache marks the handle dead. `InspectDirector` moves
   the pane to **tombstoned**: it stays open showing last-known values greyed out, adds a
   reason line, and **disables every command**. It does not close under the cursor, which is
   both worse to use and destroys the information the player needs.
4. `SelectionDirector` drops the dead handle after a one-frame grace, so the selection-changed
   event can carry the reason. If the selection empties it offers "select next of this kind".
5. Any intent already in flight against that target is rejected by the simulation with a
   "target gone" reason, which surfaces as a transient toast rather than a bulletin.

Four headless tests cover it: the inspector tombstones and disables commands; the selection
director drops dead handles with a reason; an intent against a dead target is rejected with a
reason; and an architecture test asserting the view cache never dereferences a handle.

### 2.4 The write contract

```
record struct Intent {
    IntentKind Kind;        // Def-registered id, stable across versions
    int        IssuedOnFrame;
    WorldRef   Target;      // always carries (x, y, z)
    ThingRef   Subject;
    IntentPayload Payload;  // fixed-size union, or an index into a payload arena
}
```

- **The interface never calls a simulation mutator.** Not once. Enforced by the assembly graph
  in §5 and asserted by an architecture test as well.
- `IntentGateway.Submit` performs **shape** validation only: is this kind registered, is the
  layer in range. Authority validation happens inside the simulation against real state,
  which is the only place it can be correct.
- The simulation drains the queue at the tick boundary **before** systems run, applies or
  rejects, and emits a rejection carrying a machine-readable reason. That reason is how the
  interface says "cannot build here: unsupported span" honestly instead of guessing.
- **Intents flush while paused.** A zero-tick pump drains and applies the queue when the clock
  is stopped, so designations, priority changes and panel actions all work paused. This is
  table stakes in the genre and it is easy to get wrong if pause is implemented as "do not run
  the loop".
- **Intents are logged.** An intent log plus a world seed is a replayable test case. This is
  simultaneously a determinism test and a bug-report format.
- **Optimistic display is allowed only for reversible visuals.** A designator ghost appears
  instantly and is marked provisional until the next frame confirms it. A resource count never
  moves optimistically.

### 2.5 What this demands of the architecture ADR

The interface requires five things of whichever simulation architecture wins Lane D1:

1. A **contracts assembly** of plain blittable types that both sides reference, with neither
   referencing the other.
2. An **end-of-tick-group publish hook** able to write into a preallocated buffer.
3. **Stable generational handles** for every inspectable thing.
4. An **intent drain point** at the tick boundary, functional while paused.
5. The ability to answer **bounded detail queries** without exposing live state.

Consequences, stated plainly so the ADR can cite or reject them:

- **(a) Plain C# simulation objects with Burst and Jobs on hot paths** satisfies all five most
  cheaply and preserves Harmony-style patchability. Favoured by the interface's requirements.
- **(b) DOTS/ECS** satisfies requirements 1 and 3 naturally and makes 2 and 5 a snapshot job,
  which is fine provided the job does not fight structural change. It hurts modding, because
  patching Burst-compiled systems is unpleasant.
- **(c) MonoBehaviour-per-thing is eliminated** by requirement 1 combined with Lane D5. If
  things *are* Unity objects on the main thread there is no off-thread tick and no Unity-free
  contract, and the headless test strategy collapses with it. Either that option goes or Lane
  D5 goes. The recommendation is that it goes.

This is an argument, not a measurement. It follows from two premises — the tick moves off the
main thread, and there is a contracts assembly with no Unity dependency. Drop either premise
and the conclusion goes with it.

---

## 3. Subsystem catalogue

Naming convention. A **Director** owns state and decisions and lives in `Odyssey.Hud`, free of
Unity. A **Presenter** or **Renderer** realises a director's output and lives in
`Odyssey.Presentation`. A **Registry** is data loaded from Defs.

The `z` column means the subsystem must be layer-aware from its first commit, per brief
non-negotiable 2. The last column is the one that matters for testing.

| # | Subsystem | Single responsibility | Pattern | z | Unity-free |
|---|---|---|---|---|---|
| 1 | `HudShell` | Owns the ordered set of surfaces — world layer, docked chrome, floating panels, modal, tooltip, developer overlay — and their sort order | composite | no | model only |
| 2 | `InputRouter` | Resolve one raw pointer or key event to exactly one consumer, with an explicit capture stack | state machine, chain of responsibility | no | **yes** |
| 3 | `PanelDirector` | Floating window lifecycle: open, close, focus, per-type position and size memory, persistence | registry, memento | no | **yes** |
| 4 | `TabDirector` | The main tab bar: which views exist, exclusivity, hotkeys, current tab | registry, state machine | no | **yes** |
| 5 | `SelectionDirector` | The single source of truth for selection: an ordered set of generational handles, multi-select, selection-class resolution, change events carrying reasons | observable aggregate | **yes** | **yes** |
| 6 | `InspectDirector` | Assemble the inspect pane for the current selection: which tabs, which rows, which commands, tombstone state | mediator, strategy per selection class | **yes** | **yes** |
| 7 | `GizmoRegistry` | Map selection class and state to available command descriptors — icon key, tooltip key, enabled or disabled with a reason, intent factory — intersected across a multi-selection | registry, command | **yes** | **yes** |
| 8 | `IntentGateway` | Accept intents, shape-validate, enqueue thread-safely, log for replay | command, queue | **yes** | **yes** |
| 9 | `ToolDirector` | Architect and order tool mode machine: active tool, drag shape accumulation, rotation, cancel semantics | state machine, strategy per drag shape | **yes** | **yes** |
| 10 | `PlacementValidator` | Answer "may this go at (x, y, z)" from the published frame, with a machine-readable reason per cell | specification | **yes** | **yes** |
| 11 | `GhostRenderer` | Draw the active tool's preview as instanced meshes and sparse markers, tinted by validity | — | **yes** | no |
| 12 | `AlertDirector` | Evaluate alert conditions on a staggered slow cadence; sort by severity, dedupe, hold sticky, expose a jump target including its layer | observer, scheduler, registry | **yes** | **yes** |
| 13 | `BulletinDirector` | Own the stack of raised narrative events, their dismissal, and the searchable archive | event store | **yes** | **yes** |
| 14 | `OverlayDirector` | Which overlay channels are active, per-layer dirty regions, value-to-colour mapping, the legend model | strategy, dirty-region tracker | **yes** | **yes** |
| 15 | `OverlayRenderer` | Upload dirty chunks into per-layer textures and draw one quad per active channel | — | **yes** | no |
| 16 | `SliceDirector` | Current slice layer, above-and-below display policy, clamping, jump-to-layer, follow-selection, and the Depth Ruler model | state machine, observable | **yes** | **yes** |
| 17 | `CameraDirector` | Camera position and zoom bands, jump-to, follow, and realising the cut-away for the current slice | facade | **yes** | no; its *commands* are an interface in Core |
| 18 | `TooltipDirector` | Hover delay and dismissal policy, pooled content assembled from registered providers | provider registry, object pool | sometimes | **yes** |
| 19 | `IconRegistry` | Symbolic icon key to resolved handle, with an override chain and a missing-key policy | registry, chain of responsibility | no | **yes** |
| 20 | `TextCatalogue` | Text key to string, with a raw-key debug mode and a locale fallback chain | registry | no | **yes** |
| 21 | `NumberFormatter` and `StringCache` | Allocation-free number and unit formatting; cached strings for repeated values | flyweight, cache | no | **yes** |
| 22 | `CadenceDirector` | Assign work to frequency buckets and phase-stagger by key hash so no single frame carries a spike | scheduler | no | **yes** |
| 23 | `HotkeyDirector` | Binding map with contexts — world, tool, panel, text entry — conflict detection, Def defaults, user rebinds | registry, state machine | no | **yes** |
| 24 | `TimeControlDirector` | Play, pause and speed requests, auto-pause policies, and the guarantee that intents still flow while paused | facade over the gateway | no | **yes** |
| 25 | `ViewCache` and `SimViewFrame` | The substrate: acquire the published frame, hold subscriptions, revalidate handles, expose typed read views | triple buffer, subscription | **yes** | **yes** |
| 26 | `UiDefRegistry` | Load and validate every UI Def kind | registry, validator | no | **yes** |
| 27 | `WorldMetrics` | The only place that knows the cell size and layer height | value object | **yes** | **yes** |
| 28 | `UiBudgetMonitor` | Per-director millisecond and allocation counters, shown in the developer overlay and asserted by tests | decorator | no | **yes** for counters |

**Twenty-two of twenty-eight are free of Unity.** That is the most important number in this
document. Everything except five renderers and the camera can be unit-tested in a container
with a dotnet SDK and no Unity at all.

### 3.1 Four splits that are deliberate

- **Input routing is separate from the surface stack.** "What draws on top" and "who gets this
  click" look like one problem and are not. The second is the biggest bug farm in the genre:
  click-through onto the world, a drag begun on the world and released over a panel, a modal
  that must swallow everything, escape unwinding in the right order. It gets its own pure
  state machine with eight enumerated cases and a test each.
- **The architect tool splits three ways.** The mode machine and drag-shape arithmetic are
  Unity-free and heavily testable. The placement validator is a read against the published
  frame, layer-aware, also Unity-free. Only the ghost preview needs Unity. Kept as one class
  it would be dragged into the presentation assembly and become untestable.
- **Overlays split two ways.** Channel state, dirty regions and value-to-colour mapping are
  Unity-free; texture upload and quad drawing are not. Keeping them together drags overlay
  logic into Unity and invites someone to implement an overlay as UI elements.
- **Alerts and bulletins stay separate.** An alert is a *condition* with a lifetime that is
  continuously re-evaluated and can clear itself. A bulletin is an *event*, immutable once
  raised, dismissed and archived. Merging them produces a system that is wrong for both.

### 3.2 Layer-awareness, and the ones people forget

Fourteen subsystems are layer-aware. Four of those are easy to miss, so they are called out:

- **`AlertDirector`.** An alert with no layer is a dead end on a forty-layer map.
- **`PlacementValidator`.** Support and roof rules are inherently vertical.
- **`TooltipDirector`.** A cell tooltip must state which layer it describes.
- **`GizmoRegistry`.** A command's legality can depend on the layer above, roofing being the
  obvious case.

Per brief non-negotiable 2, `WorldRef` carries `(x, y, z)` from the first commit and **there is
no two-dimensional variant of it anywhere in the codebase, not even a private one.**

---

## 4. Performance budget

### 4.1 The target machine

A 2022 mid-range laptop: a six-core mobile CPU, entry-level discrete or integrated graphics,
16 GB, 1080p. Roughly 60 to 70 per cent of the dev machine's single-thread throughput. **Every
budget below is stated as measured on that class of machine.** Gate dev-machine measurements
at 1.4× stricter.

### 4.2 Frame budget, 60 FPS, main thread

| Consumer | Budget |
|---|---|
| **HUD total, steady state** (ledger, roster bar, clock, alerts, no panels) | **2.0 ms** |
| HUD with one dense tab open | 3.5 ms |
| HUD during an active designator drag | 3.0 ms |
| — of which panel update, layout and repaint | ≤ 1.2 ms |
| — of which director frequency buckets | ≤ 0.5 ms |
| — of which frame acquire and imperative binding | ≤ 0.3 ms |
| Overlay upload and draw, any number of channels | 0.3 ms |
| Simulation-to-scene reconciliation | 2.5 ms |
| Render submission and culling | 5.0 ms |
| Camera, audio, player loop overhead | 2.5 ms |
| **Headroom** | **~4.4 ms** |

On the simulation thread, publishing must stay at or under **0.3 ms per publish**, about sixty
times a second.

### 4.3 Allocation budget

| Situation | Budget |
|---|---|
| Steady-state HUD, per frame | **0 bytes.** Hard rule, asserted by test |
| Active designator drag, per frame | ≤ 4 kB transient |
| Opening a panel, one-off | ≤ 64 kB |
| Gen-0 collections attributable to the HUD in a 60-second idle soak | **0** |
| HUD steady-state managed footprint including atlases | ≤ 12 MB |
| of which the dynamic atlas page | ≤ 8.4 MB, one 2048 × 1024 RGBA32 page at 382 keys and 64-pixel icons. ADR 0007 |

### 4.4 Draw calls and element counts

| Metric | Budget |
|---|---|
| HUD draw calls, idle | ≤ 8 |
| HUD draw calls, two panels and scroll views open | ≤ 16 |
| Overlay draw calls | ≤ 3, one quad per active channel |
| In-world ghost and marker draw calls | ≤ 2, instanced |
| Live elements, idle | ≤ 1,500 |
| Live elements, dense tab open | ≤ 2,500 |
| Realised rows in any virtualised list, regardless of data size | ≤ 40 |
| Hierarchy depth | ≤ 12 |
| Dynamic text labels | ≤ 300, every one fed from a cached string |
| Icon atlas | one 2048 page, **≤ 878 entries** at 64-pixel icons; 382 keys is 43 per cent of it. The second page exists only for a second filter mode and must stay unallocated, so **every** texture under `Assets/Art/Ui/` is point-filtered. Overflow is not a second page: it is one draw call per icon. ADR 0007 |

### 4.5 Portraits are a trap

Fifty live render-textured colonist portraits is fifty texture bindings and fifty render
passes. It breaks the atlas budget, the draw-call budget and the frame budget simultaneously.

Two options. **Composed flat avatars** built from icon layers — silhouette, apparel tint, two
or three status glyphs — living in one atlas with no render passes at all. Or a **single
shared portrait atlas** refreshed at no more than four cells per frame.

**Take composed flat avatars for M2 through M7.** They are cheaper, they match the icon-first
direction, and this is exactly the kind of corner the brief permits cutting on a prototype.
The shared portrait atlas stays documented as the graduation path.

### 4.6 Overlays: why never UI, and what instead

62,500 cells per layer across forty layers. As UI elements each cell carries hundreds of bytes
of managed state, participates in layout, and contributes geometry. That is tens of megabytes
and hundreds of milliseconds. It is not a tuning problem; it is wrong by three orders of
magnitude.

Technique by overlay shape:

| Shape | Technique | Cost |
|---|---|---|
| **Dense scalar field** — temperature, beauty, light, fertility, wealth, roof coverage, power radius | One single-channel texture per layer per channel, 256 pixels square, uploaded per dirty 32-cell chunk, drawn as **one world-space quad** at the slice height, coloured through a 256-entry lookup texture in the shader | 62.5 kB per layer; keep the current slice ± 2 resident, about 1.25 MB; worst-case churn around 200 kB/s of uploads; **one draw call**, about 0.05 ms |
| **Sparse symbolic markers** — designations, blueprints, forbidden flags, reservations | Instanced quads batched per chunk, one material, atlas-driven texture coordinates | one or two draw calls per active marker set |
| **Crisp-bordered regions** — stockpile and growing zones, rooms, power nets | Neighbour sampling inside the dense-field shader first, because blocky borders are genre-appropriate and free; escalate to a per-chunk procedural border mesh only if the owner rejects the look | zero extra draw calls in the first form |
| **Legend, channel picker, value readout** | These genuinely are UI, and they are tiny | part of the HUD budget |

`OverlayDirector` decides channels and dirty regions and is Unity-free. `OverlayRenderer`
uploads and draws. **No overlay code may reference a UI element type, and there is a test for
that.** Cross-reference `docs/design/06-rendering-and-camera.md` when written.

### 4.7 The rules, each with its verification

| Rule | Verified by |
|---|---|
| No LINQ, no string formatting, no string concatenation, no boxing enumeration, no allocating closures, in any method reachable from a frequency bucket | A banned-API analyser with a symbol list scoped to `Odyssey.Hud` and `Odyssey.Presentation`, warnings as errors. Build time, no Unity needed |
| Zero allocation in the steady-state HUD | `Hud_SteadyState_AllocatesZeroBytes`: warm up, run the full director graph 600 times against a fixed frame, assert the allocated-bytes delta is zero. **Runs headless with no Unity.** The single most valuable test in the plan |
| Every displayed number comes from the formatter's cache | `StringCache_FormatsIntTwice_AllocatesOnce`, plus a banned-symbol entry for numeric `ToString` inside `Odyssey.Hud` |
| Pools reach steady state | `TooltipPool_UnderChurn_StopsGrowing` — ten thousand hover cycles, assert the pool plateaus |
| Lists are virtualised | `Archive_With10kBulletins_RealisesUnder40Rows` |
| Frequency buckets are staggered | `Cadence_Over240Frames_NoFrameExceedsKEvaluations` and `EachBucketRanExpectedCount`. Headless |
| Overlays are never UI | `Architecture_OverlayCode_HasNoUiElementReference` — reflection over the assembly's type references |
| The assembly dependency direction holds | `Architecture_Hud_DoesNotReferenceUnityEngine_OrSim`. Headless. The strongest structural guard in the design |
| Every icon key resolves and is atlas-eligible | `IconRegistry_AllDefReferencedKeysResolve` and `AllIconTextures_MeetAtlasRules`. Fails the build if an oversized compressed icon is committed |
| Every interactive element has a tooltip, and a hotkey where applicable | `AllCommandDefs_HaveTooltipTextKey`, `AllTabDefs_HaveHotkeyOrAreExplicitlyExempt`. Mandatory because the interface is icon-only |
| Frame and draw budgets | Performance-testing scenarios over a scripted HUD tour, plus a profiler recorder on the draw-call counter for a runtime assertion and the developer overlay. **Gated on the laptop. Cannot run in the remote container** |
| Publish cost | A microbenchmark in a plain C# harness. The one performance number that could run remotely once a dotnet SDK exists |
| HUD cadence is never keyed to ticks | `Cadence_At3xSpeed_RunsSameNumberOfEvaluations`. At 3× speed the tick rate triples; the HUD's cadence is wall-clock and must not. Only the clock readout tracks game time, and it reads it from the frame |

**Every number in §4 is a budget, not a measurement.** Nothing has been compiled or profiled;
there is no Unity and no dotnet SDK in the container this was written in. The budgets are set
to be slightly uncomfortable on purpose. A budget met on the first attempt was set too loosely.

---

## 5. Assembly layout

```
Odyssey.Core                no UnityEngine   WorldRef(x,y,z), ThingRef, CellRef, Vec2i, RectI, Rgba32,
                                             fixed point, deterministic RNG, collections, StringCache,
                                             WorldMetrics, ICameraCommands, IClock
Odyssey.Defs                no UnityEngine   Def model, inheritance, patch operations, validation, registries
                                             -> Core
Odyssey.Sim.Contracts       no UnityEngine   THE BOUNDARY. SimViewFrame and its writers and readers,
                                             ViewSubscription, Intent, IntentKind, reason codes, event records
                                             -> Core
Odyssey.Sim                 no UnityEngine   The simulation.  -> Core, Defs, Sim.Contracts
Odyssey.Hud                 no UnityEngine   The twenty-two Unity-free directors, ViewCache, registries,
                                             formatting, cadence.
                                             -> Core, Defs, Sim.Contracts
                                             NEVER -> Sim.  NEVER -> UnityEngine.
Odyssey.Presentation        UnityEngine      UI Toolkit views and presenters, OverlayRenderer, GhostRenderer,
                                             CameraDirector, IconAtlasProvider, input adapters, instanced
                                             world rendering.  -> Core, Defs, Sim.Contracts, Hud
Odyssey.Runtime             UnityEngine      Composition root: starts the simulation thread, owns the publish
                                             and drain pumps, wires Hud to Presentation.  -> everything above
Odyssey.Editor              UnityEngine+Ed   SyntyInventory, scene and prefab generators, Def inspectors,
                                             IconPlaceholderGenerator.  -> Presentation, Defs

Odyssey.Tests.Core          EditMode
Odyssey.Tests.Defs          EditMode
Odyssey.Tests.Sim           EditMode
Odyssey.Tests.Hud           EditMode         the bulk of interface testing, with no Unity dependency in the
                                             code under test
Odyssey.Tests.Arch          EditMode         asserts the dependency graph and the banned-API configuration
Odyssey.Tests.Editor        EditMode         references UnityEditor; asserts importer settings and the atlas rules
Odyssey.Tests.Presentation  PlayMode         thin: element existence, virtualisation counts, input routing
                                             smoke tests, performance scenarios
```

**May reference UnityEngine:** `Presentation`, `Runtime`, `Editor`, and the test assemblies,
which necessarily reference the test framework. The constraint is on the code under test, not
on the test host. **May not:** `Core`, `Defs`, `Sim.Contracts`, `Sim`, `Hud`.

Three deliberate consequences:

- **No Unity vector, rectangle or colour types anywhere in `Hud`.** Define our own in `Core`.
  A small tax for a large payoff.
- **Do not let Unity's mathematics or collections packages leak into `Sim.Contracts`.** If
  DOTS wins Lane D1, `Sim` may reference them; `Sim.Contracts` stays plain blittable structs
  and arrays. This is the concrete form of the §2.5 constraint.
- **`Core`, `Defs`, `Sim.Contracts`, `Hud` and their tests compile under a plain project file.**
  Once the remote container gains a dotnet SDK, most of the interface test suite runs there in
  seconds with no Unity and no Synty. That is worth designing for, and it is a small argument
  for installing one sooner rather than later.

### 5.1 The Synty boundary

`Odyssey.Presentation` has **no compile-time reference to any Synty script**. Synty prefabs are
reached through a resource lookup behind `IVisualCatalogue`, which returns a primitive
placeholder when the pack is absent. All interface icons are ours, committed under
`Assets/Art/Ui/`. No Synty texture is ever packed into a UI atlas. If a Synty pack ships
interface icons they may only be consumed as an optional runtime-loaded theme resolved from
under `Assets/Synty/`, and the default build must look complete without them.

`Odyssey.Hud` and every test assembly build and pass on a clone with no `Assets/Synty/`
present. That is a CI gate, not an aspiration, and it follows directly from brief §1 and from
the consequence already recorded in `docs/research/phase0-ground.md`.

---

## 6. Input

**Mouse and keyboard only.** No gamepad, no touch, no controller. Stated as an assumption so
it can be challenged rather than discovered.

**Two bindings changed on 2026-09-16, both owner decisions, both recorded in
`14-hud-layout.md` §7a.** Q and E **rotate freely while held** rather than snapping ninety degrees
a press: this board is layered and its slice is cut at an angle, so the clearest view is rarely one
of four. And the below-slice mode cycle moved from B to **shift-V**, freeing B for the Build
command and pairing the two visibility cycles on one key.

**Hotkey clashes are caught by a test that reads the source**, not by a list somebody
maintains. The rebuilt command bar shipped with five of its eleven hotkeys already bound to camera
keys, and the guard written to prevent that passed, because its reserved list was written from
memory. `HotkeyClashTests` greps the Presentation assembly for every `keys.somethingKey` and
allows a read only where the binding map cannot do the job: Escape (the unwind rule), the capture
loop's `allKeys` (which names no key), and the shift modifiers. Since `HotkeyDirector` landed on
2026-09-17 **no component reads a key by name at all** — every poller asks the director for an
action — so the grep is a regression net for a named read made behind the map's back, and the
reserved set a command cap is checked against is the director's own defaults.

`HotkeyDirector` holds bindings as **actions, not keys**, with in-code defaults (until the UI Def
set exists to generate them) and user rebinds. Each action has a primary and an alternate slot,
because the game ships panning on WASD *or* arrows and slicing on R/F *or* PgUp/PgDn. A key
another action owns is refused and reported, never silently swapped. It runs **one global
context**: no key means two things today, and the contexts below — like `InputRouter` — arrive on
the day one does. Escape and the modifiers are not bindable at all, and the function keys are
reserved for the command bar's panels.

**Contexts** change what a key means: world, tool active, panel focused, text entry, modal.
Conflicts are detected at load and reported, not silently resolved.

`InputRouter` resolves one raw event to exactly one consumer via an explicit capture stack.
The eight cases it must get right, each with a test:

1. A click on empty world selects or deselects.
2. A click on a panel never reaches the world.
3. A drag begun on the world and released over a panel completes against the world.
4. A drag begun on a panel and released over the world does not place anything.
5. A modal swallows every pointer and key event except its own dismissal. **True of something
   for the first time on 2026-09-17**, when U38 built the start screen — until then this case
   had nothing to be true of, because the settings panel is deliberately not a modal and
   nothing else in the game was one either. The mechanism is deliberately not a flag: a
   pickable scrim covers the viewport under the panel, so `HudShell.PointOverUi` answers "the
   interface" everywhere while a modal is up and the camera rig already declines a press it is
   told belongs to the interface. The two always-on contrast scrims are explicitly *not*
   pickable, for the mirror-image reason. `HudShell.Modal()` is the one place a modal is made,
   and it returns the scrim and the panel as a pair so they cannot get out of step — a scrim
   left showing over a hidden panel is a screen that eats every click, shows nothing, and
   cannot be dismissed.
6. Escape unwinds in order: cancel the active tool, then close the top panel, then open the
   game menu. **Built 2026-09-16**, as far as there is anything to unwind: the order is decided
   by `SettingsDirector.Escape`, in the fast tier, and `SettingsPresenter` does what it says.
   The rule lives in exactly one place on purpose. Escape was the designate tool's alone until
   the settings panel wanted it, and two components reading one key would have disarmed the tool
   and opened the panel on the same keystroke — a fault that looks like a flicker and gets
   diagnosed as a rendering bug. `DesignatePresenter` now exposes `ToolArmed` and `PutToolAway`
   and reads no key at all. The last step opens the settings panel rather than a game menu,
   because there is no game menu yet.
7. A tooltip never captures the pointer.
8. Scroll over a panel scrolls the panel; scroll over the world zooms the camera; scroll with
   the layer modifier changes the slice regardless of what is under the cursor.

Layer navigation needs bindings the reference game has no need for, so they are specified
fresh in `docs/design/10-ui-panel-catalogue.md` rather than adapted.

---

## 7. Icons before any icon art exists

The mechanism is that **no code and no Def ever names a file**.

**Symbolic keys.** An icon key is a stable, namespaced string: `ui.alert.starvation`,
`ui.arch.category.power`, `ui.command.deconstruct`, `ui.overlay.temperature`, `ui.tab.work`.
Defs reference keys. Renaming a key is a patchable Def change, not a code change.

**Resolution chain**, first hit wins:

1. A mod or user override. Later-loaded mods win; this is the theming route.
2. Project art at `Assets/Art/Ui/icons/<key>.png`.
3. A generated placeholder.
4. Text fallback: a two-to-four character badge derived from the key, plus the tooltip.

**The placeholder generator** is an editor script. For every icon Def with no art it generates
a deterministic texture: a rounded square whose hue comes from a stable hash of the key mapped
into a colour-blind-safe wheel, with a two-or-three character abbreviation drawn in a built-in
font, at **64 pixels, point-filtered, sRGB, not readable, uncompressed, no mips**, which is what the
dynamic atlas actually requires. The 128 pixels this document used to specify were **not**
atlas-eligible at all: the engine's default maximum sub-texture size is 64. `docs/adr/0007-pixel-art-icon-pipeline.md`
has the arithmetic. Point filtering matters as much as size, because filter mode selects which of the
two atlas pages a texture lands on, so one bilinear placeholder allocates the second page and spends
the whole budget by itself.
Deterministic means stable across runs, so screenshots are comparable week to week and the
owner can evaluate **layout and density** long before any art exists. That is the actual goal.

Generated placeholders go to `Assets/Art/Ui/generated/` and are **gitignored and regenerated on
demand**, not committed, because otherwise every key rename churns binaries. The guard against
them silently going missing is a validation test asserting the generator covers every
unresolved key.

**Debug modes**, toggleable in the developer panel and settable from the command line so
screenshots can be scripted:

- **Missing icons** — anything falling through to a placeholder renders magenta with its key
  overlaid. Makes the art backlog visible rather than forgettable.
- **Icon keys** — draws every key over its icon, so a screenshot is self-documenting. This is
  exactly what is needed to review an interface remotely.
- **Text fallback** — every icon replaced by its label. Both an accessibility mode and the
  test of whether meaning survives without pictures.
- **No icons** — layout only, to catch layout that secretly depends on icon dimensions.

**What an icon-only interface owes the player**, enforced by tests rather than hoped for:
every interactive element has a tooltip carrying name, hotkey and a one-line description;
alert severity is encoded in colour *and* shape *and* stack position, never colour alone; icons
are authored at **64 pixels** and used at **32 and 64**, and at 128 only at a 200 per cent interface
scale. Below 32 pixels an icon is not shrunk, it is replaced by the text badge above. The art is
pixel art (ADR 0007), so a display size is offered only when it is an integer ratio of the source.
That is also why no second authored size is needed, and why the scale slider in §9 D4 steps icons
rather than scaling them smoothly.

**Four eligibility conditions**, because this document previously had two of them wrong and omitted
two altogether. The texture must be **at most 64 pixels** on each axis; **point-filtered**, and
uniformly so across the whole set; **not readable**, since Read/Write being on rejects it outright;
and **sRGB**, since under linear rendering, which URP is, a non-gamma texture is rejected.
Uncompressed and no mip-maps remain the rule but for **fidelity and memory**, not eligibility: block
compression destroys single-pixel edges and hard alpha, and mips of an icon drawn at 1:1 are a third
more memory that is never sampled. A rule kept for the wrong reason is a rule the next reader
relaxes.

**Mod-supplied overrides** arrive through chain step 1 at any size and filter mode, so the resolver
applies the same four conditions at load time and logs and down-ranks anything oversized. Otherwise
one modder's 256-pixel set costs a draw call per icon.

### 7a. Words, not abbreviations, until the art lands (owner decision, 2026-09-16)

This section was written for an icon-only interface with a two-to-four character text badge as its
fallback. The owner's judgement on reviewing it: with no art in the build, **an abbreviation is
unreadable — you cannot tell what anything is.** So the interim policy, which overrides the
icon-only default above wherever the two disagree:

- **Every icon-bearing control draws its full name beside the icon.** The name is the `name` column
  of `docs/design/icon-keys.csv` — one source, so a rename stays a data change. No control is
  labelled by a three-letter contraction of its key.
- **Icon plus label is the default interface mode**, not an accessibility mode. What §7's debug list
  calls "text fallback" is promoted to the shipping default; icon-only stays as the same toggle, so
  nothing is rebuilt when it flips back.
- **The text badge survives in exactly two places**: inside a generated 64-pixel placeholder tile,
  where no word fits, and as the sub-32-pixel rendering rule in ADR 0007. Neither is ever the only
  thing naming a control.
- **B13's exemption is void.** The developer panel was "the one region permitted to use text
  labels"; every region now is.

**Layout is the cost and it is paid now, not later.** Panel minimum widths, the command bar, the
tool palette, ledger chips, roster cards and alert rows are authored against the **longest label in
their set**, not against the icon box, because a layout that only fits while the words are absent
gets redesigned the first time they appear. Where a full name genuinely cannot fit — a roster card's
status strip, a dense chip row — the element gets a wider slot or fewer items per row, not a shorter
word.

**When this reverses.** Once the eight sheets are imported and the mapped keys resolve to real art,
the default may go back to icon-only with labels as a setting. That is an owner call made on a
screenshot, not an automatic switch, and it changes one default value.

**Tests.** The existing "does meaning survive without pictures" test stands. Two join it: every icon
key used by a control resolves to a non-empty name, and every labelled container lays out without
truncation or overflow at its longest label, at 100 and 150 per cent interface scale.

---

## 8. Modding seams

**Def declares, code supplies.** A data-only mod handles the common case; code is needed only
for novel behaviour. Directors are public and unsealed, with a documented set of stable events.

| Extension | Mechanism | Data-only? |
|---|---|---|
| Add a main tab | `TabDef` — icon key, order, hotkey, layout reference, view provider id. Table-shaped content over published fields works from data; anything novel registers a view provider under the id the Def names | yes for table-shaped tabs |
| Add an alert | `AlertDef` — severity, icon key, text key, condition id, cadence bucket, dedupe key, jump-target selector. Threshold conditions over published fields are expressed in data; novel conditions register a condition implementation | **yes** for the common case, and a cheap, valuable win |
| Add an architect category or tool | `ToolCategoryDef` and `ToolDef` — drag shape, placement rule ids, icon, intent template. Placing a Def-defined building is pure data; novel tools register a drag shape or placement rule | yes for placement tools |
| Add an inspect-pane command | `CommandDef` — icon, tooltip key, selection-class filter, requirement ids, intent template, hotkey. Dynamic sets register a command source | yes for static commands |
| Add an overlay | `OverlayDef` — channel id, colour ramp, cell field source id, legend keys. Data-only if the field is already published. A genuinely new field requires a simulation-side field too, which is correct: a new overlay over new data *is* a simulation mod | yes when the field exists |
| Override an icon or theme | An icon Def patch, plus stylesheet overrides appended to the panel | yes |
| Override text | Text Defs and locale tables | yes |
| Change layout | `UiLayoutDef`, our own markup — rows, columns, slots, list templates, mount points — interpreted into elements by a builder in the presentation assembly, subject to the same inheritance and patch operations as every other Def | yes |
| Patch behaviour | Public unsealed directors with stable events: selection changed, intent submitted, intent rejected, alert raised, tab opened | code |

Two requirements this places on Phase 3, both cheap now and expensive later:

1. **The Def system must be interface-aware from the start.** Inheritance and patch operations
   must apply to tab, alert and layout Defs exactly as they do to simulation Defs. Do not build
   a simulation-only Def loader and bolt interface Defs on at M8.
2. **`UiLayoutDef` exists because runtime markup loading appears impossible without asset
   bundles.** That turns a framework limitation into an advantage: our markup is parsed and
   validated in a Unity-free assembly, so **layout definitions become headless-testable**.
   Verify the underlying claim first — it is R11 in `docs/research/g-02-unity-ui-framework.md`
   and takes ten minutes. If a public runtime parser exists, `UiLayoutDef` becomes a thin
   wrapper rather than a parallel format.

One discipline note. Publish an explicit extension-surface document listing what is contract
and what is internal, and **do not mark everything virtual**. The brief says "virtual where
cheap", and a sea of virtual calls in a per-frame path is not cheap. Frequency-bucket entry
points and command factories: virtual. Inner loops: not.

---

## 9. What is settled, what needs a measurement, what needs the owner

### Settled here, from first principles

The Unity-free `Hud` assembly and the contracts boundary, with the dependency direction and
the architecture tests that enforce it. Two channels only, triple-buffered, frame-driven
publishing, subscription-scoped detail. Handle-based selection and the tombstone behaviour.
Overlays as textures and instanced quads, never UI elements. Wall-clock frequency buckets with
hash-phase staggering. The zero-allocation rule and its analyser and test. Icon keys, the
override chain, the placeholder generator and the four debug modes. Defs for tabs, alerts,
architect categories, commands, overlays, icons, text and layout. Composed flat avatars rather
than live portraits for the prototype. The Depth Ruler as the answer to layer question 9.
Above-and-below visibility, decided by the owner and recorded in
`docs/adr/0006-layer-visibility-policy.md`: x-ray by default, with six modes, a depth cap and a
below-slice treatment all shipped and persisted so the default stays revisable by play.

### Needs a measurement

R1 to R12 in `docs/research/g-02-unity-ui-framework.md`. **None can run in the remote
container.** R5 becomes runnable there the moment a dotnet SDK is installed. R11 and R12 are
minutes and should be done first because they shape the design.

### Needs an owner decision

| # | Question | Recommendation |
|---|---|---|
| ~~D1~~ | Icon-only forever, or icons plus a micro-label once meaning proves unclear? | **Answered 2026-09-16: icons plus the full name, now.** The owner reviewed the placeholder interface and could not tell what anything was, so meaning did not prove unclear later — it was unclear immediately. Labels are the default and carry the whole word, not an abbreviation; icon-only remains as a toggle and may become the default again once real art is in the build. Mandatory tooltips and hover-only hotkey hints stand. See §7a |
| D2 | The concept render duplicates the colonist bar top and bottom. Which survives, and what takes the freed slot? | Keep the **top** roster bar; the top edge is otherwise dead space. Bottom-left is the inspect pane. Give the **right edge** to the Depth Ruler and the alert stack |
| ~~D3~~ | Above-and-below policy: ghost the storey above, or hide it? | **Answered 2026-09-15: neither.** X-ray by default, with six modes, a depth cap and a below-slice treatment shipped for playtest. See `docs/adr/0006-layer-visibility-policy.md`. The row keeps its number so D4 to D9 keep theirs |
| ~~D4~~ | Reference resolution, scale policy, minimum supported resolution | **Built 2026-09-16.** 1920 × 1080 reference — literal now, not nominal, since every anchor in the interface specification is a 1080p pixel. The 80-to-150 per cent user control exists as a **ladder of six rungs** in the settings panel's Interface section rather than a slider, because a HUD at a fractional scale puts its one-pixel hairlines between pixels; it works by dividing the reference canvas, so the anchored layout adapts with no other code knowing. The default is chosen from the screen: 100 below 1440p, 110 at 1440p, 125 at 4K, after the owner reported the type reading too small on a 4K panel. **Amended by ADR 0007:** text and padding scale continuously, icons step through 32, 64 and 128, because pixel art at a fractional scale either shimmers or smears. See `14-hud-layout.md` §2.1 |
| D5 | Colour-blind-safe alert palette from day one? | Yes. Severity encoded as colour **and** shape **and** position. Nearly free now, expensive later |
| D6 | Is mod-supplied layout a day-one promise or an M8 one? | Day-one plumbing, because our own HUD is driven by it and therefore exercises it. M8 promise, documented and frozen |
| ~~D7~~ | Cell size | **Answered: 2.5 m x 2.5 m x 3.0 m**, measured from 2,138 Synty prefabs and confirmed by the owner (`docs/adr/0002-cell-size-and-layer-model.md`). The interface was never structurally blocked on it; `WorldMetrics` remains the only consumer. The Depth Ruler, slice control and overlay budgets can now assume 250 x 250 cells over about 40 layers, a 625 m district |
| D8 | Gamepad, ever? | No. But action-based bindings keep the door unnailed at zero cost |
| D9 | Minimap? | Defer past M8. The Depth Ruler plus jump-to-alert covers the "where am I" need on a 250 × 250 map, and a per-layer minimap is a real cost |

---

## 10. Delivery against the milestones

### M0 Foundations — scaffolding, almost no visible interface

The assemblies including contracts, hud and every test assembly. The architecture tests. The
banned-API analyser configuration. The view frame types, the triple buffer, the event ring,
the intent type, the publish and drain pumps against a stub simulation. Cadence, string cache
and formatter, world metrics, text catalogue, icon registry with the placeholder generator and
its validation tests. The budget monitor and a developer overlay showing frame time, tick,
allocations and draw calls. The zero-allocation test harness.

Visible interface: a diagnostic overlay and a speed control. **This is deliberate.** The
allocation rule, the assembly graph and the icon-key discipline cannot be retrofitted, and M0
is where they cost nothing.

### M1 World — the first real HUD

Slice director and the **Depth Ruler**; camera director with jump-to; input router and the
surface stack; a minimal cell inspect pane with no commands yet; overlay director and renderer
with **one** channel, proving the texture path end to end; tooltips; hotkeys; time controls
with the date and weather readout; the resource ledger.

Two things must be proved here because everything after depends on them: the overlay texture
path, and pointer partitioning (R3).

M1 also owes the visibility modes of ADR 0006. Two consequences land here rather than later: a
**translucency path**, because x-ray is the default and hide-and-outline alone cannot express it;
and world geometry grouped so that **roofs and floors are separably cullable** from walls and
props, which `roofs-off` needs. Both are nearly free now and expensive to retrofit.

### M2 Colonists

Selection director, single and multi-select within one class; the roster bar with mood and
health rings and status glyphs, using **composed flat avatars**; the colonist inspect pane with
needs and a gear stub; bulletin director and the archive; alert director with three or four
real conditions, layer-aware with jump-to-layer; panel director.

The tombstone behaviour gets its test here, because M2 is the first milestone where a subject
can die.

*Landed 2026-09-16 (`claude/selection-mvp`): the multi-select half of the selection row —
the ordered set in `SelectionDirector`, the drag box, shift-toggle, double-click
select-similar (screen-space, per this doc's "on screen"), and the roster bar's
click / shift-click / shift-drag-range. Everything else in M2 remains open.*

### M3 Build and dig — vertical-slice HUD complete

Tool director and the Build palette driven by category and tool Defs; placement validator
with layer-aware rules and per-cell reasons; ghost renderer, instanced; the designation overlay
as sparse instanced markers; stockpile zone painting and the zone overlay; gizmo registry and
the first inspect-pane commands.

**What M1 to M3 genuinely need:** slice navigation and the Depth Ruler, selection, the inspect
pane, the Build palette with drag shapes, two overlay families, alerts, bulletins, time
controls, tooltips, hotkeys, the roster bar, the resource ledger, the developer overlay.

**What is catalogued but not built:** the work grid, schedule, assign, animals, wildlife,
research, quests, factions, world map, the health body-part tree, the trade ledger, the social
panel, archive filtering and search, command intersection across a *heterogeneous*
multi-selection (same-class multi-select is enough for the slice), in-world labels,
localisation beyond one locale, and third-party layout patching.

### M4 Environment

Temperature, light and roof overlays. These are new overlay Defs and nothing else, which makes
M4 the test of whether §8's overlay extension point is real. Room inspector, weather readout,
fire alerts with jump-to-layer.

### M5 Sustenance

Growing-zone painting; the bills panel, which is the first serious virtualised-list stress and
the dry run for M7's dense grids; the animals tab; nutrition rows in the ledger.

### M6 Danger

The health tab with a body-part hierarchy; draft and combat mode as an explicit input-router
context; targeting commands; threat alerts; the auto-pause-on-threat policy.

The owner's concept composite shows combat orders as a mode with its own sub-menu rather than
a row of buttons. That is confirmation, and it means combat is designed as an input context
from the start rather than retrofitted.

### M7 Depth

The research node graph, windowed rather than full-screen, following the concept composite's
own information design. Power-net overlay and net inspector; the trade ledger; the factions
panel; the world map view. The work, schedule and assign tabs land here — the dense grids, and
where R1's budget gets cashed.

### M8 Openness

Freeze and document the modding surface; third-party layout patching; a second locale as proof
the text catalogue works; an accessibility pass covering the colour-blind palette, text
fallback and interface scaling; the performance pass measured **on the laptop** against §4; a
scripted screenshot suite using the icon-keys debug mode.

---

## 11. Written against an unknown cell size

Brief non-negotiable 3 says nothing is built until `docs/research/synty-inventory.md` fixes the
cell size. This document respects that and is not blocked by it, because **`WorldMetrics` is
the only subsystem that knows the cell size**, and everything else asks it.

What would change once the size is known:

- The overlay texture resolution per chunk, and therefore the upload budget in §4.6. The
  256-pixel figure assumes a chunk of 32 cells; a different cell size changes the world extent
  in metres but not the cell count, so the figure is likely to survive unchanged.
- The camera zoom bands and the height at which the overlay quad sits, both of which live in
  `docs/design/06-rendering-and-camera.md`, not here.
- The in-world label size for world-space text, which is R8 and is deferred anyway.

Nothing in §2, §3, §5, §7 or §8 depends on the cell size at all. That is by construction.

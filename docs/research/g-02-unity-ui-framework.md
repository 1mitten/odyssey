# G-02 — Which Unity runtime UI framework for a dense colony-sim HUD

## Question

Unity 6.3 offers two supported runtime UI frameworks, and the reference game uses a third
approach. Which should carry a HUD of a thousand-plus elements, a 50 × 25 priority grid, a
50-card roster bar and a ten-thousand-row archive, at 60 FPS and 3× game speed on a 2022
mid-range laptop? What evidence decides it, and what would reverse the decision?

## Findings

### F1 — The three candidates

**UI Toolkit** is Unity's current runtime UI framework: a retained visual tree authored in
UXML markup and styled with USS stylesheets, with built-in list virtualisation, a dynamic
texture atlas, and a runtime data-binding system.

**uGUI** is the older Canvas-based system: retained, prefab-authored, mature, with a large
third-party ecosystem, no built-in virtualisation, and a rebuild model where any dirty child
re-batches its canvas.

**Custom immediate mode** rebuilds every widget every frame from a draw call that also holds
the widget's logic. This is what the reference game does.

### F2 — The mechanisms that actually decide it

| Mechanism | UI Toolkit | uGUI | Immediate mode |
|---|---|---|---|
| Per-frame allocation | Retained; allocates nothing unless text or hierarchy is touched. Setting a label's text allocates the string, which a string cache removes. | Retained and similar, but text mesh rebuilds and layout rebuilds are easy to trigger by accident. | **Allocates every frame by construction.** At a thousand-plus elements this is a permanent garbage treadmill. Decisive against. |
| List virtualisation | `ListView` and `TreeView` built in; fixed-height mode is the cheap path. Covers the archive, the work grid rows, the research list, the trade ledger. | Hand-rolled or a third-party asset. | Naturally virtual, but everything is re-laid-out by hand every frame. |
| Icon batching | A dynamic atlas on the panel settings folds many small icons into one draw call. Eligibility is constrained by size, compression and mip settings, which constrains our icon pipeline — acceptable, since we author the icons. | Sprite atlases, managed manually. | Manual. |
| Change model | Transform and colour changes can be marked as such and skip geometry regeneration; layout and hierarchy changes cannot. Design consequence: animate with transforms and opacity, never with layout. | Canvas sub-mesh rebuild on any dirty child, hence the familiar "split your canvases" discipline. | No batching model beyond what you build. |
| Headless testability | Weak. A visual tree needs a live panel to lay out. Viable under `-nographics` but unproven; see R4. | Equally weak, and adds prefab dependencies. | Worst: logic and drawing are one method, so there is no seam to assert against. |
| Moddability | Best. Stylesheets and markup are text, so icon and theme overrides are trivial. One caveat: there appears to be no public runtime markup parser, so a mod cannot drop a markup file on disk and have it loaded without asset bundles. See R11 and the mitigation below. | A mod must ship prefabs, hence asset bundles built against a matching editor version. High barrier. | A mod must write code and patch draw methods. Works, but the surface is code, so internal refactors break mods. |

### F3 — The decisive point is not the framework

Whichever framework wins draws pixels. The expensive, long-lived, bug-prone part of a colony
sim HUD is the state behind it: what is selected, what the current tool is, which alerts hold,
which layer is sliced, what updates at what frequency, and how any of it is tested.

So the framework should be a **view driver at the end of the pipeline**, and all of that state
should live in an assembly with no Unity dependency at all. That makes the framework choice
reversible in weeks rather than a rewrite, and it makes the majority of the HUD unit-testable
in a container with no Unity present — which is precisely the constraint this project's remote
environment imposes today.

This reframing is the most valuable finding in this file. It means the question "which
framework" is genuinely lower-stakes than it looks, and committing now costs little.

### F4 — Where the recommendation is deliberately against the grain

Use UI Toolkit, but **do not use its runtime data-binding system as the primary update path.**
Bind imperatively from our own view cache inside frequency buckets. Three reasons: our view
layer already knows exactly what changed and when, so the binding system's change detection
is duplicated work; its cost profile is opaque and community reports are uneven; and
imperative binding keeps the decision about whether a widget updates this frame inside the
Unity-free, testable assembly. Reserve declarative bindings for static, low-churn chrome.

Do use the markup and stylesheets for **layout and skinning of the shell** — panel frames, the
tab bar, list row templates — authored visually in the editor. That is what they are good at,
and it lets the owner do visual work without touching C#, which matches the working agreement.

## Recommendation

**UI Toolkit**, ranked first, with uGUI kept warm as a fallback and immediate mode rejected for
the HUD. Immediate mode is permitted for exactly one thing: a developer diagnostic overlay in
non-shipping builds.

Recorded as `docs/adr/0003-ui-framework.md`.

### Flip conditions

Fall back to uGUI for the HUD shell if **any** of these hold, measured on the target laptop:

- **F1.** The dense stress case — a 50 × 25 priority grid, a 50-card roster bar with rings and
  glyphs, and a ten-thousand-row virtualised archive, all open — exceeds **3.5 ms** on the main
  thread, or allocates **anything** per frame after warm-up, and profiling attributes it to
  framework internals rather than our own code.
- **F2.** Roughly two hundred icons cannot be resolved into two atlas pages or fewer,
  producing more than about sixteen HUD draw calls.
- **F3.** Pointer events cannot be partitioned correctly between the HUD and the world for the
  enumerated drag cases without fighting the framework.

Abandon UI Toolkit entirely rather than partially only if two of the three fail. In every case
the Unity-free HUD assembly is untouched, which is the point of the split.

## The experiments, none of which can run in this container

There is no Unity and no dotnet SDK here, so nothing below has been measured. Every number in
`docs/design/09-ui-and-input.md` is a **budget set to be slightly uncomfortable**, not an
observation. A budget met on the first attempt was set too loosely.

| # | Question | Cheapest experiment | Size |
|---|---|---|---|
| R1 | Does a dense HUD hold 3.5 ms and zero per-frame allocation on the target laptop? | Synthetic scene: the stress case above driven by fake data at 60 Hz, with the performance-testing package and an allocation recorder. | **Run 2026-09-16, `HudStressTests`. F1 does not fire: 0.488 ms against a 1.167 ms dev budget, zero bytes a frame, zero collections. Cost tracks labels retexted per frame at about 15 us each, not tree size; ten thousand virtualised rows cost nothing; the grid is cheap and the roster bar is 90% of it. Full numbers in ADR 0003.** |
| R2 | Do about two hundred icons land in two atlas pages or fewer, and at what draw-call count? | Two hundred generated 64-pixel textures in one panel; read the atlas page count and draw stats. | 2 hours |
| R3 | Can pointer events be partitioned correctly, including a drag begun on the world and released over a panel, and a modal that must swallow everything? | A click-through torture scene exercising eight enumerated cases. | 1 day. **Most likely to bite.** |
| R4 | Do view-level tests with a live panel run under `-batchmode -nographics`? | One trivial test asserting an element exists and its layout resolved. | 1 hour. Decides whether any view-level test is CI-able. |
| R5 | What does building and publishing a world view frame cost at 3× speed, and does it stall the main thread? | A microbenchmark in a plain-C# harness. **The only item here that could run in the remote container once it has a dotnet SDK.** | 0.5 day |
| R6 | Dirty-chunk texture upload cost under worst-case churn, for example fire spreading across a layer. | Synthetic churn loop over a 256-pixel single-channel texture; measure milliseconds and garbage. | 0.5 day |
| R7 | Signed-distance-field text cost for about three hundred dynamic labels on integrated graphics; is a bitmap font path needed? | Label-count sweep from one hundred to six hundred. | 0.5 day |
| R8 | World-space UI panels versus instanced billboard glyphs for in-world labels at 50 colonists and 300 animals. | Both, same scene, same label set. | 1 day |
| R9 | Would uGUI actually be cheaper for the dense grid? | Only if R1 fails: rebuild R1's grid in uGUI and compare. | 1 day, conditional |
| R10 | Is a composed flat avatar acceptable to the owner, and what does a live portrait atlas really cost? | Render fifty portraits into one 1024-pixel atlas at four refreshes per frame; measure. | 0.5 day |
| R11 | Is there a public runtime markup parser in Unity 6.3? | Ten minutes in the API reference and the assembly browser. Decides whether our own layout Def format is a parallel format or a thin wrapper. | 10 min |
| R12 | Do the dynamic atlas eligibility rules match the assumptions in F2? | Inspect the panel settings atlas options and try one ineligible texture. | 30 min |

Run order if time is short: R11 and R12 first, because they are minutes and they shape the
design. Then R1, R3 and R4, which are the three that could change the decision. The rest are
tuning.

## Sources

Unity's primary documentation domain was **unreachable from this container**, as were the
reference game's wiki and the Steam community. The following were returned by the search
layer and are cited as retrieved rather than read in full:

- [Unity 6 UI Toolkit: news and updates](https://unity.com/blog/unity-6-ui-toolkit-updates)
- [Panel settings properties reference](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Runtime-Panel-Settings.html)
- [Optimising performance, UI Toolkit for advanced developers](https://docs.unity3d.com/6000.4/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/optimizing-performance.html)
- [Data binding best practices](https://docs.unity3d.com/6000.4/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/data-binding.html)
- [Runtime data binding](https://docs.unity3d.com/6000.2/Documentation/Manual/UIE-runtime-binding.html)
- [Performance considerations for runtime UI](https://docs.unity3d.com/6000.2/Documentation/Manual/UIE-performance-consideration-runtime.html)
- [UI Toolkit at runtime: get the breakdown](https://unity.com/blog/engine-platform/ui-toolkit-at-runtime-get-the-breakdown)
- [Panels](https://docs.unity3d.com/6000.2/Documentation/Manual/UIE-panels.html)
- [Writing editor unit tests for UI Toolkit, community discussion](https://discussions.unity.com/t/writing-editor-unit-tests-for-ui-toolkit/1586050)
- [UI Toolkit versus uGUI, 2025 guide](https://www.angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/)

## Confidence

**Medium-high on the recommendation, low on the numbers.**

High that UI Toolkit is the right first choice and that immediate mode is wrong for the HUD,
because the argument rests on structural properties rather than on measurement.

High that the framework decision is low-stakes given the Unity-free HUD assembly, because that
follows from the assembly graph rather than from any framework behaviour.

Low on every millisecond and byte figure. Nothing has been compiled or profiled. R1 through
R12 exist precisely because this file cannot settle them.

Per-claim flags:

| Claim | Confidence |
|---|---|
| List and tree virtualisation, a dynamic atlas, usage hints and a world-space panel mode exist in Unity 6.x | high, though the primary manual was unreachable |
| Runtime data binding with compile-time property bags exists in Unity 6 | high; the recommendation *not* to lean on it is a judgement call, not a fact |
| There is no public runtime markup parser | **medium-high, and load-bearing.** R11 settles it in ten minutes |
| Dynamic atlas eligibility excludes compressed and mipmapped textures and caps sub-texture size | medium. R12. If wrong, the icon pipeline gets easier |
| View-level tests work under `-nographics` | **low.** R4. The design is arranged so that a "no" costs only the thin view test suite, not the UI test strategy |
| Text rendering cost at three hundred labels on integrated graphics | low. R7 |

## Could not be determined

- Any measured frame time, allocation figure, draw-call count or atlas page count, for any of
  the three candidates. No Unity in this container.
- Whether the flip conditions are close or comfortable. They were set from first principles.
- Whether the dense grid is better served by virtualisation or by a single custom-drawn
  element that paints the whole grid itself. Worth adding to R1 as a second arm.
- Current third-party ecosystem weight for uGUI versus UI Toolkit, which would matter only if
  the fallback is taken.

## Layer questions touched (brief §5)

**Question 9, in part.** The framework constrains how the Depth Ruler and the per-layer
overlays can be drawn. The finding that matters: **overlays must not be UI elements in any
framework.** One layer of the agreed footprint is 62,500 cells, and one element per cell is
wrong by roughly three orders of magnitude in both memory and layout cost. Overlays become
one texture per layer per channel drawn on a single quad, with instanced quads for sparse
markers. That decision is framework-independent and is recorded in
`docs/design/09-ui-and-input.md` and cross-referenced from
`docs/design/06-rendering-and-camera.md` when it is written.

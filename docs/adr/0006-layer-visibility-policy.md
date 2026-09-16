# ADR 0006 — X-ray by default, with the full mode set shipped for playtest

- **Status:** accepted; amended 2026-09-16, when play duly revised it (see the amendment below)
- **Date:** 2026-09-15
- **Deciders:** owner, after judging the policies live in `docs/reference/mockups/hud-v1.html`
- **Supersedes:** the provisional recommendation in `docs/design/09-ui-and-input.md` §9 row D3 and in `docs/design/10-ui-panel-catalogue.md` region A11
- **Related:** `docs/design/10-ui-panel-catalogue.md` A11, `docs/design/09-ui-and-input.md` §§3, 9, 10, `docs/adr/0004-sim-ui-contract.md`, `docs/brief.md` §5 layer question 9 and Lane D3, `docs/design/06-rendering-and-camera.md` (not yet written)

## Context

The world is built of discrete cells stacked into about forty layers, and the camera slices it at one
layer. What happens to the layers *above* that slice was the one question the interface design
refused to settle. `09` §9 listed it as owner decision D3; `10` A11 called it "the one genuinely
unresolved design question"; `g-01` declined to answer it and pushed it to Lane B; and
`docs/reference/screenshots/README.md` recorded that the owner's own concept renders never disambiguate
it, because every one of them is a single-layer roofless cut-away.

The reason it stayed open is that it is two questions wearing one coat. There is a research half,
which comparable games answer in opposite directions and which brief Lane B owns, and there is a
taste half, which no amount of reading settles. Lane B has not run and is in any case blocked in the
remote container, whose proxy refuses the relevant sources. So rather than wait, `hud-v1.html`
implemented all three candidate policies as a live toggle over a layered scene, precisely so the
taste half could be answered by looking.

It was. The owner's answer: **x-ray by default, with hide and ghost selectable**, and, on seeing the
three, a request for the full space of options to be available so it can be playtested rather than
decided once.

That second half matters more than the first. A default chosen by eye in a mockup is evidence, not
proof, and the honest way to hold it is to ship the alternatives and keep the question falsifiable.

## Decision

**The default above-and-below policy is full x-ray: every layer above the slice renders translucent,
with opacity falling off by distance so it fades to nothing after three or four layers.**

**Six modes ship, not three**, selectable at runtime on the Depth Ruler and persisted as a user
setting:

| Mode | Behaviour |
|---|---|
| `hide` | Nothing above the slice is drawn. Maximum clarity, no sense of what is overhead |
| `ghost` | Structural outlines only. The superseded recommendation, kept because it may yet win |
| `xray-min` | One layer above, translucent. Enough to know what is immediately overhead |
| `xray` | **Default.** All layers above translucent, opacity falling with distance |
| `roofs-off` | Geometry above drawn solid **except** roofs and floors, so the player looks *into* the storey above rather than through everything |
| `full` | Everything solid, no cut-away at all. An exterior and screenshot view, and the control case |

Two further axes ship with it, because visibility above is not one variable:

- **Depth cap** — 1, 2, 3, 4 or all layers, with the fade curve applied across whatever is shown.
- **Below the slice** — `dim` (default), `hide` or `normal`. Dimming the layer below where it shows
  through holes was previously bundled into the same recommendation as the policy above; it is an
  independent choice and is treated as one.

**Six presets** name the combinations worth playing: Clear, Outline, Default, Minimal, Roofless,
Architect. One binding cycles them, replacing A11's "cycle above-and-below policy" chord.

`roofs-off` is new to the list and deserves a note: it is what the owner's concept renders actually
depict, and it was never among the three candidates. The concept art was read for its HUD and its
palette, and the thing it was showing about layering went unnoticed until the mode set was widened.

## Amendment, 2026-09-16 — the default follows the depth

The decision above stands: x-ray by default, six modes, two further axes. What changes is that
**the default is no longer one fixed setting of those axes.** It is chosen by how deep the slice
is, and the depth cap — "1, 2, 3, 4 or all layers" in the table below — is no longer part of the
default at all.

| Where the slice is | Above it | Below it |
|---|---|---|
| At or above the surface | every layer, **solid** (`full`) | `belowDepth` layers, dimmed |
| Below the surface | one layer, x-rayed (`xray-min`) | every layer to the floor, dimmed |

Above ground it resolves to `full` and not to `xray`, which is a correction: the rule first shipped
x-raying the stack and the owner's reply was *"this includes everything buildings, stones, rocks and
everything, as I noticed the mining rocks were transparent"*. It is `full` for opacity only — the
active layer stays roofless, because that is its own decision and a default must not reverse it
silently. An explicitly chosen `full`, the exterior and control view, keeps its lid as documented.

The owner asked for it after a playtest, and the reason is the one this ADR already argues from:
*"you need to be able to see within the environment — if there was ever digging introduced into the
game or underground base."* Rationale 2 says verticality has to be legible or the premise fails; a
three-layer cap under an underground base is that failure in the one direction the project is most
about. Rationale 1 says x-ray answers "what is above me, and is it about to fall on me" — which is
the surface question, and above ground it now gets the whole stack rather than four layers of it.

It cost nothing. On the prototype board "every layer above" is four layers, which is what the cap
already drew; underground the range is the same eight layers, shifted down; deeper it is fewer.
See `06-rendering-and-camera.md` §3a for the measurements and for the fade bound that makes
"every layer" finite.

**The trigger was a bug, not a preference, and it is worth recording because the ADR could not have
caught it.** Nothing alive was drawn above the slice — pawns and items were culled at the active
layer by both renderers while the terrain around them was x-rayed correctly. So the policy this ADR
chose had been half-implemented for M1 and the half that was missing was the half a player would
notice first. A mode that draws the storey above is not drawing the storey above if the people in
it are missing.

## Amendment, 2026-09-16 — solid is clickable, a ghost never is

`06-rendering-and-camera.md` §3 point 1 — carried into this ADR by reference and enforced by
`SlicePicker` — said that nothing above the slice may be a pointer target, on the strength of Going
Medieval's misclicking reports. The amendment above made everything above the surface draw *solid*,
and the two together produced a world the player could see and not touch:

> *"On my default depth level I can only select objects/things on my level — I couldn't select the
> stones for mining, for example. I should be able to click on an object in 3D space."*

**The rule is now: a click reaches anything drawn at full opacity, and never anything translucent.**
The band the picker walks is the band the renderer draws solid, and it is asked of the same object.
Underground, where the layer overhead is x-rayed, a click cannot leave the active layer upwards and
the old behaviour is unchanged — which is the case the misclicking reports are actually about, since
what makes a misclick a misclick is operating on a depth *cue*. Layers below, which are dimmed
rather than ghosted, become reachable where nothing in front of them occludes: down a shaft, over a
cliff, through a stairwell.

This does not weaken rationale 1 to 3; it completes them. A player who can see what is over their
head and cannot act on it has been given a picture, and rationale 2's "verticality has to be legible
or the premise fails" was never only about legibility.

**Flip condition F5.** If playtesting turns up the misclick complaint this rule was guarding against
— an order landing a storey away from where it was aimed — the answer is not to re-clip the ray but
to make the depth the click landed on visible before it is committed: the cursor already knows the
layer, and the Depth Ruler is the place to show it.

**Flip condition F4.** If the uncapped stack above ever costs measurable frame time on a tall map,
the cap returns above ground only — underground is where the rule earns its keep, and it is the
cheaper half.

## Rationale

1. **X-ray answers the question the player actually asks.** On a layered map the recurring question
   is not "what does this storey look like" but "what is above me, and is it about to fall on me".
   Hiding the answer optimises for a picture rather than for a decision.
2. **Verticality has to be legible or the premise fails.** The whole project rests on building up and
   digging down. A default that renders the world as a flat storey makes the defining feature
   invisible until the player goes looking for it.
3. **Structural integrity makes the layers above load-bearing information.** Unsupported spans
   collapse. A player who cannot see what sits over their head cannot reason about support, and
   collapse becomes an ambush rather than a consequence.
4. **The mode set costs almost nothing once one translucency path exists.** `hide` is a cull,
   `ghost` is a line pass, `xray-min` is `xray` with the depth cap at one, `full` is no cut-away at
   all. Only `roofs-off` needs anything extra, and what it needs is cheap if known now.
5. **A taste call should stay falsifiable.** Shipping the alternatives converts an argument into an
   experiment. The tuning values in particular — near-layer alpha, per-layer falloff, ghost line
   weight, dim strength — cannot be chosen from prose and should not be guessed in code.

## Alternatives considered

| Option | Verdict |
|---|---|
| Ghost outlines only, the previous provisional recommendation | Rejected as the *default*, retained as a mode. It reads beautifully in a still image and poorly in motion: outlines tell you a wall exists but not what it is, so every question about the storey above becomes a slice change |
| Hide everything above | Rejected as the default, retained. It is the clearest picture available and the right answer for close construction work, which is why it survives as the Clear preset |
| One policy only, chosen now and hard-coded | Rejected. The decision rests on a mockup judgement; hard-coding it would make the cheapest possible experiment impossible |
| Wait for Lane B before deciding anything | Rejected. Lane B is unrun and its sources are blocked from the remote container. The mockup already separated the taste half, which does not need research to answer |

## Consequences

**Good.** Layer question 9 now has a committed answer covering both halves, the UI and the default
render. The interface's densest region, the Depth Ruler, gains its purpose: it is the control surface
for three axes rather than a layer list. And the mockup becomes a tuning instrument.

**Constraining, and this is the consequence that earns the ADR.** X-ray by default is a materially
different *rendering* requirement from hide by default. It decides in favour of per-layer transparent
materials with depth-cued fades, and against a hard clip plane. `docs/brief.md` Lane D3 still frames
that spike as "a cut-away at layer N via clip plane **or** per-layer visibility", and a clip plane
cannot produce any of `ghost`, `xray`, `xray-min` or `roofs-off`. Without this record the benchmark
would measure the one technique the design has just ruled out.

That spike also inherits its worst case from here: sorted translucent geometry across a 250 by 250
footprint with up to forty layers, at 3× speed, on the target 2022 laptop rather than the dev machine.
Transparency does not batch or z-cull like opaque geometry, so this is the plausible failure point of
the whole rendering budget and should be the first thing D3 measures.

**Constraining, second order.** `roofs-off` requires roofs and floors to be separably cullable from
walls and props. That is a constraint on how world geometry is grouped for instanced rendering, and
it is nearly free if known at M1 and expensive to retrofit afterwards.

**Bad.** Six modes and two axes is more surface than one enum: more settings to persist, more
combinations that can look wrong, and a control region that was already dense. Mitigated by the
presets, which mean the ordinary player meets one button, not three sliders.

**Contract change.** `SetAboveBelowPolicy(policy)` was a single enum in A11's `Emits` row. It becomes
a small value of mode, depth cap and below-treatment. The sim-to-UI intent surface is ADR 0004's
subject and is cheaper to widen now than after M1. The `ui.layer.policy.*` icon count rises from
three to six.

**M1 scope.** M1 must ship a translucency path and separably cullable roofs, not merely hide and
outline. `09` §10 is updated accordingly.

## Flip conditions

- **F1.** Translucency misses the frame budget on the target laptop once Lane D3 measures it. Then
  the default reverts to `ghost`, which costs nothing because every mode ships regardless.
- **F2.** Play shows the active slice is hard to read against translucent layers above even after the
  tuning values are dialled in. Same fallback, and `xray-min` is the intermediate landing.
- **F3.** Lane B, when it runs, produces a specific reason one of the comparable games abandoned an
  x-ray default. That is evidence about the research half and should be weighed, not obeyed.

## What this does not settle

The rendering mechanism, which is Lane D3's to benchmark and `docs/design/06-rendering-and-camera.md`'s
to document. Both now exist, written by the simulation line of work, and neither has absorbed this
decision yet: reconciling them is the next step. Whether the tuning values are per-user or per-save,
proposed as per-user. And layer question 9's answer in `docs/design/00-vision.md`, which also now
exists; this ADR is the record it should cite.

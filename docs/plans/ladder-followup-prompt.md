# Prompt for the ladder follow-up session

Paste everything below the line into a fresh session. It is written for an agent that starts cold
and knows nothing except what is in the repository.

Written 2026-09-18, against `main` at `8f32dc3` (the merge of PR #110). Everything below was true
then. **Check the code before trusting any of it** — that rule is in `CLAUDE.md` for a reason, and
this document is exactly the kind that goes stale.

---

You are continuing work on Odyssey, a colony sim in the RimWorld mould with true 3D discrete
layers. Unity 6000.3.24f1.

**Work in a worktree, not in `D:\code\odyssey`.** The owner plays and another agent works in that
checkout. Use `D:\code\odyssey-inspect` (or another worktree), branch off a freshly fetched
`origin/main`, and check `Assets/Synty` is still a junction before running anything that draws
(`docs/lessons.md` — a recursive delete follows the junction and destroys the real packs).

## Read first, in this order

    CLAUDE.md                                   working rules and the current status
    docs/design/21-ladders-and-climbing.md       the ladder system, end to end — this is the one
    docs/journal.md                             the last four entries, all from 2026-09-18
    docs/lessons.md                             things that already cost someone a day

## Position

Ladders were rebuilt on 2026-09-18 (PR #110, merged). What landed:

- **The shaft rule.** A ladder may not be built with a slab or solid rock directly above it, and a
  slab may not be laid directly over a ladder. The shaft cell stays open; the ladder makes its own
  top standable; the colonist steps off sideways onto a **landing** — an orthogonal neighbour of
  the top cell with a real floor. `ConstructionGrid.AllowsLadder`, `LadderArrivesAt`,
  `HasLandingBeside`, `RefreshLaddersAround`.
- **`WorldRenderModel.LadderFacing` owns which face a ladder is on**, and has three readers: the
  mesher, the figure director and the build cursor. A wall wins where there is one; the player's
  rotation decides where there is not.
- **Ladders rotate.** `Building_Ladder` has `rotates` in the Defs *and* in `BuildShapes.Rotates`
  (the HUD's parallel table — see below), the ghost turns with R, and the ghost goes red when the
  order would be refused.
- **The climb pose** hangs the figure on the ladder and lets go over the last quarter of the rise
  (`PawnFigureDirector.ToppingOut`, `ClimbPose.LadderSteppedDrop`).

Gates at the merge: fast tier **685 Sim + 406 Hud**, EditMode **1661 / 1648 passed / 0 failed**,
PlayMode **78 / 73 / 0**. Both content gates pass.

## The three reports to work on

The owner played the merged build. **Beds are confirmed fixed.** These are the ladder complaints,
in their words:

1. *"I can't seem to place the ladder underneath a slab and has to be against the wall."*
2. *"Is it possible to use R to rotate a ladder like the bed and only have valid builds, or display
   red outline?"*
3. *"Sometimes the colonists climb up the ladder where there is wall or slab directly above. It
   should always mount the ladder where there is a gap/space above so it doesn't get up the wrong
   way."*

## What is already known about each

**Report 1 is the rule the owner asked for the day before, and that tension is the whole of the
job.** On 2026-09-18 they chose "refuse the placement" over auto-punching a hatch and over allowing
a dead ladder, and `AllowsLadder` implements exactly that. So the first move is **not** to loosen
it — it is to find out what the player actually wants to happen when they point a ladder at a
finished floor. Candidates, none of them picked: refuse as now but say why; auto-queue a deconstruct
of the slab above; let the ladder be placed and leave it inert until a hole appears. **Interview
before changing anything here.**

The second half — *"and has to be against the wall"* — is **unverified and nothing in the code
obviously requires it**. `Allows` asks `SomethingUnderfoot` (a floor or a blocking edifice below)
and `AllowsLadder` (the cell above open). Neither mentions a neighbour. Find out what was really
seen before believing either half of the sentence: it may be the *drawing* (a free-standing ladder
now faces wherever R last left it, which can look like it is floating), or it may be a refusal with
another cause.

**Report 2 may already be done.** Rotation, the turning ghost and the red refusal all landed in
#110, and the owner may not have had them when they wrote this. **Verify in play before building
anything.** If R does nothing, suspect `BuildShapes.Rotates` — the HUD keeps a parallel table of
footprints and rotatability because it cannot see `Odyssey.Sim.Construction` (ADR 0003), and
`DesignateDirector.RotatableArmed` reads *that* table to decide whether R turns the ghost or raises
the slice. `BuildShapesAgreementTests` walks it against the Defs now, so a disagreement fails the
Unity tier rather than shipping.

**Report 3 has a named prime suspect, and it is a decision that has come due.**

`ConstructionGrid.LadderArrivesAt` says a ladder's top is somewhere to arrive when *any* of three
things is true:

```csharp
if (_grid.HasFloor(top)) return true;     // <-- this one
if (IsLadder(top)) return true;
return HasLandingBeside(top);
```

**The first clause is a deliberate compatibility clause and it is almost certainly report 3.** It
was kept so that every ladder the ruined city stamps, and every ladder in a save written before the
shaft rule, would keep working — which made the new rule a strict superset of the old one and
avoided a save break. The cost is precisely what the owner is now describing: a ladder with a slab
over it still registers its connector, so a colonist still climbs it and still passes through the
floor. New ones cannot be *built* that way; the old ones are still there.

Dropping the clause is the fix, and it is the migration that was deliberately deferred in §4 of
`21-ladders-and-climbing.md`. It is **not free**, and the choice was left to the owner:

- Worldgen (`SurfacePasses`, `ShellTemplate`) stamps ladders under slabs. Those need a hole stamped
  with each ladder, or every city ladder goes dead.
- A save carries edifices and derives connectors, so a loaded colony would silently lose its
  ladders and strand anyone upstairs. M3 is unshipped and no colony outlives the milestone, so the
  recommendation on the day was to accept the break — **but the owner has not confirmed it.**

Confirm that before touching it. Then the shape of the work is: drop the clause, stamp the hole in
worldgen, re-bake `Golden.City` (`ODYSSEY_REGOLDEN=1` prints the replacement values; the constant is
hand-edited in `Golden.cs`), and check the ten-day headless run.

## How to work

- **Phases with hard stops** (`CLAUDE.md`): ground, interview, research, plan, execute. Report 1
  needs an interview and report 3 needs a confirmation; do not run a later phase on an assumed
  answer.
- **Measure, do not read.** This is the third session running where reading the code concluded
  correctly that each line was right and the fault was in the gap between two of them. The bed bug
  of 2026-09-18 survived three careful readings of one method and fell to ten lines of throwaway
  test printing *asked → got*. Write the probe.
- **The two tiers.** `scripts/test-fast.sh` while working (no Unity, compiles neither Presentation
  nor Editor). `scripts/unity.sh test editmode` before committing — it is authoritative, and it is
  the only tier that compiles the picker, the mesher, the figure director and the build cursor.
  PlayMode when presentation changed.
- **Content gates** if any Def or CSV moves: `tools/wiki/build_wiki.py --check` and
  `tools/wiki/emit_labels.py --check`. On Windows call Python by path with `PYTHONUTF8=1` — see
  `docs/lessons.md`.
- **A rule with two owners fails silently.** It has now done so three times in two days: the hop
  price, the ladder's face, and `BuildShapes`. If this work makes a second thing decide something,
  give it one owner and a test that walks both.
- Work reaches `main` only through a pull request with both tiers green.

## The one thing nobody has looked at

The **climb pose numbers** — `LadderReaching` −162°, `LadderPulling` −104°, `LadderElbowBend` −46°,
`ClimbPose.LadderSteppedDrop` 0.46. They came from two reference photographs and nothing else
depends on them. They are the owner's to tune and have never been judged in motion. Ask, while the
ladder is in front of them.

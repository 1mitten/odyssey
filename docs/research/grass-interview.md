# The grass — owner interview

**Phase:** Interview (feature-level, in the shape of `look-interview.md` and `mining-interview.md`).
**Date:** 2026-09-22. **Branch:** `claude/illustrated-look`.
**Conducted by:** Claude Code, thirteen questions in four rounds, at the owner's request:
*"is it possible to make the grass much more dense and bushier but not to affect performance as it
seems sparse and very thin — need to really thicken out but — interview me and clarify — we also
need the grass colour to be lighter green"*.

**Read first:** `docs/design/29-illustrated-look.md` §2 (what is built), `b-botw-grass.md` (what
reads at this camera and what does not).

## 1. The one fact that shaped the answers

Put to the owner before the first question, because it reverses the obvious intuition:

**At 48° looking down, most of what you see is the ground *between* the blades, not the blades.**
So "the field looks thin and dark" is partly a grass problem and partly a soil problem; density and
colour are coupled, and making the meadow denser makes it read greener without touching the colour
at all.

It stopped mattering once the owner chose near-full cover — at 15% soil visible the ground barely
contributes — which is why the answer to "lighten the soil too" could safely be *no*.

## 2. The answers

| # | Question | Answer |
|---|---|---|
| 1 | What should a meadow read as? | **A meadow — mostly covered**, soil breaking through in patches, ~15% visible. Not a continuous lawn, not today's tufts. |
| 2 | Thick grass will hide things on the floor. Which way? | **Grass must never hide anything.** |
| 3 | Lighter green — which direction? | **Spring / lime — yellow-green.** Push towards yellow as well as lighter. |
| 4 | Lighten the soil to match? | **No — grass only.** Dirt, paths and tilled ground keep reading as earth. |
| 5 | How should grass get out of the way? | **A clear ring — bare ground.** Not shortened, not parted: gone. |
| 6 | What clears it? | **Dropped items and stacks, order marks, and a margin around buildings and walls.** *Not* worn paths where colonists walk. |
| 7 | Where does the thickness come from? | **All three, balanced** — more blades per clump, broader blades, and more clumps. |
| 8 | Even meadow, or lush and thin patches? | **Vary it.** |
| 9 | Does bushier mean taller? | **Taller — up to mid-thigh**, about 1.1 m against today's 0.78 m. |
| 10 | How is "not to affect performance" held? | **Push the look, tier it later.** Make it right first; the laptop is a later problem. |
| 11 | Should the variation follow the land? | **Follow the land** — deep near water and in hollows, thin on high ground and by rock. Not noise. |
| 12 | How big is the bare ring round an item? | **Tight — just clear of the item**, about half a metre. |
| 13 | Items and marks cannot be seen by the mesher (§3). Which way? | **Build the clearance texture now**, before the height goes up. |

## 3. The obstacle found mid-interview, and why it got its own question

Question 6's answer could not be built as asked, and the reason is worth keeping.

The grass is strewn by `ChunkMesher.EmitScatter`, which reads the **render mirror** — a cell-indexed
copy of the world rebuilt only when a chunk is dirtied. The mirror carries terrain, floors, zones
and edifices. **It does not carry items or designations**, which live in the per-frame snapshot, and
`GroundScatter`'s own comment already says why it must not: *"pawns and items live in the published
snapshot, not in the cell mirror the mesher reads, and re-meshing a chunk every time somebody walked
across it would be a far worse cure than the disease."*

So the three things in answer 6 split cleanly:

- **Buildings and walls** — static, in the mirror, and the mesher can clear a margin around them for
  nothing. Built as asked.
- **Items and order marks** — dynamic, not in the mirror, and no amount of care in the mesher
  reaches them.

This mattered rather than being a detail, because answer 9 makes it worse: **shipping 1.1 m grass
without item clearing would actively worsen the exact thing answer 2 asked for.** Hence question 13
rather than a quiet substitution, and hence the mechanism goes in first.

**The mechanism is a clearance field**: a small top-down texture over a window around the camera,
into which anything that should push grass back stamps itself each frame, sampled by the grass
shader at the clump's root. The important thing about choosing it is that it is **the same mechanism
three separate wants need** — the clear ring now, the parted grass the owner set aside at question
5, and the worn paths declined at question 6. Building it once buys all three.

Built on the CPU into a `Texture2D` and uploaded, rather than splatted into a render target: the
stamps are a few hundred discs a frame into 64 KB, which is nothing, and it keeps the whole field
**testable in EditMode as ordinary logic** — which a render target would not be.

## 4. Two tensions the owner chose into, recorded so they are not read as faults

1. **Tall grass and a tight ring pull against each other.** At 1.1 m and a 48° camera, a blade two
   cells *behind* a log still crosses in front of it; a half-metre ring around the log does nothing
   about that. The risk was put in the question and the owner chose it anyway, which is a legitimate
   call — the alternative is rings so wide the meadow is holes. **If anything comes back from the
   playtest it will be this**, and the lever is the ring radius, not the height.
2. **"Follow the land" with no noise on top** may read as a contour map, because a rule keyed on
   terrain produces edges where the terrain changes. The third option offered both; the owner chose
   the land alone. Noise on top is the recorded upgrade and is a one-line change if it reads badly.

## 5. What this does not settle

- **Anything about cost on the target machine.** Answer 10 explicitly defers it. Every number this
  feature has is an RTX 5070 Ti at 640 × 480, which measures submission honestly and overdraw barely
  at all, and everything the owner asked for here — taller, broader, denser — is overdraw. The
  density ladder queued with the Look switch is the tier this leans on, and it is not built.
- **Whether any of it looks right**, because the contact sheet still cannot photograph the meadow at
  the play camera (`docs/lessons.md`). The owner's Play remains the only instrument.

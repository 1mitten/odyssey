# B — Walls down: how building games show the inside of a building

## Question

Owner request, 2026-09-24: it is hard to see colonists inside their own buildings. How do building
and colony games let a player see into a multi-storey building — *walls up / cutaway / walls
down* — and what does each draw where a hidden wall was? What is the default and the key, does build
mode switch the view on its own, and what have players complained about? And is there any evidence
on **making walls transparent** against **taking them away**?

One subagent, capped at 12 searches and 8 page reads. It reached the cap. Four of the eight reads
failed: two returned HTTP 402 (the Sims wiki and the Two Point Hospital preferences page) and two
had nothing on the topic. Points drawn from general knowledge rather than a source are marked
**[unsourced]**. Clean room: mechanics and design intent only, in our own words.

## Findings

**The Sims (1–4).**
- **The modes.** Walls Up, Cutaway (the walls nearest the camera are hidden) and Walls Down (every
  wall lowered).
- **The keys.** Home raises the walls and End lowers them; Page Up and Page Down change floor.
- **The default.** Cutaway **[unsourced]**.
- **What a lowered wall looks like.** Walls Down leaves a short stump with a capped top in the wall's
  own finish, roughly a sixth to a quarter of the wall's height **[unsourced; no source gave a
  figure]**. Cutaway lowers the near walls to the same stump.
- **Floors above.** Hidden entirely while you look at a lower storey, not made translucent
  **[unsourced]**.
- **Wall-mounted objects.** Most disappear with the wall, which players ask to change.
- **Its standing.** It is the reference other communities ask for by name: Project Zomboid and
  Going Medieval players ask for "hide walls like The Sims".

**Going Medieval.**
- **The view.** Layers step in half-height increments, and the layers above are ghosted but can
  still be clicked. C toggles roofs; Ctrl+click jumps to the level of what was clicked.
- **No wall toggle.** There is no way to hide walls, and players have asked for one.
- **The complaints.** The blurry upper level blocks the view. Furniture gets placed on the wrong
  level through a ghost that can be clicked. Cellars are hard to see. (`b-going-medieval.md` already
  records the ghosted-but-clickable complaint.)

**Timberborn.**
- **The control.** Alt + mouse wheel sets the highest visible level, with a widget as well. By
  default nothing is cut.
- **What is cut.** Everything above the chosen level is removed, not ghosted **[unsourced]**.
- **The complaints.** The top one is a filter left on and forgotten that looks like a bug
  (`b-timberborn.md`). One player asks for transparency, but for seeing **outside** a cut rather than
  into a room.

**Stonehearth.**
- **The views.** A house has three: complete, cut away to see inside, and walls removed as if not
  yet built. A separate slice control moves between levels.
- **The regression.** Per-floor slicing was lost in one alpha. Players then called placing furniture
  in the middle of a large multi-storey building "nigh impossible".

**Project Zomboid.**
- **The system.** Walls are cut around the player's character, with transparency and dithering on
  top.
- **The reception.** Players call it one of the worst cutaway systems because the layered
  see-through views are too cluttered to read. A mod (PeekAView) replaces it, and a developer has
  said they will revisit it.

**Two Point Hospital / Campus, Planet Zoo, Foundation.** No wall-hiding control was found. A Two
Point Campus forum thread titled "Walls go Down?" suggests there is none. **Dwarf Fortress** shows
one z-level at a time from above, so the question does not arise. Songs of Syx and Prison Architect
are 2D **[unsourced]**.

### Patterns

1. **Remove it rather than make it see-through.** Every complaint thread found objects to
   translucent or clickable leftovers — Going Medieval's ghosts, Project Zomboid's layered dither and
   Stonehearth's all-floors view. The one request for transparency is about the outside of a cut.
2. **Hide the storeys above completely.** The Sims does it, Going Medieval players ask for it, and it
   matches ADR 0006's rule that a ghost is never a pointer target.
3. **Leave a stump where the wall was.** It is the Sims convention, and the only thing that keeps a
   floor plan readable once the walls are gone.
4. **Keep the current mode on screen.** Timberborn's forgotten filter is the failure that shows why.

## Recommendation

- **A toggle, on by default.** Walls lowered to a solid, opaque stump in the wall's own material. No
  translucency.
- **Storeys above the slice.** Built ones are hidden entirely while walls are down; the landscape
  above stays.
- **The stump.** About a sixth to a quarter of the 3 m storey (0.5–0.75 m), capped.
- **Build mode restores full walls and hands back on exit.** Nothing found says any game does this
  automatically, so it is ours to judge in play.
- **The state is shown where the depth control is**, so a lowered view cannot be forgotten.

What was built is design 42 (`docs/design/42-walls-down.md`). The owner chose 0.75 m.

## Sources

- https://answers.ea.com/t5/PC/how-do-you-cut-away-walls/m-p/8662670
- https://www.carls-sims-4-guide.com/controls.php
- https://sims4studio.com/thread/15972/make-certain-objects-visible-walls
- https://modthesims.info/t/527333
- https://steamcommunity.com/app/1029780/discussions/2/3115908227904068442/
- https://steamcommunity.com/app/1029780/discussions/0/4361250086034818336/
- https://timberborn.wiki.gg/wiki/Key_Bindings
- https://steamcommunity.com/app/1062090/discussions/1/715612249814823066/
- https://steamcommunity.com/sharedfiles/filedetails/?id=454789388
- https://discourse.stonehearth.net/t/building-slice-view/15034
- https://steamcommunity.com/app/108600/discussions/0/846243771540634030/
- https://github.com/armakupub/PeekAView
- https://community.twopointcounty.com/two-point-studios/two-point-campus/forums/1-general-discussion/threads/1470-walls-go-down?page=1
- https://wiki.hoodedhorse.com/Clanfolk/Buildings

## Confidence

- **High:** the Sims modes and keys; Going Medieval's ghosting and the complaints about it;
  Timberborn's key and default.
- **Medium:** Stonehearth's views; the pattern of removal over transparency.
- **Low:** the stump's height as a fraction of the wall; whether any game switches the view on its
  own in build mode.

## Could not be determined

- The exact height of the Sims stump, and whether Sims 2 or 3 switch walls on their own in build
  mode.
- Wall hiding in Two Point Hospital, Planet Zoo, Foundation, Prison Architect, Frostpunk, Ixion and
  Clanfolk.
- Whether Going Medieval's current wall is cut at the slice plane.
- Any formal usability study of transparency against removal. Only player complaints were found.

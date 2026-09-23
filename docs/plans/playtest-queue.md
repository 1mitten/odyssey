# Playtest queue

Everything merged or ready that only a person at the keyboard can judge, in one place. It was the
**Waiting on the owner** section of `CLAUDE.md` until 2026-09-19, when that section had reached 134
lines of a file every session reads first, and the audit (`docs/audit/2026-09-19-baseline.md` §6)
found merges running at ten to sixty a day against a handful of playtests. The list is the
constraint, so it gets its own file and its own rules.

**Rules.** A finished piece of work adds one row here (the handover tables in the reply are the
long form; this is the index). A verdict closes the row: move it to *Judged* below with one line on
what was said and where the consequence went (a bug-patterns row, a design-doc amendment, a tuning
change). Keep the open list ordered by **which verdict unblocks the most** — a look that decides a
lever three other lines depend on goes above a nicety. **Prefer closing a row to opening a line**:
when more than about ten rows are open, the next session takes a fix or a measurement, not a new
feature, unless the owner says otherwise.

**The rule is already breached, and the breach is widening.** There are **58 open rows** as of
2026-09-22, counted rather than remembered — 28 on the day this file was written, 29 the day
after, 58 now. Twenty-nine more in three days against a ceiling of about ten, and every one of
them is a change nobody has looked at. That is the finding, not an oversight: the ceiling is
where the list should be, not where it is, and a rule that is quietly wrong on arrival is a rule
the next session learns to ignore.

## Open

- **Does the cold read?** (`claude/temperature-core`, `docs/design/28-temperature.md`.) Open the
  debug menu with backtick and press **Skip one month** four times — that is the row this review
  added, because with only *Skip one day* the season the whole model was built for was sixty
  presses away and so was never going to be looked at. Watch the clock as you go: the outdoor
  reading beside the date is the curve, and Wash → Glare → Rime should feel like a year turning
  rather than a number changing.

  In **Rime**, stand a colonist outdoors at night: the pane should say a freezing tile, the clock
  a freezing outdoors, and within hours her work should slow and then her condition. Then build a
  hut — walls, door, a floor above — put a **campfire** in it (3 wood, furniture beside the bed)
  and skip again: the room should hold comfortable, the pane should say so, and sleeping there
  should rest better than the ground outside.

  Four things only a keyboard decides. Whether **Wash's chill is mild enough** that spring feels
  benign — a wrong answer is spring already wanting a fire, and the bands are in
  `Temperature.xml`. Whether the **campfire feels like a fire or like a radiator** — a wrong
  answer is one fire holding a hall, or a fire in a cupboard not being uncomfortable; the number
  is `heatPerPass` in `Buildings.xml` and the design says what it was tuned against. Whether
  **four hours outdoors in Candle is the right amount of rope** before a colonist is in trouble —
  a wrong answer is either dying while you are reading the pane, or standing in −13 °C all night
  and being fine. And whether **going down is worth it**: dig a cellar and click a tile, which
  should read warmer than the surface in Rime and cooler in Glare — if it reads the same, the
  ground damping is not arriving where a player would ever meet it.
- **Does a wall ever go up around somebody now, and does the fix cost anything to watch?**
  (`claude/build-appearance-and-entombment`, `docs/design/30-nobody-in-a-wall.md`.) Order walls
  across a route colonists are using and let them finish while people are crossing. Three things
  to judge, none of which a test can answer: whether a builder ever visibly **pauses** at the last
  blow and whether that reads as sense or as a stall; whether you ever see a colonist **shoved one
  cell** aside as a wall completes, and whether that reads as "get out of the way" or as a
  teleport; and whether walling a **doorway** in a corridor still lets people through the site
  until it is finished. A wrong answer looks like: an order that never completes because somebody
  is idling in it, or a colonist jumping a cell for no reason you can see.

- **Is the delay building an object gone, or only the glitch?**
  (Same branch, `docs/design/06-rendering-and-camera.md` §6c.3.) The frame after a build cost
  12.53 ms and now costs 1.73; the one-to-three second wait before a wall or door *appears* is a
  separate thing and is **not** explained. The measurement says the wall is drawn on the frame
  after the tick that raised it. **The experiment:** Project Settings → Editor → Shader Compilation
  → turn **Asynchronous Shader Compilation off**, press Play, build a wall and then a door, and
  say what changed. If the delay becomes a brief freeze at the moment of building, it was the
  editor compiling a shader variant for a material the meadow had never drawn, and the fix is a
  warm-up rather than anything in the render path. If the delay is unchanged, the candidate is
  dead and the next move is the developer overlay during a build.
- **Does a pause give you your speed back?** (`claude/session-lifecycle`,
  `docs/design/09-ui-and-input.md` §12.) Space and the pause button used to resume at ×1 whatever
  you were running at, so every pause taken to give an order undid the speed you had just chosen.
  It now returns to the speed the world last actually ran at. **Press 3, Space, Space**; then
  **pause, pick ×2 from the clock, pause, unpause** — that second one should be ×2, because picking
  a speed while paused is a choice and not a toggle. A wrong answer looks like ×1 again, or the lit
  button and the actual clock rate disagreeing.

- **Does the autosave land without being felt, and does one line on the Events panel tell you
  enough?** (`claude/session-lifecycle`, `docs/design/17-start-flow.md` §14b.) Every game day it
  writes the colony over its own save and keeps `<name>-previous.odyssey` beside it, and says so on
  the Events panel. Three things only play can answer. **Is there a hitch on the day boundary** —
  the write is synchronous inside one frame and is not measured, so look at the clock rolling over
  at ×3 on a full colony. **Does the line tell you what you need before quitting**, or do you still
  open the load list to check. And **is the previous copy reassuring or clutter** in that list. A
  wrong answer looks like a stutter every morning, or a load screen you have to read twice to find
  the save you meant.
- **Does the leave prompt ask the right question at the right moment?**
  (`claude/session-lifecycle`, §14a.) Quit to main menu and Quit no longer arm; they raise a modal
  with *Save and leave · Leave without saving · Cancel*. **Quit with a colony you care about and
  read the note under the title** — it names the file saving would write to. A wrong answer looks
  like hesitating over which of the two "leave" rows is which, or being unsure whether "Save and
  leave" is about to overwrite the save you actually wanted to keep.
- **Escape on the main screen, on Load and on the character screen.** (`claude/session-lifecycle`,
  §13.) It used to lay the settings window over the load list. It should now go back one level,
  and do nothing at all on the root column. A wrong answer looks like two screens on top of each
  other again, or an Escape that goes back further than one level.

- **Is the pointer accurate now, and does the crosshair help or clutter?** (`claude/pointer-cursor`,
  `docs/design/28-pointer-cursor.md`.) Two changes under one question. The game now draws its own
  cursor — an arrow, and a **crosshair in the armed order's colour** over the world, reverting to
  the arrow over the HUD — and, separately, **every pick is now resolved after the camera has
  moved** instead of a frame before it, which is the actual candidate for *"doesn't seem super
  accurate"*. **Arm Mine and pan hard with W or the edge while the ghost is up**: that is the
  gesture the old code was wrong on and a still camera never was. A wrong answer looks like the
  ghost still trailing behind the pointer while the board slides — in which case the remaining
  offset is `SlicePicker` marching cell boxes against art drawn off them (section 5), and the next
  move is the overlay that draws the picked box and the raw ray hit together. Also worth one
  glance: whether a crosshair over the board is a help or a busy little thing in the way, and
  whether the arrow coming back over a panel reads as *this click will not reach the world*.

- **Does a quarried field read as a loss or as a bug?** (`claude/floating-crops`,
  `docs/design/22-growing.md` §10.) The reported fault is fixed: mine the soil under a sown cell
  and the seed and the zone paint go with it rather than hanging in the air. What nobody has judged
  is the *silence* — there is no confirmation before the dig and no alert after it, so a player who
  quarries under their own field finds out by looking. **Paint a few cells, sow them, mark the soil
  under two of them to mine, and watch.** A wrong answer looks like you not noticing the field
  shrank until much later, or noticing and thinking the game ate your zone by mistake; either sends
  this to an alert or a confirmation. The other half is the shape left behind: the surviving cells
  of the field stay zoned, so a field with a bite taken out of it should still read as one field.

- **Does the horizon repeat now there are eight kinds of tree instead of sixteen?**
  (`claude/huge-map`, `docs/design/06-rendering-and-camera.md` §6c.4.) The surround costs its
  batch count, and the count was sixteen tree kinds multiplying every spatial cell — so it now
  draws **eight**, which took it from 1.08 ms to 0.58 with all 3,907 trees still standing and the
  meadow frame from 2.71 to 2.14. **A slot is a colour palette over one of two silhouettes**, not a
  kind of tree, so halving them ought to be invisible: look along the rim and at the hills behind
  it, from the play camera and from a low orbit. **A wrong answer looks like a stripe** — the same
  colour of tree recurring at a regular spacing along a ridge, which is the failure this number has.
  If it reads clean, **×4 is measured at 0.371 ms** and is the next rung; if it stripes, 12 is
  untested and sits between.
- **Is vsync on, and what is the frame with it off?** (`claude/huge-map`,
  `docs/design/06-rendering-and-camera.md` §6c.5.) **Answered half of the old "which side of the
  bus" row and raised this one.** Your three 4K shots read frame 16.79 / 15.66 / ~17.5 ms at
  60 / 64 / 57 fps with gpu 8.4 and submit 5.5 inside them — 8.4 + 5.5 is not 16.79, so those
  frames are waiting on something. The overlay now prints `vsync` and `cap` beside the GPU
  figure. **Turn vsync off in Settings → Graphics and read the frame again**: that is the first
  number in this project that would be a real headroom figure. A wrong answer looks like quoting
  fps with vsync on — it hides the spare capacity and the true cost at the same time.
- **Do the tufts cost pixels?** (Same section.) At 640 x 480 they were 7 per cent of a CPU frame
  and dismissed; at 4K the **GPU is the largest single item at ~8.5 ms**, and tufts are
  alpha-tested foliage covering the ground — pixels, not calls. With vsync off, toggle
  **Grass tufts** in Settings → Graphics and read `gpu`. A wrong answer looks like reading
  `frame` instead of `gpu`, which vsync or a cap will flatten.
- **Are the tufts and the surround worth what they cost?** (Same branch and section.) Settings →
  Graphics already carries both switches. Turn the **surround** off on the meadow and look at the
  horizon: 45 per cent of the frame is a large sum for scenery, and the question is whether the
  board reads as a board or as a diorama floating in fog without it. Then the **tufts**, which cost
  a seventh of that. A wrong answer looks like both being turned off and left off — that would mean
  the levers are settings rather than the decoration being worth keeping, and the ranked options in
  §6c.3 should be spent making the expensive one cheaper instead.

- **Is a Huge board more room, or more walking?** (`claude/huge-map`, `docs/design/28-map-size.md`.)
  New Game → the **Size** control now cycles a fourth board, **Huge, 240 × 240 × 16** — twice
  Standard's ground at Standard's depth. Standard is still the default, so nothing changes unless
  you pick it. The simulation is measured and comfortable (0.883 ms per edited cell against
  Standard's 0.298, 69.8 bytes a cell, 90 ms to generate); **what no test can answer is whether the
  board is worth crossing.** Five things to look at, in the order they will hit:

  1. **Zoom out as far as it goes.** `maxDistance` is 160 m and the board is 600 m across, so you
     will see about an eighth of it. This is *already* true at Standard — "see the whole map" has
     never actually worked — but Huge is where it stops being ignorable. A wrong answer looks like:
     you cannot tell where your colony is relative to anything, in which case zoom wants to scale
     with the board and that is its own unit.
  2. **Pan corner to corner without the fast modifier.** 23 seconds at `panSpeed = 26`. A wrong
     answer looks like: you reach for the fast key every time, so the base speed is wrong for this
     board and not just slow.
  3. **Play twenty minutes.** A wrong answer looks like: colonists spend the session in transit and
     the extra ground is a tax rather than a choice — in which case Large (180 × 180 × 24, which
     ships and which nobody has ever played either) may be the size that was actually wanted.
  4. **Look for water.** `streamCount` is a per-map absolute, so Huge gets 5 water bodies on 600 m
     where Standard gets 4 on 300 m. A wrong answer looks like: the board reads as arid, or you walk
     a long way to find a pond.
  5. **Walk the wilderness for a minute.** Every noise period is in cells, not fractions of the
     board, so Huge is *more map at the same grain* rather than the same map enlarged. A wrong
     answer looks like: the same copse and the same hillside keep recurring, which means the
     periods want to scale.

  **Not a playtest item, and please do not treat it as one:** whether the frame holds. That is
  measured — Huge is **7.82 ms against a 5 ms budget** at 640 x 480 on a 5070 Ti, against Standard's
  3.18 and Large's 6.09, and all of the difference is `FrameSection.World`. **Frustum culling in
  `ChunkRenderer.Render` is the named fix and it is HT8's last open decision.** So if Huge feels
  heavy, that is expected and already has a work item; what is wanted from the keyboard is whether
  the board is worth crossing, not whether it is fast.
- **Does the storage pane sit still now?** (`claude/storage-pane`,
  `docs/design/26-storage.md` §12.) Untick every category and the warning appears under the list
  rather than in it, and the list gives up 92 px to make room, so nothing above it moves. **Is
  losing a third of the list worth the message staying put** — or would you rather the band were
  one line, or a colour on the header? And **press Allow all and Clear all with the game running,
  not paused**: until today the pane did not change until you pressed something else, so the
  presses want a look at normal speed as well as at zero. The counts on the category rows are a
  step bigger and set in the mono face; if that was not what "the numbers" meant, say which and it
  is a one-line change.

- **Does a store keep itself to what you asked for?** (`claude/storage-pane`,
  `docs/design/26-storage.md` §11.) Paint a stockpile over ground that already has something on it
  — or set a store that is holding stone to meals only — and the colonists should carry out what it
  refuses and then fill it with what it wants. Three things only a person can judge. **Is the
  emptying quick enough to read as intent** rather than as the colony forgetting about it: it is
  scanned with the loose hauling now, not with the tidying, so it should start within a job or two,
  and if you narrow a filter and wander off and come back to find it unchanged, the pass ordering is
  wrong. **Where the evicted things end up**: they go to the nearest cell no zone claims, which on a
  crowded base may be somewhere silly-looking — if you find yourself hunting for what used to be in
  a store, it needs a dumping zone rather than a nearest-cell search. And **whether a big warehouse
  can empty itself at all**: the search reaches twelve cells, so a rock in the middle of a store
  more than about twenty-four wide has nowhere it can legally be put and will stay put. A store that
  size is exactly what the tool invites you to paint.
- **Does a shelf earn its place?** (`claude/storage-shelves`, `docs/design/30-shelves.md`.) Build one
  from the Build palette's Furniture row — it is wood or stone, five material, and it turns with R.
  Then judge three things a test cannot. **Is eight stacks the right size?** One shelf does the job
  of eight tiles of painted zone; if it feels like it ends the storage game, it is too big, and if
  you find yourself building six in a row, too small. **Is Preferred the right default?** A new shelf
  outranks every painted zone, so the colony starts moving goods on to it the moment it is finished —
  that should read as the shelf working, and if instead you watch haulers cross the map to fill a
  shelf you put somewhere silly, it wants to be Normal. **Can you read what is on one from across
  the room?** The goods stand on the deck at a bit over half size, up to four stacks along the front;
  a wrong answer looks like porridge you have to click to identify, and the fallback is fewer,
  bigger visual slots.
- **Do the order marks still draw?** (`claude/mark-pass-batching`,
  `docs/design/06-rendering-and-camera.md` §6c.1.) Every standing-order mark, cut slab and build
  fill now goes through one instanced call per colour instead of one submission per cell. Nothing
  about the geometry moved, so this should look identical — but an instanced draw through a
  material that does not support instancing **draws nothing at all, silently**, and no test can
  see pixels. Arm mine or chop, drag a box over a dozen trees or rocks, and say whether the marks
  appear as they did. Then let a colonist start on one, so the cut slab shows too. A wrong answer
  looks like: the cells you dragged over look untouched, or the mark is there and the progress
  slab is not.

- **Nobody has pressed Play on the graphics settings** (`claude/confident-rubin-ydvdhc`,
  `docs/design/27-graphics-settings.md`). The Graphics tab now opens in two groups: **Display** —
  VSync, frame cap, render scale, anti-aliasing, shadow distance, display mode and resolution —
  over **Detail**, the six older toggles. Two of these can only be judged in a player build
  (`Build/Win64/Odyssey.exe`), because the Game view is not a window the game owns. What to look
  for: whether **render scale at 85%** is a trade worth having — the world softens, the HUD does
  not, and if it reads as *blurry* rather than *smaller* then FSR is not buying what §2 claims and
  the rung should go; whether the **hitch** on changing render scale or anti-aliasing is a blink
  or a stall, since the tooltip promises a blink; whether **greying the cap behind VSync** reads
  as *explained* or as *broken* — a wrong answer is reaching for the cap, finding it dead and not
  reading why; and whether seven rows in one group is a page or a wall. Also, plainly: after
  pressing every row, `git status` must be clean — a dirty `Assets/Settings/PC_RPAsset.asset`
  means the pipeline copy is wrong, and that is the one failure this design most expects.
- **Storage zones are a thing you can draw, and a colony now starts with none**
  (`claude/storage-zones`, S1, `docs/design/26-storage.md`). A stockpile tool in the orders strip —
  the sixth chip, which needed an argument and got one — paints a zone with a drag; the ground it
  covers is washed towards a blue-grey, on bare earth and on a built floor alike, for no extra draw
  calls. **The first build did not place anything at all** (§2b): a pointer names a surface, and on
  open ground the store lives in the air cell above it, so every cell of every outdoor drag was
  refused. Fixed, and the fix is a question the simulation asks rather than a lift the tool
  performs, because a store on a built floor must *not* be lifted. Four questions only a person can
  answer. **Does the drag land where you meant it to**, indoors on a slab as well as out on the
  grass? **Is the wash the right strength?** — a third of the way to the hue, so stone still reads
  as stone and planks as planks, and it is the one number here with no test behind it. **Does the
  anchor rule feel right?** — a drag begun inside a zone extends that zone, one begun outside
  founds a new one and takes any cells it crosses, and two zones that touch stay two. **And does a
  store read differently from a field?** — the two are the only tools that paint ground, they sit
  next to each other in the strip, and their hues have to be told apart at the play camera rather
  than side by side in a palette.
- **A colony starts with nowhere to put anything** (same branch). `stockpileCells` is nought, so
  until the player draws a store nothing is ever hauled: felled wood and mined stone lie where they
  fell. That is the asked-for behaviour and it is also a **first ten minutes** question — whether
  the colony reads as waiting for an instruction or as broken, and whether anything on screen says
  which. There is no "no storage" alert yet; it is S3.

- **A sleeping colonist is now the size she is drawn** (`worktree-bed-sleep-pose`,
  `docs/design/20-beds.md` §7b). She was being laid down 0.38 m long — the figure director's
  "hip height" is the 0.2 m floor of a clamp on a bone that stands on the floor — so she reached
  1.5 m past the head of the bed and lay inside the mattress. She is 2.49 m now, head on the pillow
  in the first tile, feet 2.28 m along into the second, resting on the bedding rather than in it.
  Two things want an eye rather than a test. **The two supine postures had their arm angles the
  wrong way round**, so a quarter of the colony slept with its arms a half-metre in the air and
  another quarter with both forearms through the mattress; both are measured flat now, and
  and after the owner watched it, **the arms-above-head posture is gone entirely** — it was a
  quarter of every colony, because a posture is a hash modulo four — replaced by another arms-down
  shape with one knee drawn up (§7d). **The two side sleepers no longer float**: the lift had been
  set by whatever hung lowest, which on a side sleeper is a knee propping the body up, so half the
  colony rode 9 to 12 cm above its own bedding. It is set by the trunk now. A sleeper also lies *along* the bed now rather than level across it, which on the
  steepest ground the relief makes was 0.21 m of disagreement at the pillow. Pictures are in
  `Logs/sleep-*.png` (`scripts/unity.sh shot Odyssey.EditorTools.SleepCheck.Run` remakes them),
  including one bed found on a 0.39 m end-to-end slope. **What is left is entirely a look**:
  whether four sleepers read as four people asleep, and whether a third of the mattress lying
  empty past their boots bothers you — the bed is 4.6 m and a colonist is 2.5 m, which the cell
  size fixes. And **a sleeper lies level while the bed under her is draped**, which on
  a slope disagrees by up to 0.21 m at the pillow — measured, left alone, and the numbers are in
  §7b, because fixing it changes how every sleeper is drawn and it should be judged against a
  picture of the one that is now right.

- **The profile pictures, after dark** (`claude/colonist-figures-and-portraits`,
  `docs/design/20-avatars.md` §10.7–10.8, §11). A portrait used to be lit by whatever hour it was
  taken at and then kept for the session, so a colonist generated after dusk had a black card for
  ever; the studio now owns the ambient, the fog, the sky reflection and every other directional
  light for the instant of the shot. Two things a measurement cannot settle. **Is the fixed studio
  light the right light** — the cast is a little flatter than a noon portrait was, because there is
  no sun raking across it, and the question is whether that reads as a passport photograph or as a
  portrait. And **is one light enough**: every colonist is now lit identically from the front left,
  which is consistent and may be dull. Play into the evening, spawn a colonist from the debug menu
  after dark, and say whether the new card is *worse than the daylight ones used to look* or merely
  different. A wrong answer looks like: the cards all read the same and you stop using the face to
  tell people apart.

- **The Work tab opens now — F1, or the Work item on the command bar** (`claude/happy-tesla-2onz0q`,
  PR #145, `docs/design/27-work-tab.md`). A click cycles a priority 1 → 2 → 3 → 4 → blank,
  right-click cycles back, shift-click sets the whole column, and the Simple / Detailed switch is in
  the header. **Note the colony does not start on a grid of threes**: the scenario deals two miners
  at Mining 1 and everybody else at Chopping 1, so the first screen is a division of labour somebody
  already chose. What only a person can answer: whether twenty-two columns with eighteen drawn as
  *not built yet* teach the shape of the game or just cost width (OQ-W2); whether the rotated labels
  at −66° read at a glance or need the head tilted; whether hauling's borderless, flameless column
  reads as *no skill* or as broken (OQ-W3); and whether click, right-click and shift-column are
  enough without drag-paint (OQ-W5). A wrong answer on the first looks like you scrolling past the
  dead columns to find the four that work.

  **Compare against `docs/reference/mockups/work-v1.html`**, which is the same panel in a browser
  with a *Live 4* switch the real one does not have — if the four-column view is the one you want to
  stay in, that switch is the panel's real default.

  **Growing is a live column now**, since PR #119 merged: five of the twenty-two do something
  rather than four, and the hoe has a skill behind it, so a growing cell has a border band and can
  carry flames like the others.

  **Reviewed and corrected before this first play** (`27-work-tab.md` §15). Seven things moved, and
  two of them change what there is to look at: **Simple mode drew nothing at all** — its tick and
  cross were characters neither shipped font has a glyph for, so the whole mode was empty boxes and
  no test could see it — and the panel is no longer a fixed 1,756px that hangs off the right of any
  window narrower than about 1,780. Escape closes the tab now, opening Build puts it away, and every
  cell has a tooltip naming its four signals in words. **So Simple mode is worth a look on its own
  terms**: it has never been seen by anybody.

  **And then the scrollbar went** (§16). The grid pages like the roster instead: **11 work columns
  a page**, two pages, `‹ 1 / 2 ›` in the panel header; **12 colonists a page**, `‹ 1 / 3 ›` over the
  names; wheel turns the rows, shift-wheel the columns. The day is never paged. The panel is a
  constant 1,385 px and nothing about it resizes any more. What only a person can answer: **whether
  splitting the live columns across two pages is a daily annoyance** — Construction, Growing and
  Mining are on page 1, Cutting and Hauling on page 2, and if that turns out to be a constant
  page-turn the fix is reordering `icon-keys.csv` rather than changing the page size. Also whether
  shift-wheel is a gesture anybody discovers. A wrong answer on the first looks like you turning the
  page every time you set a priority.

  **And eleven more things off your second look** (§17). The panel no longer hangs over the map —
  that was a border box: `.panel`'s 12px padding was not in the width I set. **Click a column header
  to sort the colony by that skill**, highest first; hauling sorts by priority because it has no
  skill; an accent rule under the header says which one you sorted by; the refresh button beside the
  Colonist name clears it, and so does closing the panel. **The schedule key is a palette**: click a
  block to arm it, click hours to paint it, click it again to put it down — with nothing armed the
  hours cycle as before. The column icon tiles are gone and the labels sit where they were. Each
  half carries its own title or pager. Simple is the default reading.

  **It is hooked up, and that is now tested rather than assumed** (§18). Setting a column to
  *never* really does stop the work: a colonist told never to cut leaves a marked tree standing.
  **One thing to expect rather than report:** a priority decides the *next* job, not the one in
  hand, so a colonist told to stop chopping finishes the tree she has already started. That is
  deliberate and it is the reference's behaviour. The schedule half still governs nothing at all.

  What only a person can answer: **whether sorting by skill is the thing you actually reach for**,
  or whether you wanted priority; whether the armed block stays obvious enough that you do not lose
  track of what is in your hand; and whether the panel at .995 opacity now sits too heavily over the
  world. A wrong answer on the sort looks like you clicking a header and then hunting for the
  colonist you were already looking at.

- **The bed-owner picker's tick has never been drawn** (already on `main`, PR #141,
  `docs/design/20-beds.md`). Found by the font test written for the Work tab: the mark that says
  *this is the bed this colonist owns* is a U+2713 in Archivo Narrow, which has no such glyph, so
  that column has been blank since the picker was written. It is a drawn tick now. When you play the
  bed-assignment row that is already on this list, **check the list actually marks the current owner**
  — a wrong answer looks like every name in the popover reading the same.

- **Nobody has pressed Play on the order colours of 2026-09-20** (`claude/bed-assign-and-order-colours`,
  `docs/design/16-cancel-and-deconstruct.md` §6b). Every order tool is now one colour on its chip,
  its drag cursor and the mark it leaves: chop green, **mine a deeper blue where it used to be warm
  amber**, **deconstruct orange where it used to be red**, cancel red, build cyan. A deconstruct
  order is a floor plate on the top of the wall now, like a mine order on rock, instead of a
  whole-cell wash. Open questions a picture cannot answer: whether an orange plate on a wall top
  reads as *coming down* at the play camera, and whether mine's new blue and the build blueprint's
  cyan are far enough apart when a colony is half dug and half planned — that pair is the one thing
  the owner's chosen option (*toolbar wins*) traded away, and reversing it is one constant.

- **A new game starts with no beds now** (same branch, `docs/design/20-beds.md` §7a). The owner saw
  the five real starting beds once and ruled them out; the played scenario has none, so the
  colonists sleep on the grass with the slept-on-ground thought until a bed is built. Unplayed
  since. What to look for: whether the first night on the ground reads as *build a bed* or as a
  bug, and whether a built bed is claimed on the first night by whoever reaches it, as §7 says.
  A wrong answer is a colonist still sleeping on the ground beside an unowned bed.

- **A colonist given a bed mid-night has been seen once, and went to work** (same branch, second
  play day). That is fixed — an interrupted sleep resumes, in the new bed for the one given it and
  in the nearest free bed for the one who lost it — and the fix is unplayed. Assign a sleeper's
  bed to another sleeper at night: both should stand, walk, and lie down again, and the third
  colonist should not stir. A wrong answer is anybody picking up an axe before dawn, or the new
  owner lying down in a spare bed while the one she was given stays empty. Still open from the
  first round: whether standing up mid-night reads as *obeying* or as *startling*.
- **Nobody has seen falling items drop and land** (`claude/falling-items`, `docs/design/26-falling-items.md`).
  When ground or a floor slab beneath resting items is destroyed or deconstructed, items drop down onto
  the nearest solid floor below (or despawn if over the void). Presentation accelerates airborne items
  downward quadratically ($t \propto \sqrt{h}$) and triggers `SoundIds.CarryDrop` on touchdown.
  Pawns on deconstructed floors drop without distress (`NoThought`), matching digging beneath oneself.
  Open questions a test cannot answer: whether the landing audio volume and timing feels tactile,
  and whether 0.4s to 0.85s fall duration feels visually satisfying across multiple storeys.
  Unity EditMode **1,916 total, 1,902 passed, 0 failed**; PlayMode **82 total, 77 passed, 0 failed**.

- **Nobody has given a verdict on the growing zone** (`claude/growing-zones`, **PR #119, still
  open**, `docs/design/22-growing.md`). `U46`–`U50`: the carrot crop, the paint-a-zone tool in the
  palette and the orders strip, and sow → daylight-window growth → harvest → auto re-sow. It has had
  **one play day already** — nine looks, six fixes: the sower kneels rather than chops, the zone is a
  near-black whole-tile cover, the ground is the terrain itself re-looked as earth, seeds speckle only
  under the kneel, the big carrot stage arrives at 85% so what looks pickable nearly is, and the pane
  reads `Carrot × 5 — N% grown`. The debug menu's **Skip one day** and **Ripen crops** rows are the way
  to see a harvest without the four-day wait. Open questions a picture cannot answer: whether four
  calendar days to a harvest reads as slow, and whether the interim green tint reads as "growing here"
  or as a texture fault (`22-growing.md` §9). **This row blocks a merge**, which is why it is first.

- **The toast's level number is amber now** (owner, 2026-09-21, `15-skills.md` §8j). The line is
  three labels rather than one — the words, the level in `HudTokens.Warn`, and anything after it —
  split in the model on the `{level}` placeholder so the view parses nothing. Deliberately **not** a
  rich-text tag: if rich text were ever off the player would read the tag itself and neither tier
  could catch it (P10). **Look for:** the number standing out at a glance without the line reading
  as two colours fighting; a wrong answer is the amber looking like a warning rather than emphasis.

- **The experience bar has had its first look and three changes** (owner, 2026-09-21: *"it works
  great but some visual change"*). The bar moved out of the row's bottom edge and **into** the row,
  between the label and the value; it is the needs' **green** now rather than tinted by passion; and
  it is **6 px rather than 3** with 8 px either side. No layout constant moved — the row is still
  19 px and the pane still one height — but the row has a **width budget** now, and
  `HudLayoutTests.TheSkillRowsPartsFitTheRow` holds the name column to 95 px so a future widening
  cannot silently clip *Construction*. `docs/design/15-skills.md` §8i. **What is still unjudged is
  the same list below**, minus the passion tint which no longer exists: whether the creep reads as
  progress, whether four bars on fourteen rows read as "four skills you have", and whether the
  toast reads as good news.

- **Nobody has seen the experience bar or heard a level-up** (2026-09-20, PR #139,
  `docs/design/15-skills.md` §8). A live skill's row now carries a 3 px underline that fills towards
  the next level, tinted by passion; reaching a level raises a **toast** under the alerts and chimes
  `alert-normal`. Four rows are live — Chopping, Mining, Construction, Growing — the last two having
  been greyed out as "nothing is built yet" and "nothing is planted yet" long after both began
  training, which is the bug this work actually found.
  The bar is computed to creep about **1.5 px a second** at level 0 with a minor passion, and a level
  lands after roughly two and a half minutes of solid work; by level 9→10 it is a pixel every seven
  seconds. Open questions a still cannot answer: whether that reads as progress or as a static line,
  whether the passion tint carries the four-fold spread between no passion and a burning one, whether
  an underline on four of fourteen rows reads as "four skills you have" or as a broken grid, and
  whether the toast reads as good news — `alert-normal` was chosen because it is what `Notice`
  already maps to, not because anybody judged it against a level-up.
  **The per-stroke pip was deliberately not built**: the bar's continuous movement is what the
  request was about, and a flash timed to the drawn stroke would couple the pane to the world's
  stroke clock for a decoration. If the bar reads as static, that pip is the first thing to try.
  **Compiled and reviewed on the owner's machine, 2026-09-20** (`claude/skills-review`): EditMode
  1,989 / 1,971 / 0 and PlayMode 85 / 80 / 0, so the bar, the toast row and its click handler do
  build and the shell does frame the stack. The review also found the one fault a test had not:
  the level watch kept its marks across a session boundary, so a new colony's first colonist
  announced a level she was rolled with (§8f-bis). **The fastest way to see a toast is the debug
  menu's Skip one day** — it runs a real day of ticks, so a working colonist levels inside it and
  should raise **one** row per skill, not a stack of them.
  **Merged with `main` on 2026-09-20 and re-run there** (`D:\code\odyssey-review-139`): #119 has
  landed, so nothing blocks this now. EditMode 2,257 / 2,236 / 0, PlayMode 91 / 86 / 0, the same
  as main. **The merge found one fault neither branch could have**: EV's Events panel and
  this toast stack both placed themselves "under the alerts" and solved to the same top, so with an
  event on screen the toast drew over the panel. The toast is last in that column now
  (§8h). **So there is a fifth thing to look at**: fire a supply drop from the debug menu's Events
  tab and then skip a day, and say whether the toast arriving under the Events panel reads as one
  column or as two things fighting — a wrong answer looks like the Events row jumping down the
  screen when a toast lands, which is exactly what the ordering is meant to prevent.

- **Nobody has pressed Play on the pile and bed clarity of 2026-09-19** (`claude/pile-and-bed-clarity`,
  `docs/design/24-pile-reading.md` and `20-beds.md` §13). A wood tile now draws one, two or three
  log bundles as it fills instead of one bundle for ever; the inspect title reads `Wood × 27`
  rather than saying the count in the pane's smallest line; the bed picker marks who sleeps here
  (`✓`), who sleeps elsewhere (`•`) and who has nowhere (blank); and a colonist who reaches an
  unowned bed claims it, **unless claiming it would leave a bedless colonist without one**.
  A **second** click on a cell now looks past what is lying in it and shows the tile, and a third
  comes back round to the thing. And a bed is clickable **where it is drawn**: the picker resolved
  a non-occluding cell at its floor plane while the bed stands 0.70 m up, which at 48° put the
  clickable bed a quarter of a cell behind the drawn one (`docs/bug-patterns.md`).
  Open questions a picture cannot answer: whether three bundles read as a full tile or merely as
  "some wood", whether the title is findable where the state line was not, whether the wordless
  mark column reads or wants its words back, and whether the second click reads as a cycle or as
  the game ignoring the first one. Unity EditMode **1871 total, 1857 passed, 0 failed**; PlayMode
  **82 total, 77 passed, 0 failed**.

- **The carried load has had one playtest and passed** (2026-09-19, `docs/design/24-carrying.md`).
  Three faults found and fixed — swinging arms, a load that would not turn, and both hand-overs
  snapping — and the owner is happy to merge. What is still unjudged: the seven `CarryPose`
  angles; whether a single wood bundle reads at true scale, which is the choice made over
  enlarging it; and whether losing the amount from the arms is missed now that only the activity
  line carries it. **The water case is a knowing placeholder** — the load vanishes as she wades in
  and returns as she climbs out, and whether that pop is worse than the swinging bundle it
  replaces is the question it exists to ask.
- **Nobody has played the new starting kit** (2026-09-18, `docs/design/22-starting-kit.md`): 36
  meals, no scrap, 150 each of stone and wood. Five integers in one method with nothing deriving
  from them, and explicitly invited tuning. The question a test cannot answer is whether three or
  four days of food reads as tension or as anxiety — and if the colony is starving before anybody
  has built anything, the answer is more meals rather than faster growing.
- **Nobody has renamed a colonist at the keyboard** (`19-world-setup.md` §10). Whether clicking the
  name is a discoverable way to rename somebody without a pencil or a caption, and whether sixteen
  characters is the right ceiling — it was picked for the roster strip, which is the narrowest place
  a name is drawn, not for the card where it is typed.
- **Nobody has pressed Play on the look work.** Every judgement about the day cycle, the golden
  hour, the hill wood and the colonist palette comes from contact sheets and `FrameTimeTests`.
- **The avatars and portraits are photographed, not played** — whether a 128 px render reads at
  26 px on a roster card, whether head-bone framing suits all 61 bodies, whether the one key light
  wants a fill.
- **Nobody has pressed Play on the build botch**, and its two integers are invited tuning: a novice
  botches about one wall in seven, a level-3 builder never does. Nothing announces a botch, so a
  wall that takes twice as long looks like a slow colonist.
- **Nobody has pressed Play on the orders strip, the armed banner, the cancel tool, right-click, the
  debug menu, or the interface work** (roster card, docked bars, popovers, Skills tab, Keys and
  Audio tabs). All are measured; none has been looked at. Open questions a picture cannot answer:
  whether a 34 px button is the right size, whether the strip wants to sit lower, whether the
  six-pixel right-click threshold is right, and the **20% coverage ceiling**, which is the owner's
  to reverse.
- **A floor is drawn as a sheet now, and the lip is the thing to look at.** The dotted line along
  every floor seam was the tile's rim tying with its neighbour's top face on depth
  (`docs/bug-patterns.md` P8), and it is gone — measured, 470 → 16 artefact pixels at the play
  camera. The price is that a floor **over open air** has lost its 101 mm of drawn thickness, so a
  balcony or a roof lip with no wall under it may read as paper seen edge-on. A floor on the ground
  had 93 of those millimetres buried and is unchanged. If the lip is wrong, the fix is a fascia on
  the face rather than a thicker plate.
- **Nobody has pressed Play on the three HUD fixes of 2026-09-18.** The colonist pane is one height
  on every tab now, so Needs sits in a box sized for Skills with about ninety-eight pixels of slack
  below it — whether that reads as stable or as broken is the question, and if it is broken the
  answer is more needs rather than a shorter box. The four empty Build categories are dimmed:
  whether they read as "coming later" or as broken tiles. And the palette now closes the instant a
  selection is made, which no still can tell you is decisive rather than startling — if it startles,
  the cheapest alternative is closing it only when the pane would actually overlap.
- **The coloured wood has had two playtests; the rounds since have not been played** — the cherry
  and flame canopies read as scarlet at the play camera and are the first to veto, and the measured
  tenth-of-a-stop the new shader costs was deliberately not papered over with a gain.
- **The climb has been drawn three ways in two days and only the third is unseen.** A parabola over
  the lip read as jumping; strides up the treads read as jolting; it is now the ramp surface itself,
  sampled where the figure stands, at 9.9–12.3 mm a frame (`docs/design/22-terrace-steps.md` §4b).
  If anything still jitters, the one junction left is where the ramp's 0.62 m/s meets the flat top's
  1.5 m/s — one 30 mm frame — and the honest fix there is a slower flat, not a smoother curve.
- **The whole terrace climb is now eight seconds and nobody has watched one.** Two steps of 240:
  flat ground at a walk, 3.9 m of ramp at 0.62 m/s in four strides, then the top at a walk again
  (`docs/design/22-terrace-steps.md` §4c). The lever is `MoveCost.JumpUp` — the slope cost, the
  pacing weight and the stride count are all derived from it. Also unwatched: colonists preferring
  a flat detour to walking along the foot of a terrace, which is the deliberate consequence of
  pricing that cell as a slope.
- **The new hop wants the same look the old one just failed.** `MoveCost.JumpUp` went 135 → 240 and
  the motion became an arc (`docs/design/22-terrace-steps.md` §4b) because a colonist climbed a
  terrace at 1.74 m/s against a walk's 1.50. The open questions a still cannot answer: whether 4.0 s
  to get up one block now reads as effort or as **stuck** — the exact failure of the 270 this
  replaces — whether 0.35 m over the lip is a hop or a hurdle, and whether holding the gait through
  the step shows as the feet sliding during the half-second gather. If it reads as stuck, the pose
  is the thing to look at before the price.
- **Nobody has seen a colonist climb a ladder since the pose was written for one.** Four angles
  branch on a ladder against a rock face and all four are invited tuning
  (`docs/design/21-ladders-and-climbing.md` §3): whether they read as a ladder rather than a shrug,
  whether a step of 0.46 of a leg is too big at the play camera, and whether arriving in an open
  shaft cell and stepping sideways looks like arriving or like hovering.
- **Nobody has heard the alert chimes.** Five of the owner's recordings replaced the synthesised
  two-note sine on 2026-09-19, loudness-matched to −18 LUFS. Two of them play today: `alert-normal`
  when an idle-colonists row appears, `alert-negative` when a starving or breaking one does.
  Questions a measurement cannot answer: whether −18 LUFS is right in a quiet room against the
  ambience bed and the work sounds, and whether the raid siren at 9.54 s and the joining fanfare at
  5.77 s are alerts or cutscene stings — the bake deliberately did not shorten them
  (`docs/design/24-alert-sounds.md` §6). **The chime had effectively never fired before this**: the
  audio side carried a starvation threshold on a scale a hundred times out, so there is no prior
  impression to compare against.
- **Nobody has heard the title screen.** A 151-second loop fades in over eight seconds on the
  main screens, fades out over four when a world arrives, crosses with the outdoor bed's own
  four-second arrival, and never plays in a colony. It is a **sub-bass drone** — almost everything
  below 500 Hz — so it will read completely differently on laptop speakers from headphones, and
  that is the first question to ask if the level seems wrong. Open: whether Volume 0.18 survives
  real speakers, whether eight seconds of arrival is patient or broken, and whether the four-second
  hand-over is seamless or a hole (`docs/design/17-start-flow.md` §12).
- **Nobody has heard a colonist pick anything up.** One recording became two sounds on
  2026-09-19 — `carry-lift` resampled up and brightened, `carry-drop` down and dulled, three takes
  each — and they fire on every leg of every haul, which makes the mix the whole question. They
  sit at Volume 0.40 against the axe's 0.85 and die at 120 m against its 200. Open: whether 0.40
  survives six haulers rather than one; whether lift and drop are actually told apart at the
  default camera height, which is not the same test as telling them apart side by side; and
  whether the drop wants to be heavier still (`docs/design/24-carrying.md` §12).
- **Nobody has seen the falls move.** Whether the streaks read as falling water or as a pattern
  sliding down a pane cannot be judged in a still, and stills are all anybody has looked at.
- **The shallow stream reads pale at the play camera.** Raising the alpha is the obvious fix;
  darkening the submerged bed is the better one. It changes water that has already been judged.
- **The shoreline jitter is unconfirmed either way.** The obvious explanation was falsified by
  measurement and the remaining candidate — the footing hand-over — is now continuous, so the
  report stands until somebody walks a colonist along a shore and looks
  (`docs/design/20-swimming-and-water.md`).
- **The bank ramps draw as large diagonal sheets standing proud of the meadow** — the owner's
  "diagonal wedge", proved to be `BankLayout`/`BankMesh`'s own design and not water. The most
  visible thing on the board, left unfixed on purpose because it wants its own look at.
- **Marsh reads as a sandy bank** — re-tint it greener or rename it.
- **The audio listener is on the camera**, 32–160 m up, while the catalogue authors ranges as ground
  distances. Either move the listener to the camera's focus or re-author the ranges.
- **Icon art:** nineteen keys draw real art; the rest draw an outlined square. The HUD draws icons
  at 16, 17 and 30 px while ADR 0007 says not to draw pixel art below 32 — measured, 30 px reads,
  17 px loses the grooves, 16 px goes to noise.
- **The 29 proposed proper nouns** in `docs/design/proper-nouns.csv` await approval or veto.
- ~~**The Synty junction chain wants inverting.**~~ **Done, and verified 2026-09-23.** The real
  copy is `D:\code\odyssey\Assets\Synty`, a real directory, and every worktree junctions straight
  to it in one hop; `D:\code\odyssey-audio` no longer exists. The standing rule is unchanged and
  is in `docs/lessons.md` — do not prune a worktree without checking for reparse points first.


## Judged

Rows move here with the date, the verdict in one line, and where the consequence went.

| Judged | What | Verdict | Consequence |
|---|---|---|---|
| 2026-09-20 | **Head turning and gaze** (PR #128, `docs/design/23-head-turning-and-gaze.md`) | working — owner: *"gaze … is all working now"* | none; the design doc stands |
| 2026-09-20 | **The flush selection cursor** (PR #127, `docs/design/23-flush-selection-cursor.md`) | working | none; the design doc stands |
| 2026-09-20 | **The sight fade leaving water, banks and marsh whole** (PR #123) | working — the exemptions read as deliberate | none |
| 2026-09-20 | **Soft crowd avoidance and sub-tile lateral steering** (PR #134, `docs/design/25-pawn-steering.md`) | working — the sidestep reads as courtesy, not drift | none; the design doc stands |
| 2026-09-20 | **Eight-directional diagonal movement and the strict corner rules** (PR #132) | working | none |

These five were merged on 18–19 September with no row here — the list was moved out of `CLAUDE.md`
verbatim and inherited its gaps. They were named in a *Not yet listed* table for one day and the
owner closed all five at once. **A pass on the look is not a pass on tuning that was invited by
name**: where one of those five carries a number the owner was asked to judge, the design doc still
holds the invitation.

_The player build (PRs #130/#131) never had a row and does not want one: a build that runs is
proven by the smoke run, not by a look._

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

- **Do the frogs read as frogs, and are they where the water is?** (`claude/frog`, design 30 §8.)
  New game on the meadow and look along a stream or a pond: there should be groups of three to five
  bright emerald, red-eyed frogs on the banks (about 0.9 m long since the owner's first ask), and none
  in dry fields. Watch one move: it should crouch, leap about 1.3 m, land and sit, rather than
  glide, and when several in a group hop at once they should go different ways. Open Debug → Spawn → **Spawn culvert frog** away from
  water and watch it hop back to the nearest bank. Make it rain (Debug) and see the frogs stay out
  while the hogs go under the trees. Wrong answers: a frog that slides along the ground between
  hops or lurches backwards at the start of one; frogs far from water; a frog too small to find from
  your normal camera height; a green that still sinks into the grass; or frogs you would rather see swimming, which
  is a cost stated in §8b.
- **Polish batch: trees, drops, the fire, the roster, the pane, the fade** (`claude/polish-batch`,
  designs 20 §14, 23 §11, 31 §20, 25 §10, 14 §10–§11, 38 §27).
  - Gather six or more colonists round a campfire at night. Each should be on their own tile, with
    the inner ring seated facing the fire. Wrong: two figures clipping into each other, or the
    second ring reads as a queue.
  - Halt two drafted colonists on one tile. Wrong: they jitter or swap places when a third
    arrives.
  - Single-click a roster card, then double-click it. Wrong: the single click still moves the
    camera, or 14 m is too close or too far.
  - Read the colonist pane over bright grass. Wrong: the dim grey lines are hard to read.
  - With nothing selected, walk a colonist behind trees. Wrong: the trees still fade.
  - Fire several supply drops into woodland. Wrong: a pallet lands in a trunk.

- **Is the lit-butterfly night right now?** The first look said the day was *"superb"* and the night's halos were *"big glowing saucers"*; they are gone and each butterfly is lit in its own colour instead (design 52 §5a). (`claude/ambient-butterflies`, design 52.) New game on a meadow in Larkspur or Tansy; watch the grass near the camera at the default zoom, then walk a colonist through them, then press the debug menu's new **Skip to night** (22:00, fully dark), then zoom right out. Wrong answers: a lit butterfly reads as a coloured blob rather than a wing; the colours look flat rather than glowing (the lever is `ButterflyPalette.WingGlowCeiling`, held at 1.0 against shimmer); they shimmer or vanish as they move; zoomed out, they are too small to see or too many; a colonist walking through does not scatter them.
- **Does a raid read as a band that stalks and then strikes?** (PR #233,
  `claude/sharp-lamport-8q5u4h`, design 55, reviewed 2026-09-26 §15.) Debug → Events → Raid:
  size 20, Mixed, and run at top speed through the gathering (2–4 in-game hours). Wrong answers:
  - you do not hear the war horn, or it plays over itself and feels like a cutscene (18 s);
  - 2–4 in-game hours at the edge feel like a wait rather than suspense;
  - clicking the red *Raid* alert does not put the camera on the band;
  - once they reach the colony, raiders walk back and forth between the fire and a colonist rather
    than fighting (the fault fixed in §15 #2 — say so if it is still there);
  - a band of 150 in the far form reads as a crowd rather than a band.

  Then fire 200, and 200 again while the first stands: the second press should say under the
  row that it will not fit, with the room left. **Five armed colonists who are not drafted lost to eight raiders in both
  soak seeds**: say whether that is the game you want.

- **Does changing a graphics setting still hitch, and is the board seen catching up?**
  (`claude/mesh-cost`, design 38 §26.) Open Settings → Graphics and flick the Grass ladder, then Ground
  relief, with the camera over thick meadow; then dig out a row of cells. A board-wide re-mesh now
  spends at most about 2 ms a frame and takes about a third of a second to finish. Wrong answers: a
  visible hitch when a setting changes, or chunks of old grass visibly swapping to new a few at a time.

- **Do the sandbags look like sandbags, and does cover change a fight?** (`claude/cool-darwin-akh02q`,
  PR #232, design 53 §7a-bis and §13; worktree `D:\code\odyssey-cover`.) Build → Security: drag a line
  of **Sandbags**, then an L and a T. The barricade chip is dim again, beside the turret and the trap.
  Walk a colonist across: she climbs over slowly and never stops on top. Draft two colonists with
  pistols behind the bags and use the debug menu's *Spawn pistol bandit* ten cells away: they crouch
  behind the bags, the bandits step behind anything nearby before firing, *Cover* floats over a bag
  that takes a bullet, and a bar appears over each one hit. Select a drafted gun colonist and hover a
  bandit: the readout beside the pointer gives the chance and what moved it. **Wrong answers:** the
  wall reads as a striped block rather than bags (the bags too small, or the grooves between them
  lost); the line visibly restarts at every cell; a corner with a gap or a bag poking through; the
  colour reads as stone or as plastic; bags so tall the crouched colonist is hidden or so low she
  towers over them; a colonist standing on top of the bags; bandits that shuffle about without
  shooting; or a readout that sits under the pointer or flickers.
- **Does the new bill list read at a glance, and is anything on it in the way?** (`claude/bills-pane`,
  design 49, stacked on `claude/cooking`.) Click an **Electric Cooker** with no power: the pane is
  wider (680), a warn strip reads **No power** with a green **Switch off** beside it, and the tile's
  own Switch row is gone. Add three bills with the full-width cyan **Add a bill**; cycle one's mode
  (the cell with the round arrow), step a target with the red minus and green plus (shift for ten),
  pause one (it fades, its buttons do not), move one with the stacked arrows, bin one. Then a
  campfire, which has no strip. A wrong answer is a count or a number that is not centred, a row
  whose name is cut off at a length you would use, the pane jumping under the pointer while you
  press Add or the bin, or the reorder pair being too small to hit. Say whether the empty status
  line on a working bill looks unfinished.
- **Does a cook turn carrots into meals from a bill, and does it read as cooking?** (`claude/cooking`,
  design 48 K1.) Build an **Electric Cooker** (Build → Production) on a powered line, or use a **campfire**.
  Click it: the pane shows **Bills** with *Add a bill*. Add one, stock carrots, and watch: a cook
  fetches three carrots, stands at the station tossing a frying pan, and puts a meal down. Try the
  mode (press it: until you have → make → forever), the ‹ › target, suspend, reorder and remove;
  switch the cooker off mid-cook and back on. A campfire meal also costs one wood. A wrong answer is
  a bill that does nothing, a pane that jumps when you add a row, a cook who swings at the stove
  instead of tossing a pan, or colonists who still reach for carrots with a meal in the store.
  The cooker is the POLYGON Shops stove: say whether it is the right size, and whether it faces the
  side the cook stands on. **Known:** the pan shows no food going raw → cooked yet, and the meals are
  Sci-Fi food trays.
- **Can you click a campfire a terrace above you while building?** (`claude/terrace-click`,
  design 42 §3a amended.) On a lower terrace, open Build and raise a campfire on the terrace one step
  up, then click it with the palette still open: its pane should open, with *Make this the hearth*.
  Click one on your own terrace near its edge too. Then turn Walls down off in Settings and look
  into a hillside tunnel: the layer above should still be see-through, as before. A wrong answer is
  a fire that does nothing, a click that selects the ground beside it, or rock drawn solid over a
  tunnel with Walls down off.
- **Does the Health tab read, and does a hurt colonist get treated?** (`claude/relaxed-heisenberg-zxy63b`,
  PR #213, design 43, merged with medical supplies §15.) Debug menu > Spawn > Hurt a colonist near a
  colonist, then open her pane on Health: two columns, a region with a red bar and a warning mark,
  Bleeding counting hours, Tended 0/1. She should stop what she is doing and lie down (in a bed if
  there is one) although her bar is still high: a bleeding colonist is a patient. Leave Doctor on
  for somebody and watch them fetch one box of medical supplies, kneel beside her and treat her: the
  mark turns to a cross, the hours vanish and her bar fills as they work. Click a region to see its
  injuries; click again to go back. Then press Hurt on one colonist until she goes down (four presses), and watch
  that she stays down until the treatment ends and then gets up, with one box gone from the pile.
  A wrong answer is a tab that clips or scrolls, a figure that overflows its column, a bleeding
  colonist who carries on working, a doctor who never comes or stands on her, a patient who stands
  up half-way through her treatment, or a mark drawn as a box.
- **Is the fight still a fight?** (Same branch, design 43 §14b.) Spawn 3 bandits against armed
  colonists. With pain shock, people go down at about two thirds of the bar, and a bandit cut with a
  machete bleeds to death where it lies. A wrong answer is fights over in two blows, colonists
  dying in fights nobody ordered, or bandits that never go down.
- **Does a fall hurt the way it should?** (Same branch, design 43 §7.) Build a floor, stand a
  colonist on it and take out its support: one layer bruises her and she walks on; a longer drop
  breaks something and three layers put her down. A wrong answer is a miner hurt by stepping into her
  own dig, or a fall with no injury on the Health tab.

- **Does the selection highlight make it obvious who is selected?** (`claude/selection-highlight`, design 44.)
  Click a colonist, an item, a wall, a tree and a patch of ground. Each should gain a thin white line round its own
  shape and look a touch brighter; the ground gets a pale wash. Then walk a selected colonist behind a wall: a
  fainter line should show through. Settings →
  Interface → *Selection style* → Brackets brings the old cursor back. **Second look (2026-09-25):**
  box-select a group — every colonist should have the same full white outline, none faded or washed,
  and none brightened; click one of them and that one alone brightens. Read the overlay's `gpu` line at 4K with
  and without a selection. A wrong answer is a line you have to hunt for, a colonist who looks bleached, a
  selection you lose indoors, or `gpu` moving by more than a few tenths of a millisecond.
- **The scenery made real** (design 45, `claude/meadow-forage` on `claude/meadow-nature`). New game
  on the meadow. Look for: a felled tree going over away from its cutter and sinking; bushes you can
  walk through but that slow a colonist; *Chop and clear* over a bush, then a wall there; a berry
  bush's red clusters, the **Harvest** chip on it, eight berries beside it and the bush bare for
  three days; mushrooms by the trees coming back elsewhere once eaten; grey stones in the grass
  hauled once a store takes stone. Wrong answers: a giant that looks like any other tree; a bush
  you cannot tell is a berry bush; the stones reading as litter rather than rock; a seven-button
  orders strip that reads long (then Harvest moves off it).
  **Since the first play (§12):** click anywhere on a bush's crown — ripe, picked or plain — and
  the pane names the bush; the berries sit on the bush. Wrong: a click on a bush naming the grass,
  or a berry hanging in the air beside its bush.
  **And placing (§13):** arm a wall or an order and drag over tall grass and a bush — the grass under
  the drag lies flat and the bush fades while you place, and comes back when you disarm. Wrong: a
  blueprint you still cannot see, a bush that stays faded after placing, or grass that pops.
  **And every order's cell is bare (§13a):** drag a Harvest box, a Chop box and a Mine box over tall
  grass, and place a wall — no blade lies on any plate and the plates are whole under a faded bush or
  tree. Wrong: grass across a plate, a leaf-shaped hole in one, or a bare square left behind after
  the order is cancelled or done.

- **Which rain reads as a rainy day: §7's particles, or GPU rain with wet ground?** (`claude/rain-look`,
  #203; design 43 §7, #202.) No Play needed. Open https://claude.ai/artifact/BXgdcC9mYZ6MQR3DpYWLJ3, pick
  the Play framing, and wipe between “§7 as written” and “+ wet ground”; then try the Far framing
  with the downpour, and the Close framing for splashes on grass. A wrong answer is preferring the
  particles (then §7 keeps the emitters and only the wet ground carries over), rain that reads as fog
  at the far zoom, or a meadow that looks hailed on. **Now in Play too** (2026-09-24): backtick,
  Weather, pick Rain or Downpour, and toggle *Draw as particles* to compare them moving. Read the
  overlay's `gpu` line at your own resolution with Clear and then Downpour; the budget is 0.5 ms at
  4K. A wrong answer is a sky that snaps rather than arrives, rain that keeps falling while paused,
  or rain drawn inside the hut. **Second round (2026-09-25):** Rain, Drizzle and Downpour should
  keep the colour of Clear; Storm is the grey one; zoom right out on Downpour and the rain should
  still read. Toggle *Wet ground: gloss only* and say which of the two wet looks to keep.
  **Third round (2026-09-25): the weather is real now.** Start a colony and leave it running a few
  game days, skipping days with the debug menu: the sky should change by itself, mostly clear in
  Glare, showery in Wash, grey in Rime, with the clock's glyph following. A wrong answer is a sky
  that never changes, one that flips at midnight, or a storm every other day.
- **Can you see who the rain is slowing?** (`claude/pace-readout`, design 17 §5a, 43 §6a.) New
  game, backtick > Weather > Downpour. Click a colonist out in the open: under what she is doing the
  pane should say about *Pace 90% · in the rain*, and hovering it should say *rain −10%* among the
  rest. Walk her under a roof and it should go back to about 100% with the rain gone from both.
  Draft her and the pace should roughly double (*drafted ×2*). Click a hog making for a tree, and
  the same hog under it: both should read *Sheltering*, as should its row on the Animals tab (F5).
  A wrong answer is: a line you do not notice without looking for it; a pace that is not what you
  see her walk at; *in the rain* under a roof; a hog reading *Resting* under a tree in a downpour.
- **Does the rain touch the world?** (`claude/weather-world`, design 43 §6a.) New game, backtick
  > Weather > Downpour, and watch at normal speed:
  - Colonists crossing open ground should walk visibly slower in the rain and at their usual pace
    under a roof or under a tree.
  - A field in the open should ripen faster than one under a roof. *Ripen crops* is no help here;
    let the days run.
  - Animals in the open should head for the nearest trees within a few seconds. Fell the tree one
    stands under and it should get up and go to another.

  A wrong answer is:
  - a colonist slowed under a roof, or one who is not slowed at all in the open;
  - an animal sheltering from a drizzle (the gate is 400 per mille), or one that stands in the rain
    beside a tree it could reach;
  - an animal still standing where its tree was, a minute after the tree is gone.

  Also say whether a tenth off the walking pace is too little to notice.

  **And listen** (the rain's sound, design 43 §7a). Weather tab, from Clear through Drizzle,
  Rain, Downpour and Storm, a few seconds apart:
  - Drizzle should be a light patter.
  - Rain should thicken into a roar with no seam you can hear.
  - A storm should be the loudest.
  - The birds should fall back as the rain grows.
  - Leave Downpour running for two minutes: neither loop should be heard to repeat or click.

  A wrong answer is:
  - a jump in level between two presses;
  - a click, a breath or a recognisable moment every 40 or 80 seconds;
  - rain still audible after the slice has gone underground;
  - birds as loud in a storm as on a clear day.
- **Do the bat and the crowbar only swing?** (`claude/blunt-swings`, design 33 §22.) Debug menu >
  Spawn 3 bandits, arm a colonist with a bat and another with a crowbar (*Arm every colonist*, or
  right-click > Equip), draft them and order attacks. Watch several blows in a row: each should be
  a big two-handed swing or overhead chop, cycling through three different ones. A wrong answer is
  any blow that thrusts the weapon straight forward, a blow that lands before the swing reaches the
  target (the third one especially, whose timing was broken until now), or the machete and blade
  swinging differently from before.

- **Does a new game arrive cleanly?** (`claude/meadow-hitch-check`, design 38 §25.) From the title
  screen, set up a colony and press Start. **Look for** the setup page freezing for about half a
  second, then the starfield for a blink, then the colony — already smooth, no stutter as it appears.
  **A wrong answer looks like:** a jolt or stall in the first moment the colony is on screen; the
  starfield hanging on for a noticeable time; or the interface appearing before the world does.
- **Does changing Grass in Settings stay smooth?** (same branch.) Settings -> Graphics -> Grass: step
  through the rungs to Full and back during play. **A wrong answer looks like:** a visible hitch on
  each press, or the grass just beyond the board's edge not matching the new density.
- **Does a colonist jump a one-cell stream, and does it read as a jump?** (`claude/funny-allen-2qipcn`,
  design 46.) The jump clips are linked in the committed catalogue since 2026-09-25; no rebuild is
  needed. New game, find a stream one cell wide, right-click a drafted colonist to
  the far bank. They walk to the lip, gather, leap and land on the far lip without touching the
  water. A wrong answer is a figure that slides across level with the bank (the clips did not
  resolve), a pause at the lip long enough to read as stuck, feet sliding on landing, or a colonist
  who still swims a one-cell stream. **First play (2026-09-25):** took off in the water, legs still,
  women the same as men — the catalogue had not been rebuilt, and the take-off was at the cell's
  edge, which the shoreline draws under water. The lip is now the last dry ground; play again after
  the rebuild. A wrong answer now is feet at or in the water at the gather, or a leap so long it
  reads as a launch.
- **Does a failed jump read as a slip and not a bug?** (same.) Debug menu > Cheats > *Jumps always
  fail*, then the same order. They leap, come down in the water with a splash (silent until a splash
  is sourced), float and climb out on the far side. A wrong answer is a figure that lies down in
  mid-air, a snap as it reaches the water, or a colonist stuck in the stream.
- **Does a hauler keep the load in its hands over the jump?** (same.) A wrong answer is the load
  left behind at the lip or drawn in the air beside the colonist.
- **Is one in thirty-three the right rate?** (same.) With the switch off, watch colonists cross for a
  day. A wrong answer is never seeing one fall in, or seeing it so often it reads as clumsy.
- **Can you always see where you sent a drafted colonist?** (same branch, design 33 §23.) Draft,
  select, and right-click a spot behind trees or in long grass, then one behind a rise. The line,
  the diamond, the landing ring and the colonist's bracket show through what is in front of them,
  fainter. A wrong answer is a mark still lost behind a tree or a bank, a hidden part so faint it
  might as well not be there, or so strong you cannot tell it is behind something.
- **Does swimming sound like swimming?** (same branch, design 20 §9.) Zoom right in on a colonist
  crossing water. One stroke sound per arm, on the hand going in; from the default zoom, silence.
  A wrong answer is splashes out of step with the arms, a sound heard across the board, a machine-gun
  run of splashes from several swimmers, or silence up close.

- **Does a bandit read as a bandit, not a colonist?** (`claude/bandits`, design 42.) Debug menu >
  Spawn > Spawn 3 bandits near your colonists. Each should wear a grey welding helmet, a red vest
  over bare arms and black trousers, and carry a crowbar or a bat; click one and the pane shows a
  name, "bandit" under it and a masked portrait. Zoom out past the figure cap (or spawn a crowd):
  a far bandit keeps the helmet and the red. A wrong answer is hair or a beard poking through the
  helmet, a vest that is still camo, trousers that are red, a far bandit in camo or the white
  jumpsuit, or three bandits you cannot tell from colonists at play distance.

- **Do the streams and ponds read as water with a natural edge?** (`claude/meadow-shorelines`,
  design 38 §24.) New game; pan to the nearest stream and a pond, close in and pulled back.
  **Look for** a shoreline that curves and cuts corners instead of following the cells, marsh as a
  soft dark band rather than pale tiles, a pond's deep middle as a darker blob, and murky green-teal
  water like the reference. Walk a colonist along a bank. **A wrong answer looks like:** a staircase
  still visible along a diagonal stream; pale slivers or wedges at the water's edge; a colonist
  standing in the water or floating over a bank; a waterfall missing or cut short; or the water too
  dark to read as water at dusk.
- **Can you click any water tile now, and does the water move?** (`claude/meadow-shorelines`,
  design 38 §24f.) Click shallow water, deep water, a one-cell pool and the edge of a stream: each
  should select the water tile. Click the grass beside it: that should still select the bank. Watch
  a stream for a few seconds, then a pond, then pause. **Look for** light streaks drifting downstream,
  gentle swells on ponds, a soft rim at the edge that slowly brightens and fades, and everything
  holding still on pause. **A wrong answer looks like:** a click on water selecting the bank or the
  tile beyond it; streaks flowing uphill or away from a fall; motion that reads as clouds or noise
  rather than water; or water still moving while the game is paused.
- **Does the title screen's dock read well, and does Exit ask?** (`claude/settings-frame`, design
  40.) Press Play. A dark panel down the left edge with the layered mark and ODYSSEY, four
  coloured buttons and the build line at the foot, the starfield clear to the right. New game should
  already be lit, so Enter starts a game; Up and Down move the light. Hover each button: it fills in
  its colour with a bar on its left. Settings opens the centred window over the dock, and closing it
  leaves Settings lit. Exit game asks "Exit game?" with only Exit and Cancel. A wrong answer is the
  wordmark touching the dock's edge or wrapping, the starfield dimmed, the load list not fitting the
  dock, or Exit closing the game without asking.
- **Is the settings window one steady box, and can it be driven from the keyboard?**
  (`claude/settings-frame`, design 39.) Open Menu > Settings and click through all five tabs: the
  window should not move or change size at all, and should sit dead centre. Save, Save as, Load,
  Quit to main menu and Exit game appear once, at the foot of the rail. Then press Tab and use the
  arrows, Enter and Space to change a setting without the mouse: a white ring should follow you, and
  the camera should not pan and the game should not pause while it does. A wrong answer is a window
  that shifts as you change tab, a Keys tab with a scrollbar, a ring that jumps two rows for one
  arrow press (the engine moving focus as well as us), or Space pausing the game from a switch.
- **Trees grouped and simpler far away** (`claude/meadow-trees`, design 38 §23). On Huge, zoom right
  out and pan across woods, then back in. **Look for** a smoother frame when zoomed out and trees
  that look the same up close. **A wrong answer looks like:** trees flickering or missing at the
  screen edge, a visible pop as distant trees change detail while you zoom, or a felled tree that stays
  standing for a frame.

- **Does zooming out over Full grass feel smoother, and does anything flicker or vanish?**
  (`claude/meadow-grass-perf`, PR #195, design 38 §22). Grass → Full, zoom slowly from the start out to the
  farthest pull and pan along the board's edge, watching the overlay's `frame` and `gpu`. The scenery
  (grass, flowers, bushes) is now drawn from GPU buffers. **A wrong answer looks like:** a patch of flowers
  or a bush that vanishes at the screen's edge or pops in late; grass that flickers while panning; a stutter
  when a colonist digs, builds or a crop grows; or no smoother than before at the far zoom.

- **Should four armed colonists lose to three bandits?** (`claude/combat-c7`, design 33 §21d,
  `docs/milestones/combat-report.md` §5.) **Every combat row from here down is on `main` since PR
  #194**, whatever branch it names; the combat plan is done and these are what is left of it.
  - *Set-up.* New game, five colonists. Debug menu: *Arm every colonist*, then draft them all and
    move them together. *Spawn 3 bandits* about twenty cells off.
  - *What the gate saw.* On one seed of three the gathered squad of four lost to the three; on the
    other two the squads downed nine and seven bandits over ten days. Every number in the fight is
    invented, so this is the tuning speaking.
  - *A wrong answer looks like* an armed squad that feels hopeless against three (the bandits too
    strong — their level, the machete, or the colonists' weapon roll), or three bandits that never
    get a colonist down (too weak). Say which, and roughly how many a squad of four should beat.
- **Does the landing ring read, and does a drag across the roster select the squad?**
  (`claude/draft-ring-roster-drag`, design 33 §20.)
  - *Set-up.* Any colony with four or more colonists. Draft them all.
  - *Expect.* Select the squad and right-click the ground: under each colonist's own destination a
    **pale ring** snaps in from wide, flashes once as it lands, stays faint while she walks, and
    fades as she arrives. Sent somewhere else mid-walk, the old ring fades as the new one snaps.
    Undraft mid-walk and it fades. The red attack ring (right-click a bandit) still looks like
    the same family in a different colour.
  - *Then the roster.* With no tool armed, press on one card and drag across three more: all four
    are selected, the first as the one in the pane, and the camera does not move. Drag back: the
    cards behind the pointer drop out. A plain click still selects one and jumps the camera, **on
    the release now rather than the press**. Shift-drag adds to what was selected. Right-drag still
    swaps two cards.
  - A wrong answer looks like any of these:
    - the ring lost on grass, snow or a lit floor, or read as the selection cursor;
    - a ring that pops rather than snaps, never flashes, or hangs about after she has arrived;
    - a pale ring and a red line that read as two unrelated marks;
    - a drag that selects only the first and last card, or toggles cards out;
    - the camera swinging to the first card during a drag, or a click that no longer jumps;
    - a right-drag that selects instead of reordering.

- **Do three bandits all get to work, and does a squad go upstairs?** (`claude/combat-stall-fix`,
  design 33 §19.)
  - *Set-up.* Load a save with a building on a terrace (the owner's `the-latest-tim` will do), wall
    the colonists in or put them behind a shut door, and let three bandits come.
  - *Expect.* Each bandit breaks something: one at a wall with a single side, the others at other
    walls. None stands on *Fighting* with nothing to hit for more than a moment. No bandit or
    colonist walks through a wall after a load.
  - *Also check:* draft the squad, select them all and right-click the upper floor of a two-storey
    building: all go up, on that floor, none to the room below or the ground outside. Then
    right-click the ladder itself and see whether the attack it starts (§13i) is what you want.

- **Does a bandit steal and leave?** (`claude/combat-thieves`, design 33 §17.)
  - *Set-up.* Let a bandit down every colonist, with no walls, doors or other buildings of yours
    about (beds are fine: it leaves them alone), and a few stacks lying around — the starting meals
    will do.
  - *Expect.* It walks to the nearest stack, stoops and lifts it, and walks off with it in its
    arms to the nearest edge of the board. Its activity line reads *Stealing · Meal × 12*. At the
    edge it vanishes, with no body, and the Events panel shows a red **Theft · Meal × 12** row with
    the negative chime; clicking it jumps the camera to where it left.
  - *Also check:*
    - with nothing on the board to take, it walks off empty-handed and the panel says **Bandit
      left**, in the neutral colour;
    - knock it down while it carries something and the stack is dropped where it falls;
    - spawn a colonist from the debug menu while it is walking off: within about five seconds it
      drops the stack and goes for her;
    - the debug menu's Events tab lists the two drops and nothing new.
  - A wrong answer looks like any of these:
    - a bandit that stands about for ever with everybody down and stacks lying in the open;
    - one that takes a stack while a colonist it could reach stands in the open, or while it has
      a wall of yours to break;
    - the stack still drawn on the ground after it is lifted, or nothing in its arms;
    - a body left at the edge, or colonists upset as if somebody had died;
    - the stack still in the colony's stock after it has gone.
  - Three things to judge.
    - **It walks, it does not run.** A thief that ran would be hard to catch. Say which.
    - **Its machete goes with it.** Say if a thief should drop its weapon at the edge instead.
    - **It takes the nearest stack, whatever it is**: a pile of stone is as good as the meals.
      There is no value yet; say if the choice reads as stupid.

- **Does the ring now mean only your orders?** (`claude/combat-response`, design 33 §18b.)
  - *Set-up.* Draft two colonists and select both. Spawn a bandit beside one of them from the
    debug menu, and another on an undrafted colonist about five cells from them.
  - *Expect.* No red ring while they fight of their own accord — the one striking the bandit
    beside her, the other running over to help. Right-click the bandit: the ring snaps in, as
    before. Rescue a downed colonist: no ring, and she is still carried in the arms.
  - A wrong answer looks like any of these:
    - a ring under a bandit nobody right-clicked;
    - no ring after a right-click on one;
    - a rescued colonist left on the ground or standing at the carrier's feet — the carrier lookup
      moved to a new aspect, and only Unity can say it still finds her.

- **Do Defend and Flee do what their names say?** (`claude/combat-response`, design 33 §18c–§18e.)
  - *Set-up.* Select a colonist and press the new button beside Draft on her pane: it reads
    *Fight back*, then *Defend*, then *Flee*, then round again. Box-select three and press it
    once: all three should read the same.
  - *Defend.* Put a colonist on Defend while she works. Spawn a bandit on another colonist
    about five cells from her. She drops her work, runs to it and fights it; when it is down she
    goes back to work. She is never drafted: no diamond, no four-hour clock.
  - *Flee.* Put a colonist on Flee and spawn a bandit about six cells from her. She drops her
    work and runs, well away; when it is down or far off, she goes back to work. Struck, she runs
    rather than fighting back.
  - *Fight back.* The default does what it always did: works on until she is struck.
  - A wrong answer looks like any of these:
    - a colonist at Defend who watches a friend being beaten five cells away;
    - one who comes from across the map;
    - one who drafts herself, or stands about after the fight instead of working;
    - a colonist at Flee who fights while she has room to run;
    - one who runs from a hog that is only rooting about;
    - one who never stops running once the danger is gone;
    - the button's label overlapping Draft or running off the pane (three buttons now share that
      header, and nothing in the fast tier can measure it).
  - Three things to judge.
    - Whether a setting per colonist is enough, or the colony-wide rules panel you mentioned is
      wanted now (deferred, §18a).
    - Whether Flee's eight cells is too far or not far enough (it is the help radius).
    - Whether the button showing the *current* response reads well beside Draft, which shows
      what pressing it will *do*.

- **Do drafted colonists come to help?** (`claude/combat-drafted-help`, design 33 §15.)
  - *Set-up.* Draft two or three colonists and leave them standing within about eight cells of a
    colonist who is **not** drafted. Spawn a bandit beside her from the debug menu.
  - *Expect.* As soon as it is on her, the drafted ones run to it and fight it, each on a free side.
    When it goes down they stand where they are, still drafted.
  - *Also check:*
    - a drafted colonist further off than about eight cells stays where she is;
    - one walking to a cell you clicked keeps walking, and joins only once she has stopped, if the
      fight is within eight cells of where she stopped;
    - an undrafted colonist nearby carries on with her own business;
    - a Ctrl-attack between two colonists draws nobody.
  - A wrong answer looks like any of these:
    - drafted colonists who watch a colonist being beaten a few cells away;
    - helpers who come from across the map;
    - two helpers on one tile;
    - a helper who walks back to where she stood before the fight;
    - one who chases a hog that has calmed down;
    - one who undrafts herself the moment a long fight ends.
  - Two numbers to judge.
    - ~~A **selected** helper shows the red lock-on ring~~ — answered 2026-09-24: the ring means
      only your orders, and a helper wears none now (design 33 §18b; the row above).
    - Eight cells (20 m): say if it is too far or not far enough.

- **Does a bandit break in, and do the weapons feel different on wood and stone?**
  (`claude/combat-owner-round`, design 33 §14b, §14d.)
  - *Break in.* Build a small room of wooden walls with **no door** round a colonist, or down
    every colonist. Then spawn a bandit from the debug menu. It should walk to the nearest wall
    you built and beat it down: floating numbers, the thud, and the wall gone with nothing left
    behind.
  - *Look up.* Take down another wall of the room yourself, or send a colonist out. Within a few
    seconds it should leave the wall and go for her.
  - *Weapons.* Order a drafted colonist with a machete on to a wooden wall, then on to a stone
    one; do the same with a bat.
    - The machete should chew through wood (about 10 a blow) and barely mark stone (about 4).
    - The bat should do better on stone (about 9) than on wood (about 7).
    - Her Melee should not rise while she does it.
  - A wrong answer looks like any of these:
    - a bandit that idles beside your walls;
    - one that attacks a wall while a colonist it could reach stands in the open;
    - one that keeps at a wall for a long time after a way in has opened;
    - one that goes for a ruined-city wall;
    - the same numbers from every weapon on every wall.
  - Since §16 (`claude/combat-bandit-doors`) a closed door holds it and it leaves beds alone;
    the row below is the test for both.

- **Does a door hold a bandit, and does it spare the beds?** (`claude/combat-bandit-doors`,
  design 33 §16.)
  - *The door.* Build a small room of wooden walls **with a door**, put a colonist inside and
    draft her so she stays. Spawn a bandit outside, on the door's side. It should walk to the
    door and beat it down — it never opens it — then go in for her.
  - *The far side.* Do it again with the bandit on the side away from the door. It should break
    the nearest wall, not walk round to the door. That is the rule as decided (§16e); say if it
    reads as stupid, because that is the question a smarter breach would answer.
  - *Following in.* Undraft her and let her walk out while the bandit is near. It may follow
    her through the open door; that is expected.
  - *Beds.* Build a bed, then let a bandit down every colonist. It should stand about or
    wander, and not touch the bed. Build a wall near it and it should go for the wall. Then
    order a drafted colonist on to the bed: the order still works.
  - A wrong answer looks like any of these:
    - a bandit standing in a doorway, or a door swinging open for it;
    - one that idles outside a closed door with a colonist behind it, and breaks nothing;
    - one that walks round a room to reach its door rather than breaking the nearer wall;
    - a bed with floating numbers over it and nobody ordered to strike it;
    - a hog walking through a closed door, or a colonist or a rat unable to.

- **Does friendly fire feel like anything?** (C5, `claude/combat-phase4`, design 33 §12). Draft a
  colonist, Ctrl + right-click another: she is attacked, fights back, and her mood drops by about 8
  points for a day (the Needs panel's mood; the Thoughts tab that would name it is still disabled).
  Let a colonist die: every other colonist's mood drops by about 6 for three days, and a
  bandit's death moves nobody. A wrong answer is no drop, a drop for a bandit, or a drop that
  stacks up on every blow. **Since the owner's answers (§14, `claude/combat-owner-round`)**: a
  swing that misses or is dodged gives the drop too, and a second swing renews the day rather than
  stacking. Both sides of a fight carry it.

- **Does blood read as blood, and is it too much?** (`claude/combat-blood`, design 33 §10).
  *Arm every colonist*, *Spawn 3 bandits*, and watch at the play camera's distance. Every landed
  hit should throw a few red drops from the wound along the blow and leave one mark where they
  land: a machete or arc blade a long splatter with drops thrown ahead, a bat, crowbar or fist a
  smaller round spot. Somebody going down should get a pool under the middle of the body a moment
  after they land, spreading over a few seconds; a death's pool is larger. Pause: the drops hang.
  Run the day on at speed three: the marks thin and are gone by the next day. Slice down a layer:
  they hide with it. A wrong answer is drops too small to see or so many they read as a particle
  effect, a mark that looks like a hole or a shadow rather than blood, a pool at the feet rather
  than under the body, marks sunk into a slope or floating over one, or a battlefield that is red
  from edge to edge after one fight (the cap is 200, oldest first).
- **No more orange suits past 64 colonists?** (`claude/pawn-ceiling`, PR #175, design
  `29-modular-colonists.md` §13a.) Spawn about 100 colonists from the debug menu, pull the camera out
  and pan across the colony. Everyone should be in the white uniform, near and far. A wrong answer
  looks like some colonists in burnt orange, or clothes flicking as you pan. Two things are expected
  and are not faults: a far colonist's **skin tone and hair colour** can still change as they cross
  the cap (recorded, not fixed), and the debug menu **stops spawning at 200**.

- **Bushes stay, and grass at distance is cheaper** (`claude/meadow-grass-perf`, design 38 §21).
  Walk colonists through and past bushes: **the bush stays solid** — if it fades or vanishes, the
  dressing is still in the sight fade. Then Settings → Graphics → Grass → **Full**, overlay on
  (backtick), and zoom out to the farthest pull and back: the near meadow looks as it did, the far
  field thins smoothly as you pull back and fills in as you come close — **a wrong answer is clumps
  popping in or out at a line, or the far field reading bald**; and `gpu` at the farthest pull should
  be lower than before this branch. Trees still fade for colonists.

- **Do the terraces read as slopes?** (`claude/meadow-skin`, design 38 §20). New game; walk the
  camera along a hillside and a stream. The steps between terraces should read as grassy slopes, the
  stream banks should run down into the water, and there should be no line where the board meets the
  land around it. **A wrong answer looks like:** a gap or a dark crack between cells; a slope that
  flickers where it meets flat ground; a colonist, item or tuft of grass sunk into a slope or floating
  over it; a click on a slope picking the wrong cell; the meadow a different green from before.

- **Is a dropped stack or a fallen colonist easy to see now?** (`claude/meadow-look-polish`,
  design 38 §19a). Drop a stack on long grass and next to a bush; let a colonist sleep outdoors (or
  get downed). The grass should lie flat in a ring round each, and a bush over one should fade to a
  ghost. **A wrong answer looks like:** the stack or body still half-hidden (the ring is too small),
  a bald patch much bigger than the thing, or a square of ghosted ground under it.
- **Do trees fade for colonists you have not selected?** (design 38 §19b). Let colonists walk into a
  wood with nothing selected. Crowns between the camera and any of them should ghost. **A wrong answer
  looks like:** only selected colonists get the fade, or whole walls and ground ghost round unselected
  ones.
- **Does the land beyond the board read wooded?** (design 38 §19c). Zoom right out over an edge of
  the board. **A wrong answer looks like:** bare lawn past the rim — then check Settings → Graphics →
  Surround is on and send a screenshot, because our photographs show wood there.

- **Can you see a colonist through a tree now?** (`claude/meadow-look-fixes`, design 38 §17c). Walk
  a colonist behind a tree and a bush: the leaves should fade to a faint ghost (about 15%) with the
  trunk, soft rather than dotted, and come back when the colonist leaves. **A wrong answer looks
  like:** the crown still hiding the colonist (the ghost is not reaching it), a dotted screen-door
  pattern, or the tree flickering as a colonist walks along its edge.
- **Are the lines gone from the terraces?** (same). The steps, banks and stream edges should have no
  black line; colonists, walls, furniture, piles and rock outcrops keep theirs. **A wrong answer looks
  like:** a black line still along a step (a terrain left off the ground shader), or a colonist or a
  pile that has lost its outline (something missing from the normals prepass).
- **Is there enough colour in the trees?** (same). Mostly greens, with gold and orange stands and
  the odd red, varying tree to tree. **A wrong answer looks like:** trees all one colour again, the
  autumn reading as brown or olive, or colour changing tree by tree so evenly it looks like confetti
  rather than stands.

- **Does the meadow look like the Synty screenshot now?** (`claude/meadow-look-dressing` with the
  ground-and-light half, design 38 §17). New game; zoom out to about the reference's height. Look
  for: Meadow trees in stands (birches in autumn colour, round meadow trees), round bushes across the
  meadow and at wood edges, tall-grass stands, wildflowers, stones by rock — as patches, not a
  sprinkle. Then at 3840 x 2160 on **High** on a **Huge** board, backtick for the overlay: **`gpu`
  under about 16 ms** holds 60 fps; the batch arm read 19 ms with other Unity runs on the machine.
  **A wrong answer looks like:** trees or bushes with black scribbled edges (ink on the leaves);
  a sprinkle of the same bush in rows; grass hiding a dropped item (it should part round it); the
  colours dark and olive (that is the light, judged with the ground half); Huge High well over 16 ms.

- **Does the ground read like the Meadow screenshots now?** (`claude/meadow-look-ground`, design 38
  §17a, integrated into the look PR). New game, default camera, then watch a day go by. Look for:
  the ground painted in patches — grass, clover, yellow flowers here and there — with no grid of
  tiles, in a bright yellow-green rather than lime; shade soft and cool rather than dark. **A wrong
  answer looks like:** a repeating pattern you can see at the default zoom (the 4 m repeat is too
  small); flowers everywhere rather than in patches; the ground washing out or turning too bright
  at midday (the Meadow light is too strong — one number, `Daylight.MeadowSunScale`); or dawn and
  dusk looking different from before (they should not have moved at all).

- **Does the Meadow grass read as grass, and is this the grass to judge?** (PR for
  `claude/meadow-m3-foliage`, design 38 §16) **This is the first branch to test grass in.** New game,
  play camera. Three things: (1) the meadow reads as grass and the green is the lighter spring green
  you asked for — if it reads as grey fuzz or olive, the grade is the one number to move; (2) it
  sways, and **stops dead when you pause** — if it keeps moving, the wind is on wall time; (3) drop
  or haul a stack onto grass, and designate a tree: **the grass clears in a small ring round each** —
  if a log disappears into the grass, the clearance is not reaching the drawn view. Density is
  unchanged (full cover is M4), and trees are still the old ones (M5).

- **Grass → Full: the first grass you can see change** (`claude/meadow-m6-presets`, design 38 §13,
  §15). New game on the default meadow, the camera at its starting zoom over the clearing. Settings
  → Graphics → **Grass**: press **Meadow** (today's grass), then **Full**, then **Off**, a few
  seconds apart, with the overlay (backtick) showing `gpu`. **Full** should read as a meadow mostly
  covered, soil showing only in patches, about 1 ms dearer on the GPU than Meadow at 4K; **Off**
  should be bare ground. The grass redraws a few chunks at a time, so it spreads across the screen
  over a second rather than switching at once. **A wrong answer looks like:** Full no thicker than
  Meadow (the ladder is not reaching the renderer); the frame stuttering while it redraws; or Full
  costing well over 2 ms of `gpu`. **Known, not a fault here:** at Full the grass will stand over
  dropped items and order marks — nothing clears grass round them until M3's clearance field — so
  judge the look on open meadow.

- **Do the quality presets feel right?** (`claude/meadow-m6-presets`, design 38 §15,
  `27-graphics-settings.md` §10). Settings -> Graphics: a Quality row across the top. At 3840 x 2160
  pick each of Low, Medium, High, Ultra and watch the overlay's `gpu` and the frame. **Ultra** should
  hold 60 fps with grass on every cell; if it does not, or it looks no richer than High, say so.
  **Low** should look acceptable at 1080p on a laptop — if the 70% render scale is too soft or the
  missing surround reads as the world ending, that is the row to change. Moving any lever by hand
  should light **Custom**; restarting the game should come back on the preset you left.

- **Does a rescue read, and is a five-day bed rest right?** (`claude/combat-rescue`, design 33 §11).
  Build a bed, let a bandit down a colonist, then draft another, select her and right-click the
  one on the ground: she walks over, stoops, stands up with the body across her arms, carries it to
  the bed and lays it down. Undrafted, with Rescue ticked on the Work tab, colonists do it by
  themselves. With no free bed, *No bed for the wounded* appears and nobody comes. A wrong answer is
  a body that floats beside the carrier or sinks into her, a carry too high or too low to read as
  arms (head near the carrier's shoulder is the intent), a patient who stands up at 15 % or lies
  on the floor through the bed, two colonists running for one body, or a colonist still *Downed*
  on the card for five days when you expected something else.

- **Does the hearth read as a hearth?** (`claude/campfire-art`, PR #170, `docs/design/31-campfire-art-and-fire.md`
  §17–§18d.) Build a campfire, give the colony nothing to do, and watch for a game hour. Idlers
  should drift to it two times in three and **stay** once there, about half of them turned to face
  the flames. **The crouch was judged 2026-09-24** — *"sneaking/crawling and not sat down"* — and
  taken off (design 31 §18e); a real seated clip is owed. Left to judge: whether the ring **reads
  as people at a fire rather than a queue** — a wrong answer is idlers you cannot tell from
  colonists waiting on a job. **And since the merge with combat (design 31 §19)**: the right-hand
  column is **271 px, down from 296**, so check the outdoor temperature on the clock still clears the
  speed buttons beside it — a wrong answer is the reading touching or running under them again; and
  spawn a bandit with nobody to fight near a campfire — a wrong answer is it settling at the fire.

- **Medical supplies: the box, the doctor and the patient** (`claude/medical-supplies`, design 37
  §9). Start a new game: there should be a red first-aid case among the starting piles, and it
  should go to a stockpile or a shelf like anything else. Is it readable at play distance, or
  too small? (The picture offered two olive boxes instead.) Then spawn a marauder and let it put
  a colonist down. A colonist with Doctor on should walk to the case, carry **one** box to the
  downed colonist, kneel, and the downed colonist should stand up at about 40. Once standing, they
  should go and lie in a bed, and get up at about 80. Test the same with Doctor off for everybody
  but the hurt colonist, supplies to hand, and health below 60: they should treat themselves,
  slowly. Wrong looks like: a doctor carrying the whole pile, a patient walking about with 30
  health, a colonist lying down and getting up on the spot, or nobody fetching the case at all.
  **Once health (#213) is in, two numbers here move** (design 43 §15): a colonist goes down from
  pain with about a third of her bar left, so a treatment stands her up at about 75 rather than 40,
  and she will not then go to bed; and a colonist with a cut lies down for the doctor whatever
  her bar says, until she is treated.

- **Does the lock-on ring say who you sent them at?** (`claude/combat-ring`, design 33 §7b).
  Spawn a bandit, draft two colonists, select both and right-click it: a translucent red ring
  should appear wide round its feet and snap in, in a fifth of a second, flash once as it lands
  and stay faint under it until it goes down — then fade. Right-click a hog: its ring hugs the
  hog's own length. Deselect them: it fades; reselect: it is back at rest, without the snap. A
  wrong answer is a ring you notice only when it lands (the 0.2 s is too quick), a flash that
  reads as a glitch, a hold so faint it is lost on grass or at night, a red you confuse with the
  draft's dark red over their heads or the salmon diamond over the bandit, or a ring sunk into a
  slope or floating on a terrace step.

- **Does the context menu make taking up a weapon clear?** (design 33 §7a, `claude/combat-menu`).
  Select a colonist, undrafted, and right-click the machete: a small menu opens at the pointer with
  *Equip machete* and *Cancel*, and nothing happens until a row is picked. Pick Equip: a line runs
  from her feet to the weapon with a bracket on it, her activity line says *Equipping*, and the line
  goes when it is in her hand. Then check the closes — Escape, a click elsewhere, an orbit, clicking
  another colonist — and that right-click on bare ground (drafted) still moves at once, and on a hog
  still attacks at once, with no menu. Select only a downed colonist and right-click a weapon:
  *Equip* is dim with *Downed* beside it. A wrong answer is a menu that opens under the pointer's
  arrow or off the screen edge, one that will not close, a move or attack that now asks, or Equip
  still unclear — in which case say what would make it clear (the colonist's name on the row?).

- **Should an unattended fight end in downs, never deaths?** (design 33 §3, PR #180). Only an
  order strikes a body on the ground, so a bandit left alone downs a colonist and turns to the
  next; nobody dies unless you send someone to finish it. Say if that is wrong.

- **Is a weapon held and dropped where it should be?** (checkpoint 3, C3 — `claude/combat-c2`,
  design 33 §6D–§6E). A new game lays a bat and a machete beside the food: they should lie flat and
  be recognisable. Select a colonist, undrafted, and right-click the bat: she walks over, stoops and
  stands with it in her right hand, gripped at the handle. Swap it for the machete: the bat is put
  down where she stands. A wrong answer is a weapon standing on end, hovering off the hand, held by
  its head, through the forearm, far too big or small — the grip is measured from the mesh and
  nobody has seen it — or a right-click on a weapon that does nothing without a draft.

- **Does power read?** (`claude/power`, PR #173, `docs/design/32-power.md`; merged with `main` 2026-09-24.)
  Lines, generators and heaters now take **scrap metal** (design 32 §14): a new game scatters ten
  piles of wreckage over the board, fifteen or more cells from the start, and the debug menu's
  Events tab has a **Scrap drop**. Grant wood too, then open Build → **Power**. Drag a **Conduit**
  run from open ground *through a wall* into a room; put a **Heater** beside the run inside and a
  **Generator** beside it outside. Click an ordered line: the pane should title it *Conduit* and
  offer **Cancel**; a laid one, while the lines are shown, **Remove conduit**. The **Power** button
  under the orders strip keeps the lines shown whatever is armed. Run *Odyssey → Presentation →
  Rebuild module catalogue* once first, or the generator and heater are still blocks. Watch the lines appear the moment the tool is armed and vanish when it is put
  down; a hauler should fill the generator unasked; the heater's pane should go from *not
  connected* to *powered*, and the room should warm. Then build a sixth heater on the same net and
  watch the whole net go dark and the *Power failure* alert rise.

  Four things only a keyboard decides. Whether **a line drawn through walls reads as inside the
  wall** or as a glitch floating over it — a wrong answer is a player thinking the line runs across
  the roof. Whether **a dark net reads as dark without opening the pane** — red lines and the alert
  should be enough; a wrong answer is clicking heaters to find out why the room is cold. Whether
  **five heaters to a generator** is the right size — a wrong answer is never needing a second
  generator, or needing one for the first room. And whether **the Remove conduit tool** is where a
  player looks for it, or whether they reach for Deconstruct and are surprised it leaves the line.
  And whether **scrap metal is scarce in the right way** — a wrong answer is the wreckage never
  being worth the walk, or ten piles being all the power a colony ever needs.
  And (§14c) whether **the machines sit flush**: the generator filling both its cells, a heater
  put beside a wall turning its back to it, R choosing the wall in a corner. A wrong answer is
  the stretched generator reading as distorted, or the air-conditioner's grille facing the wall
  (its front was read off the mesh, not seen).

- **Can you see a stockpile, and does its outline read?** (`claude/stockpile-drawn`,
  `docs/design/26-storage.md` §13.) Paint a stockpile on grass and one on a built floor, and two
  at once. It had been created and not drawn since 2026-09-21 unless something else re-meshed its
  chunk; now it should wash the ground and carry a line in the store's hue round its outer edge
  only, within a couple of frames. A wrong answer is no wash, a delay, a line between its own
  cells, a line lost under grass tufts, or one too heavy for a big warehouse floor.

- **Does a drafted colonist's run read as urgency, and is the deeper red findable?**
  (`claude/combat-mvp`, design 33 §2g–§2h). Draft a colonist and right-click across the board: she
  should visibly **run** — about twice her walking speed, the run clip, not a sped-up walk. The
  diamond, the order line and the destination bracket are now a deep, dark, translucent red. A
  wrong answer is a figure that skates (the walk clip played fast) or jogs so little it still
  reads as a walk; or a red too dark to find at dusk or against dark rock. The pace is one number,
  `draftedPacePerMille` in `Colonist.xml`. **And listen:** pressing T should draw a blade, once
  however many are selected, and releasing should be silent. A wrong answer is a sound late
  enough to feel like lag, one loud enough to jump at, or a clatter when five are drafted at once
  (design 33 §2i).

- **Does the meadow feel lived in, and do the comings and goings read as wildlife?**
  (`claude/wildlife`, PR to follow #167, `docs/design/30-wildlife.md`). Press Play → New game
  on the meadow and do not spawn anything: nine or ten animals should already be on the board —
  hog sounders of three to five in the woodland, rats alone by outcrops and rock faces — and
  **none inside the starting clearing**. Watch for a quarter of an hour. Things to judge that
  no test can: whether nine or ten on a 120 × 120 board reads as *alive* or as *empty*
  (`wildlifePer10000Columns` is the dial; the ceiling is 24 and the figures are the colonists'
  first); whether a **sounder** landing together reads as a family or as a clump; whether a hog
  that decides to go and walks to the edge reads as *wandering off* or as *fleeing* — a wrong
  answer is one that looks pursued; whether an arrival at the edge is noticed at all, and
  whether it looks like it walked in or like it appeared (it is placed on the ring on a rare
  tick, so it appears; the honest fix is a walk-in from off-board); whether a rat by day and a
  hog by night, resting three times as long, read as *asleep* or as *stuck*; and whether the
  city's rats in the rubble are visible from the play camera at all. Click one: still
  *Midden hog · Wandering* or *Resting*. Then **F5**, or the Animals item on the bar: the count
  strip should say how many of each kind are out there and the rows should list them nearest
  first with the KIND heading marked; click DOING and the resting ones should gather; click a
  row and the tab should close, the camera land on that animal and the pane show it, with the
  depth where you had it (owner, 2026-09-23) — a wrong answer is the view lurching to another
  layer, or the tab and the pane both on screen; an animal in a cavern below the slice is
  selected without being shown, which is the price of the depth staying put. Whether the tab **needs the distance back** (the brief dropped it; the rows are still
  ordered by it) is the question only you can answer, and whether 560 wide reads as a tab or
  as a card. After the third look (2026-09-23): whether two sounders now land in different
  parts of the meadow and each reads as a loose family rather than a knot — a wrong answer is
  all the hogs in one glade again, or a sounder so scattered it is not a sounder; and press the
  info button on a selected hog: the Almanac should open on *Midden hog* under Fauna, and what
  it says should be true of what you have watched.
- **Do the two animals read as animals, and does the hog's computed walk read as a walk?**
  (`claude/animals`, `docs/design/29-animals.md`, plan `docs/plans/animals.md`.) Debug menu →
  *Spawn midden hog* and *Spawn duct rat*, several of each, near the camera; then watch. Five
  things to judge, none a test can answer: whether a **life-size** hog (1.2 m) beside a 2.49 m
  colonist reads as a pig or as a piglet, and whether the rat is visible at all at play height;
  whether the hog's **computed trot** reads as a pig trotting beside the rat's authored walk
  (first look, 2026-09-22: *"looks awful"*, which was a walk cycling once per metre on 23 cm
  legs; it is a measured-stride trot now) — a wrong answer is feet that still slide, legs that
  paddle, or a cadence that reads as scurrying, and `QuadrupedGait.SlideFactor` is the dial;
  whether the **cursor** now sits flush round a selected hog and a rat rather than round the
  tile, and whether a click on the animal's body lands; and, after the second look ("legs are
  spindles", 2026-09-22), whether the hog's body and legs now stay the model's own shape while it
  trots — a wrong answer is any leg longer than the body is tall; whether a hog or a rat ever
  enters a stream or pond (owner, 2026-09-22: *"animals can't swim by default"*) — a wrong answer
  is either wading; and whether either ever **rests on a terrace step's foot** and snaps down
  when it sets off — a wrong answer is the snap, since walking up and down a step is allowed;
  after the fourth look (2026-09-22), whether a hog ever **snaps back a cell** mid-walk (it was
  the wander expiring mid-step; the detector reads none now), whether a hog or rat ever climbs a
  **mined face or a rock** that has no ramp drawn (a wrong answer is one on top of an outcrop or
  up a dug step), and whether the wider trot now reads as **legs moving** rather than the body
  twisting — a wrong answer is still a twist, and the honest fix is a walk clip; after the
  fifth look (2026-09-22, the owner's note on how a pig's legs work), whether a planted foot now
  **holds the ground** while the body passes over it and the swinging one lifts, carries flat
  and plants — a wrong answer is a foot that slides backwards against the ground or a leg that
  is still bent as it lands; whether the **flat two-colour** models
  read as the same game as the Synty colonists; whether a hog at the foot of a ladder **turns
  away** rather than standing at it, and a rat goes up; and whether the wander reads as an animal
  living rather than pacing a corner or standing for minutes. Click one: the pane should say
  *Midden hog · Wandering* or *Resting*, with no face, needs or tabs; the roster should not gain
  a card. Both scales are one number each in `AnimalImport.Scales`.

- **Does the frame still fall over at a high colony count?** (PRs #171 and the aspect-lookup PR,
  `docs/design/25-pawn-steering.md` §9 and `31-aspect-lookup.md`.) **One row for two units**, because
  they are one answer. Spawn colonists past a couple of
  hundred with the overlay up: the sweep now runs 2.2 to 4.8 ms across 8 to 384 colonists on this
  machine with no knee in it, against 27.8 ms at 384 before. A wrong answer looks like a bend
  anywhere in that range, which would mean a third term nobody has measured. **Nothing to look at
  below about a hundred**, and a test proves the drawn sidestep is bit-for-bit unchanged, so there
  is deliberately no "does it still look right" row.
- **Can you tell three colonists apart without reading their names, and does the colony read as a
  crew?** (PR #168, `docs/design/29-modular-colonists.md`.) Everyone now wears the same issued
  jumpsuit and identity is carried entirely by face, hair and beard. Three things only a person can
  answer: whether **three candidates** on the setup screen are distinguishable at a glance;
  whether a colony of five reads as *a crew in uniform* rather than as clones; and whether the
  uniform's white **takes the light** at dusk and dawn or goes to a flat hole in the frame. A wrong
  answer looks like: you still click each card to tell who is who, or the suit glows white at
  golden hour.

- **Do the names suit the people?** (Same PR, §11.) A name's CSV row picks the body, and the thirty
  unisex names are now dealt a sex rather than defaulting to male. Worth a few rerolls. A wrong
  answer looks like a name that reads female on a body that does not, or the same unisex name
  always coming out male across several colonies. **The pool skews male 120:90** and that is one
  CSV column if you want it evened.

- **Is the hair colour on the face a problem now?** (Same PR, §10.) Pre-existing and unchanged —
  the hair rectangle paints the scalp, brows, a band across the eyes, the jaw and the lips. It was
  invisible under brown hair and a varied cast; against a white uniform with teal and plum in the
  palette it may not be. If it reads as goggles rather than as shadow, §10 has three costed ways
  out and the cheapest experiment that decides between them.

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
| 2026-09-25 | **Ambient birds** (PR #230, `docs/design/50-ambient-birds.md`): rooks and a buzzard, their size at every zoom, perching, the scatter, the rookery, the weather | working — owner: *"superb - if this is performant - get it ready to be merged in"* | measured, then ready to merge: `FrameSection.Birds` 0.018–0.032 ms, and the frame at 640 x 480 and 4K is inside its own noise with the birds on (design 50 §8a) |
| 2026-09-25 | **Ranged combat, three rounds** (PR #225, `docs/design/47-ranged-combat.md` §10a–§12): the aim, the shot, the tracer, the sound, then accuracy from a height, weapon quality and the reach rule | working — owner, first play: *"it's really decent and everything seemed to work well"*; after the reach rule: *"great job - just played a big battle"* | the first play raised the accuracy, sent a miss past its target into the ground and landed a hit on the body wherever it stands (§10a); asked for weapon quality (§11) and the reach rule (§12), both built and played in the big battle. Ready to merge. The frame with gunfire (P4) is still unmeasured |
| 2026-09-25 | **The home area, the hearth and the Assign tab** (PR #214, `docs/design/43-home-area.md`), after the review's five fixes | working — owner: *"it all works get ready to merge in"* | none; ready to merge. The house over the hearth, which the frame test logged hidden, is covered by "it all works" |
| 2026-09-24 | **Walls down, both rounds** (PR #197, `docs/design/42-walls-down.md`) | working — owner, first look: *"works brilliantly but a few things"*; after the second round: *"excellent - get this ready for merge"* | the first look moved two things (the R / F label went; a lower terrace counts as ground and only upper storeys hide, §3a); ready to merge |
| 2026-09-24 | **Beating a wall down** (C6, `claude/combat-buildings`, `docs/design/33-combat.md` §13) | working — owner: *"Buildings work fine"* | none. The row's own question, whether a wall reads without a damage bar, was not raised, so none is built. The break-in row (§14b, §14d) stays open: it was built after that playtest |
| 2026-09-24 | **What grass costs on the GPU at 4K** (PR #183, `docs/design/38-meadow-overhaul.md` §13) | measured — owner: *"6–7 ms on gpu (sometimes bit lower) without grass tufts. On — 7 ish — spikes up to 8 moving around"* | about 0.5–1 ms, peaks ~1.5; under the 2 ms line, so M4 plans for full cover; agrees with the batch arm |
| 2026-09-24 | **Combat C2 + C3 and the three rounds after play** (PR #180, `docs/design/33-combat.md` §3–§9) | working — owner: *"it seems great ... weapons sit at hips, have a battle with tons and tons of characters - was hovering 3.5ms ... it flowed really well"* | closes the brawl and fight rows; 3.5 ms is inside the 5 ms budget on the dev GPU, unmeasured on the target laptop; the ring, menu and grip rows stay open for the "more testing later" |
| 2026-09-24 | **The Inventory tab, restyled** (PR #177, `docs/design/35-inventory-tab.md` §5a) | working — owner: *"it's good"* | none; ready to merge |
| 2026-09-23 | **The Research tab** (PR #177, `docs/design/34-research-tab.md`) | working — owner: *"the research control is fine"*, after the list was cut to what the game has | the list became Electricity, Power lines, Generator, Ladder (34 §2) |
| 2026-09-23 | **Drafting and moving** (C1, `claude/combat-mvp`, `docs/design/33-combat.md` §2) | working — owner: *"the drafting, T and moving onto surfaces, diamond and 4 hours all seemed to work"* | two asks: a drafted colonist runs (§2h), the marks a deeper translucent red (§2g); both built, re-queued above |
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

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

**The rule is already breached, on the day it was written.** There are 28 open rows. That is the
finding, not an oversight — the ceiling is where the list should be, not where it is, and the first
sessions after this one take verdicts and fixes rather than features until it comes down. A rule
that is quietly wrong on arrival is a rule the next session learns to ignore.

## Open

- **A sleeping colonist is now the size she is drawn** (`worktree-bed-sleep-pose`,
  `docs/design/20-beds.md` §7b). She was being laid down 0.38 m long — the figure director's
  "hip height" is the 0.2 m floor of a clamp on a bone that stands on the floor — so she reached
  1.5 m past the head of the bed and lay inside the mattress. She is 2.49 m now, head on the pillow
  in the first tile, feet 2.28 m along into the second, resting on the bedding rather than in it.
  Two things want an eye rather than a test. **The two supine postures had their arm angles the
  wrong way round**, so a quarter of the colony slept with its arms a half-metre in the air and
  another quarter with both forearms through the mattress; both are measured flat now. **The
  contact sheet says the second one still reads as *surrendering*** — its arms go out sideways
  rather than up past the crown, and `Posture` has only one angle per arm so it cannot express
  what it is named without a second one. Pictures are in `Logs/sleep-*.png`
  (`scripts/unity.sh shot Odyssey.EditorTools.SleepCheck.Run` remakes them); the call is whether
  it is worth the extra angle. And **a sleeper lies level while the bed under her is draped**, which on
  a slope disagrees by up to 0.21 m at the pillow — measured, left alone, and the numbers are in
  §7b, because fixing it changes how every sleeper is drawn and it should be judged against a
  picture of the one that is now right.

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
- **The Synty junction chain wants inverting.** The only real copy of the licensed packs sits inside
  `D:\code\odyssey-audio`, a worktree on a merged branch; the main checkout junctions to it. See
  `docs/lessons.md` — do not prune a worktree without checking.


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

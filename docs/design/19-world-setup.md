# 19 — World setup: one page for the board and the people

**Status: designed 2026-09-17, before the code, from an interview the same evening.** It supersedes
the *shape* of `18-colonist-select.md` — not its substrate, which survives whole — and it is the
first screen in this project that is not a panel.

Read first: `18-colonist-select.md` (what is already built underneath), `17-start-flow.md` §11 (the
seed), `09-ui-and-input.md` §4.5 (portraits, still refused) and `14-hud-layout.md`.

---

## 1. Why the shape changed

`U40` put the colonists on a screen of their own because three cards would not fit beside the seed
in the menu's fixed 420 × 384 box. The owner then asked for the **whole skills grid** on a candidate
— the control the inspect pane's Skills tab already draws, thirteen skills in two columns with the
unsimulated ones greyed — and for the seed and the people to be on one screen.

That settles it by arithmetic rather than taste: the grid alone is about 900 px wide, and three
candidates plus a seed plus a detail panel is **not a panel**. So New game leaves the menu box
entirely and becomes a full-viewport page. The box stays exactly as it is for Load and Settings,
which is the point — nothing the owner has already approved moves.

---

## 2. What the owner decided (2026-09-17, interview)

1. **A full-screen setup page.** Seed across the top, three candidates down the left, the selected
   one's detail on the right, Back and Start along the bottom.
2. **The skills control is the inspect pane's, reused.** All thirteen skills, two columns, level or
   `–`, passion pip — the same code drawing both, not a second copy that looks like it.
3. **Alphabetical, down the left column then down the right.** In *both* places: the new screen and
   the existing Skills tab, which is not alphabetical today.
4. **A candidate carries an age (18+), an occupation and a one-line backstory**, and a **traits row
   drawn now and empty until M7**.
5. **Occupation is flavour now and shapes skills later.** It does not touch the roll yet.
6. **Age is 18–65, flat, and nothing reads it yet.** It exists so that ageing, skill decay and
   health can use it when they arrive — *"an actual age 18+ only, will be important later"*.
7. **Two rerolls**: one for the board, one for the people. Hunting for a map must not cost you a
   colonist you liked.
8. **The page also carries a colony name and a map size.** Board type does not: all three
   generators exist, but the ruined city has never been played and choosing it is a different
   conversation.
9. **Occupations are proposed here for veto**, the way the 29 proper nouns were.

---

## 3. The page

```
+==================================================================================+
|  NEW GAME                                                                        |
|                                                                                  |
|  Colony [ Ashford        ]     Seed [ 3829174463 ]  Reroll     Size [ Standard ] |
+---------------------------+------------------------------------------------------+
|                           |  Wrenn, 34                                           |
|  > Wrenn          34      |  Scrapper — stripped the old city for metal           |
|    Scrapper               |  Traits  —                                           |
|    ---------------------  |                                                      |
|    Odile          29      |  Animals       -      Intellect      -               |
|    Stitcher               |  Construction  4      Medicine       -               |
|    ---------------------  |  Cooking       -      Melee          -               |
|    Kester         41      |  Crafting      -      Mining         6 *             |
|    Rail-hand              |  Cutting       3      Salvage        -               |
|                           |  Fabrication   -      Shooting       -               |
|    [ Keep ]  [ Reroll ]   |  Growing       -      Social         -               |
|                           |  Hauling       5                                     |
+---------------------------+------------------------------------------------------+
|                              Back                          Start                 |
+==================================================================================+
```

**The left column is the choosing and the right is the reading.** Three candidates are always
visible — you are picking between them, so you must be able to see them at once — and the detail is
the one you have selected. Clicking a candidate selects it; a separate **Keep** control locks it
against the reroll, because on this page a click already means "show me this one" and one gesture
cannot mean two things.

**Greyed is honest, not decorative.** Nine of the thirteen skills are not simulated. They are drawn
at `TextFaint` with `–` where a level would be, exactly as the Skills tab already does — the owner
asked for it, and the alternative (showing four) hides that the other nine exist and are coming.

---

## 4. One skills control, drawn twice

The Skills tab and the candidate detail become **one component** in `Odyssey.Hud`, fed by a list of
rows and drawn by one presenter method. Not "the same layout" — the same code.

This is the `NavGraph.HopCost` argument again, and it applies more sharply here than it did to
`ColonistDraw`: the two grids will be looked at side by side by a player comparing a candidate to a
colonist they already have, and a discrepancy in ordering, greying or pip placement would read as a
bug in the *simulation* rather than in a stylesheet.

**Alphabetical is the ordering the component imposes**, so the existing tab is reordered by this
change and `SkillCatalogue` keeps its own declaration order for everything else that reads it. The
order is by the label the player sees, not by the internal skill name, because a player scanning for
"Construction" is scanning the column they are reading.

**Down the left, then down the right:** eight rows then five, so each column is a run that can be
scanned. Across-then-down was refused because a skill's position would depend on how many come
before it in *both* columns.

---

## 5. What a colonist now is

| Field | From | Lives in | Saved? | Hashed? |
|---|---|---|---|---|
| skills, passions | `RollSeed` + id (U40) | `Odyssey.Sim` | yes | yes |
| **age** | `RollSeed` + id, own salt | `Odyssey.Hud` | **no** | **no** |
| **occupation** | `RollSeed` + id, own salt | `Odyssey.Hud` | **no** | **no** |
| ~~backstory~~ | — | — | — | — |

**The backstory line is dropped** (owner, 2026-09-17, on seeing the list): with ninety-two mundane
job titles the job *is* the story. "Cemetery gravedigger, 41" and "Nepo Rich Kid, 22" need no
sentence under them, and a pool of ninety-two would have been ninety-two rows of content to review.

**And neither age nor occupation needs the simulation at all.** Both are functions of a seed the
interface already has — `RollSeed` arrives as a `PawnAspect` — so they sit in `Odyssey.Hud` beside
`ColonistNames`, which has been exactly this kind of derived, interface-side identity since M1. No
Sim change, no new save section, no aspect, nothing new to hash. **When ageing, skill decay or
health read an age, this moves into `Odyssey.Sim`** — one function, so a move rather than a rewrite,
and the formula goes across unchanged so nobody's colonists change age on the way.

**None of the three is saved or hashed, and that is a decision rather than an oversight.** They are
pure functions of `RollSeed`, which is already saved and already hashed — so they are *derived
state*, and this project's standing rule for derived state is that it is recomputed on load rather
than written down. `SupportSolver`'s output is excluded from the hash for exactly this reason
(`StateHashCoverageTests.SupportStaysOutOfTheHash`).

The payoff is concrete: **no golden moves.** U40 re-baked all six this afternoon because `RollSeed`
itself entered the hash; adding three more fields the ordinary way would re-bake them a second time
in a day, for values that carry no information the hash does not already have.

The cost is that the derivation becomes load-bearing — change the age formula and every existing
colony silently ages. That is what a fingerprint test is for, and one is in §7.

**A backstory belongs to the occupation, not to a pool of its own.** One line per occupation, so a
Scrapper's past always mentions scrapping. An independent pool would need sixteen more content rows
and would cheerfully tell you the Archivist grew up hauling ore.

---

## 6. Content proposed for veto

**Superseded 2026-09-17: the owner supplied the list.** Ninety-two, and the tone is theirs and is
better — **mundane pre-collapse jobs**, not invented sci-fi trades. Survivors who used to collect
trolleys and read gas meters is a more human colony than survivors who were all vault-breakers, and
`Nepo Rich Kid` and `Sofa Surfer` at the head of the list set that register deliberately. They are
in `icon-keys.csv` under `ui.occupation.` with no descriptions, per the ruling above.

Ten words were changed and nothing else: `labourer` ×4 for the repo's British English rule, three
gendered titles neutralised (`Fisherman` → Fisher, `Night watchman` → Night watch, `Hotel maid` →
Hotel cleaner) and three Americanisms put into the words a British player would use
(`Garbage collector` → Refuse collector, `Gas station clerk` → Petrol station clerk, `Truck driver`
→ Lorry driver, `Busser` → Table clearer). `Grocery bagger` and `Cart collector` are left as
written; they were not in the set the owner approved changing.

~~Fourteen occupations for a ruined sci-fi city, each carrying the one line that is also its
backstory. Clean room: invented here, nothing lifted.~~ Dropped, and the list below with it:

| Occupation | Line |
|---|---|
| Scrapper | stripped the old city for metal |
| Linewalker | kept the power lines standing |
| Hab-tender | kept a housing block alive |
| Sump-diver | worked the flooded levels |
| Rail-hand | loaded and ran the freight rails |
| Kiln-keeper | fired brick and glass |
| Vaultbreaker | opened sealed rooms for a living |
| Signal-reader | listened to what was left of the networks |
| Stitcher | field medicine, and no licence for it |
| Quarryhand | cut stone before the city fell |
| Bloomwright | coaxed food out of rooftop soil |
| Mess-cook | fed a work gang twice a day |
| Runner | carried messages between holdings |
| Archivist | kept records nobody reads now |

Four map sizes, named rather than numeric — "120 × 120 × 16" is a fact about an array and
"Standard" is a choice about a game:

| Name | Cells | Note |
|---|---|---|
| Small | 80 × 80 × 16 | |
| Standard | 120 × 120 × 16 | today's board |
| Large | 180 × 180 × 24 | |
| Huge | 240 × 240 × 16 | twice Standard's ground, Standard's depth (`28-map-size.md`) |

---

## 7. How it is tested

| Claim | Where |
|---|---|
| age is 18–65, always, over many seeds | `ColonistDrawTests`, fast tier |
| the same seed gives the same age, occupation and backstory | `ColonistDrawTests` |
| the derivation is pinned, so it cannot drift silently | a fingerprint over the first 100 seeds |
| skills are alphabetical by label, in both grids | `SkillCatalogueTests`, fast tier |
| **the two grids are one component**, not two that agree | a test that reads both from the same rows |
| the page fits 1280×720 and 150 per cent interface scale | `HudLayoutTests` |
| **the colony is the three shown, on the board and size chosen** | `StartScreenTests`, PlayMode |
| no golden moves | the golden table, unedited |

That last row is the check on §5's whole argument, and it is the cheapest one here.

---

## 8. What this costs, honestly

**Kept from U40, entire:** `Pawn.RollSeed` and its save section, `ColonistDraw`, `ColonistSelect`
with its locks and distinct-name rule, the aspect that carries a seed to the interface, the name
scheme, and the no-orders change. None of that was wasted; it is the substrate this page stands on.

**Replaced:** `MenuScreen.NewGame` and `MenuScreen.Colonists` collapse into one full-viewport page.
The two screens built today become one, and `SeedField`'s `NextKey` goes with them.

**New:** a full-screen page (the first in the project), a shared skills grid component, age,
occupation and backstory on a colonist, fourteen occupations and three map sizes as content, a
colony-name field, and a map-size control.

**Size: L.** Larger than U40 was, mostly because of the page and the shared component.

---

## 9. Deliberately not in this unit

- **Portraits** (`U41`) and the `09` §4.5 carve-out.
- **Traits**, which are M7. The row is drawn and empty; that is the owner's decision 4 and the one
  thing here that is a promise rather than a feature.
- **Board type.** All three generators exist; the ruined city has never been played and choosing it
  is a different conversation.
- **Occupation biasing the skill roll.** Decision 5 defers it to when traits arrive, which is also
  when it becomes interesting.

---

## 10. Naming a colonist (2026-09-18)

Owner, 2026-09-18: *"ability to rename your colonist on creation by clicking on the name,
displaying a cursor and allowing entry of min 1 character and the maximum specified for the
game."*

### 10.1 The name is the control

Click the name on a card and the name becomes a box with the current name in it, selected. There is
no pencil, no caption and no second row: the thing you want to change is the thing you click, which
is the same gesture the board-size control on this page already uses. Clicking it also **shows**
that card, because a player editing somebody's name is looking at that person.

**One box, moved.** Three fields would be three things that can hold focus, three that can be left
with a half-typed name in them, and three the refresh has to keep in step with a reroll. Moving one
makes "only one name is being typed at a time" true by construction rather than by care.

The box goes on the **end** of the card's line stack with the name label hidden, not in the label's
place: the refresh reaches for `lines[0]` and `lines[1]` by position, so a box inserted at the front
would shift both and the refresh would write the name into the occupation.

### 10.2 What a name may be

**Sixteen characters**, trimmed, with runs of blank collapsed to single spaces and control
characters dropped. The number is about the narrowest place a name is drawn rather than about
names: the roster strip and the docked bars are the densest region in the interface, and a name that
elides *there* is a colonist the player cannot tell apart at a glance, which is the whole reason for
naming one. The colony's own field takes 32 because a colony's name is drawn once, on a screen with
room.

The cut comes **after** the trim and cannot leave a space on the end — sixteen characters of which
the last is blank would otherwise be a fifteen-character name with an invisible tail, unequal to the
same name typed the other way round.

**There is no minimum, and that is a deliberate reading of the request.** "Min 1 character" is what
the box enforces by refusing to be a name at all when it is empty: an empty box means *no name of
their own*, and puts the dealt name back. A colonist with no name is the one outcome that must not
be reachable, and forbidding an empty box would instead have meant a player who clears it is stuck
until they type something.

### 10.3 A rename belongs to the person, not to the slot

A reroll deals a different face, different skills and a different name into the slot, so a typed
name left standing over it would be the player's word attached to somebody they have never seen.
**Reroll forgets the name of whoever it rerolled**; a locked card keeps its person *and* their name,
which is what locking already means. This is `18-colonist-select.md` §2 decision 2 applied to the
typed name for the same reason it was applied to the dealt one.

Escape puts back what the slot was called when the edit began. Every keystroke commits, so there is
no state where the box and the card disagree about who this is, and no way to lose a name by
clicking the wrong thing next.

### 10.4 How it survives into the game, and into a save

Every other identity a colonist has — their pool name, their face, their age, their trade — is
*derived* from a roll seed the simulation already saves, which is what lets the game carry a whole
cast in four bytes a head. **A typed name cannot be derived from anything.** It is the first piece
of colonist identity that has to be written down.

- `ColonistNameBook` holds the exceptions by `PawnId`, and `ColonistNames.Of` answers from it
  before it reads the pool. Empty in every colony where the player took the three they were dealt,
  and an empty book costs one integer compare per figure per frame.
- `ColonistNames.Rolled` is the pool's answer with the book skipped. The select screen deals from
  it, because slot 0's `PawnId` is the same 1 the *last* colony's first colonist had — and a freshly
  dealt stranger arriving in somebody else's name is the kind of wrong that reads as the feature
  working.
- `ColonistNameSection` writes the book into the save beside the camera pose. It is an `ISaveable`
  and deliberately **not** an `IStateHashable`, on `ViewStateSection`'s argument exactly: what a
  colonist is called is not a fact about the colony, and two colonies that tick identically must
  compare equal whether or not somebody typed a name over one of them. The book is cleared when a
  colony is built, not in the section's `Apply`, because `Apply` only runs for a file that carries
  the section.
- The bootstrap **checks the roll seed** before placing a name. Which pawn a slot becomes is
  `ColonistDraw.IdForSlot`'s to say, but it rests on `ColonyScenario.Place` spawning the chosen
  colonists first and in order — a contract two files away from where it is relied on. A mismatch
  is reported rather than putting somebody's name on a stranger.

### 10.5 Not in this unit

- **Renaming in the running game.** The roster card is the obvious home and the book would need
  nothing new; it is left out because the owner asked for it *on creation* and an in-game rename
  wants its own answer about where the control lives.
- **Naming the colony's people after the colony**, surnames, or any collision rule between a typed
  name and a dealt one. Two colonists called Ada is the player's business.

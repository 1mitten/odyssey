# 18 — Colonist select: three candidates you can reroll

**Status: designed, 2026-09-17, before the code.** This is **U40** of the `MS` milestone in
`docs/plans/vertical-slice.md`. It hangs off the New game screen that `U39` built, and
`17-start-flow.md` §11 is the thing to read first — §11.1's fixed box and §11.2's four decisions
both govern here.

**The owner's three decisions were taken on 2026-09-17 and are settled, not proposals.** They are §2.
Two of them cost something elsewhere in the game, and §2 says what.

Read first: `17-start-flow.md` §4 and §11, `09-ui-and-input.md` §4.5 (portraits, which are `U41` and
not this), and ADR 0004 amendment 1 (`PawnAspect`, which is how the roster learns a colonist's name
after this change).

---

## 1. What already exists, and what is genuinely missing

**Almost all of it exists**, which is why this unit is mostly wiring and one seam.

| Piece | Where | Landed |
|---|---|---|
| Skills rolled from a seed and a pawn id | `Pawn.RollStartingSkills` | U37 |
| Passions rolled the same way | `Pawn.RollPassions` | M2 |
| A name per colonist | `ColonistNames.Of(PawnId)` | M1 |
| A screen with a seed on it, and somewhere to attach | `MenuScreen.NewGame` | U39 |
| A sparse per-pawn channel the HUD can read by name | `PawnAspect` | OQ-45 |
| A request the menu hands to the world | `ColonyRequest` | U34 |

The two rolls are **already pure functions of `(seed, pawn id)`**, which is the fact the whole unit
rests on: a candidate can be rolled with no world, no grid and no tick, because nothing about a
colonist's skills depends on any of those.

**What is missing is that the seed is the world's.** Every colonist rolls from `SimWorld.Seed`,
mixed with its own id. So there is no way to say "this one, differently" — a reroll of one candidate
would have to change the seed of the whole board.

---

## 2. What the owner decided (2026-09-17)

1. **Colonist select is a screen of its own**, reached from the New game screen: Seed → *Next* →
   Colonists → *Start*. The alternative was one taller screen, and it was refused for the reason
   §11.1 fixed the box in the first place — the panel is centred, so a screen that resizes moves
   every row under the pointer. **The cost is a row the owner already approved:** the seed screen's
   Start becomes **Next**, and Start moves to the colonist screen. One extra click per new game.
2. **A reroll changes the person, name included.** A name follows the roll rather than the slot, so
   rerolling slot 1 does not leave you looking at the same Wrenn with different numbers. **The cost
   is that colonists in a running game get different names than they do today**, because the name
   stops coming from the pawn id alone. Nothing is lost — no name is saved anywhere, and
   `ColonistNames` has always been an interface-side identity the simulation knows nothing about.
3. **A new game from the menu starts with exactly the three you chose.** The headless ten-day gate
   keeps its own five-colonist scenario, so **M3's gate is untouched** — it builds its world from
   `ScenarioDef`, never from this screen.

---

## 3. The one real change: a pawn rolls from its own seed

Everything else is wiring. This is the seam.

```
today                            after
-----                            -----
RollPassions(world.Seed)         RollPassions()      -> reads Pawn.RollSeed
RollStartingSkills(world.Seed)   RollStartingSkills()
                                 Pawn.RollSeed defaults to the world's seed at spawn
```

`Pawn.RollSeed` is the seed that pawn's own draws come from. **It defaults to the world seed at
spawn**, so a colony nobody chose — every headless run, every test, every scenario — rolls exactly
what it rolls today. The id is still mixed in, so five default colonists still differ from each
other and from the map.

**Three consequences, each of which had to be decided rather than assumed.**

**It is saved, in a section of its own.** A pawn's seed has to survive a save or a loaded colony
renames everybody, and the skills alone cannot recover it. It goes in a **new `ISaveable` section**
rather than into the pawn record, because sections are length-prefixed and a *missing* one is
simply never loaded — so a save written before this unit loads with every pawn on the world seed,
which is precisely what that save meant. `EdificeSaveSection` is the same move for the same reason,
and neither needed a format bump.

**It is hashed.** It is real state that decides what a pawn is, and the standing rule since OQ-50 is
that saved state which is not derived belongs in the hash. **Every `Simulated` golden moves** and is
re-baked in the same commit with `ODYSSEY_REGOLDEN=1`, the way `U37` and `OQ-14` did before it. A
golden that moves in a commit that did not say it would is a bug in that commit.

**It reaches the HUD as a `PawnAspect`**, `odyssey.pawn.rollseed`. That is what `ColonistNames` now
keys on, and it is exactly the seam OQ-45 built: the Hud assembly cannot reference `Odyssey.Sim` at
all, so a name is provably all a reader needs.

---

## 4. Rolling a candidate before there is a world

A candidate is rolled by **building a throwaway `Pawn` and calling the same two methods the colony
calls**. Not a copy of the arithmetic — the arithmetic itself.

**This is the `NavGraph.HopCost` lesson applied before it costs anything.** A price computed in two
places agrees by coincidence until it does not, and the failure here would be the worst kind: a
screen that shows you a miner and gives you a cook, with both halves individually correct. There is
one function, both callers use it, and a test asserts the colony's pawn equals the card that was on
screen — field for field, not spot-checked.

**Where it runs matters.** `Odyssey.Hud` cannot reference `Odyssey.Sim`, so the roll cannot happen in
the director. `Odyssey.Presentation` references both, so **the presenter rolls the candidate and
hands the director the finished card** — the same bargain `MenuDirector.ShowSaves` makes with a save
listing and `SavePrompt.Type` makes with a name's verdict, for the same reason: what is on screen
and what it means arrive together, and the Unity-free half stays testable in the fast tier.

---

## 5. The screen

Three cards in the same fixed box, each a name, a lock, and the skills worth reading.

```
+------------------------+
|      O D Y S S E Y     |
|                        |
|  Wrenn            [L]  |   a card: name, lock, and the
|  Mining 6  Cutting 3   |   skills a player would choose on
|                        |
|  Odile            [ ]  |
|  Cooking 4  Mining 2   |
|                        |
|  Kester           [ ]  |
|  Hauling 5  Cutting 4  |
|                        |
|  >  Reroll             |
|     Start              |
|     Back               |
+------------------------+
```

**A locked card is not rerolled.** That is the whole of what the lock means, and it is why Reroll is
one row rather than three: per-card reroll and a lock are the same control said twice — lock the two
you like and press Reroll is the same act as rerolling the third, with fewer things on screen. A
card is clicked to lock it.

**Which skills a card shows** is the top few by level, not all thirteen: thirteen rows times three
cards is a screen of numbers nobody reads, and the question a player is answering is "what are these
three good at". The count is derived from what fits, in `HudLayout`, so it is a fast-tier question.

**No portrait.** `U41`, and `09-ui-and-input.md` §4.5's carve-out goes with it.

---

## 6. How it is tested

| Claim | Where |
|---|---|
| a default colony rolls exactly what it rolls today | `StartingSkillsTests`, and the goldens |
| a pawn given its own seed rolls differently from one on the world seed | `StartingSkillsTests` |
| the seed round-trips, and a save without the section loads on the world seed | `PawnSeedSaveTests` |
| the seed moves the state hash | `StateHashCoverageTests` |
| **the colony's pawn is the card that was on screen** | `ColonistSelectTests`, fast tier |
| slots, locks, reroll-skips-locked, three seeds out | `ColonistSelectTests`, fast tier |
| the three cards fit the fixed body | `HudLayoutTests`, fast tier |
| **the colony is built from the three candidates shown** | `StartScreenTests`, PlayMode |

The two in bold are the unit. The first is the `HopCost` guarantee stated as a test; the second is
U39's `TheWorldIsBuiltFromTheSeedInTheBox` one level up, and it is driven through the screen for the
same reason.

---

## 6a. What building it changed about the design above

Written after the code, marked in place rather than folded in, because the wrong version is the
useful record.

- **The card had to be rolled for the slot it will occupy**, and §4 said so without the seam
  obeying it: `ColonistSelect` took a `Func<uint, Candidate>` with no slot in it, so every card was
  rolled as slot 0. Both draws mix the pawn's id in, so two of the three cards would have shown the
  right name and **the wrong skills**, and the player would have found out after pressing Start.
  Caught by reading rather than by failing — nothing in the fast tier could have noticed, which is
  why `EachCardIsRolledForTheSlotItWillOccupy` exists now.
- **A name could not simply move from the id to the seed.** §2 decision 2 says the name follows the
  roll, and taken literally that is a hash of the seed — under which **two of five colonists share a
  name more often than not**, because every colonist a world places itself shares that world's seed
  and the pool holds eight. The seed chooses where in the pool the colony starts reading and the id
  says how far along, which keeps the distinctness the id-only scheme gave for free. On the select
  screen the three carry three different seeds and *can* collide, so `ColonistSelect` draws again —
  a promise about a screen of three rather than about a name.
- **Three cards did not fit the fixed box** at a line per skill: 296 against the body's 284. That is
  §11.4a's own prediction coming true, and the answer was not to grow the box but to put both
  skills on one line — which is how §5's sketch drew them anyway, and which reads as one fact about
  a person rather than two. 242 with room to spare.
- **`PawnContext.Seed` was not populated when placement runs.** It is set by `Sync`, which happens
  on the first tick; placement is before that. The first version of the seam therefore rolled every
  colony in the game from **seed zero**, whatever board it was on, and `StartingSkillsTests` caught
  it as "two different seeds rolled identical starting skills". The context learns the seed at build
  now — a latent trap closed rather than a U40 bug fixed, since anything else reading `ctx.Seed`
  before the first tick had the same problem.

---

## 6b. The card lost its skills to the avatar, and got them back (2026-09-18)

**Reported by the owner as "I'm not seeing the skills rolled randomly on the character generation
screen".** They are rolled, and rolled well — measured over 3,000 draws off `ColonistDraw.Roll`,
only **2.6% of candidates have every live skill at zero** and the best skill is 3 or better on
**70%** of them. Nothing was wrong with the roll. The card was not showing it.

**What the card had become.** §5's sketch above is a name over `Mining 6 · Cutting 3`, and that is
what `U40` built. `U41` put a face on the card (`20-avatars.md` §3) and the skills line was squeezed
out to make room for the occupation — which is drawn from its own salt and therefore says **nothing
about what anybody can do**. So the page whose whole job is telling three people apart showed three
names and three trades, and the skills lived only in the detail pane, only for the card you had
clicked. Comparing three candidates meant clicking each in turn and remembering.

**Three artefacts survived, all describing a line nothing drew**, which is how the loss is
attributable rather than merely visible: `HudLayout.ColonistCardSkills = 2`, read by nothing, whose
own comment says it lives there "so that `ColonistCard` and what is actually drawn cannot
disagree"; `.colonist__skills` in the sheet, applied to no element; and
`HudLayout.ColonistScreenHeight`, modelling a caption and a standalone screen that `BuildSetupPage`
has not drawn since the candidates joined the seed and the board size on one full-viewport page.
The fast tier was asserting that last one fits `StartListMax` — a question about a box this screen
does not sit in.

**And the same omission had a second half that was plainly visible.** `20-avatars.md` §10.6 doubled
`Avatar` 30 → 60 and re-derived every card carrying one: the roster card to 126 × 89, the inspect
header to 60, with the strip share, the top scrim and the coverage ceiling moved to match. **The
candidate card is not on that table.** It kept the 47 px it was given when the face was 30, and then
drew a 60 px face in it — thirteen pixels taller than its own box, at a 53 px pitch. On
`Logs/setup-page.png` the three faces run into one another and over the selection outline. Nothing
failed: the sheet agreed with the model and the model agreed with itself.

**The card is re-derived from its own rows**, the way §10.6 did it: 29 for the name, 18 for the
trade, 18 for the skills — **65**, which is also clear of the 60 px face. The column went 260 → 300
because a 60 px face and a third line left 176 px of text where §3 had sized 206; the page is the
full viewport, so the column is free to grow, which is the lever that design named. The trade drops
from `TextMeta` to `TextDim`, so the three lines read as a hierarchy on the theme's existing tokens
rather than a fifth being invented: **name, then what they can do, then what they used to be.**

**What holds it now.** `SkillSummary` in `Odyssey.Hud` owns the line's rules — live skills only,
nothing at zero, ties in reading order, an em dash for the one candidate in forty with nothing to
show — so they are fast-tier questions rather than things found on screen.
`HudLayoutTests.EveryCardIsAtLeastAsTallAsTheFaceItCarries` is the arithmetic that was missing, and
it asks the same of the roster card and the inspect header.
`StartScreenTests.TheCandidateCardsDoNotRunIntoEachOther` asks it of the laid-out elements, which is
the question a player actually asks, and asserts the skills line exists and is filled.

**The lesson, and it is the same one as the rates review the day before:** a constant that nothing
reads is not harmless. Three of them here described the screen as it was designed while the screen
had quietly become something else, and each would have been trusted by the next session to read it.

---

## 6c. The setup page, played (2026-09-18)

Five corrections after the owner played §6b, and one of them reverses it.

**The card is identity alone: name, age, occupation.** *"From the left hand panels, no need to
display any skills there — Name, Age, Occupation, and resize occupation accordingly to a bigger
size."* The skills line §6b put back came off again. **That is not §6b being undone, it is the
report being answered somewhere better:** the complaint was that nothing on the page varied by
ability, and the detail pane now shows all thirteen skills in two columns with room to read them.
The occupation goes up a step to `Row` and the card is `ColonistAvatar + 2 × ColonistCardPad` — the
face governs the height, so the card cannot go back to being shorter than the thing inside it.
**`SkillSummary` and its tests are deleted rather than left unused**, which is §6b's own lesson
applied to §6b: a constant nothing reads is the artefact that misleads the next session.
**A later session should not restore the card's skills line as a fix for the original report.**

**Traits are a section, not a line of the record.** They sat inside the name/trade/traits stack,
where an empty traits row read as a third fact about the person rather than as the block M7 will
fill. It is below the skills now, under a heading, and the word "Traits" moved from a C# literal
(`"Traits  —"`) into the registry as `ui.newgame.traits`, with `ui.newgame.skills` beside it.

**Sections get `Name`; field captions get `PanelLabel`.** The owner asked for bigger, bolder
headings in two places at once and they want different answers. A section heading over a block on a
full screen read at leisure is `HudTextRole.Name` — 19/600, the one step of the scale that is both
bigger *and* bolder than the body under it. A caption over a text field is `PanelLabel` — 11/600,
upper and tracked, the idiom the stores panel, the rail and the alerts list are already introduced
by. Neither is a new rung: §6b's rule was that this page sits higher up the existing ladder rather
than adding to it.

**Every pressable row on the page is outlined**, in `PanelBorder`, so what can be clicked is visible
without hovering it. **Scoped to `.setup`**, because `.settings__row` is also the settings panel's
and the Menu popover's row and those are read over a running world where a grid of outlines is
noise. **The trap it set, found by reading rather than on screen:** `.setup .settings__row` is
specificity 0,2,0 and the green Start row's `.setup__commit` was 0,1,0, so the grey border would
have won and Start would have quietly stopped being green. Specificity beats order; the green rule
is a descendant now too.

**The page is a panel, and it is the panel the game already has.** Owner: *"use the same
transparency/translucent as the in game menus, not to reinvent."* The element wears `.panel` and
`.window`, so the fill is `rgba(12, 16, 20, 0.96)` and the border and radius are the tokens
`HudStyleSheetTests` already pins; `.setup` overrides only where it sits and how much air it keeps,
inset 24 px so the border reads as a box around the interface rather than as a screen border.
Nothing about the colour is restated anywhere.

---

## 7. Not in this unit

- **Portraits** (`U41`), and the §4.5 carve-out that comes with them.
- **Traits and backstories.** `03-systems-catalogue.md` puts them at M7. Until they exist a candidate
  is a name and a skill set, which the overnight queue's "not scheduled and why" entry already said
  was thin — the owner's answer is that this lands now and gets interesting when traits do.
- **Naming a colonist, or the colony.** `ColonyRequest.Name` is still empty; §11.5 defers it.
- **Choosing a board.** Size, map type and scenario stay defaults on the request, as `U39` left them.
- **More or fewer than three.** The count is a constant with one owner, not a knob.

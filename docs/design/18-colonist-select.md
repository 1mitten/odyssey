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

## 7. Not in this unit

- **Portraits** (`U41`), and the §4.5 carve-out that comes with them.
- **Traits and backstories.** `03-systems-catalogue.md` puts them at M7. Until they exist a candidate
  is a name and a skill set, which the overnight queue's "not scheduled and why" entry already said
  was thin — the owner's answer is that this lands now and gets interesting when traits do.
- **Naming a colonist, or the colony.** `ColonyRequest.Name` is still empty; §11.5 defers it.
- **Choosing a board.** Size, map type and scenario stay defaults on the request, as `U39` left them.
- **More or fewer than three.** The count is a constant with one owner, not a knob.

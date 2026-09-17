# 17 — The start flow: a main screen, and a session you can put down

**Status: built, played once, and revised on what that found.** Written 2026-09-17 on
`claude/start-flow` before the code, as `15-building.md` was, and amended in the same branch where
first the building and then the **playing** of it proved the design wrong. Those amendments are
marked **Corrected** in place rather than silently applied, because the wrong version is the more
useful record.

**The owner played it on 2026-09-17 and four things came back**, all of them things no tier could
have said and three of them plainly right on sight:

| What they said | Where it went |
|---|---|
| the worktree had no art | not a design fault — the licensed packs are gitignored, so a worktree has none until it is junctioned (`docs/lessons.md`). Linked. |
| *"it didn't save where the camera was and the exact state of where I was at"* | §5a, a view section in the save |
| *"when I clicked on settings it appeared below the menu"* | §4, settings is a screen of this menu now |
| *"keep it fixed width and height"* and *"add the date/time in a longer row"* | §4 |

**What no tier can still say** is in §10. This is **U38** of the `MS` milestone in `docs/plans/vertical-slice.md`, and
it absorbs the half of **U36** that never landed — the `Saves` folder on disk, which U36's own
commit message left to "the caller (a menu)". This is that menu.

**The owner's decisions are §2 and they are settled, not proposals.** They were taken in an
interview on 2026-09-17 and two of them overturn what the plan had written down; §2 says which and
why, so a later session does not re-litigate them from the plan's older text.

Read first: `09-ui-and-input.md` §6 (the input rules and the Escape unwind), `10-ui-panel-catalogue.md`
B17 and B18, and `14-hud-layout.md` (how the built HUD is drawn and measured). The session seam this
stands on is U35, in `OdysseyBootstrap.BuildSession` / `TeardownSession`.

---

## 1. What exists already, and what is genuinely missing

**The in-game menu is not missing.** The command bar has a Menu popover with a Game section, and
the settings panel has four tabs and an exit row, and both are windows with a close X and an
Escape rule. The plan's U38 row — "New game, Load, Save, Options, Quit … the project's first true
modal" — was written as though none of that existed. The owner's correction on 2026-09-17 was
one sentence: *"There is already an in game menu/settings - reuse that."*

What is actually missing is **the screen before the game**: there is nowhere to stand when no world
has been built. `OdysseyBootstrap.buildOnPlay` is true, so pressing Play lands in a colony and the
only way out is to quit the application.

Four other pieces are already in place and this unit is mostly the wiring between them:

| Piece | Where | Landed |
|---|---|---|
| `BuildSession()` / `TeardownSession()` | `OdysseyBootstrap` | U35 |
| `ColonyWorld.Build(ColonyRequest)`, `ColonyWorld.Recipe(day)` | `Sim/Pawns` | U34, U36 |
| `WorldSave.SaveToFile` / `LoadFromFile` / `ReadHeaderOnly`, `SaveRecipe` | `Sim/Saving` | U36 |
| `SeedEntry` — draw, reroll, format, parse | `Sim.Contracts` | U39, early half |

**Nothing in the repository names `Application.persistentDataPath`.** That is the gap: the format
can write a file to a path, and no code anywhere decides what the path is.

---

## 2. What the owner decided (2026-09-17)

1. **Reuse the in-game surfaces.** No second in-game menu is built. Save, Load and Quit to main
   menu join the **settings panel**, beside the exit row, under the same hairline. *(The panel
   catalogue's B18 entry says the quit half "moves here when this menu exists"; it does not, and
   the entry is amended in §8 rather than quietly ignored.)*
2. **The main screen is a true modal, and it does not pause.** A scrim over the whole viewport,
   every pointer and key event swallowed but its own dismissal — `09` §6 case 5, which has never
   had anything to test against. The simulation is not stopped.
3. **Save and Load both work, end to end**, rather than the shell-with-disabled-rows this unit
   could have been. That brings U36's disk half here and puts most of the `MS` gate in this PR.
4. **Play lands on the main screen**, with `buildOnPlay` kept as the development flag that skips
   it. The flag stays true in the test rig, so no existing PlayMode test changes.
5. **Quit to main menu tears the world down** through U35's `TeardownSession`. The main screen
   sits over an empty backdrop; nothing runs behind it.
6. **Escape from the settings panel closes straight to the world**, as it does today.

**Two consequences of (6) worth stating out loud**, because the plan's U38 row predicted the
opposite and a later session reading only the plan would go looking for a change that is not there:

- **`SettingsDirector.Escape` gains no new case.** The main screen is not reached by Escape — you
  reach it by choosing Quit to main menu — so the unwind order (tool → popover → palette → panel →
  open panel) is untouched. The plan's "Escape unwinds into it through the one rule" describes an
  in-game menu that this unit deliberately does not build.
- What Escape *does* need is the modal rule from (2): **while a modal is up, Escape is its own**,
  and does not unwind anything behind it. That is one guard in front of the existing rule, not a
  second copy of it.

---

## 3. Where consistency is centralised

The owner's instruction was that the new screen keep the existing interface's style and format and
that this be **represented in the work rather than remembered by whoever writes it**. The HUD
already enforces that in four places, and this unit adds to each rather than around it.

| Seam | Owns | What U38 puts through it |
|---|---|---|
| `Panel` → `Window` → `Popover` (`HudShell.Panels.cs`) | fill, hairline, radius, padding, the close X | a fourth link, **`Modal()`** — the scrim, the capture and the centring, in one place |
| `HudTheme` ↔ `Hud.uss`, parsed by `HudStyleSheetTests` | every colour and every anchor | one new colour token (the scrim) and the screen's anchors, both added to that test's tables |
| `HudType` / `HudText.Make` | every font size — "six sizes and no others" | every word on the screen. **Not one new size.** `TheSheetSetsNoTypeAtAll` already fails on any attempt |
| `Registry.Label` ← `docs/design/icon-keys.csv` | every word a player reads | every row name, so the owner can rename any of them without a line of C# |
| `HudLayout` (Unity-free) | the arithmetic that decides whether two regions overlap | the screen's geometry, so "it fits at 1280×720 and at 150 per cent" is a fast-tier test rather than an opinion |

**And one new seam, which is the answer to "centralised":**

`SessionCommands`, in `Odyssey.Hud`. The row set is **data, not two hand-written lists**. Each
session-level command carries its registry key, its ordering, the contexts it appears in, and
whether it is destructive enough to ask twice. The main screen builds its rows from it and the
settings panel builds its session rows from it, so the two surfaces cannot drift apart, and a
fast-tier test states each context's list exactly. It is the same bargain `HudCommands` already
makes for the command bar and `PaletteTools` for the build palette — **the table is data in the
assembly the fast tier can see, and the Presentation side only draws it.**

```
              SessionCommands (Odyssey.Hud, Unity-free, one table)
                   |                                  |
        MenuDirector (main screen)          SettingsDirector (in game)
                   |                                  |
        HudShell.Start.cs                   HudShell.Bar.cs
                   \__________ Modal()/Window() __________/
                                    |
                      Panel() + HudText + IconBadge + Registry.Label
```

---

## 4. The screen

A centred modal over a scrim, built from `Modal()`, with the game's name over a column of rows in
the `.settings__row` idiom the rest of the interface already uses. No new control is invented: a
row is an icon, a label and an optional hotkey cap, which is what a `.menu__row` and a
`.settings__row` already are.

```
+==========================================================+
| :::::::::::::::: scrim, whole viewport ::::::::::::::::: |
| :::                                                  ::: |
| :::            +------------------------+            ::: |
| :::            |      O D Y S S E Y     |            ::: |
| :::            |                        |            ::: |
| :::            |   >  New game          |            ::: |
| :::            |      Load              |            ::: |
| :::            |      Settings          |            ::: |
| :::            |      Exit game         |            ::: |
| :::            +------------------------+            ::: |
| :::                                                  ::: |
+==========================================================+
```

**Three screens, not one, and the panel is the same box on all of them.** Corrected after the owner
played it on 2026-09-17; the first version had two screens and sized itself to its content.

- *"keep it fixed width and height because it becomes hard to read between loading and saving
  screens"* — the panel is centred, so a panel that changed height moved every row under the
  pointer on the way from the menu to the load list. **A menu whose items walk away as you navigate
  is a menu you have to re-find each time.** `HudLayout.StartPanelHeight` is a constant now and
  `StartBody` is the one box every screen fills; the load list's ceiling is *derived* from it rather
  than written down, so the two cannot disagree. The root screen's four rows sit at the top and
  leave air below, which is the cheaper of the two mistakes — the alternative is a list that
  scrolls at four.
- *"when I clicked on settings - it appeared below the menu - it would [be] cleaner if main menu
  disappeared and settings appeared but could navigate back to main menu"* — both panels are
  centred, so one landed over the other and the pair read as a stack rather than as one screen
  showing what was asked for. **Settings is a third screen of this menu**, `MenuScreen.Settings`,
  and `Back()` is the way out of it — the same way out the load screen already had. The scrim
  stays while it shows, because the state is still modal and there is still no world behind any of
  it; `HudModal.ShowScrimOnly()` is the one sanctioned case where the two halves differ. Closing
  the panel by *any* means returns to the menu, because the ways out of that panel already existed
  and this has to be all of them.
- *"add the date/time in a longer row"* — a folder is mostly repeated attempts at the same colony,
  so "Ashford, Day 12" does not tell two rows apart. The meta line is now **day, board and when the
  file was written**, and the panel widened from 320 to 420 to carry it without cutting a word.
  The date is formatted in the player's local time in `SaveFiles`, and reaches `MenuDirector` as a
  **string** — formatting a date is a question about the player's machine, and `Odyssey.Hud` is
  compiled without any of that in mind.

**The root screen** above, and a **load screen** listing the saves folder —
one row per file, showing colony name, day and map, read from the header alone through
`WorldSave.ReadHeaderOnly` with no body parsed. The load screen is reached from the root and backs
out to it; that is the whole of the navigation, and it is the `MenuDirector`'s entire state.

**No world behind it.** When the main screen is showing, no session is built: the backdrop is the
scrim over whatever the camera sees, which is nothing. That is what makes the modal cheap — there
is no live HUD to swallow input from, because the HUD is not built either.

**Options opens the existing settings panel**, over the main screen, unchanged. It is the one
surface that is the same in both contexts, which is the point of reusing it.

**Corrected, and it is the owner's to settle:** the screen says **Settings** and **Exit game**,
not "Options" and "Quit". Those two rows reuse `ui.settings.panel` and `ui.settings.exit` rather
than minting near-duplicate keys, so one thing has one word everywhere and the icon art already
mapped to them keeps working — and the words those keys carry are the ones the settings panel has
always used. Changing them is one line each in `docs/design/icon-keys.csv`; inventing a second word
in C# is not an option, because the whole point of the registry is that the owner can rename any of
this without a code change.

---

## 5. The saves folder

`Application.persistentDataPath/Saves`, created on demand. The folder is Presentation's to name —
Sim cannot see `Application` — and everything *about* a file that is not the folder is Unity-free
and in the fast tier:

- **Corrected 2026-09-17, after the owner played it: a save has the name the player gave it.**
  *"I notice you keep saving a new game everytime. We should be able to name the save game (with a
  default) and then can overwrite that save if need be — otherwise lots of saves will be created."*
  §10 of this file had already admitted the fault; the folder only ever grew, because the name was
  derived from the colony and the day and then **disambiguated around whatever was already there**,
  so two saves on one day gave `landfall-day-4` and `landfall-day-4-2`, for ever.

  **The shape, and why it is two rows rather than one prompt.** A session is **bound** to a file —
  the one it was loaded from, or the one it last saved to. **Save** writes over that; **Save as**
  asks for a name and binds to the answer. Prompting on every press is the version that does not
  multiply files and *does* annoy, because a player who saves often would confirm the same name
  every time. Naming once and overwriting after is the ordinary case, so the ordinary case is one
  press and the exception says what it is. The first save of a colony has nothing bound, so it asks
  — which is where the default comes in: the colony's own name.

  **A player-chosen name is never disambiguated.** That is the whole point: a name maps to exactly
  one file, so saving again overwrites it. Naming a save that already exists therefore *is* an
  overwrite, and the prompt says "Overwrite" and asks twice before doing it.

  **The trap this opened.** The old scheme was accidentally safe: every stem contained `-day-N`, and
  that is what kept a colony called `con`, `aux` or `com1` off a **Windows reserved device name** —
  `con-day-4.odyssey` is creatable, `con.odyssey` is not. A player-chosen name has no `-day-` in
  it, so that protection had to be put back deliberately rather than inherited.

- **A save with no name of its own** — there is no longer a route to one from the interface, but the
  recipe-derived naming remains for any caller that has no player to ask.
- **The listing is sorted newest first**, by the file's own modification time, and an unreadable
  or future-version file is **listed with its reason rather than hidden**. A save the build cannot
  open is a thing the player needs to be told about; silently omitting it is how a player concludes
  their colony is gone.

## 5a. The view is saved too

**Added after the owner played it** (2026-09-17): *"it didn't save where the camera was and the
exact state of where I was at - which it should do."* They are right, and the omission was a
consequence of a rule read too literally. `CLAUDE.md` says **nothing in presentation is in a cell, a
save or the hash**, and that rule is load-bearing — but its purpose is determinism, and determinism
is the *hash's* business, not the save's. Where the camera was pointing cannot affect a tick.

So the save carries a **`"view"` section**, and the thing that makes it safe is stated rather than
assumed: it is `ISaveable` and **not `IStateHashable`**, so it cannot move the state hash, cannot
desync a load, and cannot appear in a determinism gate. It holds the camera's focus, yaw, pitch and
distance; the slice layer; the selected colonist; and the game speed, paused included.

**Three properties it has to have, none of which is obvious:**

- **It lives in the Presentation assembly.** Sim cannot see a camera, and must not learn to.
- **Reading and applying are two phases.** The section is read while the world is still being
  restored, and the camera has not been pointed anywhere yet; applying it during `Load` would
  fight the build that follows.
- **Floats are written as exact bits, not rounded.** A camera that drifts a little on each
  save-and-load round trip is a bug nobody notices for weeks and then cannot reproduce.

**A save without the section is not an error.** `WorldSave` already skips a section a build does not
recognise, so an older file simply loads without a view and keeps the generated start position —
which is exactly what it did before this existed.

**Loading is not a special path.** `WorldSave.Load`'s own contract is that the world is built from
Defs and a seed first, exactly as a new game would be, and then has its state laid over the top.
So Load is: read the header, build a session from the recipe it carries, restore into it. One
construction path, which is what U34 and U35 were for.

---

## 6. How to test it, written before the code

**Fast tier** (no Unity, ~11 s):

| What | Test |
|---|---|
| The row set per context | `SessionCommands` lists exactly these, in this order, for the main screen and for the settings panel |
| Every new word is in the CSV | extends `RegistryTests`, which already holds the ledger, the job list and the settings panel |
| The screen fits and overlaps nothing | extends `HudLayoutTests` at 1280×720, 1920×1080 and 2560×1440, at all six interface scales |
| No colour or anchor drifts | extends `HudStyleSheetTests`' two tables with the scrim and the screen's anchors |
| A file name is derived from a recipe, and collisions disambiguate | `SaveCatalogue` |
| A folder listing sorts newest first and keeps the unreadable, with a reason | `SaveCatalogue` |
| The menu's navigation | `MenuDirector`: root → load → back, and what each row asks for |

**Corrected:** the gate test is in the **fast tier**, not `Long`. This section filed it under
`Long` before anyone had timed it; measured, the whole file is about 420 ms — a 20,000-tick run is
75 ms and each full-grid hash about 3 ms. It runs after every commit, which is worth more than the
0.4 s it costs.

**The gate test, headless** (Sim): a colony is built, run, **saved**, **torn down**,
**rebuilt from the header's recipe**, **loaded**, and its full state hash equals the hash before
the save. This is the `MS` gate's central claim and OQ-50 is what makes it mean anything — before
the world was in the hash, this test could have passed over a map it could not see.

**Its negative control, which is the part worth insisting on:** the same test with one cell mined
between the save and the comparison **must fail**. A round-trip test that cannot fail is the exact
fault OQ-05's control found in `WorldRoundTripTests`, and it will be run and recorded, not assumed.

**Unity tier:** the main screen appears with `buildOnPlay` off; New game builds a session; Quit to
main menu tears one down and leaves no figures or materials behind (the U35 leak check, which
ignores with its reason in the rig without a module catalogue); Load restores a colony.

**By hand, because no tier can say it** — the list for the owner is §9.

---

## 7. What could make this bigger than it looks

- **`buildOnPlay` defaulting to false changes what Play does.** Every PlayMode test that assumes a
  world exists on Play would break. The rig sets it explicitly; if any test relies on the default,
  that is found by running the tier, not by reading.
- **The scrim is the first full-viewport pointer target in the project.** `PointOverUi` answers
  "is the pointer over interface", and a full-screen element makes it always true. The modal guard
  has to sit in front of that answer rather than inside it, or the camera stops responding the
  moment a modal is ever built.
- **Tearing a session down mid-frame.** Quit to main menu is invoked from a click inside the HUD
  the teardown destroys. The action has to be deferred to the end of the frame, or the callback
  returns into freed elements.
- **A save written while the world is ticking.** Decision (2) says the game is not paused. In
  practice the save is taken from the settings panel during a frame, between ticks, so the world is
  not mid-tick — but that is a property of where it is called from, and it gets an assertion rather
  than a comment.

---

## 8. What this amends elsewhere

- **`10-ui-panel-catalogue.md` B18** says the quit half moves out of B17 when the game menu exists.
  Decision (1) says it stays; the entry is rewritten in the same commit rather than left to
  contradict the code.
- **`09-ui-and-input.md` §6 case 5** — "a modal swallows every pointer and key event except its own
  dismissal" — has never been true of anything. It becomes true here, and the case gets its test.
  Case 6's closing note, "the last step opens the settings panel … because there is no game menu
  yet", stays accurate and is left alone.
- **`docs/plans/vertical-slice.md` U38** describes an in-game menu and an Escape change that this
  unit deliberately does not build. The row is rewritten to what was actually decided, with §2 of
  this file as the reason.

---

## 9. Deliberately not in this unit

- ~~**The seed you can read and reroll (U39's screen half).** `SeedEntry` exists and is tested; the
  text field it belongs in sits on a New game screen that this unit does not build. New game here
  starts a world on a drawn seed. **This is the next unit and it now has somewhere to attach**,
  which was the whole reason U38 came first.~~ **Built 2026-09-17 — §11.**
- **Colonist select and portraits (U40, U41).** They hang off the New game screen, not off this one.
- **A Resume row.** There is no world to resume to: the main screen only exists when no session is
  built, by decision (5).
- **Autosave.** Nothing writes a file the player did not ask for. It wants a cadence, a rotation and
  a policy about overwriting, and none of those is a menu.

---

## 10. What the tiers cannot say, and what is left open

**Nobody has pressed Play on any of it.** Every claim below the line in §6 is a test result. These
are not:

- **Whether a 420 × 384 box reads as a start screen**, or as a settings panel that has wandered into
  the middle of an empty scene. It is fixed now, at the owner's request, which means it is also the
  size of the *emptiest* screen it shows — the root's four rows leave a good deal of air under them.
- **Whether a 19 px title is a title.** The type scale is closed at six steps, so the game's name
  is set at the same size as a colonist's name in the inspect pane. A bigger one is a deliberate
  change to `HudType`, not a literal — but it is a change somebody may well want.
- **Whether landing on a menu is what you want every time you press Play.** `buildOnPlay` is right
  there and turning it on restores the old loop exactly.
- **The two words.** See §4's correction: the rows say Settings and Exit game.

Three things the code knows are unfinished, named so the next session does not rediscover them:

1. **The scenario table is written twice** — `OdysseyBootstrap.ScenarioFor` and
   `SessionRoundTripTests.ScenarioByName` each map two `defName`s by hand, because nothing in
   `Odyssey.Sim` turns a scenario name back into a `ScenarioDef`. It wants a real lookup. It is not
   urgent: a scenario acts only at tick zero, so a loaded world is unaffected by getting it wrong,
   and both copies say so out loud.
2. ~~**Save always writes a new file.** There is no overwrite, no rotation and no autosave, so a
   folder is a history and it only grows.~~ **Fixed 2026-09-17** — the owner hit it on the first
   evening, which is about as long as "not a policy anybody has chosen" was ever going to last. See
   §5. **Autosave is still not built**, and that is still deliberate: it wants a cadence, a rotation
   and a policy about what it is allowed to overwrite, and none of those is a menu.
3. **The load list has a ceiling and no search.** Eight rows before it scrolls, newest first. A
   folder of two hundred saves is a scroll.

---

## 11. The New game screen and the seed (U39, 2026-09-17)

**This is `U39` of the `MS` milestone**, and it is the second half of a unit whose first half landed
a day early: `SeedEntry` — draw, reroll, format, parse — has been in `Odyssey.Sim.Contracts` with 17
fast-tier tests since U38's branch, because those rules need no screen and would otherwise have been
written inside a text field's callback where the fast tier could never reach them. What follows is
the screen they were written for.

**What it replaces.** `HudShell.OnNewGame` was one line — `BuildSession(SeedEntry.Draw(), null)` —
so a seed was drawn, used, and never shown to anybody. The world a player got was unrepeatable by
construction: there was nowhere to read the number that made it and nowhere to type it back.

### 11.1 The screen

A fourth screen of the same box, between the root and the world.

```
+------------------------+       Root                 New game
|      O D Y S S E Y     |       ----                 --------
|                        |       New game  ------->   Seed  [ 3829174463 ]
|   Seed                 |                            Reroll
|   [ 3829174463    ]    |                            Start
|                        |                            Back  <-------+
|   >  Reroll            |                                          |
|      Start             |       Back on the root screen does nothing;
|      Back              |       Back here returns to the root.
+------------------------+
```

**It is the same `StartBody`.** Four controls in a box sized for six save rows, so the panel's height
does not move between the root, this screen, the load list and settings — the rule the owner asked
for in §4 and the reason the body became a constant. `StartNewGameHeight` is *derived* from the
parts it is made of, and a fast-tier test asserts it fits inside `StartBody` rather than trusting
that four rows are fewer than seven.

### 11.2 Four decisions, and what would change each

**1. The seed's state lives in `Odyssey.Hud`, not in the text field.** `SeedField` is a small
Unity-free director in the `SavePrompt` idiom: it holds the text, the parsed seed and whether the
text names one, it raises `Changed`, and it performs nothing. The presenter owns the `TextField` and
echoes every keystroke into it, exactly as the naming prompt already does. That is what makes every
rule below a fast-tier test instead of something judged once with a finger on the keyboard.

**2. An unreadable seed refuses to start, rather than starting something else.**
`SeedEntry.TryParse` was written to return false and leave the caller holding the seed it already
had — so the honest reading is that the field and the world can disagree, and the screen must not
resolve that disagreement silently. If the box says `twelve` and Start builds seed 3829174463, the
player has been lied to about the one number this screen exists to show. So `SeedField.Usable` is
false, `Start()` refuses, and the row is drawn disabled — the same three-part answer
`SavePrompt.CanConfirm` already gives an unusable name, including the part that matters: the rule is
enforced in the director as well as in the drawing, because a rule kept only by whoever draws it is
a rule the next caller does not have.

**3. Entering the screen draws a fresh seed.** Not "keep what was there": pressing New game twice
and getting the same world both times reads as a reroll that does not work, and there is no way to
tell that from the outside. The cost is a typed seed lost by backing out and coming in again, which
is a keystroke; the other mistake is a player who cannot tell whether the game is random.

**4. The seed is the only knob, and that is the plan's own instruction.** Size, map type and
scenario stay defaults on `ColonyRequest`, so they remain tunable later without any new interface to
unpick. A board chooser is `MS`'s business after U41, not a row somebody adds here because the space
was free.

### 11.3 Three words, and why one of them is new

| Key | Word | Why not reuse something |
|---|---|---|
| `ui.newgame.seed` | Seed | the field's label; nothing else in the game names a seed |
| `ui.newgame.reroll` | Reroll | ditto |
| `ui.newgame.start` | Start | **the interesting one.** `ui.session.newgame` is on the root row and means *open this screen*; this means *build this world from this number*. One opens a question and the other answers it, and giving both the words "New game" would put the same label twice in one navigation — the player could not tell which press committed them |

Every one goes in `docs/design/icon-keys.csv` with the wiki and `Registry.g.cs` regenerated in the
same commit, per the standing content rule. Nothing on this screen is named in C#.

### 11.4 How it is tested

| Claim | Where |
|---|---|
| draw, reroll, format, parse | `SeedEntryTests` — already green since U38's branch |
| the field holds text, parses it, refuses what is not a seed, rerolls to something different | `SeedFieldTests`, fast tier |
| New game navigates rather than building; Start raises the seed; Back returns; the seed is redrawn on entry | `MenuDirectorTests`, fast tier |
| four rows fit the fixed body | `HudLayoutTests`, fast tier |
| **the number on screen is the number the world was built from** | `StartScreenTests`, PlayMode — the one claim no fast-tier test can make, because it spans the field, the director, the bootstrap and `ColonyRequest` |

The last row is the point of having a PlayMode test here at all. Everything above it is a promise one
assembly makes; that one is the promise the whole unit exists to keep, and the way to make it bite is
to type a seed the draw would never have produced and assert the built world carries *that* number.

### 11.5 Not in this unit

- **Colonist select and portraits (U40, U41).** They hang off this screen — it is what they were
  waiting for — but a candidate is not worth showing until rolled skills are on it, and `U37` landed
  on 2026-09-17, so U40 is unblocked the moment this merges.
- **Free-text seeds in the Minecraft idiom.** `SeedEntry` records the argument: they only work if the
  typed text is kept beside the number, which is a `SaveRecipe` field now — so it is a live question
  and deliberately still not answered, because a second representation arriving before anything
  stores one is how two sources of truth start.
- **A named world.** The colony's name is still the scenario's. Naming it belongs with the same
  screen as choosing a board, and both wait for the rest of `MS`.

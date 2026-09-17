# 17 — The start flow: a main screen, and a session you can put down

**Status: built, all three tiers green, and nobody has pressed Play on it.** Written 2026-09-17 on
`claude/start-flow` before the code, as `15-building.md` was, and amended in the same branch where
the building of it proved the design wrong. Those amendments are marked **Corrected** in place
rather than silently applied, because the wrong version is the more useful record.

**What no tier can say** is in §10: whether a start screen at 320 px reads as a start screen,
whether a 19 px title is a title, whether landing on a menu instead of a colony is what the owner
wants every time they press Play. This is **U38** of the `MS` milestone in `docs/plans/vertical-slice.md`, and
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

**Two screens, not one.** The root screen above, and a **load screen** listing the saves folder —
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

- **The file name comes from the recipe.** Colony name, slugged, plus the day, plus a
  disambiguator when that collides. A save is identified by its header, never by its name; the
  name exists so the folder is legible to a human in a file browser.
- **The listing is sorted newest first**, by the file's own modification time, and an unreadable
  or future-version file is **listed with its reason rather than hidden**. A save the build cannot
  open is a thing the player needs to be told about; silently omitting it is how a player concludes
  their colony is gone.

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

- **The seed you can read and reroll (U39's screen half).** `SeedEntry` exists and is tested; the
  text field it belongs in sits on a New game screen that this unit does not build. New game here
  starts a world on a drawn seed. **This is the next unit and it now has somewhere to attach**,
  which was the whole reason U38 came first.
- **Colonist select and portraits (U40, U41).** They hang off the New game screen, not off this one.
- **A Resume row.** There is no world to resume to: the main screen only exists when no session is
  built, by decision (5).
- **Autosave.** Nothing writes a file the player did not ask for. It wants a cadence, a rotation and
  a policy about overwriting, and none of those is a menu.

---

## 10. What the tiers cannot say, and what is left open

**Nobody has pressed Play on any of it.** Every claim below the line in §6 is a test result. These
are not:

- **Whether a 320 px column reads as a start screen**, or as a settings panel that has wandered
  into the middle of an empty scene. It is the width the longest row needs and no more, which is
  this interface's rule everywhere else and may be wrong for the one screen that has nothing beside
  it to be economical against.
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
2. **Save always writes a new file.** There is no overwrite, no rotation and no autosave, so a
   folder is a history and it only grows. That is the right default for a prototype with no
   confirmation dialog, and it is not a policy anybody has chosen.
3. **The load list has a ceiling and no search.** Eight rows before it scrolls, newest first. A
   folder of two hundred saves is a scroll.

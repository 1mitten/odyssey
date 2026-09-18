# 20 — Composed flat avatars: a face beside every name

**Status: designed 2026-09-18, before the code, from an interview the same day.** It replaces the
plan's `U41 Portraits` row rather than implementing it, and the replacement is cheaper in one
specific way recorded in §9.

Read first: `09-ui-and-input.md` §4.5 (which already prescribes this and refuses the alternative),
`10-ui-panel-catalogue.md` A2 (the roster card's full specification), `19-world-setup.md` (the page
two of the four sites live on) and `docs/adr/0007-pixel-art-icon-pipeline.md`.

---

## 1. Why this unit exists

The owner asked for the profile to sit next to the person's name, and for the same avatar to serve
both character selection and the roster bar. Both halves of that are already half-built and neither
has a face in it:

| Surface | What is drawn today | Where |
|---|---|---|
| Roster card | `.card__avatar`, 26 × 26, a People-coloured tile with the name's **first letter** on it | `HudShell.Panels.cs:452` |
| Inspect header | `IconBadge("ui.pawn.colonist", 30)` — the **outlined square**, because that key has no art | `HudShell.Inspect.cs:435` |
| Setup candidate row | nothing: 260 × 47, a column of name over trade | `HudShell.Start.cs:371` |
| Setup detail pane | nothing: `.detail__name` at the top-left of a ~1516 px pane | `HudShell.Start.cs:398` |

And the colour half has existed since M1 without anybody noticing it was an avatar recipe.
`ColonistAppearance` (`Presentation/Rendering/ColonistAppearance.cs:66`) is a `Look` index plus
`Skin`, `Hair`, `Cloth` and `Cloth2` as `Rgb24` — integer arithmetic over four independent hash
streams, no float, no `System.Random`, deterministic under both runtimes. It feeds a 3D character's
atlas today. It is four colours and a shape index, which is exactly what a flat avatar needs.

**What does not exist is a human figure in any sheet.** `icon-map.csv:77` records
`ui.pawn.colonist` as a gap and says so in as many words: *"a human figure. No sheet contains one,
and this is the most-used icon in the HUD."* Only sheet 06 is even in the repository. So the art has
to come from somewhere other than the pipeline, and §2 decision 1 is where it comes from.

---

## 2. What the owner decided (2026-09-18, interview)

1. **Drawn now, a sheet later, against a layer contract.** The figure is `Painter2D` paths in
   `HudGlyph`'s existing box — the same answer the 37 build-palette icons took, for the same
   reason — but written so a ninth sheet drops in one layer at a time. `IconBadge.Dress()` already
   is that mechanism: art present suppresses the vector and draws the texture, art absent paints
   the shape.
2. **The recipe moves down into `Odyssey.Hud`.** `ColonistAppearance`, `Rgb24`, `ColonistPalette`
   and `ColonistLook` are already UnityEngine-free and the file says so; this is a file move. The
   book stays in Presentation because it touches the module catalogue.
3. **The face is dealt from the pawn's `RollSeed`**, like the name, the age and the occupation —
   not from `castSeed`. `randomCastEachSession` becomes a development override rather than the
   default behaviour.
4. **Avatar only on the roster card.** Not the mood ring, not the status glyphs, not the layer
   badge; the health arc is impossible regardless, because there is no health model.
5. **The tile is the colonist's own clothing colour**, with skin and hair over it. Fifty cards are
   then fifty fills rather than one category hue repeated fifty times.
6. **A contact sheet before a playtest.** `Logs/avatars.png` at all three sizes, judged as an image,
   before the owner spends a session on it.
7. **All four sites in one unit.** The two that already have a slot are nearly free, and splitting
   would pay for the composer twice and leave a letter tile beside a face on the same screen.

---

## 3. The layout

Three sizes, of which two are existing `HudLayout` tokens and the third is one ADR 0007 already
names as legal.

```
Roster card (106 x 63, unchanged)          Inspect header (38 tall, unchanged)
+------------------------------+           +----------------------------------+
| [ 26 ]  Wrenn                |           | [ 30 ]  Wrenn                    |
| (axe)   Chopping             |           | [    ]  Colonist — chopping      |
+------------------------------+           +----------------------------------+

Setup candidate row (260 x 47)             Setup detail pane
+------------------------------+           +----------------------------------+
| [ 30 ]  Wrenn, 34            |           | [  64  ]  Wrenn, 34              |
| [    ]  Scrapper             |           | [  64  ]  Scrapper               |
+------------------------------+           | [      ]  Traits  —              |
                                           |                                  |
                                           |  Animals   -    Intellect   -    |
```

**The roster card and the inspect header cost nothing, and that is a fact about the tests rather
than an estimate.** `HudGeometryTests.TheCardIsWideEnoughForItsRowsAndNoWider` (:521) already
budgets `2*CardPad + CardAvatar(26) + CardAvatarGap(8) + widestName` on the name row, and coverage
is summed over region *rects*, not over the elements inside them. An image of the same 26 px in the
same box moves neither. The card's initial letter goes with it.

**The candidate row becomes a row wrapping a column** — the shape `.card__top` / `.card__names`
already is on the roster card. 244 px of inner width less 30 less an 8 px gap leaves **206 px** for
the two lines. The longest occupation in the registry is 21 characters
(*Construction labourer*, *Pet groomer assistant*, of 174), which fits with room; but that is an
arithmetic claim and §7 turns it into a measured one, because the standing rule is that **only a
colonist's own name may be cut short**. If it ever stops fitting the lever is `.colonists`, whose
260 px is free to grow — the page is the full viewport, not the fixed box.

**The detail pane gets 64 px** to the left of the name / trade / traits stack, which is
26 + 18 + 4 + 18 = **66 px** tall, so the two line up within two pixels. `.setup__detail`'s top
becomes a row. This is the one place in the game with room for a portrait and it is the screen
where a player is choosing between people, which is the only moment the choice is worth looking at.

New constants: `DetailAvatar = 64`, `DetailAvatarGap = 18`, `ColonistAvatarGap = 8`. The candidate
row reuses `HudLayout.Avatar` (30) and the card reuses `CardAvatar` (26). Every one of them is
anchored in `HudStyleSheetTests`, which fails the fast tier if the sheet and the model disagree.

---

## 4. What an avatar is made of

**Four filled layers in HudGlyph's own 24-unit box**, so a 26 px card avatar and a 64 px detail
avatar are one drawing at two scales rather than two drawings:

| Layer | Shape | Colour |
|---|---|---|
| 1 Tile | rounded rectangle, the full box | `Cloth` |
| 2 Shoulders | a rounded trapezium rising from the bottom edge, one of **three** widths | `Cloth2` |
| 3 Head and neck | a rounded form on the centre line | `Skin` |
| 4 Hair | one of **eight** crown shapes over the head | `Hair` |

**There is no face.** No eyes, no mouth, no nose. This is a deliberate limit and not a stage we
grow out of: the project has already measured that its own pixel art loses its grooves at 17 px and
goes to noise at 16 (`Logs/skill-icons.png`), and a card avatar is 26 px. Two dots and a line at
that size read as damage, not as a person. The avatar is a **silhouette portrait**, which is why
the same paths work unaltered at 26, 30 and 64.

**Variation that is not colour.** Eight hair shapes times three builds is twenty-four silhouettes,
multiplied by 7 skins, 9 hairs and 14 cloths from `ColonistPalette`. The two new indices are rolled
in the `ColonistIdentity` idiom — `RollSeed` plus a salt of its own per attribute — which is the
fifth and sixth thing that seam has carried, after the name, the age, the occupation and the skills.

**The type is `ColonistFace`, in `Odyssey.Hud`.** One struct, one static `Of(uint rollSeed,
PawnId id)`, carrying four `Rgb24` values and two shape indices. It calls `ColonistAppearance.Of`
for the colours rather than re-deriving them — the `NavGraph.HopCost` rule, and the whole point of
decision 2: **the face on the card is the same derivation as the figure in the world, not a second
one that agrees by coincidence.** Being in `Odyssey.Hud` means every rule about it is a fast-tier
test at about 14 seconds rather than a Unity run.

**The element is `AvatarGlyph`, in `Odyssey.Presentation.Ui`**, a `HudGlyph` subclass that paints a
`ColonistFace` at any size. `SetFace` is a no-op on an equal face, which is what keeps
`Adr0003_F1_TheDenseHudHoldsItsBudgetAndAllocatesNothing` green: that test fails on **any** GC
collection in the measured window, not merely on a byte delta, so an avatar composed per refresh
would fail it outright. `ColonistFace` is a readonly struct with `IEquatable`, so the guard costs
no allocation either.

---

## 5. Which seed, and what it changes about the 3D figure

Today `ColonistAppearanceBook` is constructed with one `castSeed` for the whole colony and derives
from `(castSeed, pawnId)`. With `randomCastEachSession` on — the default — that seed is
`UnityEngine.Random.Range(1, int.MaxValue)`, so **pressing Play deals new faces every time**.

That cannot survive a candidate card with a face on it. A reroll changes the name, the age, the
occupation and the skills, all of which key on `RollSeed`; a face keyed on anything else would sit
there unchanged while everything around it moved, and would then be a **different** face again in
the world the player started. So:

- `ColonistAppearanceBook.For(int pawnId, uint rollSeed)` — the cache stays keyed on `pawnId`
  alone, because a pawn's `RollSeed` does not change once it has one.
- **A `RollSeed` of zero falls back to the book's own seed.** `ColonistNames.RollSeedOf` returns 0
  when the aspect is absent, which is exactly what a save written before U40 has, and that colony's
  faces should stay the faces it had.
- **`randomCastEachSession` and `colonistLookSeed` become a real override rather than a dead
  flag.** Once a face follows the pawn, a world-level cast seed decides nothing, so both inspector
  fields would have gone on sitting there doing nothing — which is worse than removing them,
  because somebody would tick one and believe it. Either of them on sets
  `ColonistAppearanceBook.Pinned`, which overrules every pawn's own seed and deals the whole colony
  from one number. That is exactly what both were for: looking at a lot of colonists quickly while
  the palette is being judged. The tooltips now say that ticking one means the people you chose on
  the setup screen are **not** the people you get.
- Callers are `PawnFigureDirector` (:1288, :721) and `ChunkRenderer` (:79-83), both of which have a
  snapshot to hand.

**No golden moves and no hash moves.** Appearance is derived presentation state: not saved, not
hashed, not in a cell — the same standing this project gives age and occupation, and the same
argument `StateHashCoverageTests.SupportStaysOutOfTheHash` makes about the support solver. What
does change is that **a given world now deals itself the same people on every load by default**,
which is what the `randomCastEachSession` tooltip has been promising as the *off* position.

---

## 6. Where each piece lives

| Piece | Assembly | Why |
|---|---|---|
| `Rgb24`, `ColonistAppearance`, `ColonistPalette`, `ColonistLook` | **moves** to `Odyssey.Hud` | Unity-free already; the file says the move is a file move |
| `ColonistFace` | `Odyssey.Hud` (new) | fast tier; `HudBoundaryTests` keeps it Unity-free and Sim-free |
| `ColonistAppearanceBook` | **also moves** to `Odyssey.Hud` | see below |
| `AppearanceBooks.For(seed, catalogue)` | `Odyssey.Presentation.Rendering` (new) | the one line the book could not take with it |
| `ColonistMaterials` | stays | fully Unity, 3D only |
| `AvatarGlyph` | `Odyssey.Presentation.Ui` (new) | `Painter2D` needs UIElements |
| the four sites | `HudShell.Panels.cs`, `HudShell.Inspect.cs`, `HudShell.Start.cs` | unchanged files, one element each |

**The book's row is a correction this file made to itself before any code was written**, recorded
rather than folded away. It first said the book stayed in Presentation "because it touches the
module catalogue" — and §7 then promised, two paragraphs later, that "a `RollSeed` of 0 falls back
to the book's seed" would be a **fast-tier** test, which a class in Presentation cannot be. The
catalogue turned out to be one convenience constructor counting the colonist family; that line is
now `AppearanceBooks.For` on the Presentation side and the rest went down. What it bought:
**eleven appearance tests moved into the fast tier** (Hud 358 → 369) and the seed-fallback rule is
provable in 14 seconds. What stayed behind with the catalogue is `ColonistLookAgreementTests`,
every case of which names a `ScriptableObject`.

---

## 7. How it is tested

| Claim | Where | Tier |
|---|---|---|
| a face is a pure function of `RollSeed` and id, stable across runs | `ColonistFaceTests` | fast |
| two colonists in one colony rarely share a silhouette, over 200 seeds | `ColonistFaceTests` | fast |
| hair, skin, cloth and shape are independent — no correlation between slots | `ColonistFaceTests` | fast |
| **the card's face is the face the world gives that colonist** | `ColonistFaceTests`, through `ColonistDraw.IdForSlot` | fast |
| a `RollSeed` of 0 falls back to the book's seed | `ColonistAppearanceTests` | fast |
| the sheet's new lengths are the model's | `HudStyleSheetTests` | fast |
| **the candidate row holds its occupation beside a 30 px avatar** | `HudGeometryTests`, real text engine | Unity |
| the roster card is still no wider than its rows need | `TheCardIsWideEnoughForItsRowsAndNoWider`, unedited | Unity |
| the HUD still allocates nothing per frame with a full strip | `Adr0003_F1_...`, unedited | Unity |
| coverage is unchanged | `HudLayoutTests`, unedited | fast |
| **an avatar at 26, 30 and 64 px is looked at before it is played** | `Logs/avatars.png` | by hand |

The two in bold are the unit. The first is `ColonistDraw`'s own guarantee one level up — a screen
that promises a miner and hands over a cook, but about a face — and the second is the one arithmetic
claim in §3 that the text engine has to settle rather than a spreadsheet.

**Three tests must pass unedited**, and that is the check on this whole design: the card geometry,
the allocation budget and the coverage ceiling. If any of them needs a number changed, the avatar
has stopped fitting in the box that was already there and the change wants rereading, not editing.

---

## 7a. What building it changed about the design above

Written after the code and marked in place rather than folded in, because the wrong version is the
useful record.

- **`AvatarGlyph` is not a `HudGlyph` subclass**, which §4 said it would be. That class paints from
  a handler subscribed privately in its constructor and dispatches on `HudGlyphKind`, an enum of one
  shape per value; an avatar is six values at once. Making its paint virtual would have loosened the
  chrome set's contract for a case that does not share it — an avatar is all fills and no strokes,
  so the stroke rule that makes a 17 px row icon and a 30 px one read as one set does not apply.
  It keeps the box, the scale and the `P` mapping, which is what makes it sit beside them.
- **`ColonistAppearanceBook` moved too**, and §6 now says so with the reason.
- **Two of §4's numbers were wrong and the contact sheet said so**, which is decision 6 earning
  itself on the first run. The three builds were 5.0, 6.3 and 7.6 half-units against a flare of
  2.4, which put every bust between 14.8 and 20.0 units wide in a 24-unit box — three builds that
  were one build. They are 4.0, 5.6 and 7.2 against a flare of 1.9 now, so the busts come out 11.8,
  15.0 and 18.2. The *long* and *ponytail* crowns reached 0.62 of the head's radius and read as
  sideburns; they reach the shoulders now.
- **`Painter2D.Arc` is not used**, and the reason is worth keeping: its angles are measured in the
  element's own space, where y runs down, so every "over the top of the head" would be written back
  to front and come out as a chin. The crowns are sampled into polygons at sixteen steps a
  half-turn, which is under a tenth of a pixel of chord error at 64 px.
- **The candidate row's text column is 200.6 px and its widest trade is 138.9** — *Cemetery
  gravedigger*, measured by `ACandidateRowHoldsItsTradeBesideTheFace` with the real text engine
  over all 174 occupations. §3 predicted 206 px from arithmetic and was close, but the rule at stake
  (only a name may be cut short) is not one to settle with arithmetic about a font.
- **`Odyssey.Editor` did not reference `Odyssey.Hud`.** Nothing in the fast tier could know:
  380 tests were green and the project did not compile. It is `docs/lessons.md`'s standing warning
  happening again, and the reason the Unity run came before the commit rather than after it.
- **The roster card's initial letter is gone, and so are its two exemptions** — the
  `.card__initial` rule, its entry in the stylesheet anchor table, and the carve-out in
  `NoLabelIsAThreeLetterPlaceholder` that let a one-letter label through. A rule that shrinks as
  the interface improves is the right shape for that rule.

---

## 8. By-hand test procedure

1. Run the contact sheet and look at it before anything else (decision 6). Twenty-four colonists at
   26, 30 and 64 px, on the HUD's own panel fill. Watch for a skin and a cloth colour that come out
   the same value at 26 px, which is the fault a palette of 7 × 14 can produce and no test can see.
2. New game → the setup page. Reroll. **Every unkept card's face changes and every kept card's does
   not** — this is decision 3 made visible, and it is the single thing most likely to be wrong.
3. Start. The three colonists on the roster strip carry the three faces that were on the cards.
4. Click one. The inspect header's outlined square is gone and it is the same face again.
5. Save, quit to the main menu, load. Same faces.
6. Press Play twice with `randomCastEachSession` **on**: the cast still changes, because the
   override still overrides.

---

## 10. Rendered portraits (owner, 2026-09-18, after playing it)

**The owner's report: *"the colonists look nothing like their profile picture."*** They are right,
and the fault is in §4 rather than in the drawing.

### 10.1 What was actually wrong

`ColonistFace.Of` calls `ColonistAppearance.Of(rollSeed, id, lookCount: 1)`, and that `1` throws
away **`Look` — which of the sixty-one Synty characters this colonist is**. The code says so in a
comment and defends it: *"an index into the catalogue's 3D colonist meshes means nothing to a
drawing."* That was wrong. It is the single most identity-bearing fact about a colonist.

So the card matched the **palette** and invented the **person**. The colours do land on the figure
— of the 61 colonist rows, **55 classify `Full`**, 5 `NoSkin`, 1 `ClothOnly` — but:

| | the figure | the card |
|---|---|---|
| Hair | a specific Synty cut, hood, hard hat or visor | one of eight generic crowns, from an unrelated salt |
| Build | whatever the artist modelled | one of three trapeziums, from another unrelated salt |
| Clothing | straps, plating and panels, recoloured | one flat trapezium in the primary cloth colour |

A colonist in a helmet was being given a ponytail. Three colours in common and a disagreement about
everything a person recognises.

### 10.2 The answer, and why it is allowed

**Render the real character once per appearance and cache it.** This is `09-ui-and-input.md` §4.5's
own named graduation path, and — worth saying plainly — it is what the plan's original `U41
Portraits` row asked for, which §9 argued out of existence. **§4.5 refused fifty portraits
re-rendered at 15 Hz while the world draws.** It did not refuse one render, cached for the session.
On the setup page that is three; on the roster it is at most the colony size, once each.

**The cache key is the `ColonistAppearance`, not the pawn** — the look index plus four colours. Two
colonists who genuinely look alike share one texture, a reload asks for what it already has, and a
colony of twenty-six with a dozen distinct appearances renders twelve times. That is the whole
performance story and it is why this is cheaper than it sounds.

**One `RenderTexture` exists for the entire game**, reused for every portrait and read back into a
small `Texture2D` per appearance. 128², so 64 kB each: a colony of twenty-six is under 1.7 MB. The
leak test the plan asked for therefore has something to count after all, and the number it counts is
one.

### 10.3 The rig, and the two traps in it

A hidden root far under the board (`HideFlags.HideAndDontSave`), a disabled camera and a light of
its own. **Everything in it is switched off except during the render call**, which is synchronous:
enable, `camera.Render()`, disable. That is what keeps a portrait light out of the world's own
frame without needing a spare layer, a rendering-layer mask or any project setting — a directional
light is global in URP, so the alternative was either a project change or a portrait lit by whatever
time of day it happened to be rendered at, frozen there for the session.

**No animator and no `PlayableGraph`.** `PawnFigureDirector.Create` builds both because a figure
walks; a portrait does not, so it is the prefab in its bind pose with the head framed. That also
means a portrait is available for a look whose *gaits* are missing, which `LooksFrom` drops — a row
with a prefab and no animation can still be photographed.

### 10.4 What the picture caught, again

`Logs/portraits.png` is the studio photographing itself at full size, and it earned its keep for the
second time on this unit. Two faults, neither visible at 30 px and neither findable by a test:

- **The crop was anchored to the top of the silhouette** (`body.max.y`), which is the top of
  whatever the character is *wearing*. Every bare head framed correctly and every hat-wearer was cut
  off at the chin — which is the signature of exactly that fault, and is why it was diagnosable from
  one image. It anchors on the **head bone** now: every pack character is a valid Mecanim humanoid
  (`docs/research/e-02`), so the bone is there to be asked for, and the animator does not need to be
  enabled to ask. Scale comes off the body's height rather than the head's, so a tall colonist is
  not photographed from further away.
- **The key light was pointing at the back of their heads.** `Euler(28, 200, 0)` against a camera
  looking the other way; the whole cast came out dim and flat and it read as "the render is murky"
  rather than as a light aimed backwards. `Euler(24, -22, 0)` at 1.6.

### 10.4a `randomCastEachSession` now defaults off, and the portrait is why

The switch overrules every pawn's own seed (§5). The setup page photographs three candidates
**before a colony exists**, and the pin is applied when the world is built — so with the switch on,
pressing Start dealt three different people than the three on the cards. That is the owner's
original complaint reappearing, this time by construction rather than by accident, and it would
have been found by playing it rather than by any test here.

It is still available and still does exactly what it says. What it was *for* — looking at a lot of
the palette quickly — is now `Logs/portraits.png`, which shows twenty-four at once and does not
require pressing Play at all.

### 10.5 What it does not change

The drawn avatar stays, as the fallback with no licensed packs, and `AvatarGlyph` chooses between
them by the idiom `IconBadge` already uses: a portrait present suppresses the paint, absent paints
the figure. So a clone without `Assets/Synty` is still correct, and none of §4 is wasted.

---

## 9. Deliberately not in this unit

- **`09-ui-and-input.md` §4.5 is not amended**, and that is the cheap outcome the plan did not
  expect. The row promised a carve-out for live render-textured portraits; §4.5 already *prescribes*
  composed flat avatars for M2 through M7 and refuses only the live ones. Choosing what the design
  file already chose needs no exception written into it. The plan row is renamed `U41 Flat avatars`
  and `vertical-slice.md` records the reversal in place rather than quietly.
- **The mood ring, the status glyphs and the layer badge** (owner decision 4). The health arc is not
  deferred but impossible: there is no health model of any kind.
- **A face.** §4, and it is a limit rather than a stage.
- **Player-chosen appearance.** `ColonistAppearanceBook.Override` (:78) is the documented seam and
  stays empty. Nothing in this unit writes to it, and when a panel does, it is the overrides that
  enter the save and never the derivation.
- **New registry keys.** An avatar is a composed element, not an icon key; `ui.pawn.colonist`
  already exists and stays a gap for the tile and thing readouts that still use it. **No content
  change, so no wiki rebuild is owed by this unit** — which is worth saying out loud, because every
  other unit this month has owed one.

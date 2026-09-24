# 42 — Bandits: the hostile renamed, and dressed as itself

Branch `claude/bandits`, worktree `D:\code\odyssey-bandits`. The owner interview was 2026-09-24,
and the build was the same day.

## 1. Why

The owner's report: *"The marauders look like colonists."* They did, exactly.
- The hostile was `PawnKind_Marauder` on `Species_Person`.
- `PawnFigureDirector.LookFor(in PawnView)` asked only `IsAnimal`, and `Repaint` dealt every
  person from `ColonistAppearanceBook.For(pawnId, rollSeed)`.

So a hostile was dealt a colonist's body, hair and beard, and the colony's issued white jumpsuit.
The only difference was the red marker over its head, and in a crowd that is not enough.

## 2. What the owner decided

| Question | Answer |
|---|---|
| Name | **Bandit**, everywhere live: the screen, the wiki, registry keys, the defName, C# identifiers, test names and current design docs. The journal keeps the old word, because it is history. |
| Head | Battle Royale's **welding helmet**, which the pack names `SM_Chr_Attach_Helmet_03`. It is worn in the **pack's own paint**, and was picked off `Logs/bandit-heads.png` (§4). |
| Body | A **red armour vest** over **bare arms**. It is the rig's own hidden `Armor_01..03` overlay. |
| Bodies | **`Character_ToplessMale_01`** and **`Character_SportyFemale_02`**, both chosen off `Logs/bandit-dressed.png`. The sports-bra body wears shorts, and the owner asked for trousers. |
| Legs | **Black trousers.** On the woman, her stripes, shoulders and wristbands go black with them (*"extras black"*). |
| What varies | Sex, skin tone, vest cut (one of three) and the red (crimson to rust). |
| Who they are | **A complete rolled person underneath.** Their name comes from the **colonist pool**: *"They could be kidnapped eventually - they need to be their own character."* |
| Inspect pane | The **name** is the title, and **bandit** is the line under it. The avatar is their own portrait, **masked**. |
| Far form | The helmet and red vest **everywhere**, past the 64-figure cap too. |
| Weapons | **A crowbar or a baseball bat.** *"not swords - not their style"* (mid-build). |
| Behaviour | **Unchanged.** Look and name only. Savagery is its own unit. |

## 3. What does not move

**Saves and the hash see only the kind's index.**
- `PawnKindSection` writes `Kind` as an int, and `Pawn` and `CorpseRegistry` hash that int.
- The defName is looked up by name at load.
- `TraverseMode.Bandit` keeps value 3, and `Incident_BanditLeft` keeps incident index 3.

So the rename moves nothing that is saved or hashed. Appearance is presentation, and its seed is
the pawn's own roll seed, which is already published.

**The weapon pick consumes no random stream.**
- `PawnContent.WeaponFor(kind, pawnId, rollSeed)` is a pure hash of the pawn.
- `PawnKindDef.weapon` became `weapons`, a `<li>` list.
- No other roll shifts, so a colony that never meets a bandit plays exactly as before, and no
  golden spawns one.
- `BanditSideTests` pins its gang to the machete it was measured with. The blunt weapons are
  slower (cooldown 120–132 ticks against 96), so the gang broke fewer walls in the run and its
  "three walls struck" count had become a measure of swing speed rather than of anybody standing
  about. The longest wait was 1–2 ticks either way.

## 4. The contact sheets

`scripts/unity.sh shot Odyssey.EditorTools.BanditSheet.Shoot` writes four sheets into `Logs/`:

- **`bandit-heads.png`**: the seven full-head pieces on both bodies. `Helmet_03` is the welder.
- **`bandit-vests.png`**: the three vests, plus a frame per body with its clothing slots in
  signal green and blue.
- **`bandit-bodies.png`**: all fifteen Battle Royale bodies under a vest.
- **`bandit-dressed.png`**: the candidates, helmeted, each vest filled red.

`BanditSheet.Measure` (`unity.sh exec`) prints the UV boxes the fill rule rests on.

**What the sheets found.** The swatch classifier cannot see these garments.
- The topless male's trousers and all three vests are **camo texture**. The camo spans a box of
  the atlas about forty swatch cells across, so clustering found his belt and boots and painted
  those instead.
- The male rig's trouser camo, (0.775, 0.262)–(0.924, 0.491), **overlaps every vest's box**. No
  single set of rectangles can paint the vest red and the trousers black.

## 5. The outfit model

A pawn is two layers, **the person** and **what they wear**. This is the uniform rule generalised:
the issued jumpsuit already rolled the person and laid the uniform over them.

- **`PawnOutfit`** is `Issued` or `Bandit`. **`PawnOutfits.For(flags)`** is the one owner of "a
  hostile person dresses as a bandit". It is asked by flags, so a corpse keeps the outfit it died
  in.
- **`ColonistAppearance.Of(…, outfit)`** rolls the person exactly as a colonist, with every
  stream in the same order. Then it replaces:
  - the **body**, with the gang's row for that sex and a vest cut rolled on its own stream;
  - the **cloth**, with a red from `BanditReds` (five shades, `#A01C1C` to `#7F2119`);
  - the **trousers**, with `BanditTrousers` (`#1E1E22`, not pure black, for the golden-hour
    grade);
  - the **head**, with the helmet.

  It **keeps** the hair and beard pieces. `HidesHair` stops them being worn, so taking the
  outfit off gives back the person.
- **The book** caches on seed *and* outfit.
  - The by-id overload scans the frame for the outfit, so it serves one pawn at a time.
  - The far form and the figures pass the `PawnView` they already hold. A per-pawn scan per frame
    is the quadratic P12 warns about.
- **The gang's rows** are six (two bodies × three vests), appended to the colonist family after
  every colonist row so no look index moves. They are flagged `bandit`, which keeps them out of
  the lottery.
- **Each row carries two sets of rectangles**, `appearance` and `overlayAppearance`:
  - **The body** keeps its classified skin, and everything else is the second cloth slot, the
    **whole atlas**, black.
  - **The vest** fills the box of its non-dark vertices red, and its straps and buckles black.

  **The shader now lets the first slot a fragment is inside win** (skin, hair, cloth, cloth2).
  For a colonist this changes nothing, because the classifier guarantees disjoint slots. For a
  bandit it is what lets the whole-atlas black sit behind the skin.
- **The far form** bakes the vest with the body.
  - `ModuleLibrary` gives the vest's parts a twin material so the merge keeps them apart, and
    `IsOverlayMaterial` says which parts they are.
  - `ChunkRenderer.BanditFarMaterials` paints the body from `appearance` with the palette's
    middle skin, and the vest from `overlayAppearance`.
  - The helmet is a third instanced bucket beside hair and beard: one draw per piece in use.
- **The helmet is worn as a character**, through `ColonistMaterials.InOwnPaint`: the pack's
  paint, with the ink hull and the draw after the outline pass. In the pack's own material it
  would lose its outline and have the trees' ink painted over it.

## 6. The pane

- A bandit is called by their name, dealt from the colonist pool by the same seed and id, with
  "bandit" under it.
- `ShowsFace` is true for any person, so the avatar is their own portrait, helmet on.
  `ShowsColonistBody` and `ShowsTabBox` stay a colonist's: no needs, skills, Health tab or Draft.
- A bandit's corpse is "Corpse of *name*".

## 7. Out of scope, recorded

- **Savage behaviour**: executing the downed, raiding, fleeing when hurt. It is its own unit.
- **Kidnap and recruitment.** The seam is there: an `Issued` outfit on a bandit's person is a
  colonist.
- **A bandit's name anywhere but the pane and the corpse**, such as alerts and the context menu.
- **The far form wears one red and one skin per body**, not each bandit's own. At that distance
  the difference is invisible, and per-person materials there would cost a material per bandit.

## 8. Do not undo by tidying

- **The two sets of rectangles per gang row.** Merging them paints the male trousers red or the
  vest black.
- **The vest's twin material in `ModuleLibrary`.** Without it the far vest merges into the body
  and inherits the body's rectangles.
- **The shader's first-slot-wins order.** A bandit's whole-atlas black relies on it.
- **The weapon hash.** Replacing it with a draw from the world's random stream moves every roll
  after the first bandit.

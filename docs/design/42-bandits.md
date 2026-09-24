# 42 — Bandits: the hostile renamed, and dressed as itself

Branch `claude/bandits`, worktree `D:\code\odyssey-bandits`. Plan: the stages below. Owner interview
2026-09-24.

## 1. Why

The owner's report: *"The marauders look like colonists."* They did, and exactly so. The hostile
was `PawnKind_Marauder` on `Species_Person`. `PawnFigureDirector.LookFor(in PawnView)` asked only
`IsAnimal`, and `Repaint` dealt every person from `ColonistAppearanceBook.For(pawnId, rollSeed)`.
So a hostile was dealt a colonist's body, hair and beard, and the colony's issued white jumpsuit.
The red marker over its head was the only difference, and in a crowd it is not enough.

## 2. What the owner decided (interview, 2026-09-24)

| Question | Answer |
|---|---|
| Name | **Bandit**, everywhere live: screen, wiki, registry keys, defName, C# identifiers, test names, current design docs. The journal keeps the old word, because it is history. |
| Head | A **full-face welding helmet** from POLYGON Battle Royale, in the **pack's own paint**. No piece is named "welder", so it is picked off a contact sheet (§4). |
| Body | A **red armour vest** over **bare arms**: the rig's own hidden `Armor_01..03` overlay on `Character_ToplessMale_01` or `Character_SportsBraFemale_01`. Both bodies were already kept out of the colonist lottery on register. |
| Legs | **Black trousers.** |
| What varies | Sex, skin tone, vest cut (one of three), and the red (crimson to rust). |
| Who they are | **A complete rolled person underneath.** Gender, age, face, hair and beard are rolled as a colonist's are, and hidden under the helmet. The name comes from the **colonist pool**. *"They could be kidnapped eventually - they need to be their own character."* |
| Inspect pane | The **name** is the title and **Bandit** is the kind line. The avatar is their own portrait, **masked**. Needs, skills and tabs stay hidden. |
| Far form | The helmet and red vest **everywhere**, past the 64-figure cap too. |
| Weapons | **A crowbar or a baseball bat.** *"not swords - not their style"* (mid-build, 2026-09-24). |
| Behaviour | **Unchanged.** Look and name only. Savagery is its own unit. |

## 3. What does not move

**Saves and the hash see only the kind's index.** `PawnKindSection` writes `Kind` as an int, and
`Pawn` and `CorpseRegistry` hash the int. The defName is looked up by name when the content loads.
So the rename keeps index **3** and moves nothing that is saved or hashed. Appearance is
presentation, and its seed is the pawn's own roll seed, which is already published.

**The weapon pick consumes no random stream.** It is a pure hash of the pawn's id and roll seed.
No other roll shifts, so a colony that never meets a bandit plays exactly as before. No golden
spawns one.

## 4. The contact sheet

`scripts/unity.sh shot Odyssey.EditorTools.BanditSheet.Shoot` writes two sheets:

- **`Logs/bandit-heads.png`**: the seven full-head pieces on the male (row one) and female (row
  two) bandit bodies.
- **`Logs/bandit-vests.png`**: three vests per body, plus a frame per body with the two clothing
  slots painted green (`cloth`) and blue (`cloth2`). That frame is how we learn which slot is the
  trousers, because `CharacterSwatches` ranks clusters by size and never says where on the body
  one is worn.

The log carries each body's cluster table (`CharacterSwatches.DescribeBody`) and each vest's
(`ClassifyOverlay`).

## 5. The outfit model

A pawn is two layers, **the person** and **what they wear**. This is the uniform rule
generalised: `ColonistAppearance.Of` already rolls a person and then lays the issued jumpsuit over
them, and "switching the uniform off gives back exactly the cast that would have been dealt".

- **`PawnOutfit`** is `Issued` or `Bandit`. `PawnOutfits.For(PawnView)` is the one owner of "a
  hostile person dresses as a bandit". The figure director, the far form, the pane and the
  portrait all ask it.
- **The deal.** Everything the colonist roll does, in the same order, is rolled first. Then the
  bandit outfit replaces:
  - the **body**, with the bandit row for (sex, vest cut);
  - the **cloth**, with a red from `BanditReds`, on a stream of its own;
  - the **trousers**, with a near-black. Not pure black, for the reason the uniform is not pure
    white: the golden-hour grade needs somewhere to go.

  It **keeps** the hair and beard pieces, so taking the outfit off later is the person unchanged.
- **The helmet hides the hair and the beard; it does not delete them.** The slots are not worn
  while a head piece is.

## 6. Out of scope, recorded

- **Savage behaviour**: executing the downed, raiding, fleeing when hurt. This is its own unit
  and its own design.
- **Kidnap and recruitment.** The seam is there: an `Issued` outfit on a bandit's person is a
  colonist.
- **A bandit's name anywhere but the pane and the corpse**, such as alerts and the context menu.

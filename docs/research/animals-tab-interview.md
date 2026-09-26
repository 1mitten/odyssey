# Interview — the Animals tab, to be designed in Claude Design before it is built

**Phase 1, 2026-09-23.** Owner: *"do we need to design an animals tab - we can get claude design
to do it - make us a prompt that lists animals in the game? anything you can think is relevant
and useful, interview and ask me questions so we can pass a good prompt to claude design."*

## Ground

| Fact | Where |
|---|---|
| The bar has promised two tabs since M1: **Animals** on F5 ("tame beasts and their training") and **Wildlife** on F6 ("what is out there"). | `docs/design/icon-keys.csv`, `HudCommands` |
| **Wildlife is built** (design 30 §6): a count per kind, a row per wild animal — kind, doing, layer, cells from the colony — twelve to a page, click to select and jump. F5 is still a dead item. | `docs/design/30-wildlife.md` |
| **No taming, no training, no health, no diet, no age or sex** exist in the simulation. An animal wanders, rests, comes and goes. The health model is the next unit after animals (interview 2026-09-22). | `docs/design/29-animals.md` |
| Two creatures have art and are in the game: the **midden hog** (meat and leather, "the one you meet first") and the **duct rat** (vermin, small game). Three are named and proposed with no art: the **girder cat** (climbing predator), the **loper** (feral maintenance machine), the **dray hog** (bred pack animal for caravans). | `docs/design/proper-nouns.csv` |
| The HUD's look is a fixed system: panels at `#0c1014` with a 13% white border, text `#eef3f6` / 66% / 50%, accent `#6fd3e3`, warn `#e8b55c`, bad `#e06a5c`, good `#7fc98c`; **Archivo Narrow** for words, **IBM Plex Mono** for every figure; six type steps; rows 29–30 px; panels docked bottom-left over the command bar at a constant width (Work: 1,385 px, paged 11 columns × 12 rows). | `docs/design/14-hud-layout.md`, `HudTheme`, `HudType` |
| Precedent: the Work tab was **mocked first** (`docs/reference/mockups/work-v1.html`), then built to the picture; the whole HUD likewise (`hud-v2.html`, published as an artifact). | `docs/reference/mockups/` |
| RimWorld's Animals tab, for the clean-room comparison: name, master, follow (drafted / fieldwork), training (tameness, obedience, release, rescue, haul), allowed area, slaughter, sex and age, pregnancy; its Wildlife tab is what F6 already is. Wildness per kind sets tame and train chance; tameness is a 0–5 meter that decays without handling. | `docs/research/a-09-animals.md` |

## Questions

Each has a recommendation; **"go with the recommendations"** is a complete answer. The answers
become the brief.

**1. Design the pair, or the tamed tab alone?** Recommend **the pair in one brief**: the Animals
tab (F5, tamed) and the Wildlife panel (F6, built) share columns and one gesture joins them —
the *Tame* order on a Wildlife row is how an animal crosses from one to the other. Designing
F5 alone would draw a second table that disagrees with the first.

**2. What is a tamed animal *for* here?** The columns follow from the answer. Recommend the
three roles the register already names: **livestock** (the hog: meat and leather, later milk or
wool for other kinds), **pack** (the dray hog: caravans), **guard or hunter** (the girder cat,
later). Vermin are never tamed. Say if you want pets or bonding in the fiction at all.

**3. The taming model the tab must show.** Recommend RimWorld's shape, in our words: a per-kind
**wildness** (the hog low, the cat high, the loper never), a per-animal **tameness** meter that
a handler raises with food and that decays if nobody handles it, and **training** as three
ticks — *tame*, *obey*, *haul* — with *release* and *rescue* left out until there is combat.
The tab shows the meter and the ticks; the alternative is a one-shot "feed to tame" with no
decay, which needs no meter and no handler skill.

**4. Where a tamed animal may go.** Recommend **an allowed area** painted like a zone (growing
and storage zones already exist), shown as a column of area names; fenced **pens** later, if at
all. The alternative is pens first, which is a building unit before a UI one.

**5. Names.** Recommend a tamed animal is **named on taming** from a small register (a
`animal-names.csv` beside `colonist-names.csv`, so the wiki holds it) and is renameable; a wild
one stays "Midden hog". Or no names, and the kind plus a number.

**6. The tab's shape.** Recommend **the Work tab's**: one table docked bottom-left at the Work
tab's constant 1,385 px, rows = animals (portrait, name, kind), columns = the controls (master,
area, training ticks, slaughter mark, food and health bars), twelve rows a page with the same
pager, sortable by clicking a column header. The alternative is the narrower Wildlife card
(three columns) which cannot hold controls.

**7. The detail view.** Recommend the brief includes **the inspect pane for a tamed animal** —
what the click on a row or on the figure shows: name, kind, master, tameness, training, health
(the health model's body parts are already in the registry), needs, and the commands *rename,
slaughter, release*. This is the colonist pane's animal twin and the same pane already shows a
wild animal as *Midden hog · Wandering*.

**8. What Claude Design is given.** Recommend it gets, verbatim: the tokens table above, the two
font names, `hud-v2.html` as the base plate, `work-v1.html` as the table precedent, the creature
register lines, and the Wildlife panel as built. The alternative is a description only, which
produces a generic game UI.

**9. What comes back.** Recommend **a static HTML mockup** at 1920 × 1080, one file per state
(tab closed, tab open, inspect pane on a tamed animal, the Tame order on a Wildlife row), so it
drops into `docs/reference/mockups/` and publishes through the existing `artifact_body.py`
pipeline as the HUD did. The alternative is images, which cannot be diffed or measured.

## Answers

**2026-09-23, owner:** one tab, called *Animals*, with wildlife under it and tamed animals
later; the tamed half **deferred entirely** (questions 2–5); the Work tab's table shape (6) and
the inspect pane (7): yes; the recommended inputs (8) and deliverables (9). The brief:
`docs/reference/mockups/animals-tab-brief.md`.

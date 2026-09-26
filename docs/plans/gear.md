# Gear — the staged plan

**2026-09-25.** From `docs/research/gear-interview.md` (twenty-two owner answers). The interface is
designed once by Claude Design (`docs/reference/mockups/gear-brief.md`) and built a unit at a time.
**G1 is built** (2026-09-25, `docs/design/47-gear-tab.md`), widened by the owner to the whole tab
with a Hud-side preview of everything past the hand. The rest waits on research
(`a-19-apparel-and-gear.md`) and its own design doc. Each unit is its own `claude/*` branch and PR, and goes through
`docs/process.md`.

## Units

| Unit | Delivers | Sim, save, hash | Depends on |
|---|---|---|---|
| **G1** Gear tab and weapon verbs — **built 2026-09-25, design 47** | The Gear tab enabled for people: the paper doll with **Weapon live**, **Body** reading *Issued jumpsuit*, the other four slots and the kit drawn empty with their reason; **Unequip** and **Drop** on the weapon tile (one new intent, `WeaponHand.PutDown` reused); the pane's tab names moved into the registry (design 33 §9d asks for it in this commit); `HudLayout.InspectTabBody` re-derived if the doll is taller than Skills | One intent. No golden moves unless a test uses it. | Claude Design's answer |
| **G2** Trip caps | `ItemDef.carryLimit` per commodity in the XML; a haul lifts `min(stack, carryLimit)` with `ColonyItems.SplitOff`; the wiki shows the cap | Behaviour change: goldens re-baked and measured with `GoldenColonyProbe` | a-19 numbers |
| **G3** The kit — **built 2026-09-26, design 54**, branch `claude/gear-kit` (stacked on G1) | `ItemDef.size` (Pocket / Pack / Arms) and `kitCap`; two belt slots per person, items `CarriedBy` the pawn with a slot index; *Take into kit* and *Drop*; a doctor treats from her own kit first; a ration is eaten from the kit | New section `odyssey.gear`, saved and hashed; re-bake | G1 |
| **G4** Apparel core (design 29 CL1–CL3, CL5) | `ApparelDef`: slot, art, warmth, armour, rain, mood; Head, Face, Body and Back worn, saved and drawn (garment = body-mesh swap, headgear on the head socket, the pack on a new back socket unlocking four kit slots); quality rolled from `Quality.xml`; *Wear* and *Remove* | Worn slots saved and hashed; far-form buckets keyed on the piece | G3 |
| **G5** Armour and the effects (CL4) | The Armour slot (the vest overlay); warmth widens a person's comfort band (design 28), armour enters design 33's blow, rain buys back design 43's pace factor, quality moves mood; the effects line on the tab | Goldens re-baked | G4, a-19 |
| **G6** Loadouts | The Loadout column on Assign (F4), the picker and the editor; a colonist with a loadout fetches to match; none keeps what they have | Loadouts and each person's choice saved | G3, G4 |
| **G7** Sources | An *Apparel drop* incident beside the Medical drop; the **Strip** order on the downed and the dead; bandits' helmet and vest become worn items | Incident Def and one order | G4 |
| **G8** Crafting | Tailor bench, bills, fabric from salvaged cloth, a fibre crop and traders | Large | **Its own interview**; bills, and a-13 for traders |

## Gates

Every unit: the fast tier and the Long tier green, both content `--check`s and the icon gate, the
Unity tier where Presentation or the HUD moved, a handover with the two tables, and a row in
`docs/plans/playtest-queue.md`. Units that move the hash re-bake the goldens and **measure** the
difference with `GoldenColonyProbe` against `main` before calling it the hash seeing more.

## Content

Every new slot name, item, verb and alert goes into `docs/design/icon-keys.csv` in the same commit,
with the wiki and `Registry.g.cs` rebuilt. The mock item names in the brief (*Wool cap*, *Field
coat*, *Padded vest*…) are placeholders until then.

# Combat — the plan

**Approved by the owner 2026-09-23** after an interview of five rounds (every answer is in the
decision table of `docs/design/33-combat.md` §1). Branch `claude/combat-mvp`, worktree
`D:\code\odyssey-combat`, from `claude/wildlife`. One PR; **merge after #167 (animals) and #169
(wildlife)**, which it is built on. Temperature (#164) is independent: combat adds save sections,
not a format bump, so the two do not collide on the number.

## Units

| Unit | What | Checkpoint | Goldens | State |
|---|---|---|---|---|
| C0 | Research `a-10-melee-combat.md`, pack inventory `synty-sword-combat.md`, design 33, this plan | — | — | **done** |
| C1 | Draft and move: drafted state, `Job_DraftHold`, `Job_Goto`, `SetDrafted`, `OrderMove`, Drafted think node, four-hour auto-undraft, the job-count load guard; T, the pane's Draft button, `OrderModel`, `OrderPresenter`, the drafted marker and order line | **▶ playtest** | move: two job defs in the job counters | **built** |
| C2 | Health and melee against a marauder: species hit points, revenge and natural attack, `CombatDef`, live Melee skill, `PawnKind_Marauder`, attack / flee / downed jobs, `OrderAttack`, adjacent auto-attack, `CombatSystem`, corpses; health tab, corpse pane, spawn marauder, feedback, the clip layer and its fallback | **▶ playtest** | move: skill and job defs | designed |
| C3 | Weapons: bat, crowbar, machete, sci-fi blade; equip job and order; stun; starting kit; held prop | **▶ playtest** | move: items | designed |
| C4 | Rescue and healing in bed | — | move: work type | designed |
| C5 | Friendly fire and its mood | — | **none, asserted** | designed |
| C6 | Buildings as targets | — | **none, asserted** | designed |
| C7 | Ten-day gate with and without hostiles, benchmark rows, records, wiki republish | — | — | — |

## Blocked on the owner

- **Nothing for C1.** The Sword Combat package was unpacked into the shared `Assets/Synty` on
  2026-09-23 (GUIDs preserved; see `docs/research/synty-sword-combat.md`), so the clips are there
  when C2 wires them.

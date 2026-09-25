# 50 — Raids

**Written 2026-09-25**, from the owner's request and a two-round interview the same day. Branch
`claude/sharp-lamport-8q5u4h`. Plan: `docs/plans/raids.md`.

> *"Can we look into a new event. This type of event is a raid, raids can have varying numbers of
> 1 to 100 … They can be triggered, happen at certain times, part of a interaction question etc.
> … a slider from 0 to 200 where we choose the amount to spawn for a raid … they will slowly come
> from a random direction … wander around the edge of the map, they will start to wander (maybe few
> in game hours) and explore in further before raiding your home/attacking you. … a dropdown for
> the type of enemy in the raid … a mix of melee and projectiles etc. We could also use some other
> sound for a raid as indicator."* — the owner, 2026-09-25

## 1. What a raid is

A raid is a **band of hostiles that behaves as one**. A bandit before this unit was a solo mind:
it hunted from the tick it spawned (design 33 §5). A raid puts a group between the incident and the
bandits' minds. The **group** holds the phase, the places and the clocks. Each **member** asks the
group what the band is doing before it asks its own mind. When the band assaults, the member's own
mind (the unchanged `HostileThinkNode`) does the fighting.

This follows the reference's pattern of a group controller that pawns defer to. **The names are
ours** (clean room): `RaidGroup`, `RaidPhase`, `RaidSystem`, `RaidThinkNode`, `RaidMixDef`,
`RaidWorker`, `RaidBudget`, `RaidView`.

**What existed before and is reused, not rebuilt:**
- the incident door (`Incidents.TryFire`, design 23);
- the bandit, its outfit and its melee weapons (design 42);
- the pistol and the reach rule (design 47);
- the edge census the wildlife walks in by (`SurfaceCensus.Edge`, design 30);
- the edge search a thief leaves by (`EdgeTarget.Find`, design 33 §17);
- the hearth (design 43, whose §9 named "bandits going for the hearth as the raid's target" as a
  follow-on);
- the owner's assault horn, which was already the game's `alert-raid` (§7).

## 2. The owner's rulings (2026-09-25)

| Question | Ruling |
|---|---|
| The 200-pawn ceiling | Raise it to **400**, measured first (§11). |
| The enemy dropdown | **Raid mixes** as Defs: Bandits (melee), Gunmen (pistols), Mixed (~70/30). The pistol bandit becomes a kind of its own, the **gunman**. |
| The build-up | **Gather, probe, assault** (§3). |
| The target | **The hearth**, falling back to where the colony started. They fight and break on the way. |
| The end | **Withdraw at half**: when half the band is downed or dead, the rest walk to the nearest edge and leave, and any loot goes with them. The downed stay. |
| Arrival | **One edge**, spread along a stretch of it, trickling in over a few seconds. |
| Sound | **Two cues**: a horn on arrival with a *Raid warning* Events row, and the assault horn with the *Raid* alert. The owner supplied both recordings (§7). |
| Scope | The Def carries its gates, so a storyteller can fire it later. An alert whose click jumps the camera. **No auto-draft.** Slider at 0 means **auto size from headcount and days**, because the game has no wealth measure. |
| Out of scope | The choice event ("pay tribute or fight"): its own unit, needing a dialogue. |

## 3. The phases

| Phase | What the band does | Ends when |
|---|---|---|
| **Arriving** | Members appear one by one on their edge slots over `arrivalTicks`, and each walks to the gather point as it lands. | The last member has spawned. |
| **Gathering** | Members mill within `gatherRadius` of the gather point: a short wander, then a wait. | `loiterTicks` pass (drawn at fire time from `loiterHoursMin`–`loiterHoursMax`), or the **early trigger**. |
| **Probing** | Members advance to the probe point, `probeFraction` of the way from the gather point to the target, and mill there. | `probeHours` pass, or the early trigger. |
| **Assaulting** | Each member walks toward the target in legs, and hands over to its own mind when there is something to fight (§5). | Half the band is down (→ Withdrawing), or every member is down or gone. |
| **Withdrawing** | Each standing member walks to the nearest edge it can reach and leaves, keeping what it carries. It does not look back to the fight. | No member is standing on the board. |

**The early trigger.** During Arriving, Gathering or Probing, the band goes straight to Assaulting
if either of these holds:
- a member was **struck** (it holds `RetaliateAgainst`); or
- a **standing colonist** is within `earlyTriggerCells` of a standing member.

The owner accepted 15 cells, which is the reference's feel for a staging band that notices you. A
band that is poked does not stand and take it.

**Timing.** The group's clocks are checked every `CheckTicks` (30). A phase change **interrupts**
every member so that each thinks again under the new phase. The interrupts are spread over
`StaggerTicks` (20), so 200 path searches do not land in one tick.

**What the group owns:** the phase and the tick it began, the three places (gather, probe,
target), the arrival schedule, the members, and the band's starting size. A member's own job is
still its own.

## 4. Arrival

At fire time the worker:
1. takes a `SurfaceCensus` in the bandit's traverse mode from the colony's start;
2. splits the reachable edge cells by **side** (west, east, south, north);
3. draws a side with any cells, then a **centre** cell on that side;
4. sorts that side's cells by distance from the centre and gives the nearest `size` of them to the
   members, cycling if a side has fewer cells than the band, in which case two members share a
   slot and `FreeSpawnCell` spreads them.

Member `i` is scheduled to arrive at `fireTick + i × arrivalTicks / size`. With the default 300
ticks, a band of any size arrives over five seconds at normal speed.

**The gather point** is the reachable surface cell nearest to a point `gatherInset` (12) cells
inward from the edge centre, toward the target.

The whole schedule is decided and saved at fire time, so the system's tick draws nothing, and a
save taken mid-trickle resumes the same trickle.

## 5. The assault

**The target** is `Hearth.Cell` when there is a hearth. Otherwise it is the colony's start cell,
which `ColonyWorld` hands the context (`PawnContext.ColonyStart`). A colony with neither — a test
fixture — targets the band's own gather point, so the band simply hunts.

**The leg.** An assaulting member walks toward the target at most `LegCells` (16) at a time and
thinks again at the end of each leg. It **hands over** to `HostileThinkNode` (the bandit's own
mind, unchanged) in any of these cases:
- a standing colonist is within `EngageCells` (12);
- it is retaliating;
- it is within `ArriveCells` (6) of the target;
- the target cannot be reached, for example behind a wall. Then its own mind hunts the nearest
  reachable colonist or breaks the nearest colony building, exactly as a lone bandit does.

Handing over means the node returns false and the tree falls through. So once the band has
arrived, every member fights the way design 33 already tunes a fight.

## 6. The withdrawal

**The half.** The band withdraws once `downed + dead ≥ retreatPerMille × startingSize / 1000`,
with `retreatPerMille` at 500. A member counts as **dead** when it is gone from the registry
without having left. A member that walked off the board with loot has **left** and counts as
neither.

**Leaving.** A withdrawing member is given `Job_Steal` with no loot: a walk to the nearest edge and
`Theft.Leave`. Two things are changed for a raid member:
- `StealJobDriver.LookUp` does not send a withdrawing member back to the fight;
- `Theft.Leave` writes no per-pawn *Bandit left* row for a raid member, or a band of a hundred
  would post a hundred rows. A **theft** row is still written, because that stack is a real loss.

**The downed stay** on the board as hostile pawns. The group ends when nothing is pending and no
member is standing. A downed raider that recovers after its band has gone thinks as a lone bandit.
That is the rescue and capture seam (design 33 §17f), not this unit's.

## 7. Sound

The owner supplied two Pixabay recordings on 2026-09-25:

| Cue | Recording | Clip | Played by |
|---|---|---|---|
| Arrival | freesound_community, `war-horn-horror-73771` | `alert-raid-arrive`, 18.0 s stereo, baked by `tools/audio/bake_raid.sh` | the raid's **Events row** (`ui.bulletin.raidincoming`) |
| Assault | trading_nation, `low-horn-185556` | **`alert-raid`, already in the game** | the **Raid alert** (`ui.alert.raid`), raised while a raid is assaulting |

**The assault horn was already the siren.** Baked through the alert chain, the low horn is
`alert-raid.wav` sample for sample (correlation 1.0000, 9.54 s and −18.6 LUFS both). So the
owner's `notification-raid.mp3` of 2026-09-19 is this same file. No clip was added for it, and the
siren's source and licence are now known.

**The arrival horn is kept whole at 18 s**, because the bake shortens nothing musical. Its cooldown
covers the clip, as the siren's does. It came out 1.3 dB louder than the siren (−17.3 LUFS), and
the catalogue takes that back (volume 0.78 against 0.9).

**The Events row's chime is picked per incident now.** It was picked by favourability alone, so a
Bad bulletin always played `alert-negative`; `BulletinChime.For` is the override.

## 8. Content

- **`PawnKind_Gunman`** (kind index 4, appended): a person, Hostile, armed from the table
  `Item_Pistol`, the bandit's traverse mode and motive. It is dressed as a bandit, because
  `PawnOutfits` keys on person and Hostile. The debug menu's *Spawn pistol bandit* row now spawns
  this kind rather than a bandit with the weapon override.
- **`RaidMixDef`** (`Defs/Core/Events/RaidMixes.xml`): a label key and a list of
  `(kind, perMille)` entries summing to 1,000. There are three, in an append-only `MixOrder`:
  Bandits (1000 bandit), Gunmen (1000 gunman), Mixed (700 bandit, 300 gunman). A band of `n` is
  split by **largest remainder**, so it is exact: 10 in Mixed is 7 and 3, and 1 in Mixed is one
  bandit.
- **`Incident_Raid`**: Bad, ThreatBig, worker `Raid`, bulletin `ui.bulletin.raidincoming`. Its
  gates are set for the storyteller that will read them (`earliestDay` 3, `minRefireDays` 4). Its
  own parameters sit in a nested `<raid>` block, as design 23 §8 asked:

| Field | Value | |
|---|---|---|
| `perColonist` | 1 | auto size (§9) |
| `daysPerExtra` | 5 | auto size |
| `minSize` / `maxAutoSize` | 1 / 30 | auto size |
| `arrivalTicks` | 300 | 5 s at normal speed |
| `gatherInset` / `gatherRadius` | 12 / 5 cells | |
| `loiterHoursMin` / `Max` | 2 / 4 | owner |
| `probeFraction` / `probeHours` | 500 ‰ / 1 | owner: "about halfway, about an hour" |
| `earlyTriggerCells` | 15 | owner |
| `retreatPerMille` | 500 | owner |
| `mix` | `RaidMix_Mixed` | a storyteller's default |

Every number but the owner's is **INVENTED** and a playtest number.

## 9. Firing it

**The intent.** `InvokeIncident` was A = incident. It is now:
- A = incident;
- B = size, where 0 means the incident's own choice;
- C = mix + 1, where 0 means the incident's own choice.

The existing rows send 0 and 0, so a supply drop is unchanged. `IncidentParms` gains a `Mix`, and
its long-declared, never-read `Points` carries the size.

**Auto size** (`RaidBudget.AutoSize`) is:

`clamp(perColonist × standing colonists + day ÷ daysPerExtra, minSize, maxAutoSize)`

The owner chose headcount and days because nothing in the game has a value to total. A wealth
measure replaces this function and nothing else.

**The ceiling.** A raid that would take the registry past `PawnRegistry.PawnCeiling` is
**refused, not trimmed**, and says so. A debug slider that silently delivered fewer raiders than it
showed would be a lie.

**The debug menu.** The Events tab's Raid row carries a **size slider** (0–200, showing *Auto* at
0) and a **mix dropdown** filled from the content. They are the Settings window's own controls.

## 10. Save and hash

- `RaidSystem` is its own save section, `odyssey.raids`, appended with **no format bump**. A save
  from before raids has no section and loads with no band.
- It is **hashed only while a group exists**, the pattern `Projectiles` set, so no golden can
  move: no golden fires a raid.
- The member-to-group lookup is rebuilt on load and is neither saved nor hashed.

## 11. Cost

To be measured (R8) before the ceiling moves, on the scale-target board with 20 colonists and
200 raiders, in each phase.

**The known risk.** While any hostile stands anywhere on the board, every colonist that is not
already fighting runs the response scan (`HostilityResponses.Notices` → `Melee.HoldTarget`), which
walks every pawn every tick. With 200 raiders loitering at the edge for four hours, that is
colonists × ~220 checks a tick. The measurement decides whether it needs an index. §11a will hold
the numbers.

## 12. Not to undo by tidying

- **The group decides the phase; the member's own mind decides the fight.** A raid that re-implements
  target choice drifts from design 33's tuning the first time either is changed.
- **The schedule is drawn once, at fire time.** A system tick that draws from the world's stream
  makes a save's resume depend on when it was taken.
- **Refuse, don't trim** at the ceiling (§9).
- **Hashed only while a group exists** (§10).

## 13. For a person at the keyboard

- Whether 18 s of war horn is an alert or a cutscene.
- Whether 2–4 hours of loitering is suspense or a wait.
- Whether 136 of 200 raiders drawn in far form reads as a band.
- Whether a walled-in hearth, which leaves the band to hunt the nearest colonist, reads as a raid
  or as a crowd.

## 14. Later, and recorded

- A **storyteller** that reads the gates.
- The **choice event** (tribute).
- A real **wealth** measure behind `RaidBudget`.
- **Sappers**, **sieges** and arrival by **drop pod**.
- **Kidnap** on withdrawal (design 33 §17f).
- Merging a band's theft rows into one.
